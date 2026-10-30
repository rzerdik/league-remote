<script setup lang="ts">
import { computed } from 'vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import StatGrid from '../../components/lr/StatGrid.vue'
import type { LiveTeam } from '../../contracts'
import { formatCompact } from '../../format'
import { knowsMyTeam, teamItemValue } from '../../liveGame'
import { drakeName } from '../../liveGame/objectives'
import { sideName } from '../../teams'
import LivePlayerRow from './LivePlayerRow.vue'

/**
 * One team of the running game: "Your Team" / "Enemy Team" with its side (spectating: "Blue Side" / "Red Side"), its
 * kills and objectives (Summoner's Rift: towers, inhibitors, dragons, barons, heralds, grubs; other maps: towers and
 * inhibitors) and the estimated worth of its items, the drakes it took by element, then its players in the game's
 * order; `footer` under it (the item lead).
 */
const props = defineProps<{ team: LiveTeam; rift: boolean; enemyNumber?: number; footer?: string }>()

/** Spectating (no team is yours) the side is the title; `enemyNumber` then numbers sideless teams ("Team 2"). */
const title = computed(() => {
  if (!knowsMyTeam.value) return sideName(props.team.side) || (props.enemyNumber ? `Team ${props.enemyNumber}` : 'Team')
  if (props.team.isMyTeam) return 'Your Team'
  return props.enemyNumber ? `Enemy Team ${props.enemyNumber}` : 'Enemy Team'
})
const trailing = computed(() => (knowsMyTeam.value ? sideName(props.team.side) || undefined : undefined))

const cells = computed(() => {
  const t = props.team
  const items = teamItemValue(t)
  const list = [{ label: 'Kills', value: String(t.kills) }]
  if (items != null) list.push({ label: 'Item Gold', value: formatCompact(items) })
  list.push({ label: 'Towers', value: String(t.towers) }, { label: 'Inhibitors', value: String(t.inhibitors) })
  if (!props.rift) return list
  list.push(
    { label: 'Dragons', value: String(t.dragons.length + t.elders) },
    { label: 'Barons', value: String(t.barons) },
    { label: 'Heralds', value: String(t.heralds) },
    { label: 'Grubs', value: String(t.grubs) },
  )
  return list
})

/** "Infernal, Ocean, Elder ×2" — the drakes in the order they fell (four = the soul). */
const drakes = computed(() => {
  const names = props.team.dragons.map(drakeName)
  if (props.team.elders) names.push(props.team.elders > 1 ? `Elder ×${props.team.elders}` : 'Elder')
  return names.join(', ')
})
</script>

<template>
  <ListSection :header="title" :header-trailing="trailing" :footer="footer">
    <StatGrid :cells="cells" />
    <ListRow v-if="rift && drakes" text wrap title="Drakes" :subtitle="drakes" />
    <LivePlayerRow v-for="(p, i) in team.players" :key="`${p.championId}-${i}`" :player="p" />
  </ListSection>
</template>
