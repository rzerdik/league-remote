<script setup lang="ts">
import { computed } from 'vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import type { MatchSummary } from '../../contracts'
import { plural, record, winRate } from '../../format'
import { positionLabel } from '../../labels'
import { roleStats } from '../../profileStats'

/**
 * Roles over the last Summoner's Rift games that counted, most played first, with games and win rate. The role is
 * League's own guess after each game (it can miss in a single game, so the footer says so); games without one (ARAM,
 * Arena) are left out, and so is the section when no game has one.
 */
const props = defineProps<{ matches: MatchSummary[] }>()

const stats = computed(() => roleStats(props.matches))
</script>

<template>
  <ListSection v-if="stats.length" header="Roles" footer="The role League guessed after each game; it can be off now and then.">
    <ListRow
      v-for="s in stats"
      :key="s.position"
      wrap
      :title="positionLabel(s.position)"
      :subtitle="`${plural(s.games, 'game')} · ${record(s.wins, s.games - s.wins)}`"
      :value="winRate(s.wins, s.games - s.wins)"
    />
  </ListSection>
</template>
