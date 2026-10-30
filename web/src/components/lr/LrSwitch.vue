<script setup lang="ts">
import { haptic } from '../../haptics'

/** iOS 26 switch: toggles on release, thumb thrown with spring-bouncy, selection haptic. On = green, off = fill. */
const model = defineModel<boolean>({ required: true })
defineProps<{ ariaLabel?: string; disabled?: boolean }>()

function toggle() {
  model.value = !model.value
  haptic('selection')
}
</script>

<template>
  <button type="button" class="lr-switch" role="switch" :aria-checked="model" :aria-label="ariaLabel" :disabled="disabled" @click="toggle">
    <span class="lr-switch-thumb" />
  </button>
</template>
