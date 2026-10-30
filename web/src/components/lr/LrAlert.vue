<script setup lang="ts">
import {
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogOverlay,
  AlertDialogPortal,
  AlertDialogRoot,
  AlertDialogTitle,
} from 'reka-ui'
import LrButton from './LrButton.vue'

/**
 * Centred glass alert for confirmations that can't be undone (Unpair, Leave Party). Cancel leads, the action trails
 * (`destructive` = red). Escape and a tap outside = Cancel; focus is trapped inside while it is up (reka-ui).
 */
const open = defineModel<boolean>('open', { required: true })
withDefaults(
  defineProps<{ title: string; message?: string; cancelLabel?: string; actionLabel: string; destructive?: boolean }>(),
  { cancelLabel: 'Cancel' },
)
defineEmits<{ action: [] }>()
</script>

<template>
  <AlertDialogRoot v-model:open="open">
    <AlertDialogPortal>
      <AlertDialogOverlay class="lr-alert-wrap" />
      <AlertDialogContent class="lr-alert lr-glass-thick">
        <AlertDialogTitle class="headline">{{ title }}</AlertDialogTitle>
        <AlertDialogDescription v-if="message" class="subheadline">{{ message }}</AlertDialogDescription>
        <div class="lr-alert-actions">
          <AlertDialogCancel as-child><LrButton variant="gray">{{ cancelLabel }}</LrButton></AlertDialogCancel>
          <AlertDialogAction as-child>
            <LrButton :variant="destructive ? 'destructive' : 'prominent'" @click="$emit('action')">{{ actionLabel }}</LrButton>
          </AlertDialogAction>
        </div>
      </AlertDialogContent>
    </AlertDialogPortal>
  </AlertDialogRoot>
</template>
