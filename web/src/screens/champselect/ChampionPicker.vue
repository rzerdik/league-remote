<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import ChampionGrid, { findChampions } from '../../components/ChampionGrid.vue'
import LaneFilter from '../../components/lr/LaneFilter.vue'
import SearchField from '../../components/lr/SearchField.vue'
import Sheet, { type DetentId } from '../../components/lr/Sheet.vue'
import type { ChampSelectState } from '../../contracts'
import { myTurnNow } from '../../draft'
import { haptic } from '../../haptics'
import { championLanes, laneChampions, type Lane } from '../../lanes'
import { cssSize, SIZE } from '../../styles/tokens'

/**
 * The non-modal champion picker of your pick or ban: the turn header (`turn` slot), search, lane filter and the grid
 * of what League lets you pick or ban. It rises to medium when your turn starts and rests at peek before it; peek keeps
 * the header and search above the small Lock In button (SIZE.pickerPeek), which floats over the sheet on your own
 * turn: then the grid scrolls clear of it and fades out above it. `hidden` steps it aside without losing its place
 * (never glass on glass). `detent` tells the screen how much room the draft board has.
 */
const props = defineProps<{ cs: ChampSelectState; canAct: boolean; hidden: boolean; selectedId: number }>()
const emit = defineEmits<{ select: [championId: number] }>()
defineSlots<{ turn(): unknown }>()

const isBan = computed(() => props.cs.myAction?.type === 'ban')
const hot = computed(() => myTurnNow(props.cs))

const query = ref('')
const lane = ref<Lane | null>(null)
watch(
  () => props.cs.myAction?.id,
  () => {
    query.value = ''
    lane.value = null
  },
)

const detent = defineModel<DetentId>('detent', { default: 'peek' })
detent.value = hot.value ? 'medium' : 'peek'
// The lane filter is hidden at peek: a filter you can't see must not keep filtering.
watch(detent, (d) => {
  if (d === 'peek') lane.value = null
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
  findChampions(isBan.value ? props.cs.bannableChampionIds : props.cs.pickableChampionIds, query.value, laneChampions(lane.value)),
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
    :medium-fraction="0.45"
    label="Champion picker"
    :body-class="hot ? 'lr-picker-body lr-picker-body--action' : 'lr-picker-body'"
  >
    <template #header>
      <div class="lr-picker-head">
        <slot name="turn" />
        <SearchField v-model="query" placeholder="Search" label="Search Champions" @focus="detent = 'large'" />
        <!-- Not at peek: that detent's height is just the turn header and the search. -->
        <LaneFilter v-if="detent !== 'peek' && championLanes" v-model="lane" />
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
