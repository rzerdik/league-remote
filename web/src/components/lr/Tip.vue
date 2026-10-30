<script setup lang="ts">
import { XIcon } from '@lucide/vue'
import type { Component } from 'vue'
import LrButton from './LrButton.vue'

/**
 * TipKit-style inline tip that teaches one feature; dismissible for good (the consumer persists it). The text moves
 * under the symbol and the dismiss button at large text sizes (a container query in components.css).
 */
defineProps<{ icon: Component; title: string; message?: string; actionLabel?: string; actionBusy?: boolean }>()
defineEmits<{ action: []; dismiss: [] }>()
</script>

<template>
  <div class="lr-tip">
    <div class="lr-tip-grid">
      <component :is="icon" class="lr-tip-icon" :stroke-width="1.6" aria-hidden="true" />
      <div class="lr-tip-body">
        <span class="headline">{{ title }}</span>
        <p class="subheadline"><slot>{{ message }}</slot></p>
        <LrButton v-if="actionLabel" variant="plain" :loading="actionBusy" @click="$emit('action')">{{ actionLabel }}</LrButton>
      </div>
      <button type="button" class="lr-tip-close" aria-label="Dismiss Tip" @click="$emit('dismiss')">
        <XIcon class="lr-icon" :stroke-width="2.5" aria-hidden="true" />
      </button>
    </div>
  </div>
</template>
