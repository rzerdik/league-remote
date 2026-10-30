<script lang="ts">
/** What LoadoutStrip draws: MatchParticipant has these fields; live players are mapped by `loadoutOf` (liveGame/). */
export interface Loadout {
  /** Item ids by slot (0–5, then the trinket); 0 = empty. */
  items: number[]
  spell1Id: number
  spell2Id: number
  keystone: number
  /** Spell names for VoiceOver when the ids are unknown to Data Dragon's spell list (mode-only spells). */
  spellNames?: [string, string]
}
</script>

<script setup lang="ts">
import { computed } from 'vue'
import { itemName, runeById, spellById } from '../../ddragon'
import { plural } from '../../format'
import ItemIcon from '../lr/ItemIcon.vue'
import RuneIcon from '../lr/RuneIcon.vue'
import SpellIcon from '../lr/SpellIcon.vue'

/**
 * What a player has (a finished game's participant, or a live scoreboard line), as the client's match history draws it:
 * summoner spells and keystone (`loadout`), then the seven item slots (the last is the trinket; empty slots are wells;
 * `noTrinket`: a map without trinkets, `hasTrinkets()` in teams.ts, draws no empty trinket well). The two groups wrap
 * as wholes at large text sizes. One image for VoiceOver, naming what it can ("Flash, Ignite, Electrocute. Items: …").
 */
const props = defineProps<{ player: Loadout; loadout?: boolean; noTrinket?: boolean }>()

const items = computed(() => {
  const slots = props.player.items.slice(0, 7)
  while (slots.length < 7) slots.push(0)
  if (props.noTrinket && !slots[6]) slots.pop()
  return slots
})

const label = computed(() => {
  const parts: string[] = []
  if (props.loadout) {
    const [name1, name2] = props.player.spellNames ?? []
    const loadout = [
      spellById(props.player.spell1Id)?.name || name1,
      spellById(props.player.spell2Id)?.name || name2,
      runeById(props.player.keystone)?.name,
    ]
    const named = loadout.filter(Boolean)
    if (named.length) parts.push(named.join(', '))
  }
  const held = items.value.filter((id) => id > 0)
  const names = held.map(itemName).filter(Boolean)
  if (!held.length) parts.push('No items')
  else parts.push(names.length === held.length ? `Items: ${names.join(', ')}` : plural(held.length, 'item'))
  return parts.join('. ')
})
</script>

<template>
  <span class="loadout-strip" role="img" :aria-label="label">
    <span v-if="loadout" class="loadout-group">
      <SpellIcon :spell-id="player.spell1Id" size="sm" />
      <SpellIcon :spell-id="player.spell2Id" size="sm" />
      <RuneIcon :rune-id="player.keystone" />
    </span>
    <span class="loadout-group">
      <ItemIcon v-for="(id, i) in items" :key="i" :item-id="id" :class="{ 'is-trinket': i === 6 }" />
    </span>
  </span>
</template>

<style scoped>
.loadout-strip {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-1) var(--space-2);
}
/* Wraps too, where even one group is wider than the row (the largest text sizes). */
.loadout-group {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-0-5);
}
/* The trinket slot is round in the client. */
.is-trinket {
  border-radius: var(--radius-full);
}
</style>
