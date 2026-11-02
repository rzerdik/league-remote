<script setup lang="ts">
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import RankIcon from '../../components/lr/RankIcon.vue'
import type { RankedQueueStats } from '../../contracts'
import { plural, record, winRate } from '../../format'
import { rankLabel, rankedQueueName } from '../../labels'

/**
 * Ranks per queue, as the client lists them: Solo/Duo, Flex (also when unranked), then other ranked queues that have a
 * tier. Each row: the rank ("Gold II", "Unranked", "Placements"), the queue, and at the end LP over wins / losses.
 */
defineProps<{ ranks: RankedQueueStats[] }>()

function title(q: RankedQueueStats): string {
  return rankLabel(q.tier, q.division) || (q.isProvisional && q.provisionalGamesRemaining > 0 ? 'Placements' : 'Unranked')
}

/** "47 LP"; placements left while placing; '' unranked. */
function headline(q: RankedQueueStats): string {
  if (q.tier) return `${q.leaguePoints} LP`
  if (q.isProvisional && q.provisionalGamesRemaining > 0) return `${plural(q.provisionalGamesRemaining, 'game')} left`
  return ''
}

/** "34W 28L · 55%"; "92 wins" when League doesn't report the losses (other players); '' without games. */
function recordLine(q: RankedQueueStats): string {
  if (q.losses === null) return q.wins > 0 ? plural(q.wins, 'win') : ''
  const rate = winRate(q.wins, q.losses)
  return rate ? `${record(q.wins, q.losses)} · ${rate}` : ''
}

/** Series in progress ("W L –"), as one character per game; '' outside a series. */
function series(q: RankedQueueStats): string {
  if (!q.promos) return ''
  return `Series ${[...q.promos].map((c) => (c === 'W' ? 'W' : c === 'L' ? 'L' : '–')).join(' ')}`
}
</script>

<template>
  <ListSection v-if="ranks.length" header="Ranked">
    <ListRow
      v-for="q in ranks"
      :key="q.queueType"
      wrap
      :title="title(q)"
      :subtitle="[rankedQueueName(q.queueType), series(q)].filter(Boolean).join(' · ')"
    >
      <template #lead><RankIcon :tier="q.tier" /></template>
      <template v-if="headline(q) || recordLine(q)" #trailing>
        <span class="lr-row-meta">
          <span v-if="headline(q)" class="body rank-lp">{{ headline(q) }}</span>
          <span v-if="recordLine(q)" class="footnote rank-record">{{ recordLine(q) }}</span>
        </span>
      </template>
    </ListRow>
  </ListSection>
</template>

<style scoped>
.rank-lp {
  color: var(--label);
  font-variant-numeric: tabular-nums;
}
.rank-record {
  font-variant-numeric: tabular-nums;
}
</style>
