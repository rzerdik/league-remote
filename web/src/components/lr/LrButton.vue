<script setup lang="ts">
import type { Component } from 'vue'
import ActivityIndicator from './ActivityIndicator.vue'

/**
 * Capsule button. One `prominent` per screen, and it lives in the action bar. `icon` makes an icon-only circle (needs
 * `ariaLabel`); `leading` puts an icon before the label. `loading` swaps the icon for the activity indicator — pass
 * the "-ing…" label yourself ("Locking In…"). `pressed` makes it a toggle (aria-pressed, accent fill while on).
 * `clear` is clear glass, only over the camera feed.
 */
const props = withDefaults(
  defineProps<{
    variant?: 'prominent' | 'glass' | 'clear' | 'destructive' | 'success' | 'gray' | 'plain'
    size?: 'sm' | 'md' | 'lg'
    block?: boolean
    icon?: Component
    leading?: Component
    ariaLabel?: string
    loading?: boolean
    disabled?: boolean
    danger?: boolean
    pressed?: boolean
    type?: 'button' | 'submit'
  }>(),
  { variant: 'gray', size: 'md', type: 'button', pressed: undefined },
)
</script>

<template>
  <button
    :type="props.type"
    class="lr-btn headline"
    :class="[
      `lr-btn--${props.variant}`,
      props.size !== 'md' && `lr-btn--${props.size}`,
      { 'lr-btn--block': props.block, 'lr-btn--icon': !!props.icon, 'lr-btn--danger': props.danger },
      { 'lr-glass-interactive': props.variant === 'glass' || props.variant === 'clear' || props.variant === 'prominent' },
    ]"
    :disabled="props.disabled || props.loading"
    :aria-label="props.ariaLabel"
    :aria-busy="props.loading || undefined"
    :aria-pressed="props.pressed"
  >
    <ActivityIndicator v-if="props.loading" :size="18" decorative />
    <component :is="props.icon ?? props.leading" v-else-if="props.icon || props.leading" class="lr-icon" :stroke-width="1.75" aria-hidden="true" />
    <slot />
  </button>
</template>
