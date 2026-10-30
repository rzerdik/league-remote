import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useBusy } from '../../composables/useBusy'
import type { ChampSelectState } from '../../contracts'
import { championName } from '../../ddragon'
import { haptic, tapFeedback } from '../../haptics'
import { sendCommand } from '../../hub'
import { play } from '../../sound'
import { DURATION } from '../../styles/tokens'

/**
 * Your pick or ban from the picker: the champion you tapped (shown at once, sent as a hover) and Lock In / Ban.
 * League's state stays the truth: a hover only shows until League echoes it, and Lock In stays usable after a failure.
 */
export function useChampionChoice(cs: () => ChampSelectState, canAct: () => boolean) {
  const action = computed(() => cs().myAction)
  const isBan = computed(() => action.value?.type === 'ban')

  /** The champion you tapped last, until League echoes it. */
  const pending = ref<number | null>(null)
  const hover = useBusy()
  watch(
    () => action.value?.id,
    () => (pending.value = null),
  )
  // Drop ours once League echoes the one we sent last (an echo of an older tap must not revert a newer one still on its
  // way), or when nothing of ours is in flight (hovered on the PC).
  watch(
    () => action.value?.championId,
    (id) => {
      if (id === pending.value || !hover.busy.value) pending.value = null
    },
  )
  const selectedId = computed(() => pending.value ?? (action.value?.championId || 0))

  /** Your own hover while picking: your translucent portrait and your slot in the pick order. */
  const myHoverId = computed(() => (action.value && !action.value.completed && !isBan.value ? selectedId.value : 0))

  /**
   * Sends hovers one at a time, each for the action on screen; taps made meanwhile collapse into the latest, which is
   * sent next.
   */
  async function sendHovers() {
    let sent = 0
    while (pending.value && pending.value !== sent && action.value) {
      const id = pending.value
      sent = id
      const r = await sendCommand({ type: 'HoverChampion', championId: id, actionId: action.value.id })
      if (!r?.success && pending.value === id) pending.value = null
    }
  }
  function tap(id: number) {
    if (!canAct() || !action.value) return
    if (id !== selectedId.value) {
      haptic('selection')
      play('hover')
    }
    pending.value = id
    void hover.run(sendHovers) // already running: it picks this tap up when the current hover returns
  }

  /**
   * The lock-in / ban pop on your portrait. It hangs off the command's result, which the connector reports once the
   * client confirmed: by then League has already moved `myAction` on.
   */
  const popping = ref(false)
  let popTimer: ReturnType<typeof setTimeout> | undefined
  onBeforeUnmount(() => clearTimeout(popTimer))

  const lock = useBusy()
  const lockDisabled = computed(
    () => lock.busy.value || !action.value?.isInProgress || action.value.completed || !(selectedId.value > 0),
  )
  function lockIn() {
    if (lockDisabled.value || !action.value) return
    const championId = selectedId.value
    const actionId = action.value.id
    tapFeedback()
    void lock.run(async () => {
      const r = await sendCommand({ type: 'LockChampion', championId, actionId })
      if (!r?.success) return
      haptic('success')
      popping.value = true
      clearTimeout(popTimer)
      popTimer = setTimeout(() => (popping.value = false), DURATION.bouncy)
    })
  }

  const lockVerb = computed(() => (isBan.value ? 'Ban' : 'Lock In'))
  /** Named on the button when it fits ("Lock In Ahri"); a long name drops off and the verb stays (.lr-fit-label). */
  const lockName = computed(() => (selectedId.value > 0 ? championName(selectedId.value) : ''))
  /** VoiceOver hears the champion with the verb, or what to do first. */
  const lockLabel = computed(() =>
    lockName.value ? `${lockVerb.value} ${lockName.value}` : `${lockVerb.value}: tap a champion first`,
  )

  return { selectedId, myHoverId, tap, lockIn, locking: lock.busy, lockDisabled, lockVerb, lockName, lockLabel, popping }
}
