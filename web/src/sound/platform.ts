/**
 * Platform facts the sound engines share (no imports, so `index.ts`, `music.ts` and `session.ts` can all use it).
 *
 * iOS / iPadOS WebKit (every browser there, and the Home Screen PWA) interrupts a page's Web Audio context when the app
 * goes to the background or the phone locks — the context reports "interrupted" — which also silences `<audio>`
 * elements routed into it with `createMediaElementSource`. A plain `<audio>` element is not interrupted: it keeps playing
 * in the background, and while one plays the page keeps running (timers, the hub socket). So on iOS everything that
 * must sound while the app is not on screen goes through plain media elements instead of Web Audio
 * (`BACKGROUND_MEDIA_ELEMENTS`). Elsewhere (Chrome, Android, desktop) a hidden page's Web Audio context keeps running and
 * nothing changes.
 */

function detectIos(): boolean {
  if (typeof navigator === 'undefined') return false
  // Dev only: `?bg-audio=ios` runs the iOS background path in Chromium, to review it without an iPhone.
  if (import.meta.env.DEV && new URLSearchParams(location.search).get('bg-audio') === 'ios') return true
  const ua = navigator.userAgent
  // iPadOS reports itself as a Mac; a Mac has no touch points.
  return /iPad|iPhone|iPod/.test(ua) || (/Macintosh/.test(ua) && navigator.maxTouchPoints > 1)
}

/** Background sound has to go through plain `<audio>` elements (iOS WebKit), see above. */
export const BACKGROUND_MEDIA_ELEMENTS = detectIos()

/** The page is not on screen (minimised, another app, the lock screen). */
export function pageHidden(): boolean {
  return document.visibilityState === 'hidden'
}

const silentUrls = new Map<string, string>()
/**
 * A short mono 16-bit WAV, generated (nothing shipped). `dither` = 0 is digital silence, used to prime media elements
 * inside a gesture. `dither` > 0 adds ±1 LSB noise (about −90 dB, inaudible): the background keep-alive loop is never
 * pure silence, in case the platform treats a silent element as not playing.
 */
export function silentClip(seconds = 0.1, dither = false): string {
  const id = `${seconds}:${dither}`
  const cached = silentUrls.get(id)
  if (cached) return cached
  const rate = 8000
  const samples = Math.max(1, Math.round(rate * seconds))
  const buf = new ArrayBuffer(44 + samples * 2)
  const v = new DataView(buf)
  const str = (o: number, s: string) => [...s].forEach((ch, i) => v.setUint8(o + i, ch.charCodeAt(0)))
  str(0, 'RIFF')
  v.setUint32(4, 36 + samples * 2, true)
  str(8, 'WAVE')
  str(12, 'fmt ')
  v.setUint32(16, 16, true)
  v.setUint16(20, 1, true)
  v.setUint16(22, 1, true)
  v.setUint32(24, rate, true)
  v.setUint32(28, rate * 2, true)
  v.setUint16(32, 2, true)
  v.setUint16(34, 16, true)
  str(36, 'data')
  v.setUint32(40, samples * 2, true)
  if (dither) for (let i = 0; i < samples; i++) v.setInt16(44 + i * 2, i % 2 ? 1 : -1, true)
  const url = URL.createObjectURL(new Blob([buf], { type: 'audio/wav' }))
  silentUrls.set(id, url)
  return url
}

/** A volume for `<audio>.volume` (0..1). iOS ignores it (always the hardware volume); macOS Safari and others honour it. */
export function elementVolume(v: number): number {
  return Math.min(1, Math.max(0, Number.isFinite(v) ? v : 0))
}
