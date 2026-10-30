import { onScopeDispose, watch, type Ref } from 'vue'

/**
 * Keeps the screen on while `active` (queue, ready check, champ select), so a timer on the phone isn't lost to
 * auto-lock. Screen Wake Lock API: Safari 16.4+, and in installed (home screen) web apps since iOS 18.4. The system
 * drops the lock whenever the app is hidden, so it is re-requested on return. Unsupported or refused: silently off.
 */
export function useWakeLock(active: Ref<boolean>) {
  let sentinel: WakeLockSentinel | null = null
  /** A request in flight: a second acquire (visibility + watcher at once) must not take a second lock. */
  let requesting = false

  async function acquire() {
    if (!active.value || sentinel || requesting || document.visibilityState !== 'visible' || !('wakeLock' in navigator))
      return
    requesting = true
    try {
      const s = await navigator.wakeLock.request('screen')
      sentinel = s
      // Only forget the lock this listener belongs to: an older one released later must not orphan a newer lock.
      s.addEventListener('release', () => {
        if (sentinel === s) sentinel = null
      })
      if (!active.value) void release()
    } catch {
      sentinel = null // low battery mode, not allowed, ...
    } finally {
      requesting = false
    }
  }

  async function release() {
    const s = sentinel
    sentinel = null
    await s?.release().catch(() => undefined)
  }

  const onVisibility = () => void acquire()
  document.addEventListener('visibilitychange', onVisibility)
  watch(active, (on) => void (on ? acquire() : release()), { immediate: true })

  onScopeDispose(() => {
    document.removeEventListener('visibilitychange', onVisibility)
    void release()
  })
}
