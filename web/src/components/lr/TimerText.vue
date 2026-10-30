<script setup lang="ts">
import { computed } from 'vue'
import { countdownAnnouncement, countdownState } from '../../countdown'
import { formatSeconds } from '../../format'

/**
 * A counting-down time ("0:24"), tabular: orange under 10 s, red with a 1 Hz tick under 5 s. `role="timer"` stays
 * silent; with `announce`, VoiceOver hears "10 seconds left" and "5 seconds left" once each. The type style comes
 * from `class` (title-2, title-3, timer…).
 */
defineOptions({ inheritAttrs: false })
const props = defineProps<{ seconds: number; announce?: boolean }>()

const state = computed(() => countdownState(props.seconds))
</script>

<template>
  <span
    v-bind="$attrs"
    class="lr-timer-value"
    :class="state && `is-${state}`"
    role="timer"
    aria-live="off"
    :aria-label="`${Math.ceil(seconds)} seconds left`"
    >{{ formatSeconds(seconds) }}</span
  >
  <span v-if="announce" class="lr-sr" aria-live="polite">{{ countdownAnnouncement(seconds) }}</span>
</template>
