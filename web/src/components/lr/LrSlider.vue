<script setup lang="ts">
import { Volume1Icon, Volume2Icon } from '@lucide/vue'
import { computed } from 'vue'

/** Volume slider: accent-filled track, white pill thumb; min/max volume symbols at the ends. */
const model = defineModel<number>({ required: true })
const props = withDefaults(defineProps<{ min?: number; max?: number; step?: number; label: string; muted?: boolean }>(), {
  min: 0,
  max: 100,
  step: 5,
})
defineEmits<{ input: []; change: [] }>()

const fill = computed(() => `${((model.value - props.min) / (props.max - props.min)) * 100}%`)
</script>

<template>
  <span class="lr-slider-row" :class="{ 'is-muted': muted }">
    <Volume1Icon class="lr-icon" :stroke-width="1.75" aria-hidden="true" />
    <input
      v-model.number="model"
      class="lr-slider"
      :class="{ 'is-muted': muted }"
      type="range"
      :min="min"
      :max="max"
      :step="step"
      :style="{ '--lr-value': fill }"
      :aria-label="label"
      @input="$emit('input')"
      @change="$emit('change')"
    />
    <Volume2Icon class="lr-icon" :stroke-width="1.75" aria-hidden="true" />
  </span>
</template>
