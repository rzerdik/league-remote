<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ddragon } from '../../ddragon'
import { championLanes, laneChampions, type Lane } from '../../lanes'
import ChampionGrid, { findChampions } from '../ChampionGrid.vue'
import LaneFilter from '../lr/LaneFilter.vue'
import SearchField from '../lr/SearchField.vue'
import Sheet from '../lr/Sheet.vue'

/**
 * A modal champion picker (Settings › Auto Pick & Ban › a preset's Add Ban / Add Pick): every champion Data Dragon
 * knows, by name, with search and a lane filter, minus the ones already on the list (`exclude`). Picking one emits it
 * and closes; the ring and tile colours follow the list (`red` for bans). Works without a League client: presets
 * aren't tied to the champions you own today (the connector skips ones it can't pick), and only the lane filter needs
 * League, so it stays away while the client is closed.
 */
const props = defineProps<{ title: string; tone?: 'accent' | 'red'; exclude: number[] }>()
const open = defineModel<boolean>('open', { required: true })
const emit = defineEmits<{ select: [championId: number] }>()

const query = ref('')
const lane = ref<Lane | null>(null)
/** Taken when the sheet opens: the champion just picked mustn't vanish from the grid while the sheet slides away. */
const excluded = ref<number[]>([])
watch(
  open,
  (o) => {
    if (!o) return
    query.value = ''
    lane.value = null
    excluded.value = [...props.exclude]
  },
  { immediate: true },
)

const items = computed(() => {
  const skip = new Set(excluded.value)
  return findChampions([...ddragon.champions.keys()].filter((id) => !skip.has(id)), query.value, laneChampions(lane.value))
})

function choose(id: number) {
  emit('select', id)
  open.value = false
}
</script>

<template>
  <Sheet v-model:open="open" modal :detents="['large']" :title="title" body-class="lr-picker-body">
    <template #header>
      <div class="lr-picker-head">
        <SearchField v-model="query" placeholder="Search" label="Search Champions" />
        <LaneFilter v-if="championLanes" v-model="lane" />
      </div>
    </template>
    <ChampionGrid :items="items" :tone="tone" empty-text="No Results" @select="choose" />
  </Sheet>
</template>
