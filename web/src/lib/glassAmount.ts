import { readText, writeText, KEYS } from './storage'

/**
 * This phone's glass amount: 0 ultraclear, 0.5 default, 1 tinted, the in-app stand-in for iOS 27's Liquid Glass slider,
 * which no page can read (nor Reduce Transparency, on iOS Safari). Without a stored amount nothing is set and glass
 * reads its tokens directly; a stored one turns on the mix in `styles/components/glass.css`.
 */
function apply(amount: number | null) {
  const root = document.documentElement
  if (amount === null) {
    delete root.dataset.glassAmount
    root.style.removeProperty('--lr-glass-amount')
  } else {
    root.dataset.glassAmount = String(amount)
    root.style.setProperty('--lr-glass-amount', String(amount))
  }
}

function clamp(amount: number): number {
  return Math.min(1, Math.max(0, amount))
}

/** The stored amount, or null for the default (nothing stored, or something unreadable). */
export function storedGlassAmount(): number | null {
  const text = readText(KEYS.glassAmount)
  const amount = text === null ? NaN : Number(text)
  return Number.isFinite(amount) ? clamp(amount) : null
}

/** Store and apply an amount; null goes back to the default. */
export function setGlassAmount(amount: number | null) {
  const value = amount === null ? null : clamp(amount)
  writeText(KEYS.glassAmount, value === null ? null : String(value))
  apply(value)
}

/** Apply the stored amount at start. */
export function installGlassAmount() {
  apply(storedGlassAmount())
}
