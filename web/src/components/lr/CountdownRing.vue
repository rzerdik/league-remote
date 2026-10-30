<script setup lang="ts">
import { computed } from 'vue'
import { countdownAnnouncement } from '../../countdown'

/**
 * The Ready Check countdown: a ring that empties linearly around the whole seconds left (SF Pro Rounded, tabular).
 * Green, red in the last seconds. `role="timer"` is silent; VoiceOver hears "10 seconds left" and "5 seconds left".
 */
const props = defineProps<{ remaining: number; total: number }>()

// The ring turns red for the last seconds (design system › CountdownRing).
const RED_UNDER_S = 4
// SVG user units: the ring is drawn in a 210-unit box and scales with the element.
const BOX = 210
const STROKE = 10
const R = (BOX - STROKE) / 2
const CIRC = 2 * Math.PI * R

const seconds = computed(() => Math.max(0, Math.ceil(props.remaining)))
const dashOffset = computed(() => {
  const fraction = Math.max(0, Math.min(1, props.remaining / props.total))
  return (CIRC * (1 - fraction)).toFixed(2)
})
</script>

<template>
  <div class="lr-countdown" role="timer" aria-live="off" :aria-label="`${seconds} seconds left`">
    <svg class="lr-ring" :class="{ 'is-urgent': remaining < RED_UNDER_S }" :viewBox="`0 0 ${BOX} ${BOX}`" aria-hidden="true">
      <circle class="lr-ring-track" :cx="BOX / 2" :cy="BOX / 2" :r="R" fill="none" :stroke-width="STROKE" />
      <circle
        class="lr-ring-bar"
        :cx="BOX / 2"
        :cy="BOX / 2"
        :r="R"
        fill="none"
        :stroke-width="STROKE"
        :stroke-dasharray="CIRC.toFixed(2)"
        :stroke-dashoffset="dashOffset"
      />
    </svg>
    <span class="countdown" aria-hidden="true">{{ seconds }}</span>
  </div>
  <span class="lr-sr" aria-live="polite">{{ countdownAnnouncement(remaining) }}</span>
</template>
