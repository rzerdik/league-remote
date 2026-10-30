<script setup lang="ts">
import { computed } from 'vue'
import ListSection from '../../components/lr/ListSection.vue'
import StatGrid from '../../components/lr/StatGrid.vue'
import type { PostGameSummary, PostGameTeam } from '../../contracts'
import { formatCompact } from '../../format'
import { ARENA_MAP_ID, finishedTeamLine } from '../../teams'
import ParticipantRow from '../profile/ParticipantRow.vue'

/**
 * One team of the post-game scoreboard, like a game's details in the Profile tab: "Your Team" / "Enemy Team" with its
 * result and side (Arena: the duo's place), its totals, then its players (your team by name, enemies by champion —
 * the connector never sends their names).
 */
const props = defineProps<{ team: PostGameTeam; summary: PostGameSummary; enemyNumber?: number }>()

/** Arena: duos without sides. */
const arena = computed(() => props.summary.mapId === ARENA_MAP_ID || props.summary.gameMode === 'CHERRY')

const title = computed(() => {
  if (props.team.isMyTeam) return arena.value ? 'Your Duo' : 'Your Team'
  return props.enemyNumber ? `Enemy Team ${props.enemyNumber}` : 'Enemy Team'
})

const trailing = computed(() =>
  finishedTeamLine(
    { teamId: props.team.teamId, win: props.team.win, placement: props.team.participants[0]?.placement ?? 0 },
    props.summary.result,
    arena.value,
  ),
)

/** Kills, deaths and assists in cells of their own: a team's "39 / 21 / 41" doesn't fit one narrow cell. */
const cells = computed(() => [
  { label: 'Kills', value: String(props.team.kills) },
  { label: 'Deaths', value: String(props.team.deaths) },
  { label: 'Assists', value: String(props.team.assists) },
  { label: 'Gold', value: formatCompact(props.team.gold) },
])
</script>

<template>
  <ListSection :header="title" :header-trailing="trailing">
    <StatGrid :cells="cells" />
    <ParticipantRow
      v-for="(p, i) in team.participants"
      :key="i"
      :player="p"
      :duration-seconds="summary.durationSeconds"
      :map-id="summary.mapId"
    />
  </ListSection>
</template>
