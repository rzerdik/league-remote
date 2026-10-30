<script setup lang="ts">
import { computed } from 'vue'
import StatGrid from '../../components/lr/StatGrid.vue'
import type { MatchTeam } from '../../contracts'
import { formatCompact } from '../../format'
import { ARENA_MAP_ID, SUMMONERS_RIFT_MAP_ID } from '../../teams'

/**
 * A team's totals in a game's details (a StatGrid): kills and gold, then the objectives the map has (Summoner's Rift:
 * towers, inhibitors, dragons, barons, heralds, voidgrubs; other maps: towers and inhibitors; Arena: none).
 */
const props = defineProps<{ team: MatchTeam; mapId: number }>()

const cells = computed(() => {
  const t = props.team
  const list = [
    { label: 'Kills', value: String(t.kills) },
    { label: 'Gold', value: formatCompact(t.gold) },
  ]
  if (props.mapId === ARENA_MAP_ID) return list
  list.push({ label: 'Towers', value: String(t.towers) }, { label: 'Inhibitors', value: String(t.inhibitors) })
  if (props.mapId !== SUMMONERS_RIFT_MAP_ID) return list
  list.push(
    { label: 'Dragons', value: String(t.dragons) },
    { label: 'Barons', value: String(t.barons) },
    { label: 'Heralds', value: String(t.heralds) },
    { label: 'Grubs', value: String(t.grubs) },
  )
  return list
})
</script>

<template>
  <StatGrid :cells="cells" />
</template>
