<script setup lang="ts">
import { computed } from 'vue'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import Tag from '../../components/lr/Tag.vue'
import type { PostGameSummary } from '../../contracts'
import {
  lpDelta,
  lpDeltaSpoken,
  masteryGained,
  masteryLine,
  masteryTitle,
  rankAfter,
  rankChange,
  rankQueue,
} from '../../postGame'

/**
 * What the game gave you, as the client's post-game shows it: the LP change (queue, the rank after the game, Promoted /
 * Demoted as a tag) and your champion's mastery (points gained, the level after the game, Level Up and the grade as
 * tags). A ranked game whose LP hasn't been reported yet says it is coming; an unranked one has no Ranked section.
 */
const props = defineProps<{ summary: PostGameSummary }>()

const rank = computed(() => props.summary.rank)
const lpTone = computed(() => {
  const d = rank.value?.leaguePointsDelta ?? 0
  return d > 0 ? 'green' : d < 0 ? 'red' : 'gray'
})
const change = computed(() => (rank.value ? rankChange(rank.value) : ''))
const mastery = computed(() => props.summary.mastery)
</script>

<template>
  <ListSection v-if="rank" header="Ranked">
    <ListRow wrap :title="rankQueue(rank)" :value="lpDelta(rank)" :value-tone="lpTone" :aria-label="`${rankQueue(rank)}, ${lpDeltaSpoken(rank)}, ${rankAfter(rank)}${change ? `, ${change}` : ''}`">
      <template #subtitle>
        <span class="lr-row-subline">
          <span>{{ rankAfter(rank) }}</span>
          <Tag v-if="change" :tone="rank.change === 'demoted' ? 'red' : 'accent'">{{ change }}</Tag>
        </span>
      </template>
    </ListRow>
  </ListSection>
  <ListSection v-else-if="summary.isRanked" header="Ranked">
    <ListRow busy title="LP Change" subtitle="League is updating your rank." />
  </ListSection>

  <ListSection v-if="mastery" header="Mastery">
    <ListRow wrap :title="masteryTitle(mastery)" :value="masteryGained(mastery)">
      <template #lead><ChampionIcon :champion-id="mastery.championId" decorative /></template>
      <template #subtitle>
        <span class="lr-row-subline">
          <span>{{ masteryLine(mastery) }}</span>
          <Tag v-if="mastery.leveledUp" tone="accent">Level Up</Tag>
          <Tag v-if="mastery.grade">Grade {{ mastery.grade }}</Tag>
        </span>
      </template>
    </ListRow>
  </ListSection>
</template>
