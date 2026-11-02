<script setup lang="ts">
import { computed, useSlots } from 'vue'
import { formatSeconds } from '../../format'
import type { StatusTone } from '../../labels'
import StatusLine from './StatusLine.vue'

/**
 * The In Game hero card: one big clock (light, tabular) and nothing to do. Eyebrow: a green dot + "Live", or the
 * activity indicator + "Loading" with the indeterminate bar under a muted clock. Once the game ended (`result`; the
 * screen's title then says "Game Over") the eyebrow goes, the result ("Victory" / "Defeat", green / red text — the
 * word says it, not the hue) takes the clock's place as the headline and the stopped clock steps back to a
 * secondary line.
 * The clock is a silent timer. The default slot (the live kill score) goes beside the clock, at the card's end, with the
 * clock a size smaller (`clock-card`); under it when the two don't fit side by side (large text; live-game.css).
 */
/** `result`: a MatchResult once the game ended ('win' / 'loss' are shown; any other only stops the clock), '' before. */
const props = defineProps<{ seconds: number | null; loaded: boolean; result?: string }>()
const slots = useSlots()

const ended = computed(() => !!props.result)

const clockLabel = computed(() => {
  if (props.seconds === null) return 'Game loading'
  const s = Math.floor(props.seconds)
  const m = Math.floor(s / 60)
  return `${ended.value ? 'Game length' : 'Game time'} ${m} ${m === 1 ? 'minute' : 'minutes'} ${s % 60} seconds`
})
const outcome = computed(() =>
  props.result === 'win' ? { text: 'Victory', tone: 'win' } : props.result === 'loss' ? { text: 'Defeat', tone: 'loss' } : null,
)
const clockStyle = computed(() => (outcome.value ? 'title-3' : slots.default ? 'clock-card' : 'clock-hero'))

const eyebrow = computed<{ tone?: StatusTone; busy: boolean; text: string } | null>(() => {
  if (ended.value) return null
  return props.loaded ? { tone: 'green', busy: false, text: 'Live' } : { busy: true, text: 'Loading' }
})
</script>

<template>
  <div class="lr-live" :class="{ 'is-ended': ended }">
    <div class="lr-live-body">
      <div class="lr-live-time">
        <StatusLine v-if="eyebrow" class="lr-live-eyebrow footnote" :tone="eyebrow.tone" :busy="eyebrow.busy" :text="eyebrow.text" />
        <p v-if="outcome" class="lr-live-result large-title" :class="`is-${outcome.tone}`">{{ outcome.text }}</p>
        <span
          class="lr-live-clock"
          :class="[clockStyle, { 'is-loading': seconds === null }]"
          role="timer"
          aria-live="off"
          :aria-label="clockLabel"
          >{{ formatSeconds(seconds ?? 0) }}</span
        >
        <div v-if="!loaded" class="lr-progress" role="progressbar" aria-label="Loading screen"><i /></div>
      </div>
      <slot />
    </div>
  </div>
</template>
