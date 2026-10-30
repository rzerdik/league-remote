<script setup lang="ts">
import type { Component } from 'vue'
import type { StatusTone } from '../../labels'
import StatusDot from './StatusDot.vue'

/**
 * A small capsule label: role preferences ("Mid") at the trailing edge of a row; a state ("Picking", "Swap Sent") on
 * the row's subtitle line, in a `.lr-row-subline`, so it never pushes the list's trailing column out of line.
 * `tone` accent / red tints it; a leading `icon` or pulsing `dot` (StatusDot tone) can precede the text.
 */
defineProps<{ tone?: 'accent' | 'red'; icon?: Component; dot?: StatusTone; pulse?: boolean }>()
</script>

<template>
  <span class="lr-tag caption-1" :class="tone && `lr-tag--${tone}`">
    <StatusDot v-if="dot" :tone="dot" :pulse="pulse" />
    <component :is="icon" v-else-if="icon" :stroke-width="2" aria-hidden="true" />
    <slot />
  </span>
</template>
