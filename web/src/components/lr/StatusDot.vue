<script setup lang="ts">
import type { StatusTone } from '../../labels'

/** Status tones, plus ally blue and your own accent (Champion Select). */
export type DotTone = StatusTone | 'blue' | 'accent'

/** A status dot (decorative: the word next to it carries the meaning). `pulse` while something is in progress. */
defineProps<{ tone?: DotTone; pulse?: boolean }>()
</script>

<template>
  <span class="lr-dot" :class="[tone && `lr-dot--${tone}`, { 'lr-dot--pulse': pulse }]" aria-hidden="true" />
</template>
