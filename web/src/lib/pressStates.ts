/**
 * Press state on pointer-down (not on release) for rows, tiles, menu items and buttons: adds `.is-pressed` at once
 * and removes it on release/cancel. Installed once on the document; CSS (:active and .is-pressed) draws the state.
 * The empty touchstart listener also makes iOS apply :active.
 */
const PRESSABLE =
  '.lr-btn, .lr-tab, button.lr-row, .lr-row--action, .lr-menu-item, .lr-grid-tile, .lr-spell-slot, .lr-call-btn, .lr-segment'

let installed = false

export function installPressStates(root: Document = document) {
  if (installed) return
  installed = true
  const release = () => {
    for (const el of root.querySelectorAll('.is-pressed')) el.classList.remove('is-pressed')
  }
  root.addEventListener(
    'pointerdown',
    (e) => {
      const el = (e.target as Element | null)?.closest?.(PRESSABLE) as HTMLButtonElement | null
      if (!el || el.disabled || el.getAttribute('aria-disabled') === 'true') return
      el.classList.add('is-pressed')
      // Glass lights up from under the finger: the point goes on this element only, never on a parent.
      if (el.classList.contains('lr-glass-interactive')) {
        const box = el.getBoundingClientRect()
        el.style.setProperty('--lr-px', `${e.clientX - box.left}px`)
        el.style.setProperty('--lr-py', `${e.clientY - box.top}px`)
      }
    },
    { passive: true },
  )
  for (const type of ['pointerup', 'pointercancel', 'dragstart'] as const)
    root.addEventListener(type, release, { passive: true })
  // A finger that starts scrolling is no longer a press.
  root.addEventListener('scroll', release, { passive: true, capture: true })
  root.addEventListener('touchstart', () => {}, { passive: true })
}
