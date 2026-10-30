<script setup lang="ts">
import { CheckIcon, XIcon } from '@lucide/vue'
import { acceptRequest, busy, cancelRequest, declineRequest, incomingRequests, outgoingRequests } from '../../friendRequests'
import { riotId } from '../../riotId'
import Avatar from '../lr/Avatar.vue'
import ListRow from '../lr/ListRow.vue'
import ListSection from '../lr/ListSection.vue'
import LrButton from '../lr/LrButton.vue'

/**
 * Friends tab › pending friend requests: `incoming` ones with Decline (✕) / Accept (✓) (above the friend list), or the
 * `outgoing` ones the user sent with Cancel (below it; no alert: a withdrawn request can simply be sent again).
 * Renders nothing when there are none.
 */
defineProps<{ direction: 'incoming' | 'outgoing' }>()
</script>

<template>
  <ListSection v-if="direction === 'incoming' && incomingRequests.length" header="Friend Requests" :header-trailing="String(incomingRequests.length)">
    <ListRow v-for="r in incomingRequests" :key="r.puuid" :title="r.gameName" :busy="busy.has(r.puuid)">
      <template #lead><Avatar :name="r.gameName" /></template>
      <template v-if="!busy.has(r.puuid)" #trailing>
        <span class="request-actions">
          <LrButton variant="gray" size="sm" :icon="XIcon" :aria-label="`Decline ${riotId(r)}`" @click="declineRequest(r)" />
          <LrButton variant="success" size="sm" :icon="CheckIcon" :aria-label="`Accept ${riotId(r)}`" @click="acceptRequest(r)" />
        </span>
      </template>
    </ListRow>
  </ListSection>

  <ListSection v-if="direction === 'outgoing' && outgoingRequests.length" header="Sent Requests" :header-trailing="String(outgoingRequests.length)">
    <ListRow v-for="r in outgoingRequests" :key="r.puuid" :title="r.gameName" :busy="busy.has(r.puuid)">
      <template #lead><Avatar :name="r.gameName" /></template>
      <template v-if="!busy.has(r.puuid)" #trailing>
        <LrButton variant="plain" size="sm" :aria-label="`Cancel request to ${riotId(r)}`" @click="cancelRequest(r)">Cancel</LrButton>
      </template>
    </ListRow>
  </ListSection>
</template>

<style scoped>
/* Decline and Accept stay side by side, also when large text wraps the row's trailing content under the name. */
.request-actions {
  display: inline-flex;
  flex: none;
  gap: var(--space-3);
}
</style>
