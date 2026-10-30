import { isCriticalPhase } from '../gameflow'
import { store } from '../store'
import { KEYS, readText, writeText } from './storage'

/**
 * Whether a page reload now would cost something time-critical. Reads the last state the PC sent even while signed
 * out (a dropped connection mid-draft is still mid-draft), so it errs on the side of not reloading. Every reload the
 * app does by itself or offers (a stale chunk, an update, "Couldn't Load" › Try Again) asks this first.
 */
export function reloadWouldInterrupt(): boolean {
  const s = store.state
  return !!s && (isCriticalPhase(s.gameflowPhase) || s.readyCheck?.state === 'InProgress')
}

/** At most one automatic reload per this long: a chunk that still fails afterwards shows "Couldn't Load" instead. */
const RELOAD_EVERY_MS = 30_000

/**
 * A screen's code couldn't be loaded even after its retries (lazy.ts) — usually an update replaced the app's files.
 * Reload once to pick up the current version, unless that would interrupt something (above) or it was tried in the
 * last 30 s. Returns whether it reloads.
 */
export function reloadForMissingCode(): boolean {
  if (reloadWouldInterrupt()) return false
  const last = Number(readText(KEYS.preloadReloadAt, 'session') || 0)
  if (Date.now() - last < RELOAD_EVERY_MS) return false
  // Without storage the throttle can't work: never reload then, or a missing chunk could reload forever.
  if (!writeText(KEYS.preloadReloadAt, String(Date.now()), 'session')) return false
  location.reload()
  return true
}

/** How often a resumed app looks for a new service worker. */
const UPDATE_CHECK_MS = 10 * 60 * 1000

/**
 * An installed iOS app is resumed rather than reloaded, so it would keep running an old build for days: look for a new
 * worker when the app comes back (throttled), and once one has taken over (it activates at once, see sw.js), reload
 * the next time the app is put away, unless that would interrupt a ready check, champ select or a game (main.ts, built
 * app only).
 */
export function installUpdateReload() {
  let lastUpdateCheck = Date.now()
  let updated = false
  const hadController = !!navigator.serviceWorker.controller // the first install also fires controllerchange
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (hadController) updated = true
  })
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') {
      if (updated && !reloadWouldInterrupt()) location.reload()
      return
    }
    if (Date.now() - lastUpdateCheck < UPDATE_CHECK_MS) return
    lastUpdateCheck = Date.now()
    void navigator.serviceWorker.getRegistration().then((reg) => reg?.update()).catch(() => undefined)
  })
}
