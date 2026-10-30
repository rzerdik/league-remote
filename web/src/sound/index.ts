import { reactive, watch } from 'vue'
import type { ClientMusicKey, ClientSoundKey, ClientSoundManifest } from '../contracts'
import { within } from '../lib/timeout'
import { backgroundReady, playOnElement, primeBackground, updateBackgroundVolume } from './background'
import { BACKGROUND_MEDIA_ELEMENTS, pageHidden } from './platform'
import { playback, type SoundCategory, soundSettings } from './settings'
import { type SynthKey, synthRecipes } from './synth'

/**
 * Sound engine: the League client's own sounds served by the PC app (`/client-sounds/manifest.json`; which file plays
 * when is in `ClientSoundCatalog.cs`), decoded with Web Audio; `synth.ts` covers the moments League has no sound for.
 * Music (`music.ts`) shares the context. The AudioContext is created from a user gesture (required on iOS). iOS
 * suspends it in the background and can leave it "interrupted" or dead after a call, Siri or the lock screen, so it is
 * resumed when the page comes back, replaced when it can't be resumed, and every play makes sure it is running first.
 *
 * Background (Settings › Play in Background, on by default): on iOS a cue for a hidden page plays the client's file on
 * a primed `<audio>` element instead (`background.ts`); `session.ts` keeps the page running so the cues can fire.
 * Settings live in `settings.ts`. Nothing runs until `installSound()` (install.ts) wires up the gesture listeners.
 */

/** Client-sourced keys (contracts/clientSounds.ts) plus `chatSent`, which the client doesn't have (it is silent when you send). */
export type SoundKey = ClientSoundKey | 'chatSent'

/** Always synthesized: League has no sound for these. */
const SYNTH_ONLY = new Set<string>(Object.keys(synthRecipes))

function isSynthKey(key: string): key is SynthKey {
  return SYNTH_ONLY.has(key)
}

/** Per-key gain relative to the sound volume, and its category. */
const KEYS: Record<SoundKey, { volume: number; category: SoundCategory }> = {
  matchFound: { volume: 1, category: 'readyCheck' },
  readyCountdown: { volume: 0.8, category: 'readyCheck' },
  readyPortal: { volume: 0.7, category: 'readyCheck' },
  readyPortalAram: { volume: 0.7, category: 'readyCheck' },
  readyAccept: { volume: 0.8, category: 'readyCheck' },
  readyDecline: { volume: 0.7, category: 'readyCheck' },
  readyAcceptedLoop: { volume: 0.6, category: 'readyCheck' },
  readySuccess: { volume: 0.85, category: 'readyCheck' },
  readyFailed: { volume: 0.8, category: 'readyCheck' },
  queueStart: { volume: 0.7, category: 'party' },
  queueCancel: { volume: 0.6, category: 'party' },
  memberJoined: { volume: 0.7, category: 'party' },
  memberLeft: { volume: 0.7, category: 'party' },
  inviteSent: { volume: 0.6, category: 'party' },
  leaderGranted: { volume: 0.75, category: 'party' },
  gameStart: { volume: 0.85, category: 'party' },
  inviteReceived: { volume: 0.85, category: 'social' },
  chatMessage: { volume: 0.7, category: 'social' },
  friendRequest: { volume: 0.8, category: 'social' },
  banPhase: { volume: 0.7, category: 'champSelect' },
  pickPhase: { volume: 0.7, category: 'champSelect' },
  yourTurn: { volume: 0.95, category: 'champSelect' },
  yourBan: { volume: 0.95, category: 'champSelect' },
  lockIn: { volume: 0.85, category: 'champSelect' },
  ban: { volume: 0.8, category: 'champSelect' },
  allyBan: { volume: 0.6, category: 'champSelect' },
  serialAllyBan: { volume: 0.7, category: 'champSelect' },
  serialEnemyBan: { volume: 0.7, category: 'champSelect' },
  allyPick: { volume: 0.7, category: 'champSelect' },
  enemyPick: { volume: 0.7, category: 'champSelect' },
  allyFirstPick: { volume: 0.75, category: 'champSelect' },
  enemyFirstPick: { volume: 0.75, category: 'champSelect' },
  pickSniped: { volume: 0.85, category: 'champSelect' },
  swapRequest: { volume: 0.8, category: 'champSelect' },
  positionSwapRequest: { volume: 0.8, category: 'champSelect' },
  swapAccepted: { volume: 0.75, category: 'champSelect' },
  swapDeclined: { volume: 0.7, category: 'champSelect' },
  benchUpdate: { volume: 0.7, category: 'champSelect' },
  reroll: { volume: 0.8, category: 'champSelect' },
  timerTick: { volume: 0.8, category: 'countdown' },
  timerTickSmall: { volume: 0.7, category: 'countdown' },
  hover: { volume: 0.35, category: 'taps' },
  click: { volume: 0.3, category: 'taps' },
  chatSent: { volume: 0.35, category: 'taps' },
}

export { playback, SOUND_CATEGORIES, type SoundCategory, type SoundSettings, soundSettings } from './settings'

watch(
  () => JSON.stringify(soundSettings),
  () => {
    if (master && ctx) master.gain.setTargetAtTime(soundSettings.muted ? 0 : soundSettings.volume, ctx.currentTime, 0.02)
    if (soundSettings.muted || (pageHidden() && !soundSettings.background)) stopAll()
    updateBackgroundVolume()
  },
)
watch(
  () => playback.userPaused,
  (paused) => paused && stopAll(),
)

// ----- audio context -----
let ctx: AudioContext | null = null
let master: GainNode | null = null
/** The current context has played its silent unlock buffer (iOS needs one inside a gesture, per context). */
let unlocked = false

/** How long a resume may take before we give up on it: a late cue is worse than none. */
const RESUME_TIMEOUT_MS = 400

type AudioContextCtor = typeof AudioContext
function contextCtor(): AudioContextCtor | null {
  const w = window as unknown as { AudioContext?: AudioContextCtor; webkitAudioContext?: AudioContextCtor }
  return w.AudioContext ?? w.webkitAudioContext ?? null
}

/** Closed, or iOS Safari's extra "interrupted" state (not in the DOM typings): a call, Siri or an alarm took output. */
function isDead(c: AudioContext): boolean {
  const state: string = c.state
  return state === 'closed' || state === 'interrupted'
}

/** Listeners for the music engine: every gesture (to unlock media elements) and a context being dropped. */
const gestureListeners = new Set<() => void>()
const discardListeners = new Set<() => void>()
const runningListeners = new Set<() => void>()

/** Called on every user gesture, after the context was created or resumed (iOS only unlocks media inside one). */
export function onAudioGesture(fn: () => void) {
  gestureListeners.add(fn)
}
/** Called when the context is replaced: nodes made on it (media element sources) are dead. */
export function onContextDiscarded(fn: () => void) {
  discardListeners.add(fn)
}
/** Called when the context starts running again (after a resume). */
export function onContextRunning(fn: () => void) {
  runningListeners.add(fn)
}

/** The running-or-resumable context, if a gesture has created one. */
export function audioContext(): AudioContext | null {
  return ctx
}

function ensureContext(): AudioContext | null {
  if (ctx) return ctx
  const Ctor = contextCtor()
  if (!Ctor) return null
  let c: AudioContext
  try {
    c = new Ctor({ latencyHint: 'interactive' })
  } catch {
    return null
  }
  const comp = c.createDynamicsCompressor()
  comp.threshold.value = -14
  comp.ratio.value = 4
  master = c.createGain()
  master.gain.value = soundSettings.muted ? 0 : soundSettings.volume
  master.connect(comp).connect(c.destination)
  // Interrupted while the app is open (a call ends, Siri closes): try to get it back right away.
  c.addEventListener('statechange', () => {
    if (c !== ctx) return
    if (c.state === 'running') for (const fn of runningListeners) fn()
    else if (document.visibilityState === 'visible') void wake()
  })
  ctx = c
  unlocked = false
  return c
}

/** Drops a context that can't be resumed; the next gesture builds a fresh one (decoded buffers carry over). */
function discardContext() {
  const c = ctx
  if (!c) return
  stopAll()
  ctx = null
  master = null
  unlocked = false
  waking = null
  for (const fn of discardListeners) fn()
  if (c.state !== 'closed') void c.close().catch(() => {})
}

/** The resume in flight, per context: a replaced context must not answer with its predecessor's result. */
let waking: { c: AudioContext; p: Promise<boolean> } | null = null
/**
 * Make sure the existing context is running: resume it (bounded). Outside a gesture only a `closed` context is
 * dropped — iOS often still reports "interrupted" for a moment after a call or on return and brings the context back
 * itself, and a replacement could not start without a gesture anyway (`onGesture` replaces interrupted ones).
 * Never creates a context.
 */
function wake(): Promise<boolean> {
  const c = ctx
  if (!c) return Promise.resolve(false)
  if (c.state === 'running') return Promise.resolve(true)
  if (c.state === 'closed') {
    discardContext()
    return Promise.resolve(false)
  }
  if (waking?.c === c) return waking.p
  const p = within(
    c.resume().then(
      () => c.state === 'running',
      () => false,
    ),
    RESUME_TIMEOUT_MS,
    false,
  )
    .then((ok) => {
      if (!ok && c === ctx && c.state === 'closed') discardContext()
      return ok
    })
    .finally(() => {
      if (waking?.p === p) waking = null
    })
  waking = { c, p }
  return p
}

/** Called from every user gesture: create, resume or replace the context (iOS only allows that inside a gesture). */
function onGesture() {
  // Using the app again ends a pause from the lock screen.
  if (playback.userPaused) playback.userPaused = false
  // Closed: replace it now, inside a gesture, where a new one is allowed to start. Interrupted (just back from the
  // background or a call): try this gesture's resume first — replacing it would drop the music that kept playing on
  // its plain twins and restart every track at 0. Only if it still won't run is it dropped (the next tap rebuilds).
  if (ctx?.state === 'closed') discardContext()
  else if (ctx && isDead(ctx)) {
    const stuck = ctx
    stuck.resume().catch(() => {})
    setTimeout(() => {
      if (ctx === stuck && isDead(stuck)) discardContext()
    }, RESUME_TIMEOUT_MS)
  }
  const c = ensureContext()
  if (!c) return
  if (c.state !== 'running') {
    // resume() inside this very event: iOS unlocks on `touchend`, not the `pointerdown` that may have started a wake.
    c.resume().catch(() => {})
    void wake()
  }
  void loadManifest()
  if (!unlocked) {
    // Only counts once output runs, so later gesture events of the same tap replay it until it takes.
    unlocked = c.state === 'running'
    // iOS: playing a (silent) buffer inside the gesture fully unlocks output.
    try {
      const b = c.createBuffer(1, 1, c.sampleRate)
      const s = c.createBufferSource()
      s.buffer = b
      s.connect(c.destination)
      s.start(0)
    } catch {
      /* ignore */
    }
  }
  primeBackground()
  for (const fn of gestureListeners) fn()
}

/** Back from the background, a call or the lock screen: get the context running before the next cue. */
function onReturn() {
  if (document.visibilityState === 'visible') void wake()
}

/** Unlock audio on the user's gestures and wake it on return (install.ts). */
export function installAudioUnlock() {
  for (const ev of ['pointerdown', 'touchend', 'keydown'] as const) {
    window.addEventListener(ev, onGesture, { capture: true, passive: true })
  }
  window.addEventListener('pageshow', onReturn)
  window.addEventListener('focus', onReturn)
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') onReturn()
    // Sounds go on in the background unless that is switched off.
    else if (!soundSettings.background) stopAll()
  })
}

// ----- client sounds and music from the PC app -----
let manifest: Partial<Record<string, string>> = {} // resolved URLs
/** Resolved music URLs (streamed by music.ts, never decoded here). */
export const musicUrls = reactive<Partial<Record<ClientMusicKey, string>>>({})
let manifestPromise: Promise<void> | null = null
const buffers = new Map<string, AudioBuffer | null>()
const loading = new Map<string, Promise<AudioBuffer | null>>()

/** How long one sound file may take to download before it is given up on (a later play tries again). */
const SOUND_FETCH_TIMEOUT_MS = 15_000

function resolveUrl(url: string): string {
  return url.startsWith('/') ? url : `/client-sounds/${url}`
}

function loadManifest(): Promise<void> {
  manifestPromise ??= (async () => {
    try {
      const r = await within(fetch('/client-sounds/manifest.json', { cache: 'no-cache' }), 8000, null)
      if (!r?.ok) {
        manifestPromise = null // retry on a later gesture (connector restarted, network back)
        return
      }
      const json = (await r.json()) as Partial<ClientSoundManifest> | null
      const sounds: Record<string, string> = {}
      for (const [k, v] of Object.entries(json?.sounds ?? {}))
        if (typeof v === 'string' && v && !isSynthKey(k) && k in KEYS) sounds[k] = resolveUrl(v)
      manifest = sounds
      for (const [k, v] of Object.entries(json?.music ?? {}))
        if (typeof v === 'string' && v) musicUrls[k as ClientMusicKey] = resolveUrl(v)
      for (const k of Object.keys(manifest)) void loadBuffer(k)
    } catch {
      manifestPromise = null // offline: retry on a later gesture
    }
  })()
  return manifestPromise
}

function loadBuffer(key: string): Promise<AudioBuffer | null> {
  const url = manifest[key]
  if (!url || !ctx) return Promise.resolve(null)
  let p = loading.get(key)
  if (!p) {
    const c = ctx
    p = (async () => {
      let data: ArrayBuffer
      try {
        const r = await fetch(url, { signal: AbortSignal.timeout(SOUND_FETCH_TIMEOUT_MS) })
        if (!r.ok) return null
        data = await r.arrayBuffer()
      } catch {
        loading.delete(key) // stalled or offline: a later play tries again
        return null
      }
      let buffer: AudioBuffer | null
      try {
        buffer = await new Promise<AudioBuffer>((resolve, reject) => {
          // Callback form for older Safari; the promise form is used where available.
          const ret = c.decodeAudioData(data, resolve, reject) as Promise<AudioBuffer> | undefined
          ret?.then(resolve, reject)
        })
      } catch {
        buffer = null // undecodable: this key stays silent
      }
      buffers.set(key, buffer)
      return buffer
    })()
    loading.set(key, p)
  }
  return p
}

// ----- playback -----
interface Playing {
  key: SoundKey
  stop: () => void
}
const playing = new Set<Playing>()

function stopAll() {
  for (const p of [...playing]) p.stop()
}

/** Unmuted, audible volume, its category on, and on screen or allowed in the background (the context may need waking). */
function audible(key: SoundKey): boolean {
  return (
    !soundSettings.muted &&
    !playback.userPaused &&
    soundSettings.volume > 0 &&
    soundSettings.categories[KEYS[key].category] !== false &&
    (soundSettings.background || !pageHidden())
  )
}

export interface PlayOptions {
  /** Gain on top of the key's own (1 = as set). */
  volume?: number
  /** Start this many seconds into the sound (a ready check that was already running when the phone heard of it). */
  offset?: number
  /** Repeat until stopped (the ready check's accepted loop). */
  loop?: boolean
}

/**
 * Play a sound by key. Returns a stop function (no-op if nothing played). Skipped (not queued) while muted, its category
 * is off, hidden with Play in Background off, or before the first user gesture has unlocked audio. A suspended context
 * (back from the background, a call) is woken first; the sound plays once it runs again, or is dropped if that takes
 * longer than a moment. A hidden page on iOS plays it through a media element instead (see the top of this file).
 */
export function play(key: SoundKey, opts: PlayOptions = {}): () => void {
  const background = BACKGROUND_MEDIA_ELEMENTS && pageHidden()
  // In the background (iOS) the primed elements are what plays, whatever became of the context.
  const ready = audible(key) && (background ? backgroundReady() : !!ctx)
  if (import.meta.env.DEV)
    console.debug(
      `[sound] ${key}${ready ? (background ? ' (background element)' : '') : ' (skipped: muted, off, hidden or not unlocked yet)'}`,
    )
  if (!ready) return () => {}
  if (background) return playInBackground(key, opts)
  if (ctx!.state === 'running') return start(key, opts)
  let cancelled = false
  let stopSound = () => {}
  void wake().then((ok) => {
    if (ok && !cancelled && audible(key) && ctx?.state === 'running') stopSound = start(key, opts)
  })
  return () => {
    cancelled = true
    stopSound()
  }
}

function start(key: SoundKey, opts: PlayOptions): () => void {
  if (!ctx || !master) return () => {}
  const c = ctx
  const out = c.createGain()
  out.gain.value = KEYS[key].volume * (opts.volume ?? 1)
  out.connect(master)

  const buffer = buffers.get(key)
  let duration: number
  let source: AudioBufferSourceNode | null = null
  if (buffer) {
    const offset = Math.max(0, opts.offset ?? 0)
    if (offset >= buffer.duration && !opts.loop) {
      out.disconnect()
      return () => {}
    }
    source = c.createBufferSource()
    source.buffer = buffer
    source.loop = !!opts.loop
    source.connect(out)
    source.start(0, offset % buffer.duration)
    duration = opts.loop ? Infinity : buffer.duration - offset
  } else if (isSynthKey(key) && !opts.loop && !(opts.offset ?? 0)) {
    duration = synthRecipes[key](c, out, c.currentTime + 0.005)
  } else {
    // The client's file isn't decoded yet (or the PC has none for this key): skip this once.
    if (manifest[key] && !loading.has(key)) void loadBuffer(key)
    out.disconnect()
    return () => {}
  }

  const entry: Playing = {
    key,
    stop: () => {
      if (!playing.delete(entry)) return
      clearTimeout(timer)
      try {
        out.gain.setTargetAtTime(0, c.currentTime, 0.03)
        source?.stop(c.currentTime + 0.15)
      } catch {
        /* already stopped */
      }
      setTimeout(() => out.disconnect(), 250)
    },
  }
  const timer = Number.isFinite(duration)
    ? setTimeout(() => {
        playing.delete(entry)
        out.disconnect()
      }, (duration + 0.3) * 1000)
    : undefined
  playing.add(entry)
  return entry.stop
}

// ----- background playback on iOS -----
/** A cue while the page is hidden on iOS. Synthesized keys and keys whose file is unknown are skipped: they need Web Audio. */
function playInBackground(key: SoundKey, opts: PlayOptions): () => void {
  const url = manifest[key]
  const entry: Playing = { key, stop: () => {} }
  const stop = url ? playOnElement(url, KEYS[key].volume * (opts.volume ?? 1), opts, () => playing.delete(entry)) : null
  if (!stop) {
    if (import.meta.env.DEV) console.debug(`[sound] ${key} (background: ${url ? 'no primed element' : 'no client file'})`)
    return () => {}
  }
  entry.stop = stop
  playing.add(entry)
  return stop
}

/** Stop every playing instance of a key (e.g. the ready check countdown once you accepted). */
export function stop(key: SoundKey) {
  for (const p of [...playing]) if (p.key === key) p.stop()
}

/** Very quiet click for primary button taps. */
export function tapSound() {
  play('click')
}
