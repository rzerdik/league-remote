<script setup lang="ts">
import { computed } from 'vue'
import ActivityIndicator from '../../components/lr/ActivityIndicator.vue'
import ListSection from '../../components/lr/ListSection.vue'
import RankIcon from '../../components/lr/RankIcon.vue'
import Tag from '../../components/lr/Tag.vue'
import type { PostGameSummary } from '../../contracts'
import { formatSeconds } from '../../format'
import {
  lpDelta,
  lpDeltaSpoken,
  masteryGained,
  masteryLine,
  masteryTitle,
  myLine,
  postGameQueue,
  postGameTitle,
  rankAfter,
  rankChange,
} from '../../postGame'

/**
 * The result card at the top of Post-Game: the result word (a heading, so VoiceOver reads it right after the screen's
 * "Game Over"; green / red for Victory / Defeat, neutral for a Remake or an Arena place; the word says it, not the hue),
 * queue · duration, then what the game gave you as value-over-label stats: the LP change with the rank after the game
 * (the emblem beside the word), and the champion's mastery points. Then the mastery level line and the tags (Promoted /
 * Demoted, Level Up, the grade). A ranked game whose LP hasn't been reported yet says it is coming; an unranked one has
 * no LP stat. "Game Over" from postGameTitle (the PC couldn't tell which line is yours) would only repeat the screen's
 * title: the card then leads with the queue line.
 */
const props = defineProps<{ summary: PostGameSummary }>()

const word = computed(() => {
  const title = postGameTitle(props.summary)
  return title === 'Game Over' ? '' : title
})
/** Only a plain win or loss is coloured: an Arena place stays neutral, whatever `result` says about the top four. */
const tone = computed(() => {
  const me = myLine(props.summary)
  if (!me || me.placement > 0) return ''
  return props.summary.result === 'win' ? 'is-win' : props.summary.result === 'loss' ? 'is-loss' : ''
})
const meta = computed(() => {
  const s = props.summary
  return [postGameQueue(s), formatSeconds(s.durationSeconds)].filter(Boolean).join(' · ')
})

const rank = computed(() => props.summary.rank)
const lpPending = computed(() => !rank.value && props.summary.isRanked)
const lpTone = computed(() => {
  const d = rank.value?.leaguePointsDelta ?? 0
  return d > 0 ? 'is-win' : d < 0 ? 'is-loss' : ''
})
const change = computed(() => (rank.value ? rankChange(rank.value) : ''))
const mastery = computed(() => props.summary.mastery)
const hasTags = computed(() => !!change.value || !!mastery.value?.leveledUp || !!mastery.value?.grade)
</script>

<template>
  <ListSection bare>
    <div class="pg-result">
      <div class="pg-head">
        <div class="pg-head-text">
          <h2 v-if="word" class="large-title" :class="tone">{{ word }}</h2>
          <p class="subheadline lr-secondary">{{ meta }}</p>
        </div>
        <RankIcon v-if="rank" :tier="rank.tier" />
      </div>

      <dl v-if="rank || lpPending || mastery" class="pg-stats">
        <div v-if="rank" class="lr-stats-cell">
          <dt class="footnote">{{ rankAfter(rank) }}</dt>
          <dd class="title-3" :class="lpTone" :aria-label="lpDeltaSpoken(rank)">{{ lpDelta(rank) }}</dd>
        </div>
        <div v-else-if="lpPending" class="lr-stats-cell">
          <dt class="footnote">LP Change</dt>
          <dd class="title-3 pg-pending"><ActivityIndicator :size="18" decorative />Updating…</dd>
        </div>
        <div v-if="mastery" class="lr-stats-cell">
          <dt class="footnote">{{ masteryTitle(mastery) }}</dt>
          <dd class="title-3">{{ masteryGained(mastery) }}</dd>
        </div>
      </dl>

      <p v-if="mastery" class="subheadline lr-secondary">{{ masteryLine(mastery) }}</p>

      <div v-if="hasTags" class="pg-tags">
        <Tag v-if="change" :tone="rank?.change === 'demoted' ? 'red' : 'accent'">{{ change }}</Tag>
        <Tag v-if="mastery?.leveledUp" tone="accent">Level Up</Tag>
        <Tag v-if="mastery?.grade">Grade {{ mastery.grade }}</Tag>
      </div>
    </div>
  </ListSection>
</template>

<style scoped>
.pg-result {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  padding: var(--space-5);
  background: var(--bg-elevated);
  border-radius: var(--radius-card);
}
.pg-head {
  display: flex;
  align-items: flex-start;
  gap: var(--space-3);
}
.pg-head-text {
  flex: 1;
  min-width: 0;
}
.pg-head h2 {
  overflow-wrap: break-word;
}
.is-win {
  color: var(--green-text);
}
.is-loss {
  color: var(--red-text);
}
/* Two stats side by side, one under the other once a value no longer fits (large text). */
.pg-stats {
  margin: 0;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, calc(var(--font-size-title-3) * 6.5)), 1fr));
  gap: var(--space-3) var(--space-4);
}
.pg-pending {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}
.pg-tags {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-1) var(--space-2);
}
</style>
