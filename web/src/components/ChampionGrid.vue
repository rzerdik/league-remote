<script lang="ts">
import { championName, ddragon } from '../ddragon'

export interface ChampionItem {
  id: number
  name: string
  unavailable?: boolean
}

/**
 * The champions among `ids` that match the search and the lane filter (`lane`: that lane's champions), by name. Only ids
 * Data Dragon knows: League also lists placeholders (-1, 0) and ids that aren't champions (60001…).
 */
export function findChampions(ids: Iterable<number>, query: string, lane: ReadonlySet<number> | null): ChampionItem[] {
  const q = query.trim().toLowerCase()
  return [...new Set(ids)]
    .filter((id) => id > 0 && ddragon.champions.has(id) && (!lane || lane.has(id)))
    .map((id) => ({ id, name: championName(id) }))
    .filter((c) => !q || c.name.toLowerCase().includes(q))
    .sort((a, b) => a.name.localeCompare(b.name))
}
</script>

<script setup lang="ts">
import ChampionIcon from './lr/ChampionIcon.vue'
import PickerGrid from './lr/PickerGrid.vue'

/** A `PickerGrid` of champion portraits (the draft's picker, the ARAM bench, auto pick lists), or a note while Data Dragon loads. */
defineProps<{
  items: ChampionItem[]
  selectedId?: number
  tone?: 'accent' | 'red'
  disabled?: boolean
  emptyText: string
}>()
defineEmits<{ select: [championId: number] }>()
</script>

<template>
  <p v-if="!ddragon.version" class="lr-grid-empty subheadline">Loading champions…</p>
  <PickerGrid
    v-else
    :items="items"
    :selected-id="selectedId"
    :tone="tone"
    :disabled="disabled"
    :empty-text="emptyText"
    @select="$emit('select', $event.id)"
  >
    <template #art="{ item }"><ChampionIcon :champion-id="item.id" size="grid" decorative /></template>
  </PickerGrid>
</template>
