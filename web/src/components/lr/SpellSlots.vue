<script setup lang="ts">
import { computed, ref } from 'vue'
import { ddragon, spellById, type SpellInfo } from '../../ddragon'
import PickerGrid from './PickerGrid.vue'
import Sheet from './Sheet.vue'
import SpellIcon from './SpellIcon.vue'

/**
 * The two summoner spell slots of a list (an `li` inside a ListSection): tapping one opens the spell picker sheet.
 * Picking the spell that sits in the other slot swaps the two (as in the client). Emits the new pair; the parent sends
 * it (Champion Select › Loadout) or keeps it (an auto pick preset). An empty slot (0) shows `placeholder`. `tray`: just the
 * two icons, with the default slot (the rune page) beside them on the same line (the draft's loadout).
 */
const props = withDefaults(
  defineProps<{
    spell1Id: number
    spell2Id: number
    /** What the picker offers; Summoner's Rift spells by default. */
    spells?: SpellInfo[]
    disabled?: boolean
    placeholder?: string
    tray?: boolean
  }>(),
  { spells: undefined, placeholder: 'Choose' },
)
const emit = defineEmits<{ change: [spell1Id: number, spell2Id: number] }>()

const slot = ref<1 | 2 | null>(null)
/** Kept after close so the sheet title doesn't change while it animates out. */
const shownSlot = ref<1 | 2>(1)
const open = computed({
  get: () => slot.value !== null,
  set: (o: boolean) => {
    if (!o) slot.value = null
  },
})
function openSlot(s: 1 | 2) {
  shownSlot.value = s
  slot.value = s
}
const spellOf = (s: 1 | 2) => Number(s === 1 ? props.spell1Id : props.spell2Id)

function choose(key: number) {
  const s = slot.value
  slot.value = null
  if (!s) return
  let s1 = Number(props.spell1Id)
  let s2 = Number(props.spell2Id)
  if (s === 1) {
    if (key === s2) s2 = s1
    s1 = key
  } else {
    if (key === s1) s1 = s2
    s2 = key
  }
  if (s1 !== Number(props.spell1Id) || s2 !== Number(props.spell2Id)) emit('change', s1, s2)
}

const items = computed(() => (props.spells ?? ddragon.spells).map((s) => ({ id: s.key, name: s.name })))
</script>

<template>
  <li v-if="tray" class="lr-tray">
    <button
      v-for="s in [1, 2] as const"
      :key="s"
      type="button"
      class="lr-tray-spell"
      aria-haspopup="dialog"
      :aria-label="`Summoner spell ${s}: ${spellById(spellOf(s))?.name ?? 'none'}`"
      :disabled="disabled"
      @click="openSlot(s)"
    >
      <SpellIcon :spell-id="spellOf(s)" size="slot" />
    </button>
    <slot />
  </li>
  <li v-else class="lr-spell-slots">
    <button
      v-for="s in [1, 2] as const"
      :key="s"
      type="button"
      class="lr-spell-slot"
      aria-haspopup="dialog"
      :aria-label="`Summoner spell ${s}: ${spellById(spellOf(s))?.name ?? 'none'}`"
      :disabled="disabled"
      @click="openSlot(s)"
    >
      <SpellIcon :spell-id="spellOf(s)" size="slot" />
      <span class="body" :class="{ 'lr-secondary': !spellById(spellOf(s)) }">{{ spellById(spellOf(s))?.name ?? placeholder }}</span>
    </button>
  </li>

  <Sheet v-model:open="open" modal :detents="['medium']" :title="`Summoner Spell ${shownSlot}`">
    <PickerGrid :items="items" :selected-id="spellOf(shownSlot)" @select="choose($event.id)">
      <template #art="{ item }"><SpellIcon :spell-id="item.id" size="grid" /></template>
    </PickerGrid>
  </Sheet>
</template>
