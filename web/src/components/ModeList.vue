<script setup lang="ts">
import { computed, ref } from 'vue'
import { tapFeedback } from '../haptics'
import { QUEUES } from '../labels'
import { sendCommand } from '../hub'
import { liveState } from '../store'
import ListRow from './lr/ListRow.vue'
import ListSection from './lr/ListSection.vue'

/**
 * Game modes, one section per map, for starting a lobby (Play: rows end in a chevron) or switching it (Lobby › Game
 * Mode sheet: the current mode has a trailing check). Plain text rows: the names tell the modes apart, symbols only
 * added noise. While a request is in flight the tapped row shows the activity indicator and the others are disabled.
 * Emits `done` after the client accepted the change.
 */
const props = defineProps<{ mode: 'start' | 'choose'; current?: number | 'practice' | null; disabled?: boolean }>()
const emit = defineEmits<{ done: [] }>()

interface Mode {
  key: number | 'practice'
  name: string
  map: string
}
const available = computed(() => liveState.value?.availableQueueIds ?? null)
const modes = computed<Mode[]>(() => [
  // Only what the client offers right now (disabled modes fail to start); everything when the connector doesn't say.
  ...QUEUES.filter((q) => available.value?.includes(q.id) ?? true).map((q) => ({ key: q.id, name: q.name, map: q.map })),
  { key: 'practice' as const, name: 'Practice Tool', map: "Summoner's Rift" },
])
/** One section per map, in the order the maps first appear (Summoner's Rift, Howling Abyss, Rings of Wrath). */
const groups = computed(() => {
  const byMap = new Map<string, Mode[]>()
  for (const m of modes.value) byMap.set(m.map, [...(byMap.get(m.map) ?? []), m])
  return [...byMap].map(([map, list]) => ({ map, list }))
})

const busyKey = ref<number | 'practice' | null>(null)
async function choose(m: Mode) {
  if (busyKey.value !== null) return
  if (props.mode === 'choose' && m.key === props.current) return emit('done')
  tapFeedback()
  busyKey.value = m.key
  try {
    const r = await sendCommand(m.key === 'practice' ? { type: 'CreatePracticeTool' } : { type: 'CreateLobby', queueId: m.key })
    if (r?.success) emit('done')
  } finally {
    busyKey.value = null
  }
}
</script>

<template>
  <ListSection v-for="g in groups" :key="g.map" :header="g.map">
    <ListRow
      v-for="m in g.list"
      :key="m.key"
      tappable
      text
      :title="m.name"
      :chevron="mode === 'start'"
      :checked="mode === 'choose' && m.key === current"
      :busy="busyKey === m.key"
      :disabled="disabled || (busyKey !== null && busyKey !== m.key)"
      @click="choose(m)"
    />
  </ListSection>
</template>
