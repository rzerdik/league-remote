<script setup lang="ts">
import { BanIcon, LockIcon } from '@lucide/vue'
import { computed, ref, watch } from 'vue'
import { cancelAutoPickAction, pendingAction, pendingSecondsLeft, useAutoPickToasts } from '../../autoPick'
import ActionBanner from '../../components/lr/ActionBanner.vue'
import { useBusy } from '../../composables/useBusy'
import { championName } from '../../ddragon'
import { tapFeedback } from '../../haptics'
import { liveAutoPick } from '../../store'

/**
 * Champion Select: the auto ban / lock-in the PC is about to make ("Auto-locking Ahri", "In 3 s") as an actionable
 * banner with Cancel ('CancelAutoPickAction'; the hover stays, so the user picks on from there). The countdown is the
 * connector's (`pendingSecondsLeft`); the seconds are hidden from VoiceOver, which hears the banner once. Also toasts
 * each new line of the automation's log while Champion Select is up ("Auto-banned Zed").
 */
useAutoPickToasts(() => true)

const pending = pendingAction
/**
 * The action just cancelled (confirmed by the PC): its banner hides at once instead of waiting for the state to drop
 * it. Optimistic like the master switch: the next AutoPickState after that decides (still carrying it: it shows).
 */
const cancelledId = ref<number | null>(null)
let cancelResolved = false
watch(liveAutoPick, () => {
  if (cancelResolved) cancelledId.value = null
})
const shown = computed(() => (pending.value && pending.value.actionId !== cancelledId.value ? pending.value : null))

const seconds = computed(() => Math.ceil(pendingSecondsLeft() ?? 0))
const name = computed(() => championName(shown.value?.championId ?? 0) || 'your champion')
const isBan = computed(() => shown.value?.kind === 'ban')
/** Still counting down and cancellable; otherwise the request is on its way to League. */
const counting = computed(() => !!shown.value?.cancellable && seconds.value > 0)
const title = computed(() => {
  if (!counting.value) return isBan.value ? `Banning ${name.value}…` : `Locking In ${name.value}…`
  return isBan.value ? `Auto-banning ${name.value}` : `Auto-locking ${name.value}`
})

const cancel = useBusy()
function onCancel() {
  const p = shown.value
  if (!p || !p.cancellable) return
  tapFeedback()
  void cancel.run(async () => {
    cancelResolved = false
    const ok = await cancelAutoPickAction(p.actionId)
    // Confirmed but the state still shows it: hide it until the next update says otherwise.
    cancelledId.value = ok && liveAutoPick.value?.run?.pending?.actionId === p.actionId ? p.actionId : null
    cancelResolved = true
  })
}
</script>

<template>
  <Teleport defer to="#lr-banners-actions">
    <TransitionGroup name="lr-pop">
      <ActionBanner
        v-if="shown"
        :key="`auto-${shown.actionId}`"
        :icon="isBan ? BanIcon : LockIcon"
        :icon-tone="isBan ? 'red' : 'accent'"
        :title="title"
        decline-label="Cancel"
        :busy="cancel.busy.value ? 'decline' : null"
        :disabled="!shown.cancellable || cancel.busy.value"
        @decline="onCancel"
      >
        <template #message>
          <template v-if="counting">
            <span aria-hidden="true">In {{ seconds }} s</span>
            <span class="lr-sr">In a few seconds. Cancel to choose yourself.</span>
          </template>
          <template v-else>Sending to League</template>
        </template>
      </ActionBanner>
    </TransitionGroup>
  </Teleport>
</template>
