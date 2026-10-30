import { BACKGROUND_MEDIA_ELEMENTS, elementVolume, silentClip } from './platform'
import { soundSettings } from './settings'

/**
 * Background playback on iOS: Web Audio is interrupted while the page is hidden, so a cue then plays the client's file
 * on one of a few plain `<audio>` elements, primed inside a gesture so they may play later unasked, at the hardware
 * volume (iOS ignores `<audio>.volume`).
 */

interface BgElement {
  el: HTMLAudioElement
  primed: boolean
  busy: boolean
  /** The key's gain × the play's own, for `el.volume` where the platform honours it. */
  volume: number
}
/** Enough for a cue on top of a looping one (the ready check's countdown under Match Found) and a chat sound. */
const BG_POOL_SIZE = 4
const pool: BgElement[] = []

/** Some element is primed and could play now. */
export function backgroundReady(): boolean {
  return pool.some((e) => e.primed)
}

/** Inside a gesture (iOS only): fill the pool and play-then-pause a silent clip on each so it may play later unasked. */
export function primeBackground() {
  if (!BACKGROUND_MEDIA_ELEMENTS || !soundSettings.background || soundSettings.muted) return
  while (pool.length < BG_POOL_SIZE) {
    const el = new Audio()
    el.preload = 'auto'
    el.setAttribute('playsinline', '')
    pool.push({ el, primed: false, busy: false, volume: 1 })
  }
  for (const e of pool) {
    if (e.primed || e.busy) continue
    e.el.src = silentClip()
    e.primed = true
    void e.el.play()?.then(
      () => {
        if (!e.busy) e.el.pause()
      },
      () => (e.primed = false),
    )
  }
}

/** The sound volume changed: apply it to the elements playing now. */
export function updateBackgroundVolume() {
  for (const e of pool) if (e.busy) e.el.volume = elementVolume(e.volume * soundSettings.volume)
}

function release(e: BgElement) {
  e.el.pause()
  e.el.onended = null
  e.el.removeAttribute('src')
  try {
    e.el.load()
  } catch {
    /* ignore */
  }
  e.busy = false
}

/**
 * Play `url` on a free primed element. Returns its stop function (`onEnd` runs once when it ends or is stopped), or
 * null when no primed element is free.
 */
export function playOnElement(
  url: string,
  volume: number,
  opts: { offset?: number; loop?: boolean },
  onEnd: () => void,
): (() => void) | null {
  const slot = pool.find((e) => !e.busy && e.primed)
  if (!slot) return null
  slot.busy = true
  slot.volume = volume
  const { el } = slot
  el.src = url
  el.loop = !!opts.loop
  el.volume = elementVolume(volume * soundSettings.volume)
  let done = false
  const stop = () => {
    if (done) return
    done = true
    release(slot)
    onEnd()
  }
  const offset = Math.max(0, opts.offset ?? 0)
  if (offset > 0)
    el.addEventListener(
      'loadedmetadata',
      () => {
        if (done) return
        const d = el.duration
        if (!opts.loop && Number.isFinite(d) && offset >= d) return stop()
        try {
          el.currentTime = Number.isFinite(d) && d > 0 ? offset % d : offset
        } catch {
          /* not seekable yet */
        }
      },
      { once: true },
    )
  el.onended = stop
  void el.play()?.catch(stop)
  return stop
}
