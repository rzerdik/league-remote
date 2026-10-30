<script setup lang="ts">
import { CircleAlertIcon } from '@lucide/vue'
import { computed } from 'vue'
import EmptyState from '../../components/lr/EmptyState.vue'
import LargeTitle from '../../components/lr/LargeTitle.vue'
import type { MatchSummary } from '../../contracts'
import { matchDate } from '../../format'
import { matchDuration, matchQueue, resultLabel } from '../../profile'
import { enemyNumbers } from '../../teams'
import TeamSection from './TeamSection.vue'
import { useMatchDetails } from './useMatchDetails'

/**
 * A game of the match history as a pushed view (Profile tab: back button, no tab bar). The header comes from the
 * summary right away (result, queue, length, date); the teams arrive with 'LoadMatchDetails' — a spinner until then,
 * an error with Try Again if they don't. Own team first. `puuid` + `playerName`: one of another player's games (their
 * profile view): their team first, under their name, their row highlighted.
 */
const props = defineProps<{ match: MatchSummary; puuid?: string; playerName?: string }>()

const { details, status, error, load } = useMatchDetails(
  () => props.match.gameId,
  () => props.puuid ?? null,
)

const subtitle = computed(() => [matchQueue(props.match), matchDuration(props.match), matchDate(props.match.startedAt)].filter(Boolean).join(' · '))
const enemyNumber = computed(() => enemyNumbers(details.value?.teams ?? []))
</script>

<template>
  <div class="lr-content lr-swipe-follow">
    <LargeTitle :title="resultLabel(match.result)">{{ subtitle }}</LargeTitle>

    <EmptyState v-if="status === 'loading'" busy title="Loading Game" />
    <EmptyState
      v-else-if="status === 'error'"
      :icon="CircleAlertIcon"
      title="Couldn’t Load This Game"
      :message="error"
      action-label="Try Again"
      @action="load"
    />
    <template v-else-if="details">
      <TeamSection
        v-for="(t, i) in details.teams"
        :key="t.teamId"
        :team="t"
        :match="match"
        :enemy-number="enemyNumber[i]"
        :player-name="playerName"
      />
    </template>
  </div>
</template>
