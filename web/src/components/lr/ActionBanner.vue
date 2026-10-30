<script setup lang="ts">
import type { Component } from 'vue'
import LrButton from './LrButton.vue'

/**
 * An actionable banner (glass-thick, drops from the top) for a request someone else made: a swap in Champion Select,
 * a lobby invite. Teleport it into `#lr-banners-actions` (BannerHost) inside a `<TransitionGroup name="lr-pop">`; it
 * stays until answered or until the request is gone. Decline (gray) leads, the positive verb (prominent) trails.
 * `busy` puts the spinner on the answer in flight; `disabled` locks both (another answer is in flight).
 * Without `actionLabel` only the decline button shows (an auto lock-in you can only cancel). `iconTone` `red` for a ban.
 * The `message` slot replaces the message text (a countdown that VoiceOver mustn't hear every second).
 */
withDefaults(
  defineProps<{
    icon: Component
    title: string
    message?: string
    actionLabel?: string
    declineLabel?: string
    busy?: 'accept' | 'decline' | null
    disabled?: boolean
    /** Only the positive action can't be taken (e.g. the invite can't be accepted right now). */
    actionDisabled?: boolean
    iconTone?: 'accent' | 'red'
  }>(),
  { declineLabel: 'Decline', busy: null, iconTone: 'accent' },
)
defineEmits<{ accept: []; decline: [] }>()
</script>

<template>
  <div class="lr-banner lr-banner--actions lr-glass-thick" role="alert">
    <div class="lr-banner-top">
      <component :is="icon" class="lr-icon" :class="`lr-sym-${iconTone}`" :stroke-width="1.75" aria-hidden="true" />
      <span class="lr-banner-text">
        <span class="headline">{{ title }}</span>
        <span v-if="message || $slots.message" class="subheadline"><slot name="message">{{ message }}</slot></span>
      </span>
    </div>
    <div class="lr-banner-actions">
      <LrButton variant="gray" size="sm" :loading="busy === 'decline'" :disabled="disabled" @click="$emit('decline')">
        {{ declineLabel }}
      </LrButton>
      <LrButton
        v-if="actionLabel"
        variant="prominent"
        size="sm"
        :loading="busy === 'accept'"
        :disabled="disabled || actionDisabled"
        @click="$emit('accept')"
      >
        {{ actionLabel }}
      </LrButton>
    </div>
  </div>
</template>
