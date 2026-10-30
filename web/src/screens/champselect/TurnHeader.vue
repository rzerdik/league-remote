<script setup lang="ts">
import StatusDot, { type DotTone } from '../../components/lr/StatusDot.vue'
import Tag from '../../components/lr/Tag.vue'
import TimerText from '../../components/lr/TimerText.vue'

/**
 * The top of the champion picker sheet, the part that stays on screen at every detent, and the draft's status while
 * the picker is up: the turn being played ("Enemy Picks", "Declare Intent"; "Your Pick" in accent, "Your Ban" in
 * red) with the acting side's dot (`DRAFT_DOT`, as in the action bar's status), when yours comes as a tag ("You're
 * Next", "In 3 Turns") and the turn timer — the same TimerText as the action bar's status, so both turn orange and
 * red together. No timer (infinite, or run out): no "0:00".
 */
defineProps<{ title: string; tone: 'pick' | 'ban' | null; dot?: DotTone; seconds: number | null; upNext?: string }>()
</script>

<template>
  <div class="lr-turn" :class="tone && `lr-turn--${tone}`">
    <!-- The stage and when your turn comes are announced; the timer only at 10 and 5 seconds. -->
    <span class="lr-turn-text" aria-live="polite">
      <span class="lr-turn-title title-3"><StatusDot v-if="dot" :tone="dot" />{{ title }}</span>
      <Tag v-if="upNext" tone="accent">{{ upNext }}</Tag>
    </span>
    <TimerText v-if="seconds !== null" class="timer" :seconds="seconds" announce />
  </div>
</template>
