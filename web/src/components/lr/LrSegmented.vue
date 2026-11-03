<script setup lang="ts" generic="T extends string">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { haptic } from '../../haptics'
import { animateSpring, reduceMotion, VelocityTracker, type Spring } from '../../lib/spring'
import { SPRING } from '../../styles/tokens'

/**
 * iOS segmented control switching between views of one screen (In Game: Overview · Scoreboard · Events). A capsule
 * track with a thumb that slides to the selected segment (spring-snappy; a cross-fade under Reduce Motion). Drag the
 * thumb sideways and it follows the finger as a glass lens, then settles on the nearest segment with the release
 * velocity (under Reduce Motion it hops between segments; not while the segments wrap). Tabs for VoiceOver: `role="tablist"`, the selected one `aria-selected`, arrow keys move between them. The panel it switches is
 * the parent's: give it `role="tabpanel"`, `:id="panelId(id)"` and `:aria-labelledby="tabId(id, value)"`.
 * At large text sizes the segments wrap onto more rows (never truncate) and the track becomes a rounded rectangle.
 */
/** Name the control with `aria-label` (it lands on the tablist). */
const props = defineProps<{ id: string; options: { value: T; label: string }[] }>()
const model = defineModel<T>({ required: true })

const root = ref<HTMLElement | null>(null)
/** By option index (a function ref: a v-for ref array isn't kept in order). */
const buttons: (HTMLButtonElement | null)[] = []
const thumb = ref<{ x: number; y: number; w: number; h: number } | null>(null)
const wrapped = ref(false)
/** No slide on the first placement (or after a resize): the thumb appears where it belongs. */
const placed = ref(false)

function measure(animate: boolean) {
  const i = props.options.findIndex((o) => o.value === model.value)
  const el = buttons[i]
  if (!el) return
  placed.value = animate && thumb.value !== null
  thumb.value = { x: el.offsetLeft, y: el.offsetTop, w: el.offsetWidth, h: el.offsetHeight }
  wrapped.value = buttons.some((b) => !!b && b.offsetTop !== buttons[0]?.offsetTop)
}

let observer: ResizeObserver | null = null
onMounted(() => {
  measure(false)
  observer = new ResizeObserver(() => measure(false))
  if (root.value) observer.observe(root.value)
})
onBeforeUnmount(() => observer?.disconnect())
watch(model, () => void nextTick(() => measure(true)))

function select(value: T) {
  model.value = value
}

// ----- dragging the thumb -----
/** Travel (px) before a press on the selected segment becomes a drag. */
const HYSTERESIS = 4
/** The thumb's x while the finger has it or it settles afterwards; null when it rests on its segment. */
const liveX = ref<number | null>(null)
const dragging = ref(false)
const thumbTransform = computed(() => `translate(${liveX.value ?? thumb.value?.x ?? 0}px, ${thumb.value?.y ?? 0}px)`)
let drag: { pointer: number; startX: number; grabX: number; vt: VelocityTracker; hovered: number } | null = null
let settle: Spring | null = null
/** A drag's release also clicks the segment it started on: that click must not select it again. */
let swallowClick = false

/** The segment whose middle is nearest to a thumb at `x`. */
function nearestIndex(x: number): number {
  let best = 0
  let distance = Infinity
  buttons.forEach((b, i) => {
    if (!b) return
    const d = Math.abs(b.offsetLeft - x)
    if (d < distance) [best, distance] = [i, d]
  })
  return best
}

function onThumbDown(e: PointerEvent, value: T) {
  swallowClick = false
  if (value !== model.value || wrapped.value || !thumb.value) return
  settle?.stop()
  drag = { pointer: e.pointerId, startX: e.clientX, grabX: liveX.value ?? thumb.value.x, vt: new VelocityTracker(), hovered: -1 }
  drag.vt.add(e.clientX)
}

function onThumbMove(e: PointerEvent) {
  if (!drag || drag.pointer !== e.pointerId || !thumb.value) return
  const dx = e.clientX - drag.startX
  if (!dragging.value) {
    if (Math.abs(dx) < HYSTERESIS) return
    dragging.value = true
    placed.value = false
    ;(e.currentTarget as Element).setPointerCapture?.(e.pointerId)
  }
  drag.vt.add(e.clientX)
  const last = buttons[props.options.length - 1]
  const x = Math.min(last?.offsetLeft ?? 0, Math.max(buttons[0]?.offsetLeft ?? 0, drag.grabX + dx))
  const over = nearestIndex(x)
  if (over !== drag.hovered) {
    if (drag.hovered >= 0) haptic('selection')
    drag.hovered = over
  }
  liveX.value = reduceMotion.value ? (buttons[over]?.offsetLeft ?? x) : x
}

function onThumbUp(e: PointerEvent) {
  if (!drag || drag.pointer !== e.pointerId) return
  const velocity = drag.vt.velocity()
  const hovered = drag.hovered
  drag = null
  if (!dragging.value) {
    // Grabbed mid-settle and let go without moving: finish settling on its segment.
    if (liveX.value !== null) settleOn(props.options.findIndex((o) => o.value === model.value), 0)
    return
  }
  dragging.value = false
  // A cancelled gesture clicks nothing, so there is no click to swallow.
  swallowClick = e.type === 'pointerup'
  settleOn(hovered, velocity)
  const option = props.options[hovered]
  if (option) select(option.value)
}
function settleOn(index: number, velocity: number) {
  const target = buttons[index]
  if (!target) {
    liveX.value = null
    return
  }
  settle = animateSpring({
    from: liveX.value ?? target.offsetLeft,
    to: target.offsetLeft,
    velocity,
    ...SPRING.snappy,
    onUpdate: (x) => (liveX.value = x),
    onComplete: () => (liveX.value = null),
  })
}
onBeforeUnmount(() => settle?.stop())

function onSegmentClick(e: MouseEvent, value: T) {
  // A keyboard activation (detail 0) is never the click that ends a drag.
  const swallow = swallowClick && e.detail > 0
  swallowClick = false
  if (!swallow) select(value)
}

/** Arrow keys move the selection (and focus) like a native tab bar; Home / End jump to the ends. */
function onKey(e: KeyboardEvent, index: number) {
  const n = props.options.length
  const next =
    e.key === 'ArrowRight' || e.key === 'ArrowDown'
      ? (index + 1) % n
      : e.key === 'ArrowLeft' || e.key === 'ArrowUp'
        ? (index - 1 + n) % n
        : e.key === 'Home'
          ? 0
          : e.key === 'End'
            ? n - 1
            : -1
  if (next < 0) return
  e.preventDefault()
  select(props.options[next]!.value)
  buttons[next]?.focus()
}
</script>

<script lang="ts">
/** The id of a segment (for the panel's aria-labelledby). */
export function tabId(id: string, value: string): string {
  return `${id}-tab-${value}`
}
/** The id the switched panel carries (aria-controls of every segment). */
export function panelId(id: string): string {
  return `${id}-panel`
}
</script>

<template>
  <div ref="root" class="lr-segmented" :class="{ 'is-wrapped': wrapped }" role="tablist">
    <span
      v-if="thumb"
      class="lr-segmented-thumb"
      :class="{ 'is-sliding': placed && liveX === null, 'is-dragging': dragging }"
      :style="{ transform: thumbTransform, width: `${thumb.w}px`, height: `${thumb.h}px` }"
      aria-hidden="true"
    />
    <button
      v-for="(o, i) in options"
      :id="tabId(id, o.value)"
      :key="o.value"
      :ref="(el) => (buttons[i] = el as HTMLButtonElement | null)"
      type="button"
      class="lr-segment subheadline"
      role="tab"
      :aria-selected="o.value === model"
      :aria-controls="panelId(id)"
      :tabindex="o.value === model ? 0 : -1"
      @click="onSegmentClick($event, o.value)"
      @keydown="onKey($event, i)"
      @pointerdown="onThumbDown($event, o.value)"
      @pointermove="onThumbMove"
      @pointerup="onThumbUp"
      @pointercancel="onThumbUp"
    >
      {{ o.label }}
    </button>
  </div>
</template>
