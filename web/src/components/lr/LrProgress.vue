<script setup lang="ts">
import { computed } from 'vue'

/**
 * A determinate progress bar (XP to the next level): the accent fill over a `fill` track. `value` 0–1; `label` names
 * it for VoiceOver ("Level progress"), which reads the percentage. The indeterminate bar is `.lr-progress > i`.
 */
const props = defineProps<{ value: number; label: string }>()
const percent = computed(() => Math.round(Math.min(1, Math.max(0, props.value)) * 100))
</script>

<template>
  <div
    class="lr-progress lr-progress--value"
    role="progressbar"
    :aria-label="label"
    aria-valuemin="0"
    aria-valuemax="100"
    :aria-valuenow="percent"
    :style="{ '--lr-progress': `${percent}%` }"
  >
    <b />
  </div>
</template>
