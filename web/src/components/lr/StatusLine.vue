<script setup lang="ts">
import type { StatusTone } from '../../labels'
import ActivityIndicator from './ActivityIndicator.vue'
import StatusDot from './StatusDot.vue'

/**
 * A status word with its dot (or the activity indicator while `busy`): "● Ready", "◌ Pairing…". Never colour alone:
 * the text says the state. Inherits the font and colour of where it sits (subtitle, row value, eyebrow).
 */
defineProps<{ tone?: StatusTone; pulse?: boolean; busy?: boolean; text?: string }>()
</script>

<template>
  <span class="lr-status">
    <ActivityIndicator v-if="busy" :size="14" decorative />
    <StatusDot v-else-if="tone" :tone="tone" :pulse="pulse" />
    <span><slot>{{ text }}</slot></span>
  </span>
</template>
