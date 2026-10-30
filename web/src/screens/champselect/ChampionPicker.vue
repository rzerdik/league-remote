<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import ChampionGrid, { findChampions } from '../../components/ChampionGrid.vue'
import ClassFilter from '../../components/lr/ClassFilter.vue'
import SearchField from '../../components/lr/SearchField.vue'
import Sheet, { type DetentId } from '../../components/lr/Sheet.vue'
import type { ChampSelectState } from '../../contracts'
import type { ChampionClass } from '../../ddragon'
import { myTurnNow } from '../../draft'
import { haptic } from '../../haptics'
import { cssSize, SIZE } from '../../styles/tokens'

/**
 * The non-modal champion picker of your pick or ban: the turn header (`turn` slot), search, class filter and the grid
 * of what League lets you pick or ban. It rises to medium when your turn starts and rests at peek before it; peek keeps
 * the header and search above the floating Lock In button (SIZE.pickerPeek). `hidden` steps it aside without losing
 * its place (never glass on glass).
 */
const props = defineProps<{ cs: ChampSelectState; canAct: boolean; hidden: boolean; selectedId: number }>()
const emit = defineEmits<{ select: [championId: number] }>()
defineSlots<{ turn(): unknown }>()

const isBan = computed(() => props.cs.myAction?.type === 'ban')
const hot = computed(() => myTurnNow(props.cs))

const query = ref('')
const cls = ref<ChampionClass | null>(null)
watch(
  () => props.cs.myAction?.id,
  () => {
    query.value = ''
    cls.value = null
  },
)

const detent = ref<DetentId>(hot.value ? 'medium' : 'peek')
// The class filter is hidden at peek: a filter you can't see must not keep filtering.
watch(detent, (d) => {
  if (d === 'peek') cls.value = null
})
// The yourTurn / pickPhase sound is useSoundCues'.
watch(hot, (h) => {
  if (!h) return
  detent.value = 'medium'
  haptic('selection')
})
watch(
  () => props.canAct,
  (can) => {
    if (can && !hot.value) detent.value = 'peek'
  },
)

const items = computed(() =>
  findChampions(isBan.value ? props.cs.bannableChampionIds : props.cs.pickableChampionIds, query.value, cls.value),
)
</script>

<template>
  <!-- Lets the last section scroll clear of the picker (+ the action bar, via .lr-content's padding). -->
  <div v-if="canAct" :style="{ height: cssSize(SIZE.pickerPeek) }" aria-hidden="true" />

  <Sheet
    :open="canAct && !hidden"
    v-model:detent="detent"
    :detents="['peek', 'medium', 'large']"
    :peek-height="SIZE.pickerPeek"
    label="Champion picker"
    body-class="lr-picker-body"
  >
    <template #header>
      <div class="lr-picker-head">
        <slot name="turn" />
        <SearchField v-model="query" placeholder="Search" label="Search Champions" @focus="detent = 'large'" />
        <!-- Not at peek: that detent's height is just the turn header and the search. -->
        <ClassFilter v-if="detent !== 'peek'" v-model="cls" />
      </div>
    </template>

    <ChampionGrid
      :items="items"
      :selected-id="selectedId"
      :tone="isBan ? 'red' : 'accent'"
      empty-text="No Results"
      @select="emit('select', $event)"
    />
  </Sheet>
</template>
