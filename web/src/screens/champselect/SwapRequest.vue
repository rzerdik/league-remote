<script setup lang="ts">
import ActionBanner from '../../components/lr/ActionBanner.vue'
import type { SwapOffer } from '../../contracts'
import { SWAP_ICON, SWAP_QUESTION } from './swaps'

/** Actionable banner for an incoming pick-order, role or champion-trade request. Stays until answered or gone. */
defineProps<{
  offer: SwapOffer
  message: string
  /** Which answer is in flight, if any. */
  busy: 'accept' | 'decline' | null
  /** Another swap command is in flight: lock both buttons. */
  disabled?: boolean
}>()
defineEmits<{ accept: []; decline: [] }>()
</script>

<template>
  <ActionBanner
    :icon="SWAP_ICON[offer.kind]"
    :title="SWAP_QUESTION[offer.kind]"
    :message="message"
    :action-label="offer.kind === 'Trade' ? 'Trade' : 'Swap'"
    :busy="busy"
    :disabled="disabled"
    @accept="$emit('accept')"
    @decline="$emit('decline')"
  />
</template>
