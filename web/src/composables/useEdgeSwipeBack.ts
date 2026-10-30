import { nextTick, onBeforeUnmount, onMounted, type Ref } from 'vue'
import { haptic } from '../haptics'
import { animateSpring, type Spring } from '../lib/spring'
import { SPRING } from '../styles/tokens'

/**
 * iOS interactive pop: a swipe from the left edge drags the pushed view (open conversation) to the right and goes back
 * when let go far or fast enough, otherwise it springs back: a spring-snappy JS spring that starts from where the
 * finger let go, with its velocity (instant under Reduce Motion). An installed web app has no system back gesture, so this
 * is the only one. Elements with .lr-swipe-follow move with the finger (--lr-swipe-x on <html>); they are the parts
 * of the pushed view, not their fixed-position containers' parents (a transform there would unpin fixed children).
 */
const EDGE = 28 // px from the left edge where the swipe can start
const LOCK = 10 // px of travel before deciding horizontal (swipe) vs vertical (scroll)

export function useEdgeSwipeBack(enabled: Ref<boolean>, back: () => void) {
  const root = document.documentElement
  let startX = 0
  let startY = 0
  let tracking = false // started at the edge, direction not decided yet
  let swiping = false // locked horizontal
  let lastX = 0
  let lastT = 0
  let velocity = 0 // px/ms, rightward positive
  let settle: Spring | null = null

  function setX(px: number) {
    root.style.setProperty('--lr-swipe-x', `${Math.max(0, px)}px`)
  }
  function reset() {
    root.style.removeProperty('--lr-swipe-x')
  }

  function onStart(e: TouchEvent) {
    if (!enabled.value || e.touches.length !== 1 || settle) return
    const t = e.touches[0]
    if (t.clientX > EDGE) return
    tracking = true
    swiping = false
    startX = lastX = t.clientX
    startY = t.clientY
    lastT = e.timeStamp
    velocity = 0
  }

  function onMove(e: TouchEvent) {
    if (!tracking) return
    const t = e.touches[0]
    const dx = t.clientX - startX
    const dy = t.clientY - startY
    if (!swiping) {
      if (Math.abs(dx) < LOCK && Math.abs(dy) < LOCK) return
      if (dx <= 0 || Math.abs(dy) > Math.abs(dx)) {
        tracking = false // a scroll, or a swipe the wrong way
        return
      }
      swiping = true
      ;(document.activeElement as HTMLElement | null)?.blur?.() // drop the keyboard, as iOS does
    }
    e.preventDefault()
    const dt = e.timeStamp - lastT
    if (dt > 0) velocity = 0.8 * ((t.clientX - lastX) / dt) + 0.2 * velocity
    lastX = t.clientX
    lastT = e.timeStamp
    setX(dx)
  }

  function onEnd() {
    if (!tracking) return
    tracking = false
    if (!swiping) return
    swiping = false
    const dx = lastX - startX
    const width = window.innerWidth
    const done = dx > width * 0.35 || (velocity > 0.5 && dx > 30)
    if (done) haptic('light')
    const spring = animateSpring({
      from: dx,
      to: done ? width : 0,
      velocity: velocity * 1000,
      ...SPRING.snappy,
      onUpdate: setX,
      onComplete: async () => {
        settle = null
        if (done) {
          back()
          await nextTick()
        }
        reset()
      },
    })
    // Under Reduce Motion it lands at once (and has already called onComplete).
    settle = spring.done ? null : spring
  }

  onMounted(() => {
    document.addEventListener('touchstart', onStart, { passive: true })
    document.addEventListener('touchmove', onMove, { passive: false })
    document.addEventListener('touchend', onEnd)
    document.addEventListener('touchcancel', onEnd)
  })
  onBeforeUnmount(() => {
    document.removeEventListener('touchstart', onStart)
    document.removeEventListener('touchmove', onMove)
    document.removeEventListener('touchend', onEnd)
    document.removeEventListener('touchcancel', onEnd)
    settle?.stop()
    reset()
  })
}
