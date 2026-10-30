import { reactive, watch } from 'vue'
import { KEYS, readText, writeText } from './lib/storage'
import { soundSettings, tapSound } from './sound'

/*
 * Haptics. Android/Chrome: Vibration API. iOS Safari (18+): no Vibration API, but toggling a hidden
 * <input type="checkbox" switch> from inside a user gesture plays the system selection tick — only synchronously inside
 * a tap handler; anywhere else it is a silent no-op. Treat haptics as a bonus, never the only signal. They have their
 * own switch in Settings and respect the mute button.
 */

export type HapticKind = 'selection' | 'light' | 'medium' | 'heavy' | 'success' | 'warning' | 'error'

const PATTERNS: Record<HapticKind, number | number[]> = {
  selection: 8,
  light: 10,
  medium: 18,
  heavy: 28,
  success: [12, 60, 18],
  warning: [20, 80, 20],
  error: [30, 60, 30, 60, 30],
}

export const hapticSettings = reactive({ enabled: readText(KEYS.haptics) !== 'off' })
watch(
  () => hapticSettings.enabled,
  (on) => writeText(KEYS.haptics, on ? 'on' : 'off'),
)

let iosSwitch: HTMLLabelElement | null = null

export function haptic(kind: HapticKind = 'selection') {
  if (!hapticSettings.enabled || soundSettings.muted) return
  try {
    if (typeof navigator !== 'undefined' && typeof navigator.vibrate === 'function') {
      navigator.vibrate(PATTERNS[kind])
      return
    }
    if (!iosSwitch && typeof document !== 'undefined') {
      const label = document.createElement('label')
      label.setAttribute('aria-hidden', 'true')
      label.style.cssText = 'position:fixed;width:1px;height:1px;overflow:hidden;opacity:0;pointer-events:none;'
      const input = document.createElement('input')
      input.type = 'checkbox'
      input.setAttribute('switch', '')
      input.tabIndex = -1
      label.appendChild(input)
      document.body.appendChild(label)
      iosSwitch = label
    }
    const taps = kind === 'success' || kind === 'warning' ? 2 : kind === 'error' ? 3 : 1
    iosSwitch?.click()
    for (let i = 1; i < taps; i++) setTimeout(() => iosSwitch?.click(), i * 70)
  } catch {
    /* unsupported: silent */
  }
}

/**
 * A tap on a control that sends something: the quiet click and a haptic on the same frame. Use it in tap handlers
 * (iOS only plays a haptic inside a user gesture); confirmations from the client get their own haptic when they land.
 */
export function tapFeedback(kind: HapticKind = 'selection') {
  tapSound()
  haptic(kind)
}

/** Vibrate where supported (Android); silently ignored elsewhere. Independent of the sound mute. */
export function vibrate(pattern: number | number[]) {
  if (document.visibilityState !== 'visible') return
  // Chrome rejects (and logs) vibrate() before the first user interaction with the page.
  if ((navigator as Navigator & { userActivation?: { hasBeenActive: boolean } }).userActivation?.hasBeenActive === false)
    return
  try {
    navigator.vibrate?.(pattern)
  } catch {
    /* not supported */
  }
}
