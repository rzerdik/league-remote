<script setup lang="ts">
import { computed } from 'vue'
import StatusDot from '../../components/lr/StatusDot.vue'
import type { LiveTeam } from '../../contracts'
import { knowsMyTeam } from '../../liveGame'

/**
 * The kill score on the In Game card, your team (blue dot) against the enemy (red dot), like the game's own top bar;
 * several enemy teams (an unexpected mode) are added up. Spectating: Blue Side against Red Side. Nothing when neither
 * is known. One phrase for VoiceOver. Beside the clock: the score over the two names. Under it (large text): one
 * wrapping line.
 */
const props = defineProps<{ teams: LiveTeam[] }>()

const score = computed(() => {
  if (knowsMyTeam.value) {
    return {
      left: { name: 'Your Team', kills: props.teams.find((t) => t.isMyTeam)?.kills ?? 0 },
      right: { name: 'Enemy', kills: props.teams.filter((t) => !t.isMyTeam).reduce((n, t) => n + t.kills, 0) },
    }
  }
  const blue = props.teams.find((t) => t.side === 'ORDER')
  const red = props.teams.find((t) => t.side === 'CHAOS')
  return blue && red ? { left: { name: 'Blue Side', kills: blue.kills }, right: { name: 'Red Side', kills: red.kills } } : null
})
const label = computed(() =>
  score.value ? `Kills: ${score.value.left.name} ${score.value.left.kills}, ${score.value.right.name} ${score.value.right.kills}` : '',
)
</script>

<template>
  <p v-if="score" class="live-score" role="img" :aria-label="label">
    <span class="live-score-team">
      <StatusDot tone="blue" />
      <span class="footnote lr-secondary">{{ score.left.name }}</span>
    </span>
    <!-- The score is one unit: it never breaks between its numbers ("5 –" / "3"), the team names wrap around it. -->
    <span class="title-2 live-score-n">{{ score.left.kills }}<span class="lr-secondary"> – </span>{{ score.right.kills }}</span>
    <span class="live-score-team">
      <span class="footnote lr-secondary">{{ score.right.name }}</span>
      <StatusDot tone="red" />
    </span>
  </p>
</template>

<style scoped>
.live-score {
  margin: var(--space-2) 0 0;
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: var(--space-1) var(--space-3);
}
.live-score-team {
  display: inline-flex;
  align-items: baseline;
  gap: var(--space-2);
  white-space: nowrap;
}
.live-score-team :deep(.lr-dot) {
  align-self: center;
}
.live-score-n {
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}
/* Same width as LiveGameCard's side-by-side layout (live-game.css). */
@container lr-live (min-width: 19.5em) {
  .live-score {
    margin: 0;
    flex: none;
    display: grid;
    grid-template-columns: auto auto;
    justify-items: center;
    gap: var(--space-1) var(--space-3);
  }
  .live-score-n {
    grid-column: 1 / -1;
    grid-row: 1;
  }
}
</style>
