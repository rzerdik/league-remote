import { onBeforeUnmount, onMounted, toValue, type MaybeRefOrGetter } from 'vue'

/*
 * Modal layers: a modal sheet, the Ready Check cover, the QR scanner. While one is up, everything else in <body> is
 * inert (VoiceOver, Tab and taps can't reach it) — #app, the action bar, the tab bar, lower layers — except the banner
 * host: banners stay above everything and swap requests must stay answerable. The top layer takes focus, Tab cycles
 * through what is still live (the layer and the banners) instead of leaving the page, and focus goes back where it
 * was when the layer leaves. Layers live in <body> (Teleport), so each is a direct child of it. reka-ui alerts and
 * menus opened on top trap focus themselves and are left alone.
 */

interface Layer {
  parts: HTMLElement[]
  restoreFocus: HTMLElement | null
}

const layers: Layer[] = []
const ALWAYS_LIVE = '.lr-banner-host, [role="alertdialog"][data-state], [data-reka-popper-content-wrapper]'
const MARK = 'lrInert'
/** reka-ui content that runs its own focus trap. */
const SELF_TRAPPING = '[role="alertdialog"][data-state], [data-reka-popper-content-wrapper]'
const TABBABLE =
  'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), ' +
  'textarea:not([disabled]), [tabindex]:not([tabindex="-1"]), [contenteditable="true"]'
let observer: MutationObserver | null = null

function apply() {
  const top = layers.at(-1)
  for (const child of Array.from(document.body.children) as HTMLElement[]) {
    const live = !top || top.parts.some((p) => child.contains(p)) || child.matches(ALWAYS_LIVE)
    if (!live && !child.inert) {
      child.inert = true
      child.dataset[MARK] = ''
    } else if (live && child.dataset[MARK] !== undefined) {
      child.inert = false
      delete child.dataset[MARK]
    }
  }
}

/** What Tab can reach while a layer is up, in document order: tabbable, visible and outside every inert subtree. */
function tabbables(): HTMLElement[] {
  const out: HTMLElement[] = []
  for (const child of Array.from(document.body.children) as HTMLElement[]) {
    if (child.inert) continue
    const candidates = [...(child.matches(TABBABLE) ? [child] : []), ...child.querySelectorAll<HTMLElement>(TABBABLE)]
    for (const el of candidates) if (!el.closest('[inert]') && el.getClientRects().length) out.push(el)
  }
  return out
}

/**
 * The focus trap. Inert already keeps Tab off everything behind the layer; what's left is the edges: past the last
 * live control the browser would move focus out of the page (Safari's toolbar), and after the focused control went
 * away (a disabled Accept, a closed banner) focus sits on <body> or on something now inert. Wrap around in the first
 * case, step back into the top layer in the second, and leave every other Tab to the browser's own order.
 */
function onKeydown(e: KeyboardEvent) {
  if (e.key !== 'Tab' || e.defaultPrevented || !layers.length) return
  const active = document.activeElement as HTMLElement | null
  if (active?.closest(SELF_TRAPPING)) return
  const list = tabbables()
  if (!active || active === document.body || !document.body.contains(active) || active.closest('[inert]')) {
    e.preventDefault()
    const top = layers.at(-1)!
    const inLayer = list.filter((el) => top.parts.some((p) => p.contains(el)))
    const target = (e.shiftKey ? inLayer.at(-1) : inLayer[0]) ?? top.parts[0]
    target?.focus({ preventScroll: true })
    return
  }
  const ahead = e.shiftKey ? Node.DOCUMENT_POSITION_PRECEDING : Node.DOCUMENT_POSITION_FOLLOWING
  if (list.some((el) => el !== active && active.compareDocumentPosition(el) & ahead)) return
  e.preventDefault()
  ;(e.shiftKey ? list.at(-1) : list[0])?.focus({ preventScroll: true })
}

/**
 * While a layer is up: something teleported into <body> (a banner action, the action bar) gets the same treatment,
 * and Tab is kept inside (onKeydown).
 */
function watchBody(on: boolean) {
  if (on && !observer) {
    observer = new MutationObserver(apply)
    observer.observe(document.body, { childList: true })
    document.addEventListener('keydown', onKeydown)
  } else if (!on && observer) {
    observer.disconnect()
    observer = null
    document.removeEventListener('keydown', onKeydown)
  }
}

const QUIET = 'lrQuietFocus'

/**
 * Focus that the app moved, not the user (a layer opening, a banner stack unfolding): the element has focus — VoiceOver starts there, Return
 * and Space work — but no ring until the keyboard is used. Browsers draw :focus-visible for script focus whenever
 * the last input wasn't a pointer (Chrome before any tap at all), so the Ready Check opened with a square ring around
 * Accept on a phone nobody had touched. `[data-lr-quiet-focus]` (components.css) hides the ring; the first key press
 * or the element losing focus removes the mark, so a keyboard user sees the ring from then on.
 */
export function focusQuietly(el: HTMLElement) {
  el.dataset[QUIET] = ''
  el.focus({ preventScroll: true, focusVisible: false })
  const clear = () => {
    delete el.dataset[QUIET]
    el.removeEventListener('blur', clear)
    document.removeEventListener('keydown', clear, true)
  }
  el.addEventListener('blur', clear)
  document.addEventListener('keydown', clear, true)
}

/**
 * Put `parts` (the layer's elements in <body>, e.g. a sheet and its scrim) on top: the rest goes inert and `focus`
 * (or the first part) takes focus, quietly (no ring until the keyboard is used). Returns the function that removes
 * the layer again.
 */
export function pushModalLayer(parts: HTMLElement[], focus?: HTMLElement | null): () => void {
  const layer: Layer = { parts, restoreFocus: document.activeElement as HTMLElement | null }
  layers.push(layer)
  apply()
  watchBody(true)
  const target = focus ?? parts[0]
  if (target) focusQuietly(target)
  let removed = false
  return () => {
    if (removed) return
    removed = true
    const i = layers.indexOf(layer)
    if (i >= 0) layers.splice(i, 1)
    apply()
    if (!layers.length) watchBody(false)
    if (layer.restoreFocus?.isConnected && i === layers.length) layer.restoreFocus.focus({ preventScroll: true })
  }
}

/** A component that is a modal layer for as long as it is mounted (the Ready Check cover, the scanner). */
export function useModalLayer(
  root: MaybeRefOrGetter<HTMLElement | null>,
  focus?: MaybeRefOrGetter<HTMLElement | null | undefined>,
) {
  let release: (() => void) | null = null
  onMounted(() => {
    const el = toValue(root)
    if (el) release = pushModalLayer([el], toValue(focus))
  })
  onBeforeUnmount(() => release?.())
}
