<script setup lang="ts">
import { cssSize } from '../../styles/tokens'

/**
 * iOS-style activity indicator: 8 spokes, stepped rotation (keeps turning under Reduce Motion). Sizes 14 (in text),
 * 18 (in buttons), 20 (rows), 36 (empty states). Inherits currentColor (label-secondary by default). `decorative`
 * when the text next to it already says what is loading.
 */
withDefaults(defineProps<{ size?: 14 | 18 | 20 | 36; label?: string; decorative?: boolean }>(), {
  size: 20,
  label: 'Loading',
  decorative: false,
})
const SPOKES = Array.from({ length: 8 }, (_, i) => ({ angle: i * 45, opacity: (0.25 + 0.75 * (i / 7)).toFixed(2) }))
</script>

<template>
  <svg
    class="lr-spinner"
    :style="{ '--s': cssSize(size) }"
    viewBox="0 0 24 24"
    :role="decorative ? undefined : 'progressbar'"
    :aria-label="decorative ? undefined : label"
    :aria-hidden="decorative || undefined"
  >
    <line
      v-for="s in SPOKES"
      :key="s.angle"
      x1="12"
      y1="2.5"
      x2="12"
      y2="7"
      stroke="currentColor"
      stroke-width="2.4"
      stroke-linecap="round"
      :opacity="s.opacity"
      :transform="`rotate(${s.angle} 12 12)`"
    />
  </svg>
</template>
