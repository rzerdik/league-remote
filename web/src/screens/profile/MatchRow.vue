<script setup lang="ts">
import { computed } from 'vue'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import ListRow from '../../components/lr/ListRow.vue'
import LoadoutStrip from '../../components/profile/LoadoutStrip.vue'
import type { MatchSummary } from '../../contracts'
import { kda, kdaRatio, kdaSpoken, ordinal, relativeTime } from '../../format'
import { matchDuration, matchQueue } from '../../profile'
import { ARENA_MAP_ID, hasTrinkets } from '../../teams'
import MatchResultText from './MatchResultText.vue'

/**
 * One game of the match history, like the client's list: the champion, the result (Victory / Defeat / Remake /
 * Aborted) with how long ago, the queue and length (Arena: the placement), your K / D / A with ratio and CS (per
 * minute only in the details, where there is room), and the items you ended with. Tap → the game's details.
 */
const props = defineProps<{ match: MatchSummary }>()
defineEmits<{ click: [] }>()

const me = computed(() => props.match.me)
/** A remake or aborted game nobody scored in has no line to show ("0 / 0 / 0 · 0.00 KDA" says nothing). */
const showStats = computed(() => {
  const short = props.match.result === 'remake' || props.match.result === 'aborted'
  return !short || me.value.kills + me.value.deaths + me.value.assists > 0
})
const queueLine = computed(() =>
  [matchQueue(props.match), me.value.placement > 0 ? ordinal(me.value.placement) : '', matchDuration(props.match)]
    .filter(Boolean)
    .join(' · ')
    // "Solo/Duo" may break after its slash (largest text sizes), not mid-word.
    .replaceAll('/', '/\u200B'),
)
</script>

<template>
  <ListRow tappable chevron @click="$emit('click')">
    <template #lead><ChampionIcon :champion-id="me.championId" /></template>
    <span class="match-row">
      <span class="match-row-line">
        <MatchResultText class="headline" :result="match.result" />
        <span class="footnote lr-secondary">{{ relativeTime(match.startedAt) }}</span>
      </span>
      <span class="subheadline lr-secondary">{{ queueLine }}</span>
      <span v-if="showStats" class="match-row-line match-row-stats">
        <span class="body" :aria-label="kdaSpoken(me)">{{ kda(me) }}</span>
        <span class="footnote lr-secondary">{{ kdaRatio(me) }}<template v-if="match.mapId !== ARENA_MAP_ID"> · {{ me.cs }} CS</template></span>
      </span>
      <LoadoutStrip :player="me" :no-trinket="!hasTrinkets(match.mapId)" />
    </span>
  </ListRow>
</template>

<style scoped>
.match-row {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-0-5);
}
.match-row-line {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  justify-content: space-between;
  gap: 0 var(--space-2);
}
.match-row-stats {
  justify-content: flex-start;
  font-variant-numeric: tabular-nums;
}
.match-row > :last-child {
  margin-top: var(--space-1);
}
</style>
