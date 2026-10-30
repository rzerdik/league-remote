<script setup lang="ts">
import { BugIcon, CastleIcon, CrownIcon, EyeIcon, FlameIcon, SkullIcon, SwordsIcon, TowerControlIcon, TrophyIcon } from '@lucide/vue'
import { computed, type Component } from 'vue'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import IconTile, { type TileColor } from '../../components/lr/IconTile.vue'
import ListRow from '../../components/lr/ListRow.vue'
import StatusDot from '../../components/lr/StatusDot.vue'
import Tag from '../../components/lr/Tag.vue'
import { formatSeconds } from '../../format'
import { sideRelation } from '../../liveGame'
import type { FeedItem } from '../../liveGame/feed'

/**
 * One line of the In Game event feed: who did it (the champion's portrait for kills, the objective's tile otherwise),
 * what happened in words ("Ahri killed Zed"), the victim's portrait at the end, and a meta line: which team scored (a
 * blue or red dot plus "Your Team" / "Enemy", spectating "Blue Side" / "Red Side" — never colour alone; nothing when
 * the side is unknown), the game time and the folded-in tags ("First Blood", "First Tower"). Structures (turrets,
 * inhibitors) are tinted by who took them: blue for your team / Blue Side, red for the enemy / Red Side.
 */
const props = defineProps<{ item: FeedItem }>()

const e = computed(() => props.item.event)
const killLike = computed(() => e.value.kind === 'kill' || e.value.kind === 'firstBlood' || e.value.kind === 'multikill')

const TILES: Record<string, { icon: Component; tile: TileColor }> = {
  dragon: { icon: FlameIcon, tile: 'orange' },
  baron: { icon: CrownIcon, tile: 'purple' },
  herald: { icon: EyeIcon, tile: 'cyan' },
  grub: { icon: BugIcon, tile: 'purple' },
  turret: { icon: TowerControlIcon, tile: 'gray' },
  firstTower: { icon: TowerControlIcon, tile: 'gray' },
  inhibitor: { icon: CastleIcon, tile: 'gray' },
  inhibitorRespawned: { icon: CastleIcon, tile: 'gray' },
  ace: { icon: SwordsIcon, tile: 'indigo' },
  gameEnd: { icon: TrophyIcon, tile: 'accent' },
}

/** Destroyed structures: tinted by the side that took them (a near-black tile read as empty). */
const STRUCTURES = new Set(['turret', 'firstTower', 'inhibitor'])

/** Kills lead with the killer; a kill without a champion behind it (turret, minions) with a skull. */
const lead = computed<{ champion: number } | { icon: Component; tile: TileColor }>(() => {
  if (killLike.value) return e.value.actorChampionId ? { champion: e.value.actorChampionId } : { icon: SkullIcon, tile: 'gray' }
  if (e.value.kind === 'dragon' && e.value.dragonType === 'Elder') return { icon: FlameIcon, tile: 'indigo' }
  const base = TILES[e.value.kind] ?? { icon: SwordsIcon, tile: 'gray' }
  if (STRUCTURES.has(e.value.kind)) {
    const r = relation.value
    return { ...base, tile: r === 'mine' || r === 'blue' ? 'blue' : r === 'enemy' || r === 'red' ? 'red' : 'gray' }
  }
  return base
})

const SIDES = {
  mine: { tone: 'blue', text: 'Your Team' },
  enemy: { tone: 'red', text: 'Enemy' },
  blue: { tone: 'blue', text: 'Blue Side' },
  red: { tone: 'red', text: 'Red Side' },
} as const
const relation = computed(() => (e.value.kind === 'gameEnd' ? 'unknown' : sideRelation(e.value.side, e.value.isMyTeam)))
const side = computed(() => (relation.value === 'unknown' ? null : SIDES[relation.value]))
/** Tags: gold for your team, red for the enemy, neutral otherwise (spectating, unknown side). */
const tagTone = computed(() => (relation.value === 'mine' ? 'accent' : relation.value === 'enemy' ? 'red' : undefined))
const tags = computed(() => [...props.item.tags, ...(e.value.stolen ? ['Stolen'] : [])])
</script>

<template>
  <ListRow text wrap>
    <template #lead>
      <ChampionIcon v-if="'champion' in lead" :champion-id="lead.champion" size="tile" decorative />
      <IconTile v-else :icon="lead.icon" :color="lead.tile" />
    </template>
    <template #title>{{ item.text }}</template>
    <template #subtitle>
      <span class="event-meta">
        <span class="event-when">
          <template v-if="side"><StatusDot :tone="side.tone" />{{ side.text }} · </template>{{ formatSeconds(item.time) }}
        </span>
        <Tag v-for="t in tags" :key="t" :tone="tagTone">{{ t }}</Tag>
      </span>
    </template>
    <template v-if="killLike && e.victimChampionId" #trailing>
      <ChampionIcon :champion-id="e.victimChampionId" size="tile" state="dead" decorative />
    </template>
  </ListRow>
</template>

<style scoped>
/* Side · time, then the tags; the tags wrap under it when the line is full (large text). */
.event-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-1) var(--space-2);
}
.event-when {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  font-variant-numeric: tabular-nums;
}
</style>
