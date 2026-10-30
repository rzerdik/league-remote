<script setup lang="ts">
import { computed, ref } from 'vue'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import type { MatchSummary } from '../../contracts'
import { championName } from '../../ddragon'
import { kdaRatio, plural, record, winRate } from '../../format'
import { championStats, countedGames } from '../../profileStats'

/**
 * Champions over the last games that counted (profileStats.ts): most played first, each with its games, KDA ratio
 * (summed over those games, as League computes it) and win rate. The first few show; the rest fold away like Mastery.
 */
const props = defineProps<{ matches: MatchSummary[] }>()

const FOLDED = 3
const expanded = ref(false)
const stats = computed(() => championStats(props.matches))
const shown = computed(() => (expanded.value ? stats.value : stats.value.slice(0, FOLDED)))
const hidden = computed(() => stats.value.length - FOLDED)
const games = computed(() => countedGames(props.matches).length)
</script>

<template>
  <ListSection v-if="stats.length" header="Champions" :header-trailing="`Last ${plural(games, 'game')}`">
    <ListRow
      v-for="s in shown"
      :key="s.championId"
      wrap
      :title="championName(s.championId)"
      :subtitle="`${plural(s.games, 'game')} · ${record(s.wins, s.games - s.wins)} · ${kdaRatio(s)}`"
      :value="winRate(s.wins, s.games - s.wins)"
    >
      <template #lead><ChampionIcon :champion-id="s.championId" decorative /></template>
    </ListRow>
    <ListRow
      v-if="hidden > 0"
      tappable
      text
      variant="accent"
      :title="expanded ? 'Show Fewer' : `Show ${hidden} More`"
      @click="expanded = !expanded"
    />
  </ListSection>
</template>
