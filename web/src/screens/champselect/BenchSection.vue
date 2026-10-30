<script setup lang="ts">
import { DicesIcon } from '@lucide/vue'
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import ChampionGrid from '../../components/ChampionGrid.vue'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import LrButton from '../../components/lr/LrButton.vue'
import { useBusy } from '../../composables/useBusy'
import type { ChampSelectState } from '../../contracts'
import { championName, ddragon } from '../../ddragon'
import type { DraftStage } from '../../draft'
import { haptic, tapFeedback } from '../../haptics'
import { sendCommand } from '../../hub'
import { play } from '../../sound'

/**
 * Bench modes (ARAM): the champion you were dealt, with Reroll, and the shared bench. One tap on a bench champion takes
 * it, without a confirmation, like the client's bench; yours goes back to the bench. The swap shows at once (your
 * champion and the bench trade places, and your Your Team row through `pending`) and is dropped again when League
 * echoes your new champion, refuses, or never answers. Like the client, the bench rests for a moment after each swap or reroll.
 */
const props = defineProps<{ cs: ChampSelectState; stage: DraftStage }>()
/** The swap on its way (the bench champion you tapped) or null: the Your Team row shows it too. */
const emit = defineEmits<{ pending: [championId: number | null] }>()

/** The client's CHAMPION_BENCH_SWAP_COOLDOWN_MS (rcp-fe-lol-champ-select, patch 16.21). Behaviour, not a visual value. */
const BENCH_COOLDOWN_MS = 3000
/** Drop an optimistic swap League accepted but never echoed (the state normally follows within a second). */
const PENDING_TIMEOUT_MS = 5000

const me = computed(() => props.cs.myTeam.find((m) => m.isLocalPlayer) ?? null)
const dealtId = computed(() => me.value?.championId ?? 0)

/** The bench champion you tapped, until League shows it as yours. */
const pendingId = ref<number | null>(null)
let pendingTimer: ReturnType<typeof setTimeout> | undefined
function setPending(id: number | null) {
  clearTimeout(pendingTimer)
  pendingId.value = id
  if (id !== null) pendingTimer = setTimeout(() => (pendingId.value = null), PENDING_TIMEOUT_MS)
}
// Any change of your champion is League's answer (the swap, a reroll, or a teammate's trade): the state is the truth.
watch(dealtId, () => setPending(null))
watch(pendingId, (id) => emit('pending', id))

const myId = computed(() => pendingId.value ?? dealtId.value)

/** League's pickable list (empty or a placeholder when it has none): champions not on it are grayed, as in the client. */
const pickable = computed(() => {
  const ids = props.cs.pickableChampionIds.filter((id) => id > 0)
  return ids.length ? new Set(ids) : null
})

/** The bench as it will be once the swap lands: the tapped champion's place goes to yours. */
const bench = computed(() =>
  (props.cs.benchChampionIds ?? [])
    .map((id) => (id === pendingId.value && dealtId.value > 0 ? dealtId.value : id))
    // League also lists ids Data Dragon doesn't know; show real champions only.
    .filter((id) => ddragon.champions.has(id))
    .map((id) => ({ id, name: championName(id), unavailable: !!pickable.value && !pickable.value.has(id) && id !== dealtId.value })),
)

const swap = useBusy()
const reroll = useBusy()
const coolingDown = ref(false)
let coolTimer: ReturnType<typeof setTimeout> | undefined
function rest() {
  coolingDown.value = true
  clearTimeout(coolTimer)
  coolTimer = setTimeout(() => (coolingDown.value = false), BENCH_COOLDOWN_MS)
}
onBeforeUnmount(() => {
  clearTimeout(coolTimer)
  clearTimeout(pendingTimer)
  emit('pending', null)
})

const benchLocked = computed(
  () => !props.cs.canBenchSwap || swap.busy.value || reroll.busy.value || coolingDown.value || pendingId.value !== null,
)

function take(id: number) {
  if (benchLocked.value || id === dealtId.value) return
  tapFeedback()
  setPending(id)
  void swap.run(async () => {
    const r = await sendCommand({ type: 'BenchSwap', championId: id })
    if (!r?.success) {
      if (pendingId.value === id) setPending(null)
      return
    }
    haptic('success')
    rest()
  })
}

const rerollsLeft = computed(() => props.cs.rerollsRemaining)
const rerollDisabled = computed(
  () => !props.cs.canReroll || reroll.busy.value || swap.busy.value || coolingDown.value || pendingId.value !== null,
)
function doReroll() {
  if (rerollDisabled.value) return
  // The client's own reroll click (sfx-cs-button-reroll-click) instead of the generic tap.
  play('reroll')
  haptic('selection')
  void reroll.run(async () => {
    const r = await sendCommand({ type: 'Reroll' })
    if (r?.success) rest()
  })
}

const myName = computed(() => championName(myId.value) || 'Dealing…')
const rerollLine = computed(() => {
  if (!props.cs.allowRerolling) return undefined
  const n = rerollsLeft.value
  return n === 0 ? 'No rerolls left' : n === 1 ? '1 reroll left' : `${n} rerolls left`
})

const footer = computed(() => {
  if (props.stage === 'starting') return 'The game is starting.'
  if (!props.cs.canBenchSwap) return 'The bench opens shortly.'
  return 'Tap a champion to take it. Yours goes to the bench.'
})
</script>

<template>
  <ListSection header="Your Champion">
    <ListRow variant="you" :title="myName" :subtitle="rerollLine">
      <template #lead>
        <ChampionIcon :champion-id="myId" state="you" decorative />
      </template>
      <template v-if="cs.allowRerolling" #trailing>
        <LrButton
          variant="gray"
          size="sm"
          :leading="DicesIcon"
          :loading="reroll.busy.value"
          :disabled="rerollDisabled"
          :aria-label="myId > 0 ? `Reroll ${championName(myId)}` : 'Reroll'"
          @click="doReroll"
        >
          <span>Reroll</span>
        </LrButton>
      </template>
    </ListRow>
  </ListSection>

  <ListSection header="Bench" :header-trailing="bench.length ? String(bench.length) : undefined" :footer="footer">
    <li class="lr-bench">
      <ChampionGrid :items="bench" :disabled="benchLocked" empty-text="The bench is empty." @select="take" />
    </li>
  </ListSection>
</template>

<style scoped>
.lr-bench {
  padding: var(--space-4) var(--space-4) var(--space-3);
}
</style>
