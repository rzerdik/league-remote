<script setup lang="ts">
import { UserPlusIcon } from '@lucide/vue'
import { computed } from 'vue'
import { useAnswers } from '../../composables/useAnswers'
import type { ReceivedInvitation } from '../../contracts'
import { haptic, tapFeedback } from '../../haptics'
import { queueName } from '../../labels'
import { sendCommand } from '../../hub'
import { playerShortName } from '../../riotId'
import { liveState } from '../../store'
import ActionBanner from '../lr/ActionBanner.vue'

/**
 * Received game invites as actionable banners above the toasts (BannerHost's #lr-banners-actions), on every tab.
 * The connector only lists invites outside champ select and the game, so they never cover the draft.
 */
const invitations = computed(() => liveState.value?.invitations ?? [])

const answers = useAnswers<'accept' | 'decline'>()
function answer(kind: 'accept' | 'decline', i: ReceivedInvitation) {
  tapFeedback()
  void answers.answer(i.id, kind, async () => {
    const r = await sendCommand({ type: kind === 'accept' ? 'AcceptInvitation' : 'DeclineInvitation', invitationId: i.id })
    if (r?.success && kind === 'accept') haptic('success')
  })
}

/** "to Ranked Flex"; League's restriction when it won't let you join. */
function detail(i: ReceivedInvitation) {
  if (!i.canAccept) return "You can't join this queue right now."
  return `to ${i.queueName || (i.queueId > 0 ? queueName(i.queueId) : '') || 'a game'}`
}
</script>

<template>
  <Teleport defer to="#lr-banners-actions">
    <TransitionGroup name="lr-pop">
      <ActionBanner
        v-for="i in invitations"
        :key="i.id"
        :icon="UserPlusIcon"
        :title="`${playerShortName(i.fromRiotId)} invited you`"
        :message="detail(i)"
        action-label="Accept"
        :busy="answers.busyFor(i.id)"
        :disabled="!!answers.inFlight.value"
        :action-disabled="!i.canAccept"
        @accept="answer('accept', i)"
        @decline="answer('decline', i)"
      />
    </TransitionGroup>
  </Teleport>
</template>
