import { ref } from 'vue'
import { SPRING } from '../styles/tokens'

/*
 * Springs with Apple parameters (response = period in s, damping = ratio). Interruptible: retarget() keeps the current
 * value AND velocity, so reversing a gesture never hits a brick wall. Always start from the live on-screen value.
 */

const reduceMotionQuery = typeof matchMedia === 'function' ? matchMedia('(prefers-reduced-motion: reduce)') : null

/** Reactive Reduce Motion preference: springs jump to their target and surfaces cross-fade instead. */
export const reduceMotion = ref(!!reduceMotionQuery?.matches)
reduceMotionQuery?.addEventListener('change', (e) => (reduceMotion.value = e.matches))

export interface SpringOptions {
  from: number
  to: number
  /** Initial velocity in units per second (e.g. a gesture's release velocity in px/s). */
  velocity?: number
  /** Seconds; default spring-smooth (SPRING.smooth). */
  response?: number
  /** Damping ratio; 1 = no overshoot, 0.8 = spring-bouncy. Default spring-smooth's. */
  damping?: number
  onUpdate?: (value: number) => void
  onComplete?: () => void
}

export interface Spring {
  readonly value: number
  readonly velocity: number
  readonly done: boolean
  stop(): void
  retarget(to: number): void
}

export function animateSpring(o: SpringOptions): Spring {
  const response = o.response ?? SPRING.smooth.response
  const k = Math.pow((2 * Math.PI) / response, 2)
  const c = (4 * Math.PI * (o.damping ?? SPRING.smooth.damping)) / response
  const st = { value: o.from, velocity: o.velocity ?? 0, target: o.to, raf: 0, done: false }
  let last = 0

  function frame(t: number) {
    if (!last) last = t
    const dt = Math.min(0.064, (t - last) / 1000)
    last = t
    const steps = Math.max(1, Math.ceil(dt / 0.004))
    const h = dt / steps
    for (let i = 0; i < steps; i++) {
      const a = -k * (st.value - st.target) - c * st.velocity
      st.velocity += a * h
      st.value += st.velocity * h
    }
    if (Math.abs(st.value - st.target) < 0.05 && Math.abs(st.velocity) < 2) {
      st.value = st.target
      st.velocity = 0
      st.done = true
      o.onUpdate?.(st.value)
      o.onComplete?.()
      return
    }
    o.onUpdate?.(st.value)
    st.raf = requestAnimationFrame(frame)
  }

  if (reduceMotion.value) {
    st.value = st.target
    st.done = true
    o.onUpdate?.(st.value)
    o.onComplete?.()
  } else {
    st.raf = requestAnimationFrame(frame)
  }

  return {
    get value() {
      return st.value
    },
    get velocity() {
      return st.velocity
    },
    get done() {
      return st.done
    },
    stop() {
      cancelAnimationFrame(st.raf)
      st.done = true
    },
    retarget(to: number) {
      st.target = to
      if (st.done) {
        st.done = false
        last = 0
        st.raf = requestAnimationFrame(frame)
      }
    },
  }
}

/** Where a flick will come to rest (Apple's projection; d 0.998 = normal scroll feel). v in px/s. */
export function project(velocity: number, deceleration = 0.998): number {
  return ((velocity / 1000) * deceleration) / (1 - deceleration)
}

/** Progressive resistance past a boundary. */
export function rubberband(overshoot: number, dimension: number, c = 0.55): number {
  return (overshoot * dimension * c) / (dimension + c * Math.abs(overshoot))
}

/** Velocity from the last ~100 ms of pointer samples (units/s). */
export class VelocityTracker {
  private samples: [number, number][] = []
  add(position: number, time = performance.now()) {
    this.samples.push([position, time])
    if (this.samples.length > 8) this.samples.shift()
  }
  velocity(): number {
    const s = this.samples
    const n = s.length
    if (n < 2) return 0
    const now = s[n - 1]![1]
    let i = n - 1
    while (i > 0 && now - s[i - 1]![1] < 100) i--
    const dt = (s[n - 1]![1] - s[i]![1]) / 1000
    return dt > 0 ? (s[n - 1]![0] - s[i]![0]) / dt : 0
  }
}
