import { watch } from 'vue'
import type { ClientMusicKey } from '../contracts'
import {
  audioContext,
  musicUrls,
  onAudioGesture,
  onContextDiscarded,
  onContextRunning,
  playback,
  soundSettings,
} from './index'
import { BACKGROUND_MEDIA_ELEMENTS, elementVolume, pageHidden, silentClip } from './platform'

/**
 * Music engine: the League client's music and ambience loops (`ClientSoundCatalog.cs` — Music), streamed with
 * `<audio>` elements (a draft track decoded to PCM would be ~90 MB; five layers of them would sink an iPhone) and routed
 * through the shared AudioContext, because iOS ignores `<audio>.volume`: every voice gets its own gain node for fades
 * and the music volume. Like the client's champ select music mixin, `setMusic()` takes the full list of tracks that
 * should play now; tracks already playing keep going (their volume follows), new ones start — layers in sync with the
 * master track — and the rest fade out.
 *
 * iOS lets a media element play without a gesture only once a gesture has played it, so a small pool of elements is
 * primed (a silent clip played and paused) inside the first gestures while music is on. A replaced AudioContext (after a
 * call) takes its element sources with it: the pool is rebuilt from the next gesture. Music off / volume 0: voices fade
 * out and release their files.
 *
 * Background (Settings › Play in Background, on by default; off = everything pauses while hidden): music keeps going.
 * On iOS the context is interrupted in the background, which silences routed elements, and an element routed with `createMediaElementSource` can never play straight to the speaker again — so every pool slot there also
 * has a plain "twin" element. While the page is hidden (or the context isn't running yet on return) each voice plays on
 * its twin, handed over at the same position, and comes back to the routed element once the context runs on screen.
 * Trade-off: on the twin the music volume, per-layer volumes and fades don't apply (iOS ignores `<audio>.volume`; a
 * silent layer is muted instead), so in the background the music plays at the phone's volume and stops without a fade.
 * The buffered fallback (no Ogg in `<audio>`) is Web Audio only: on iOS it falls silent in the background and carries
 * on when the app comes back.
 */

export interface MusicTrack {
  key: ClientMusicKey
  /** 0..1 relative to the music volume (the client's 0.37 = 1). */
  volume: number
  /** Default true (the client loops its music unless told otherwise). */
  loop?: boolean
  /** Layers follow the master track's position (the client resyncs them every second when > 50 ms off). */
  master?: boolean
  /** Start this long after being asked (the draft ban base waits for the transition stinger). */
  delayMs?: number
  /** Start this many seconds in (finalization joins its 60 s track where the timer is). */
  offset?: number
  /** Keep playing this long after leaving the list before fading out. */
  stopDelayMs?: number
  /** Fade-out length when leaving (client default 1263 ms). */
  fadeOutMs?: number
  /** Fade-in length (the client's lobby / queue / blind-pick music fades in over 3 s). */
  fadeInMs?: number
}

/**
 * The client's music is Ogg Vorbis. Web Audio decodes it everywhere we've seen (the client sounds play on iPhone), but a
 * browser whose `<audio>` can't play Ogg gets the buffered fallback below instead of silence.
 */
const OGG_MEDIA = typeof Audio !== 'undefined' && new Audio().canPlayType('audio/ogg; codecs="vorbis"') !== ''

/** Enough for the busiest moment: five pick layers starting while three ban layers fade out, plus a stinger. */
const POOL_SIZE = 10
const DEFAULT_FADE_OUT_MS = 1263
const DEFAULT_FADE_IN_MS = 400
const RESYNC_EVERY_MS = 1000
const RESYNC_TOLERANCE_S = 0.05

interface Element {
  el: HTMLAudioElement
  node: MediaElementAudioSourceNode
  gain: GainNode
  primed: boolean
  busy: boolean
  /** iOS: the plain element the voice moves to in the background (see the top of this file). */
  twin: HTMLAudioElement | null
  twinPrimed: boolean
}

interface Voice {
  key: ClientMusicKey
  track: MusicTrack
  slot: Element
  /** Playing on the slot's plain twin instead of the routed element. */
  plain: boolean
  startTimer?: ReturnType<typeof setTimeout>
  stopTimer?: ReturnType<typeof setTimeout>
  stopping: boolean
  /** Waiting for a (delayed) start. */
  pending: boolean
  /** One-shot (stinger): released when it ends. */
  oneShot?: boolean
}

let bus: GainNode | null = null
let busCtx: AudioContext | null = null
let pool: Element[] = []
let desired: MusicTrack[] = []
const voices = new Map<ClientMusicKey, Voice>()
const stingers = new Set<Voice>()
/** One-shot tracks (blind pick, finalization) that played to their end: not restarted while they stay wanted. */
const endedOneShots = new Set<ClientMusicKey>()
let resyncTimer: ReturnType<typeof setInterval> | undefined

function musicOn(): boolean {
  return soundSettings.music && soundSettings.musicVolume > 0
}

/** Not paused from the lock screen, and on screen or allowed to go on in the background. */
function allowedNow(): boolean {
  return !playback.userPaused && (soundSettings.background || !pageHidden())
}

function contextRunning(): boolean {
  return audioContext()?.state === 'running'
}

/** Voices should be on their plain twins now: iOS, and hidden or the context not (yet) running. */
function plainNow(): boolean {
  return BACKGROUND_MEDIA_ELEMENTS && (pageHidden() || !contextRunning())
}

/** Web Audio can be heard now (the buffered fallback, and routed elements). */
function audibleNow(): boolean {
  return musicOn() && allowedNow() && contextRunning()
}

/** Element voices can be heard now: through the running context, or on the twins (iOS, once a gesture built them). */
function elementsAudible(): boolean {
  return musicOn() && allowedNow() && (contextRunning() || (BACKGROUND_MEDIA_ELEMENTS && pool.some((e) => e.twin)))
}

/** The element a voice is playing on. */
function active(v: Voice): HTMLAudioElement {
  return v.plain && v.slot.twin ? v.slot.twin : v.slot.el
}

/** A twin's volume where the platform honours `<audio>.volume` (not iOS); a silent layer is muted everywhere. */
function setTwinVolume(el: HTMLAudioElement, volume: number) {
  const v = elementVolume(volume * soundSettings.musicVolume)
  el.volume = v
  el.muted = v === 0
}

/** The bus for the current context (music volume), created on first use. */
function ensureBus(): GainNode | null {
  const c = audioContext()
  if (!c) return null
  if (bus && busCtx === c) return bus
  bus = c.createGain()
  bus.gain.value = soundSettings.musicVolume
  bus.connect(c.destination)
  busCtx = c
  return bus
}

function newAudio(): HTMLAudioElement {
  const el = new Audio()
  el.preload = 'auto'
  el.setAttribute('playsinline', '')
  return el
}

function newElement(c: AudioContext, out: GainNode): Element | null {
  try {
    const el = newAudio()
    const node = c.createMediaElementSource(el)
    const gain = c.createGain()
    gain.gain.value = 0
    node.connect(gain).connect(out)
    const twin = BACKGROUND_MEDIA_ELEMENTS ? newAudio() : null
    return { el, node, gain, primed: false, busy: false, twin, twinPrimed: false }
  } catch {
    return null
  }
}

/** Play-then-pause a silent clip inside a gesture so iOS lets the element play later without one. */
function primeAudio(el: HTMLAudioElement, isBusy: () => boolean, failed: () => void) {
  el.src = silentClip()
  void el.play()?.then(
    () => {
      if (!isBusy()) el.pause()
    },
    failed,
  )
}

// ----- buffered fallback (no Ogg in <audio>) -----
/**
 * Only the master track (or the first one), decoded and looped on an AudioBufferSourceNode: one long track in PCM is
 * affordable, a stack of draft layers is not, so layers and stingers are left out. Only the current track's buffer is
 * kept. Hidden page with Play in Background off: it stops, and starts again (from its offset) on return.
 */
interface Buffered {
  key: ClientMusicKey
  src: AudioBufferSourceNode | null
  gain: GainNode
}
let buffered: Buffered | null = null
/** A whole track is a few MB: generous, but a stalled download must not block the retry for good. */
const MUSIC_FETCH_TIMEOUT_MS = 60_000
let bufferedLoad: { key: ClientMusicKey; p: Promise<AudioBuffer | null> } | null = null

function loadBuffer(key: ClientMusicKey, url: string): Promise<AudioBuffer | null> {
  if (bufferedLoad?.key === key) return bufferedLoad.p
  const p = (async () => {
    const c = audioContext()
    if (!c) return null
    try {
      const r = await fetch(url, { signal: AbortSignal.timeout(MUSIC_FETCH_TIMEOUT_MS) })
      if (!r.ok) return null
      return await c.decodeAudioData(await r.arrayBuffer())
    } catch {
      return null
    }
  })()
  bufferedLoad = { key, p }
  // A failed fetch or decode (connector restarting) is retried on the next apply, not remembered for the session.
  void p.then((b) => {
    if (!b && bufferedLoad?.p === p) bufferedLoad = null
  })
  return p
}

function stopBuffered(ms = DEFAULT_FADE_OUT_MS) {
  const b = buffered
  buffered = null
  if (!b) return
  fade(b.gain, 0, ms)
  setTimeout(() => {
    try {
      b.src?.stop()
    } catch {
      /* already stopped */
    }
    b.gain.disconnect()
  }, ms + 50)
}

function applyBuffered() {
  if (!musicOn()) return stopBuffered()
  if (!audibleNow()) return
  const track = desired.find((t) => t.master && musicUrls[t.key]) ?? desired.find((t) => musicUrls[t.key])
  if (!track) return stopBuffered()
  if (buffered?.key === track.key) {
    fade(buffered.gain, track.volume, 300)
    return
  }
  stopBuffered(track.fadeOutMs ?? DEFAULT_FADE_OUT_MS)
  const c = audioContext()
  const out = ensureBus()
  if (!c || !out) return
  const gain = c.createGain()
  gain.gain.value = 0
  gain.connect(out)
  const b: Buffered = { key: track.key, src: null, gain }
  buffered = b
  void loadBuffer(track.key, musicUrls[track.key]!).then((buf) => {
    if (!buf || buffered !== b || audioContext() !== c) return
    const src = c.createBufferSource()
    src.buffer = buf
    src.loop = track.loop !== false
    src.connect(gain)
    src.start(c.currentTime + (track.delayMs ?? 0) / 1000, (track.offset ?? 0) % buf.duration)
    b.src = src
    if (import.meta.env.DEV) console.debug(`[music] ${track.key} ▶ (buffered)`)
    fade(gain, track.volume, track.fadeInMs ?? DEFAULT_FADE_IN_MS)
  })
}

/** Inside a gesture: fill the pool and play-then-pause a silent clip on each element so iOS lets it play later. */
function prime() {
  if (!musicOn()) return
  if (!OGG_MEDIA) return apply()
  const c = audioContext()
  const out = ensureBus()
  if (!c || !out) return
  while (pool.length < POOL_SIZE) {
    const e = newElement(c, out)
    if (!e) break
    pool.push(e)
  }
  for (const e of pool) {
    if (e.busy) continue
    if (!e.primed) {
      e.primed = true
      primeAudio(e.el, () => e.busy, () => (e.primed = false))
    }
    if (e.twin && !e.twinPrimed) {
      e.twinPrimed = true
      primeAudio(e.twin, () => e.busy, () => (e.twinPrimed = false))
    }
  }
  apply()
}

function takeElement(): Element | null {
  const free = pool.find((e) => !e.busy && e.primed) ?? pool.find((e) => !e.busy)
  if (free) free.busy = true
  return free ?? null
}

function clearAudio(el: HTMLAudioElement) {
  cancelSeek(el)
  el.pause()
  el.onended = null
  el.removeAttribute('src')
  try {
    el.load()
  } catch {
    /* ignore */
  }
}

function release(slot: Element) {
  slot.gain.gain.cancelScheduledValues(0)
  slot.gain.gain.value = 0
  clearAudio(slot.el)
  if (slot.twin) clearAudio(slot.twin)
  slot.busy = false
}

function ramp(slot: Element, to: number, ms: number) {
  fade(slot.gain, to, ms)
}

function fade(gain: GainNode, to: number, ms: number) {
  const c = audioContext()
  if (!c) return
  const g = gain.gain
  g.cancelScheduledValues(c.currentTime)
  g.setValueAtTime(g.value, c.currentTime)
  g.linearRampToValueAtTime(to, c.currentTime + Math.max(0.01, ms / 1000))
}

/** Seeks still waiting for an element's metadata: one per element, so a stale one can't jump a later track. */
const pendingSeeks = new WeakMap<HTMLAudioElement, { t: number; go: () => void }>()

function cancelSeek(el: HTMLAudioElement) {
  const p = pendingSeeks.get(el)
  if (p) el.removeEventListener('loadedmetadata', p.go)
  pendingSeeks.delete(el)
}

/** Where the element is, or will be once its pending seek lands. */
function position(el: HTMLAudioElement): number {
  return el.readyState < 1 ? (pendingSeeks.get(el)?.t ?? el.currentTime) : el.currentTime
}

/** Set the position once the element knows its duration (Safari ignores earlier seeks). */
function seek(el: HTMLAudioElement, t: number) {
  cancelSeek(el)
  const go = () => {
    pendingSeeks.delete(el)
    try {
      el.currentTime = Number.isFinite(el.duration) && el.duration > 0 ? t % el.duration : t
    } catch {
      /* not seekable yet */
    }
  }
  if (el.readyState >= 1) go()
  else {
    pendingSeeks.set(el, { t, go })
    el.addEventListener('loadedmetadata', go, { once: true })
  }
}

function masterVoice(): Voice | null {
  for (const v of voices.values()) if (v.track.master && !v.stopping && !v.pending) return v
  return null
}

/** Drop a voice now: its timers, its place in the maps and its slot. */
function finish(v: Voice) {
  clearTimeout(v.startTimer)
  clearTimeout(v.stopTimer)
  if (voices.get(v.key) === v) voices.delete(v.key)
  stingers.delete(v)
  release(v.slot)
}

/** A one-shot (track or stinger) is released when it ends, on whichever element it is playing by then. */
function onEnded(v: Voice, el: HTMLAudioElement) {
  el.onended = () => {
    if (active(v) !== el) return
    if (v.oneShot) {
      stingers.delete(v)
      release(v.slot)
    } else if (voices.get(v.key) === v) {
      endedOneShots.add(v.key)
      voices.delete(v.key)
      release(v.slot)
    }
  }
}

function startVoice(v: Voice) {
  // A delayed start that fires once music can't be heard (music off, hidden with Play in Background off): drop it, the
  // next apply() starts over.
  if (!elementsAudible()) {
    if (voices.get(v.key) === v) voices.delete(v.key)
    release(v.slot)
    return
  }
  v.pending = false
  const url = musicUrls[v.key]
  if (!url) return
  v.plain = plainNow() && !!v.slot.twin
  const el = active(v)
  if (el.getAttribute('src') !== url) el.src = url
  el.loop = v.track.loop !== false
  const m = masterVoice()
  if (v.track.offset) seek(el, v.track.offset)
  else if (m && m !== v && !v.track.master) seek(el, position(active(m)))
  else if (el.currentTime > 0) seek(el, 0)
  if (v.plain) setTwinVolume(el, v.track.volume)
  if (import.meta.env.DEV) console.debug(`[music] ${v.key} ▶ ${v.track.volume.toFixed(2)}${v.plain ? ' (plain element)' : ''}`)
  void el.play().then(
    () => {
      if (!v.plain) ramp(v.slot, v.track.volume, v.track.fadeInMs ?? DEFAULT_FADE_IN_MS)
    },
    () => {
      /* not allowed yet (no gesture): the next gesture primes and applies again */
    },
  )
  if (v.track.loop === false) onEnded(v, el)
}

function stopVoice(v: Voice, immediate = false) {
  if (v.stopping) return
  v.stopping = true
  clearTimeout(v.startTimer)
  const fade = () => {
    if (import.meta.env.DEV) console.debug(`[music] ${v.key} ◼`)
    // A plain twin can't fade on iOS (`<audio>.volume` is ignored there): it stops at once.
    const ms = immediate || v.plain ? 60 : (v.track.fadeOutMs ?? DEFAULT_FADE_OUT_MS)
    if (!v.plain) ramp(v.slot, 0, ms)
    v.stopTimer = setTimeout(() => finish(v), ms + 50)
  }
  if (v.pending) {
    voices.delete(v.key)
    release(v.slot)
    return
  }
  if (!immediate && v.track.stopDelayMs) v.stopTimer = setTimeout(fade, v.track.stopDelayMs)
  else fade()
}

/**
 * Move a playing voice between its routed element and its plain twin (iOS), at the same position. Going to the twin
 * happens as the page is hidden, before iOS interrupts the context; coming back waits until the context runs on screen.
 */
function setOutput(v: Voice, plain: boolean) {
  const twin = v.slot.twin
  if (v.plain === plain || !twin) return
  const from = active(v)
  const to = plain ? twin : v.slot.el
  const url = from.getAttribute('src')
  if (!url) return
  if (to.getAttribute('src') !== url) to.src = url
  to.loop = from.loop
  seek(to, position(from))
  const wasPlaying = !from.paused
  if (plain) setTwinVolume(to, v.track.volume)
  else {
    // Back on screen: straight to its level, a fade-in here would dip.
    const g = v.slot.gain.gain
    g.cancelScheduledValues(0)
    g.value = v.track.volume
  }
  v.plain = plain
  from.pause()
  if (v.oneShot || v.track.loop === false) {
    from.onended = null
    onEnded(v, to)
  }
  if (import.meta.env.DEV) console.debug(`[music] ${v.key} → ${plain ? 'plain element' : 'Web Audio'}`)
  if (!wasPlaying) return
  void to.play().catch(() => {
    // Not allowed (an element no gesture has primed): stay where it was.
    if (v.plain !== plain) return
    v.plain = !plain
    void from.play().catch(() => {})
  })
}

/** Put every playing voice on the right element for now (iOS: plain twins while hidden, see the top of this file). */
function handoff() {
  if (!BACKGROUND_MEDIA_ELEMENTS) return
  const plain = plainNow()
  for (const v of [...voices.values(), ...stingers]) {
    if (v.pending) continue
    // A fade-out on the routed element would go silent with the context anyway: finish it now.
    if (v.stopping) {
      if (plain && !v.plain) finish(v)
      continue
    }
    setOutput(v, plain)
  }
}

/** Bring the voices in line with `desired` (or pause / stop them when music can't be heard). */
function apply() {
  if (!OGG_MEDIA) return applyBuffered()
  if (!musicOn()) {
    for (const v of [...voices.values(), ...stingers]) stopVoice(v)
    return
  }
  if (!elementsAudible()) return
  handoff()
  for (const k of endedOneShots) if (!desired.some((t) => t.key === k)) endedOneShots.delete(k)
  const wanted = new Map(
    desired.filter((t) => musicUrls[t.key] && !endedOneShots.has(t.key)).map((t) => [t.key, t]),
  )
  for (const v of [...voices.values()]) {
    const t = wanted.get(v.key)
    if (!t || v.stopping) {
      if (!t) stopVoice(v)
      continue
    }
    v.track = t
    if (!v.pending) {
      const el = active(v)
      if (el.paused && !(t.loop === false && el.ended)) void el.play().catch(() => {})
      if (v.plain) setTwinVolume(el, t.volume)
      else ramp(v.slot, t.volume, 300)
    }
  }
  // Layers starting with a delayed master wait for it, like the client (`scheduleMusicTrackPlay`).
  const masterTrack = [...wanted.values()].find((t) => t.master)
  const masterVoiceNow = masterTrack && voices.get(masterTrack.key)
  const masterDelay = masterTrack && (!masterVoiceNow || masterVoiceNow.stopping) ? (masterTrack.delayMs ?? 0) : 0
  for (const [key, t] of wanted) {
    const existing = voices.get(key)
    if (existing && !existing.stopping) continue
    if (existing) {
      // Fading out but wanted again: let it fade and start a fresh voice.
      voices.delete(key)
    }
    const slot = takeElement()
    if (!slot) break
    const v: Voice = { key, track: t, slot, plain: false, stopping: false, pending: true }
    voices.set(key, v)
    const delay = t.master ? (t.delayMs ?? 0) : Math.max(t.delayMs ?? 0, masterDelay)
    // A layer joining a delayed master starts just after it, so it can take the master's position.
    const wait = delay + (!t.master && masterDelay ? 50 : 0)
    if (wait) v.startTimer = setTimeout(() => voices.get(key) === v && !v.stopping && startVoice(v), wait)
    else startVoice(v)
  }
  resync()
}

/** Layers drift a little on `<audio>`: pull them back onto the master track like the client does. */
function resync() {
  clearInterval(resyncTimer)
  if (voices.size < 2) return
  resyncTimer = setInterval(() => {
    const m = masterVoice()
    if (!m || active(m).paused) return
    const at = active(m).currentTime
    for (const v of voices.values()) {
      if (v === m || v.pending || v.stopping || v.track.master || v.track.offset) continue
      const el = active(v)
      if (Math.abs(el.currentTime - at) > RESYNC_TOLERANCE_S) seek(el, at)
    }
  }, RESYNC_EVERY_MS)
}

/** The tracks that should play now (replaces the previous list; the same keys keep playing). */
export function setMusic(tracks: MusicTrack[]) {
  desired = tracks
  apply()
}

/** A one-shot transition (the client's draft stingers), on top of whatever plays. */
export function playMusicStinger(key: ClientMusicKey, volume = 1) {
  if (!OGG_MEDIA || !elementsAudible() || !musicUrls[key]) return
  const slot = takeElement()
  if (!slot) return
  const v: Voice = {
    key,
    track: { key, volume, loop: false, fadeInMs: 10 },
    slot,
    plain: plainNow() && !!slot.twin,
    stopping: false,
    pending: true,
    oneShot: true,
  }
  stingers.add(v)
  const el = active(v)
  el.src = musicUrls[key]!
  el.loop = false
  onEnded(v, el)
  if (v.plain) setTwinVolume(el, volume)
  if (import.meta.env.DEV) console.debug(`[music] ${key} (stinger${v.plain ? ', plain element' : ''})`)
  v.pending = false
  void el.play().then(
    () => {
      if (!v.plain) ramp(slot, volume, 10)
    },
    () => {
      stingers.delete(v)
      release(slot)
    },
  )
}

/** Hidden with Play in Background off: everything pauses (and picks up where it was on return). */
function pauseAll() {
  stopBuffered(60)
  for (const v of voices.values()) active(v).pause()
  // A stinger is a moment, not a loop: drop it rather than leave it paused holding a pool slot.
  for (const v of stingers) release(v.slot)
  stingers.clear()
}

function dropPool() {
  clearInterval(resyncTimer)
  endedOneShots.clear()
  buffered = null
  bufferedLoad = null
  for (const v of [...voices.values(), ...stingers]) {
    clearTimeout(v.startTimer)
    clearTimeout(v.stopTimer)
  }
  voices.clear()
  stingers.clear()
  for (const e of pool) {
    e.el.pause()
    e.el.removeAttribute('src')
    e.twin?.pause()
    e.twin?.removeAttribute('src')
  }
  pool = []
  bus = null
  busCtx = null
}

/** Hook the music engine to gestures, the audio context, visibility and the settings (install.ts). */
export function installMusic() {
  onAudioGesture(prime)
  onContextDiscarded(dropPool)
  // Running again (on screen after the background, a call): voices come back from their twins, waiting ones start.
  onContextRunning(apply)
  document.addEventListener('visibilitychange', () => {
    if (pageHidden() && !soundSettings.background) pauseAll()
    else apply()
  })
  // Paused from the lock screen: pause where it is; played again (there or by touching the app): carry on.
  watch(
    () => playback.userPaused,
    (paused) => (paused ? pauseAll() : apply()),
  )
  window.addEventListener('pageshow', () => apply())

  watch(
    () => [soundSettings.music, soundSettings.musicVolume] as const,
    ([on, volume]) => {
      const c = audioContext()
      if (bus && c) bus.gain.setTargetAtTime(volume, c.currentTime, 0.05)
      if (on) prime()
      apply()
    },
  )
  // The manifest arrived (or changed): start what was waiting for its file.
  watch(
    () => Object.keys(musicUrls).length,
    () => apply(),
  )
}
