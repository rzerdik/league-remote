<script setup lang="ts">
import { CircleCheckIcon, InfoIcon, TriangleAlertIcon, WifiOffIcon } from '@lucide/vue'
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch, type Component } from 'vue'
import { focusQuietly } from '../../composables/useModalLayer'
import { haptic } from '../../haptics'
import { animateSpring, rubberband, VelocityTracker, type Spring } from '../../lib/spring'
import { dismissToast, store, type Toast } from '../../store'
import { DURATION, SPRING } from '../../styles/tokens'

/**
 * Glass banners in iOS notification proportions, dropping from the top to just below the status bar (clear of its
 * blur), fed by store.toasts (toast()), newest on top; info and success for dur-banner, warnings and errors for
 * dur-banner-error, paused while touched; swipe up to dismiss (1:1, rubber-band downward). Actionable banners (swap
 * requests, invites) are teleported into #lr-banners-actions above them and stay until answered.
 * A stack never covers more than two cards' worth of the screen: the newest toast always shows, older ones fold into
 * a "+N More" capsule under it while action banners take the room (tap it to unfold them). At large text sizes the
 * toasts drop their message line (components.css) — the title says what happened.
 */
interface Shown {
  id: number
  kind: Toast['kind']
  title: string
  message: string
  y: number
  timer?: ReturnType<typeof setTimeout>
  remaining: number
  startedAt: number
}

/** Toasts kept at all (older ones are dismissed). */
const MAX_KEPT = 3
/** Cards on screen at once, action banners included, unless the user unfolds the stack. */
const MAX_CARDS = 2
/** Time left after a touch ends, at least (ms). */
const MIN_AFTER_TOUCH_MS = 800
/** A swipe up dismisses past this offset (px) or this upward speed (px/s). */
const DISMISS_OFFSET = -24
const DISMISS_VELOCITY = -300
/** Rubber-band range when pulled down (px). */
const PULL_RANGE = 120

const KIND: Record<Toast['kind'], { icon: Component; tone: string; ms: number }> = {
  info: { icon: InfoIcon, tone: 'lr-sym-blue', ms: DURATION.banner },
  success: { icon: CircleCheckIcon, tone: 'lr-sym-green', ms: DURATION.banner },
  warning: { icon: WifiOffIcon, tone: 'lr-sym-orange', ms: DURATION.bannerError },
  error: { icon: TriangleAlertIcon, tone: 'lr-sym-red', ms: DURATION.bannerError },
}

const shown = reactive<Shown[]>([])
const seen = new Set<number>()

/** "Pairing is no longer valid. Please pair again." → title + message. */
function split(text: string): { title: string; message: string } {
  const m = /^(.+?[.!?…])\s+(.+)$/.exec(text.trim())
  const title = (m ? m[1]! : text.trim()).replace(/\.$/, '')
  return { title, message: m ? m[2]! : '' }
}

function schedule(b: Shown) {
  clearTimeout(b.timer)
  b.startedAt = Date.now()
  b.timer = setTimeout(() => dismiss(b.id), b.remaining)
}
function pause(b: Shown) {
  clearTimeout(b.timer)
  b.remaining = Math.max(MIN_AFTER_TOUCH_MS, b.remaining - (Date.now() - b.startedAt))
}

function dismiss(id: number) {
  const i = shown.findIndex((b) => b.id === id)
  if (i < 0) return
  clearTimeout(shown[i]!.timer)
  shown.splice(i, 1)
  dismissToast(id)
}

watch(
  () => store.toasts.map((t) => t.id),
  () => {
    for (const t of store.toasts) {
      if (seen.has(t.id)) continue
      seen.add(t.id)
      const b: Shown = reactive({ id: t.id, kind: t.kind, ...split(t.text), y: 0, remaining: t.ms ?? KIND[t.kind].ms, startedAt: 0 })
      shown.unshift(b)
      schedule(b)
      if (t.kind === 'error' || t.kind === 'warning') haptic(t.kind)
      while (shown.length > MAX_KEPT) dismiss(shown[shown.length - 1]!.id)
    }
  },
  { immediate: true },
)
onBeforeUnmount(() => shown.forEach((b) => clearTimeout(b.timer)))

/** Action banners on screen (they are teleported in by their owners, so counted from the DOM). */
const actionCount = ref(0)
let observer: MutationObserver | null = null
onMounted(() => {
  const host = document.getElementById('lr-banners-actions')
  if (!host) return
  const count = () => (actionCount.value = host.querySelectorAll(':scope > .lr-banner').length)
  observer = new MutationObserver(count)
  observer.observe(host, { childList: true })
  count()
})
onBeforeUnmount(() => observer?.disconnect())

/** The user tapped "+N More": every toast shows until the stack is small again. */
const unfolded = ref(false)
const room = computed(() => (unfolded.value ? shown.length : Math.max(1, MAX_CARDS - actionCount.value)))
const visible = computed(() => shown.slice(0, room.value))
const folded = computed(() => shown.length - visible.value.length)
watch(
  () => shown.length + actionCount.value <= MAX_CARDS,
  (small) => {
    if (small) unfolded.value = false
  },
)
/** Unfold, and move focus to the newest toast: the button goes away, so focus would otherwise drop to <body>. */
async function unfold() {
  haptic('selection')
  unfolded.value = true
  await nextTick()
  const newest = shown[0] && hostEl.value?.querySelector<HTMLElement>(`[data-toast-id="${shown[0].id}"]`)
  if (newest) focusQuietly(newest)
}
const hostEl = ref<HTMLElement | null>(null)

// ----- swipe up to dismiss -----
let drag: { id: number; y0: number; pointer: number; vt: VelocityTracker } | null = null
const springs = new Map<number, Spring>()
function onDown(e: PointerEvent, b: Shown) {
  springs.get(b.id)?.stop()
  pause(b)
  drag = { id: b.id, y0: e.clientY - b.y, pointer: e.pointerId, vt: new VelocityTracker() }
  drag.vt.add(e.clientY)
  ;(e.currentTarget as Element).setPointerCapture?.(e.pointerId)
}
function onMove(e: PointerEvent, b: Shown) {
  if (!drag || drag.pointer !== e.pointerId) return
  drag.vt.add(e.clientY)
  const dy = e.clientY - drag.y0
  b.y = dy > 0 ? rubberband(dy, PULL_RANGE) : dy
}
function onUp(e: PointerEvent, b: Shown) {
  if (!drag || drag.pointer !== e.pointerId) return
  const v = drag.vt.velocity()
  drag = null
  if (b.y < DISMISS_OFFSET || v < DISMISS_VELOCITY) {
    // Up and out of sight: past the top of the screen by its own height.
    const out = -((e.currentTarget as HTMLElement).getBoundingClientRect().bottom + 1)
    springs.set(
      b.id,
      animateSpring({ from: b.y, to: b.y + out, velocity: v, ...SPRING.snappy, onUpdate: (y) => (b.y = y), onComplete: () => dismiss(b.id) }),
    )
    return
  }
  springs.set(b.id, animateSpring({ from: b.y, to: 0, velocity: v, ...SPRING.snappy, onUpdate: (y) => (b.y = y) }))
  schedule(b)
}
</script>

<template>
  <div ref="hostEl" class="lr-banner-host">
    <div id="lr-banners-actions" class="lr-banners-actions" />
    <TransitionGroup name="lr-banner">
      <div
        v-for="b in visible"
        :key="b.id"
        :data-toast-id="b.id"
        tabindex="-1"
        class="lr-banner lr-glass-thick"
        :role="b.kind === 'error' || b.kind === 'warning' ? 'alert' : 'status'"
        :style="b.y ? { transform: `translateY(${b.y}px)` } : undefined"
        @pointerdown="onDown($event, b)"
        @pointermove="onMove($event, b)"
        @pointerup="onUp($event, b)"
        @pointercancel="onUp($event, b)"
      >
        <component :is="KIND[b.kind].icon" class="lr-icon" :class="KIND[b.kind].tone" :stroke-width="1.75" aria-hidden="true" />
        <span class="lr-banner-text">
          <span class="headline">{{ b.title }}</span>
          <span v-if="b.message" class="subheadline">{{ b.message }}</span>
        </span>
      </div>
      <button
        v-if="folded"
        key="more"
        type="button"
        class="lr-banner-more lr-glass-thick footnote"
        :aria-label="`Show ${folded} More ${folded === 1 ? 'Notification' : 'Notifications'}`"
        @click="unfold"
      >
        {{ folded }} More
      </button>
    </TransitionGroup>
  </div>
</template>

<style>
.lr-banners-actions {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  width: 100%;
}
.lr-banners-actions:empty {
  display: none;
}
/* The folded toasts: a small capsule under the stack. */
.lr-banner-more {
  pointer-events: auto;
  position: relative;
  min-height: var(--control-sm);
  padding: 0 var(--space-4);
  border: 0;
  border-radius: var(--radius-full);
  color: var(--label);
  font-weight: 600;
  cursor: pointer;
}
/* 44-pt hit region around the capsule. */
.lr-banner-more::after {
  content: '';
  position: absolute;
  inset: calc((var(--hit-min) - var(--control-sm)) / -2) 0;
}
</style>
