<script setup lang="ts">
import { computed } from 'vue'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import type { MatchSummary, MatchTeam } from '../../contracts'
import { championName } from '../../ddragon'
import { ARENA_MAP_ID, finishedTeamLine } from '../../teams'
import ParticipantRow from './ParticipantRow.vue'
import TeamTotals from './TeamTotals.vue'

/**
 * One team of a game's details: "Your Team" / "Enemy Team" with its result and side, its totals and objectives, its
 * bans, then its players (in the game's order: top, jungle, mid, bottom, support in matchmade games). `playerName`:
 * another player's game (their profile), so their team is "Vexa’s Team" and their row isn't "you".
 */
const props = defineProps<{ team: MatchTeam; match: MatchSummary; enemyNumber?: number; playerName?: string }>()

const title = computed(() => {
  if (props.team.isMyTeam) return props.playerName ? `${props.playerName}’s Team` : 'Your Team'
  return props.enemyNumber ? `Enemy Team ${props.enemyNumber}` : 'Enemy Team'
})

const trailing = computed(() =>
  finishedTeamLine(
    { teamId: props.team.teamId, win: props.team.win, placement: props.team.participants[0]?.placement ?? 0 },
    props.match.result,
    props.match.mapId === ARENA_MAP_ID,
  ),
)

const bans = computed(() => props.team.bans.filter((id) => id > 0))
const bansLabel = computed(() => `Bans: ${bans.value.map(championName).join(', ')}`)
</script>

<template>
  <ListSection :header="title" :header-trailing="trailing">
    <TeamTotals :team="team" :map-id="match.mapId" />
    <ListRow v-if="bans.length" text title="Bans">
      <template #trailing>
        <span class="lr-spells" role="img" :aria-label="bansLabel">
          <ChampionIcon v-for="(id, i) in bans" :key="i" :champion-id="id" size="ban" state="banned" decorative />
        </span>
      </template>
    </ListRow>
    <ParticipantRow
      v-for="(p, i) in team.participants"
      :key="i"
      :player="p"
      :duration-seconds="match.durationSeconds"
      :map-id="match.mapId"
      :me-label="playerName"
    />
  </ListSection>
</template>
