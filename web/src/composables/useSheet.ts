import { onBeforeUnmount, onMounted, reactive, ref, type Ref } from 'vue'
import { haptic } from '../haptics'
import { animateSpring, project, reduceMotion, rubberband, VelocityTracker, type Spring } from '../lib/spring'
import { DURATION, SPRING } from '../styles/tokens'

/*
 * Bottom sheet with detents. Drag the grabber/header 1:1 (pointer capture, grab offset kept); the body drags the sheet
 * only when it is scrolled to the top (or to expand it), otherwise it scrolls. 8px hysteresis before committing to a
 * drag. Release: project the flick, pick the detent nearest to the PROJECTED position and spring there with the
 * release velocity (damping 0.8 after a fast flick, 1.0 otherwise). Past the ends: rubber-band. Grabbing a moving
 * sheet stops it where it is.
 */

export interface SheetDetent {
  id: string
  /** Visible height in px, measured from the sheet's bottom edge. */
  height: number
}

/** Open modal sheets: floating chrome (action bar, the non-modal picker) hides while one is up — never glass on glass. */
export const sheetStack = reactive({ modal: 0 })

/** Travel (px) before a touch commits to dragging the sheet. */
const HYSTERESIS = 8
/** How far below the screen a hidden sheet rests (px). */
const HIDDEN_GAP = 48
/** A release faster than this (px/s) is a flick: the sheet settles with spring-bouncy. */
const FLICK_VELOCITY = 300

export interface UseSheetOptions {
  el: Ref<HTMLElement | null>
  body: Ref<HTMLElement | null>
  /** The opaque layer behind the content: fades in as the sheet rises to large. */
  backing: Ref<HTMLElement | null>
  detents: () => SheetDetent[]
  /** Modal sheets can be swiped down past the smallest detent to dismiss. */
  dismissible: () => boolean
  onDetent?: (id: string) => void
  onDismiss?: () => void
  /** A swipe that would have dismissed while `dismissible` is false (unsaved changes): the sheet springs back. */
  onDismissBlocked?: () => void
}

export function useSheet(o: UseSheetOptions) {
  const detent = ref<string | null>(null)
  let offset = 0
  let anim: Spring | null = null
  /** Presenting or hiding: the sheet sweeps past other detents on its way, which must not be reported as a choice. */
  let travelling = false
  let hideTimer: ReturnType<typeof setTimeout> | undefined

  const sorted = () => [...o.detents()].sort((a, b) => a.height - b.height)
  const maxH = () => sorted().at(-1)?.height ?? 0
  const minH = () => sorted()[0]?.height ?? 0
  const hidden = () => maxH() + HIDDEN_GAP

  function nearest(h: number): SheetDetent | undefined {
    return sorted().reduce<SheetDetent | undefined>(
      (p, d) => (!p || Math.abs(d.height - h) < Math.abs(p.height - h) ? d : p),
      undefined,
    )
  }

  /** 0 at or below the highest partial detent, 1 at large: inset, corners and opacity follow it (sheets.css). */
  function rise(y: number): number {
    const list = sorted()
    const large = list.find((d) => d.id === 'large')
    if (!large) return 0
    const partial = list.at(-2)
    if (!partial) return 1
    return Math.min(1, Math.max(0, (maxH() - y - partial.height) / (large.height - partial.height)))
  }

  function apply(y: number) {
    offset = y
    const el = o.el.value
    if (!el) return
    el.style.transform = `translate3d(0,${y}px,0)`
    const r = String(rise(y))
    el.style.setProperty('--lr-sheet-rise', r)
    if (o.backing.value) o.backing.value.style.opacity = r
    const near = nearest(maxH() - y)
    if (near && near.id !== detent.value && y < hidden() - 1 && !travelling) {
      detent.value = near.id
      o.onDetent?.(near.id)
    }
  }

  function layout() {
    const el = o.el.value
    if (el) el.style.height = `${maxH()}px`
  }

  function springTo(y: number, velocity = 0, bouncy = false, done?: () => void) {
    anim?.stop()
    anim = animateSpring({
      from: offset,
      to: y,
      velocity,
      ...(bouncy ? SPRING.bouncy : SPRING.smooth),
      onUpdate: apply,
      onComplete: done,
    })
  }

  function snapTo(id: string, velocity = 0, bouncy = false) {
    const d = sorted().find((x) => x.id === id) ?? sorted()[0]
    if (!d) return
    clearTimeout(hideTimer) // reopened while a Reduce Motion hide was pending
    travelling = false
    detent.value = d.id
    layout()
    springTo(maxH() - d.height, velocity, bouncy)
  }

  /** Rise from below the screen to `id`. Under Reduce Motion: place it and cross-fade. */
  function present(id: string) {
    layout()
    const el = o.el.value
    const d = sorted().find((x) => x.id === id) ?? sorted()[0]
    if (!el || !d) return
    clearTimeout(hideTimer)
    detent.value = d.id
    if (reduceMotion.value) {
      travelling = false
      apply(maxH() - d.height)
      el.style.opacity = '0'
      requestAnimationFrame(() => (el.style.opacity = '1'))
      return
    }
    travelling = true
    apply(hidden())
    springTo(maxH() - d.height, 0, false, () => (travelling = false))
  }

  /** Leave the way it came (down), then `done`. */
  function hide(done: () => void, velocity = 0) {
    const el = o.el.value
    if (!el) return done()
    travelling = true
    if (reduceMotion.value) {
      el.style.opacity = '0'
      clearTimeout(hideTimer)
      hideTimer = setTimeout(done, DURATION.fade)
      return
    }
    springTo(hidden(), velocity, false, done)
  }

  /** Keep the current detent when the viewport changes size. */
  function relayout() {
    if (!detent.value || offset >= hidden() - 1) return
    anim?.stop()
    layout()
    const d = sorted().find((x) => x.id === detent.value)
    if (d) apply(maxH() - d.height)
  }

  // ----- drag core (shared by the pointer path on the handle and the touch path on the body) -----
  let drag: { y0: number; off0: number; moved: boolean } | null = null
  let vt = new VelocityTracker()

  function begin(y: number) {
    anim?.stop() // grab it mid-flight: continue from the presentation value
    travelling = false // a grab during present ends the sweep: from here the finger chooses
    drag = { y0: y, off0: offset, moved: false }
    vt = new VelocityTracker()
    vt.add(y)
  }
  function move(y: number): boolean {
    if (!drag) return false
    const dy = y - drag.y0
    if (!drag.moved && Math.abs(dy) < HYSTERESIS) return false
    if (!drag.moved) {
      drag.moved = true
      drag.y0 = y // track 1:1 from here so the sheet doesn't jump by the hysteresis
      drag.off0 = offset
    }
    vt.add(y)
    let next = drag.off0 + (y - drag.y0)
    const top = 0
    const bottom = maxH() - minH()
    if (next < top) next = top - rubberband(top - next, maxH())
    if (next > bottom && !o.dismissible()) next = bottom + rubberband(next - bottom, maxH())
    apply(next)
    return true
  }
  function end() {
    if (!drag) return
    const moved = drag.moved
    drag = null
    if (!moved) return
    const v = vt.velocity() // px/s, + = downwards
    const projectedH = maxH() - (offset + project(v))
    if (projectedH < minH() * 0.5) {
      if (o.dismissible()) {
        o.onDismiss?.()
        return
      }
      o.onDismissBlocked?.()
    }
    const target = nearest(projectedH)
    if (!target) return
    const changed = target.id !== detent.value
    snapTo(target.id, v, Math.abs(v) > FLICK_VELOCITY)
    if (changed || target.height !== maxH() - offset) haptic('selection')
  }

  // Pointer path: grabber / header (touch-action: none there).
  let pointerId: number | null = null
  function onHandlePointerDown(e: PointerEvent) {
    const t = e.target as Element
    if (t.closest('button:not(.lr-grabber), input, a, [role="button"]')) return
    if (e.pointerType === 'mouse' && e.button !== 0) return
    pointerId = e.pointerId
    ;(e.currentTarget as Element).setPointerCapture?.(e.pointerId)
    begin(e.clientY)
  }
  function onHandlePointerMove(e: PointerEvent) {
    if (e.pointerId === pointerId) move(e.clientY)
  }
  function onHandlePointerUp(e: PointerEvent) {
    if (e.pointerId !== pointerId) return
    pointerId = null
    end()
  }

  // Touch path: the scrolling body. Decide per gesture whether it scrolls the content or drags the sheet.
  let mode: 'undecided' | 'drag' | 'scroll' = 'undecided'
  let touchY0 = 0
  let touchX0 = 0
  function onTouchStart(e: TouchEvent) {
    if (e.touches.length !== 1) return
    mode = 'undecided'
    touchY0 = e.touches[0]!.clientY
    touchX0 = e.touches[0]!.clientX
  }
  function onTouchMove(e: TouchEvent) {
    const y = e.touches[0]?.clientY
    if (y == null || mode === 'scroll') return
    if (mode === 'undecided') {
      const dy = y - touchY0
      const dx = (e.touches[0]?.clientX ?? touchX0) - touchX0
      if (Math.abs(dy) < HYSTERESIS && Math.abs(dx) < HYSTERESIS) return
      // A sideways swipe (the Skins carousel) scrolls its row; it never drags the sheet.
      if (Math.abs(dx) > Math.abs(dy)) {
        mode = 'scroll'
        return
      }
      const atTop = (o.body.value?.scrollTop ?? 0) <= 0
      const atMax = offset <= 0.5
      if ((dy > 0 && atTop) || (dy < 0 && !atMax)) {
        mode = 'drag'
        begin(touchY0)
      } else {
        mode = 'scroll'
        return
      }
    }
    if (e.cancelable) e.preventDefault()
    move(y)
  }
  function onTouchEnd() {
    if (mode === 'drag') end()
    mode = 'undecided'
  }

  onMounted(() => {
    window.addEventListener('resize', relayout)
  })
  onBeforeUnmount(() => {
    window.removeEventListener('resize', relayout)
    anim?.stop()
    clearTimeout(hideTimer)
  })

  /** Wire the body's touch listeners (call when the body element exists). */
  function bindBody(body: HTMLElement) {
    body.addEventListener('touchstart', onTouchStart, { passive: true })
    body.addEventListener('touchmove', onTouchMove, { passive: false })
    body.addEventListener('touchend', onTouchEnd, { passive: true })
    body.addEventListener('touchcancel', onTouchEnd, { passive: true })
    return () => {
      body.removeEventListener('touchstart', onTouchStart)
      body.removeEventListener('touchmove', onTouchMove)
      body.removeEventListener('touchend', onTouchEnd)
      body.removeEventListener('touchcancel', onTouchEnd)
    }
  }

  function cycle() {
    const list = sorted()
    const i = list.findIndex((d) => d.id === detent.value)
    const next = list[(i + 1) % list.length]
    if (next) {
      snapTo(next.id)
      haptic('selection')
    }
  }

  return {
    detent,
    present,
    hide,
    snapTo,
    relayout,
    cycle,
    bindBody,
    handle: { onPointerdown: onHandlePointerDown, onPointermove: onHandlePointerMove, onPointerup: onHandlePointerUp, onPointercancel: onHandlePointerUp },
  }
}
