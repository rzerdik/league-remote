<script setup lang="ts">
import { computed } from 'vue'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import StatGrid from '../../components/lr/StatGrid.vue'
import LoadoutStrip from '../../components/profile/LoadoutStrip.vue'
import type { MatchParticipant } from '../../contracts'
import { championName } from '../../ddragon'
import { formatCompact, kda, kdaRatio, kdaSpoken, perMinute } from '../../format'
import { ARENA_MAP_ID, hasTrinkets } from '../../teams'

/**
 * You after the game: champion and level (Arena: your place is the screen's title), then K / D / A, the ratio, CS and
 * CS per minute, gold, damage to champions and vision as a stat grid, then spells, keystone and the items you ended with.
 */
const props = defineProps<{ me: MatchParticipant; durationSeconds: number; mapId: number }>()

const arena = computed(() => props.mapId === ARENA_MAP_ID)

const cells = computed(() => {
  const p = props.me
  const csRate = arena.value ? '' : perMinute(p.cs, props.durationSeconds)
  return [
    { label: 'K / D / A', value: kda(p), spoken: kdaSpoken(p) },
    { label: 'KDA', value: kdaRatio(p, { withWord: false }) },
    ...(arena.value ? [] : [{ label: 'CS', value: String(p.cs) }]),
    ...(csRate ? [{ label: 'CS / Min', value: csRate, spoken: `${csRate} per minute` }] : []),
    { label: 'Gold', value: formatCompact(p.gold), accent: true },
    { label: 'Damage', value: formatCompact(p.damage) },
    ...(hasTrinkets(props.mapId) ? [{ label: 'Vision', value: String(p.visionScore) }] : []),
  ]
})
</script>

<template>
  <ListSection header="You">
    <ListRow variant="you" wrap :title="championName(me.championId) || 'Your Champion'" :subtitle="`Level ${me.champLevel}`">
      <template #lead><ChampionIcon :champion-id="me.championId" state="you" decorative /></template>
    </ListRow>
    <StatGrid :cells="cells" />
    <ListRow>
      <LoadoutStrip class="you-loadout" :player="me" loadout :no-trinket="!hasTrinkets(mapId)" />
    </ListRow>
  </ListSection>
</template>

<style scoped>
/* Fills the row from the start (a row's content otherwise sits at its end, like a value). */
.you-loadout {
  flex: 1;
}
</style>
