import { nextTick, onBeforeUnmount, ref } from 'vue'
import { haptic } from '../haptics'
import { animateSpring, type Spring } from '../lib/spring'
import { SPRING } from '../styles/tokens'

/**
 * Drag to reorder an inset-grouped list, like an iOS list's reorder control: a pointer or finger on a row's grip lifts
 * the row, which follows it; the rows it passes step aside (CSS transition, `is-shifting`), a selection haptic marks
 * each new place, and on release the list is reordered (`onMove(from, to)`) and the row settles into its slot on a
 * spring that takes the release velocity. Pointer and touch only: the consumer exposes the grip to keyboards and
 * VoiceOver as `role="slider"` with its own arrow-key reordering (see ChampionListSection).
 *
 * Rows are found by `data-reorder-index` on the list's items (`<ListRow :data-reorder-index="i">`). While a drag (and
 * its settle) runs, `active` is true: the list's TransitionGroup must not animate the reorder itself then (its
 * `move-class` without a transition), or it would fight the row's own transform.
 */
export function useDragReorder(onMove: (from: number, to: number) => void) {
  /** Index of the row being dragged (its original place), or null. */
  const dragging = ref<number | null>(null)
  /** A drag or its settle is running. */
  const active = ref(false)

  let pointerId: number | null = null
  let rows: HTMLElement[] = []
  let rects: DOMRect[] = []
  let from = 0
  let target = 0
  let startY = 0
  let offset = 0
  let samples: { t: number; y: number }[] = []
  let settle: Spring | null = null
  let settlingRow: HTMLElement | null = null

  /** Ends a settle still running (a new drag, unmount): the row goes straight to its slot. */
  function finishSettle() {
    settle?.stop()
    settle = null
    if (settlingRow) settlingRow.style.transform = ''
    settlingRow = null
    active.value = false
  }

  function rowsOf(handle: Element): HTMLElement[] {
    const list = handle.closest('[data-reorder-index]')?.parentElement
    if (!list) return []
    return [...list.querySelectorAll<HTMLElement>(':scope > [data-reorder-index]')].sort(
      (a, b) => Number(a.dataset.reorderIndex) - Number(b.dataset.reorderIndex),
    )
  }

  function onPointerdown(e: PointerEvent, index: number) {
    if (pointerId !== null || (e.pointerType === 'mouse' && e.button !== 0)) return
    // Measured below with transforms counted: a row still settling from the last drag goes to its slot first.
    finishSettle()
    const handle = e.currentTarget as HTMLElement
    rows = rowsOf(handle)
    if (rows.length < 2 || !rows[index]) return
    e.preventDefault()
    try {
      handle.setPointerCapture?.(e.pointerId)
    } catch {
      // A pointer the browser no longer tracks: the drag still follows the grip's own events.
    }
    pointerId = e.pointerId
    rects = rows.map((r) => r.getBoundingClientRect())
    from = target = index
    startY = e.clientY
    offset = 0
    samples = [{ t: e.timeStamp, y: e.clientY }]
    dragging.value = index
    active.value = true
    haptic('selection')
  }

  function onPointermove(e: PointerEvent) {
    if (e.pointerId !== pointerId) return
    const me = rects[from]!
    // The row stays within the list.
    offset = Math.min(rects.at(-1)!.bottom - me.bottom, Math.max(rects[0]!.top - me.top, e.clientY - startY))
    samples.push({ t: e.timeStamp, y: e.clientY })
    if (samples.length > 5) samples.shift()
    rows[from]!.style.transform = `translateY(${offset}px)`

    // Its new place: each row its leading edge has reached the middle of (the first and last places too, where the row
    // stops at the list's edge, whatever the rows' heights).
    const mid = (k: number) => rects[k]!.top + rects[k]!.height / 2
    let to = from
    for (let k = from + 1; k < rows.length; k++) if (me.bottom + offset >= mid(k)) to = k
    for (let k = from - 1; k >= 0; k--) if (me.top + offset <= mid(k)) to = k
    if (to === target) return
    target = to
    haptic('selection')
    rows.forEach((row, k) => {
      if (k === from) return
      const shift = from < k && k <= to ? -me.height : to <= k && k < from ? me.height : 0
      row.style.transform = shift ? `translateY(${shift}px)` : ''
    })
  }

  async function onPointerup(e: PointerEvent) {
    if (e.pointerId !== pointerId) return
    pointerId = null
    const first = samples[0]!
    const last = samples.at(-1)!
    const velocity = last.t > first.t ? ((last.y - first.y) / (last.t - first.t)) * 1000 : 0
    const me = rects[from]!
    // The slot it lands in, and how far the row is from it now.
    const passed = rects.slice(Math.min(from, target) + (target > from ? 1 : 0), Math.max(from, target) + (target > from ? 1 : 0))
    const slotTop = me.top + (target > from ? 1 : -1) * passed.reduce((sum, r) => sum + r.height, 0)
    const fromSlot = me.top + offset - slotTop
    const row = rows[from]!

    // The rows that stepped aside are where the reorder puts them: drop their transforms without animating.
    for (const r of rows) {
      r.style.transition = 'none'
      r.style.transform = ''
    }
    dragging.value = null
    if (target !== from) onMove(from, target)
    await nextTick()
    row.style.transform = fromSlot ? `translateY(${fromSlot}px)` : ''
    void row.offsetHeight
    for (const r of rows) r.style.transition = ''
    settlingRow = row
    settle = animateSpring({
      from: fromSlot,
      to: 0,
      velocity,
      response: SPRING.snappy.response,
      damping: SPRING.snappy.damping,
      onUpdate: (v) => (row.style.transform = v ? `translateY(${v}px)` : ''),
      onComplete: () => {
        row.style.transform = ''
        active.value = false
        settle = null
        settlingRow = null
      },
    })
  }

  function onPointercancel(e: PointerEvent) {
    if (e.pointerId !== pointerId) return
    // Cancelled (the system took the gesture): no move, everything back in place.
    target = from
    void onPointerup(e)
  }

  onBeforeUnmount(finishSettle)

  /** Classes for row `index`: the lifted row, and the others while they step aside. */
  function rowClass(index: number) {
    if (dragging.value === null) return undefined
    return index === dragging.value ? 'is-dragging' : 'is-shifting'
  }

  return { dragging, active, rowClass, handlers: { onPointerdown, onPointermove, onPointerup, onPointercancel } }
}
