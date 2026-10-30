<script setup lang="ts">
import { computed } from 'vue'

/**
 * Unread count. Default: accent capsule at the end of a row. `on`: the small red badge pinned to the top-trailing
 * corner of a symbol (tab bar, nav button), fixed in size like the bars it sits on; its parent must be
 * `position: relative`. VoiceOver hears "3 unread".
 */
const props = defineProps<{ count: number; on?: boolean }>()
const MAX = 99
const text = computed(() => (props.count > MAX ? `${MAX}+` : String(props.count)))
</script>

<template>
  <span v-if="count > 0" class="lr-badge" :class="[on ? 'lr-badge--on tab-label' : 'caption-1']">
    <span aria-hidden="true">{{ text }}</span>
    <span class="lr-sr">{{ count }} unread</span>
  </span>
</template>
