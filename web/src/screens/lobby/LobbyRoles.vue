<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import Sheet from '../../components/lr/Sheet.vue'
import { useBusy } from '../../composables/useBusy'
import { isPosition, Positions, type LobbyState, type Position } from '../../contracts'
import { tapFeedback } from '../../haptics'
import { positionLabel } from '../../labels'
import { sendCommand } from '../../hub'

/**
 * Lobby › Your Roles: the local player's two role preferences (Draft Pick, Ranked Solo/Duo, Ranked Flex): optimistic
 * `pending`, `draftFirst` (a first primary waits for the secondary before sending), picking the other slot's role swaps
 * the two, Fill clears the secondary, locked while searching.
 */
const props = defineProps<{ lobby: LobbyState }>()

const me = computed(() => props.lobby.members.find((m) => m.isLocalPlayer) ?? null)
const current = computed(() => ({
  first: isPosition(me.value?.firstPositionPreference) ? (me.value!.firstPositionPreference as Position) : null,
  second: isPosition(me.value?.secondPositionPreference) ? (me.value!.secondPositionPreference as Position) : null,
}))

const { busy, run } = useBusy()
/** Optimistic values while a change is on its way; cleared when the client's state echoes back (or the change fails). */
const pending = ref<{ first: Position; second: Position | null } | null>(null)
/** A primary role picked while no secondary is set yet: nothing is sent until the secondary is chosen too. */
const draftFirst = ref<Position | null>(null)
watch(
  () => [me.value?.firstPositionPreference, me.value?.secondPositionPreference],
  () => {
    pending.value = null
    draftFirst.value = null // roles changed elsewhere (the PC): an unsent half choice is stale
  },
)

const shown = computed(() => {
  if (pending.value) return pending.value
  if (draftFirst.value) return { first: draftFirst.value, second: null }
  return current.value
})
const locked = computed(() => props.lobby.isSearching)
const secondDisabled = computed(() => locked.value || busy.value || !shown.value.first || shown.value.first === 'FILL')

const slot = ref<1 | 2 | null>(null)
/** The slot the sheet shows; kept while the sheet animates closed so its title doesn't flip. */
const sheetSlot = ref<1 | 2>(1)
/** The slot whose change is in flight (shows the activity indicator). */
const sentSlot = ref<1 | 2>(1)
watch(slot, (s) => {
  if (s) sheetSlot.value = s
})
const sheetOpen = computed({
  get: () => slot.value !== null,
  set: (open: boolean) => {
    if (!open) {
      slot.value = null
      draftFirst.value = null // dismissed before choosing the secondary: nothing was sent, show the real roles
    }
  },
})

function open(s: 1 | 2) {
  if (locked.value || busy.value || (s === 2 && secondDisabled.value)) return
  tapFeedback()
  slot.value = s
}

function choose(pos: Position) {
  const s = slot.value
  if (!s) return
  tapFeedback()
  let first = shown.value.first
  let second = shown.value.second
  if (s === 1) {
    if (pos === second) second = first // picking the other slot's role swaps the two
    first = pos
    if (first === 'FILL') second = null
  } else {
    if (pos === first) first = second
    second = pos
  }
  if (!first) {
    slot.value = null
    return
  }
  if (first !== 'FILL' && !second) {
    // Primary chosen first time: ask for the secondary before sending (the client needs both). The sheet stays up and
    // turns into the Secondary Role picker.
    draftFirst.value = first
    slot.value = 2
    return
  }
  slot.value = null
  draftFirst.value = null
  if (first === current.value.first && second === current.value.second) return
  pending.value = { first, second }
  sentSlot.value = s
  void run(async () => {
    const r = await sendCommand({
      type: 'SetPositionPreferences',
      firstPosition: first,
      secondPosition: first === 'FILL' ? null : second,
    })
    if (!r?.success) pending.value = null
  })
}

/** Role taken by the other slot (choosing it swaps). */
const otherRole = computed(() => (sheetSlot.value === 1 ? shown.value.second : shown.value.first))
const slotRole = computed(() => (sheetSlot.value === 1 ? shown.value.first : shown.value.second))

const primaryValue = computed(() => positionLabel(shown.value.first) || 'Choose')
const secondaryValue = computed(() =>
  shown.value.first === 'FILL' ? 'None' : positionLabel(shown.value.second) || 'Choose',
)

/** The primary role is only sent together with a secondary one. */
const footer = computed(() => (draftFirst.value ? 'Now pick a secondary role.' : ''))
</script>

<template>
  <ListSection header="Roles">
    <ListRow
      tappable
      text
      pop-up
      title="Primary"
      :value="primaryValue"
      :busy="busy && sentSlot === 1"
      :disabled="locked || busy"
      @click="open(1)"
    />
    <ListRow
      tappable
      text
      pop-up
      title="Secondary"
      :value="secondaryValue"
      :busy="busy && sentSlot === 2"
      :disabled="secondDisabled"
      @click="open(2)"
    />
    <template v-if="footer" #footer>{{ footer }}</template>
  </ListSection>

  <Sheet
    v-model:open="sheetOpen"
    modal
    :detents="['medium']"
    :title="sheetSlot === 2 ? 'Secondary Role' : 'Primary Role'"
    body-class="lr-sheet-body--lists"
  >
    <ListSection>
      <ListRow
        v-for="p in Positions"
        :key="p"
        tappable
        :title="positionLabel(p)"
        :value="p === otherRole && p !== slotRole ? 'Swap' : undefined"
        :checked="p === slotRole"
        @click="choose(p)"
      />
    </ListSection>
  </Sheet>
</template>
