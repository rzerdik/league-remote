<script setup lang="ts">
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import type { MatchSummary } from '../../contracts'
import { plural } from '../../format'
import { haptic } from '../../haptics'
import { openMatch, openPlayerMatch } from '../../navigation'
import MatchRow from './MatchRow.vue'

/**
 * The last games, newest first (as many as the client's own match history shows). Tap one for its details. `player`:
 * another player's history (their profile view), whose games open inside that view.
 */
const props = defineProps<{ matches: MatchSummary[]; player?: boolean }>()

function choose(m: MatchSummary) {
  haptic('selection')
  if (props.player) openPlayerMatch(m.gameId)
  else openMatch(m.gameId)
}
</script>

<template>
  <ListSection
    header="Match History"
    :header-trailing="matches.length ? plural(matches.length, 'game') : undefined"
  >
    <MatchRow v-for="m in matches" :key="m.gameId" :match="m" @click="choose(m)" />
    <ListRow
      v-if="!matches.length"
      text
      title="No Games Yet"
      :subtitle="player ? 'Their recent games show up here.' : 'Games you play show up here.'"
    />
  </ListSection>
</template>
