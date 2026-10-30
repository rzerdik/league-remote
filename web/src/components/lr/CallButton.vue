<script setup lang="ts">
import { CheckIcon, XIcon } from '@lucide/vue'

/**
 * A Ready Check answer, laid out like the incoming-call buttons: a coloured circle with its label under it.
 * Decline (red, xmark) leads, Accept (green, dark check, breathing ring) trails. The label is the accessible name.
 */
defineProps<{ kind: 'accept' | 'decline'; label: string; disabled?: boolean }>()
</script>

<template>
  <button type="button" class="lr-call-btn" :class="`lr-call-btn--${kind}`" :disabled="disabled">
    <span>
      <CheckIcon v-if="kind === 'accept'" :stroke-width="2.75" aria-hidden="true" />
      <XIcon v-else :stroke-width="2.75" aria-hidden="true" />
    </span>
    <span class="subheadline">{{ label }}</span>
  </button>
</template>
