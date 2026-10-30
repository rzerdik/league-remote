<script setup lang="ts" generic="T extends string">
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'

/**
 * iOS segmented control switching between views of one screen (In Game: Overview · Scoreboard · Events). A capsule
 * track with a thumb that slides to the selected segment (spring-snappy; a cross-fade under Reduce Motion). Tabs for
 * VoiceOver: `role="tablist"`, the selected one `aria-selected`, arrow keys move between them. The panel it switches is
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
      :class="{ 'is-sliding': placed }"
      :style="{ transform: `translate(${thumb.x}px, ${thumb.y}px)`, width: `${thumb.w}px`, height: `${thumb.h}px` }"
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
      @click="select(o.value)"
      @keydown="onKey($event, i)"
    >
      {{ o.label }}
    </button>
  </div>
</template>
