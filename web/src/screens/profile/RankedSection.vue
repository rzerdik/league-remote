<script setup lang="ts">
import { TrophyIcon, UserIcon, UsersIcon } from '@lucide/vue'
import type { Component } from 'vue'
import type { TileColor } from '../../components/lr/IconTile.vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import { RankedQueueTypes, type RankedQueueStats } from '../../contracts'
import { plural, record, winRate } from '../../format'
import { rankLabel, rankedQueueName } from '../../labels'

/**
 * Ranks per queue, as the client lists them: Solo/Duo, Flex (also when unranked), then other ranked queues that have a
 * tier. Each row: the rank ("Gold II", "Unranked", "Placements"), the queue, and at the end LP over wins / losses.
 */
defineProps<{ ranks: RankedQueueStats[] }>()

function tile(q: RankedQueueStats): { icon: Component; color: TileColor } {
  if (q.queueType === RankedQueueTypes.solo) return { icon: UserIcon, color: 'blue' }
  if (q.queueType === RankedQueueTypes.flex) return { icon: UsersIcon, color: 'indigo' }
  return { icon: TrophyIcon, color: 'purple' }
}

function title(q: RankedQueueStats): string {
  return rankLabel(q.tier, q.division) || (q.isProvisional && q.provisionalGamesRemaining > 0 ? 'Placements' : 'Unranked')
}

/** "47 LP"; placements left while placing; '' unranked. */
function headline(q: RankedQueueStats): string {
  if (q.tier) return `${q.leaguePoints} LP`
  if (q.isProvisional && q.provisionalGamesRemaining > 0) return `${plural(q.provisionalGamesRemaining, 'game')} left`
  return ''
}

/** "34W 28L · 55%"; '' without games. */
function recordLine(q: RankedQueueStats): string {
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
      :icon="tile(q).icon"
      :tile-color="tile(q).color"
    >
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
