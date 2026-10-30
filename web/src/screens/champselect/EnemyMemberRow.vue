<script setup lang="ts">
import { computed } from 'vue'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import ListRow from '../../components/lr/ListRow.vue'
import type { EnemyMember } from '../../contracts'
import { championName } from '../../ddragon'
import { hoveringLine } from '../../draft'
import { positionLabel } from '../../labels'

/**
 * One enemy in Champion Select, built like a Your Team row (TeamMemberRow): portrait at full opacity, the locked
 * champion as the title, "Picking…" with a breathing red ring during their turn (and the champion they hover, which
 * League shows then), the role when League shares it. Never player names; enemy bans stay hidden until the reveal.
 */
const props = defineProps<{ enemy: EnemyMember }>()

const champ = computed(() => (props.enemy.championId > 0 ? championName(props.enemy.championId) : ''))
const role = computed(() => positionLabel(props.enemy.assignedPosition))
const acting = computed(() => props.enemy.isActing && !props.enemy.isLocked)

const title = computed(() => {
  if (props.enemy.isLocked && champ.value) return champ.value
  return acting.value ? 'Picking…' : 'Not Picked Yet'
})
const subtitle = computed(() => (acting.value && champ.value ? hoveringLine(props.enemy.championId) : role.value || undefined))
const portraitState = computed(() => {
  if (acting.value) return 'enemy-acting' as const
  return props.enemy.championId > 0 ? null : ('empty' as const)
})
</script>

<template>
  <ListRow :title="title" :subtitle="subtitle">
    <template #lead>
      <ChampionIcon :champion-id="enemy.championId" :state="portraitState" decorative />
    </template>
  </ListRow>
</template>
