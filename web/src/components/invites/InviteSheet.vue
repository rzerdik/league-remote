<script setup lang="ts">
import { UserPlusIcon, UsersIcon } from '@lucide/vue'
import { computed, ref } from 'vue'
import type { Friend, LobbyState } from '../../contracts'
import { tapFeedback } from '../../haptics'
import { invitableFriends, invitationStatus, inviteFriend } from '../../invites'
import FriendRow from '../friends/FriendRow.vue'
import EmptyState from '../lr/EmptyState.vue'
import ListSection from '../lr/ListSection.vue'
import Sheet from '../lr/Sheet.vue'

/**
 * "Invite to Party": friends who are in League and free (online / away), with the state of invites already sent from
 * this lobby (Invited / Declined, from LobbyState.invitations). Tapping a row sends SendInvitation; a declined friend
 * can be invited again.
 */
const props = defineProps<{ open: boolean; lobby: LobbyState; friends: Friend[] }>()
const emit = defineEmits<{ 'update:open': [open: boolean] }>()

/** Each friend's sent invite, as it reads (Invited / Declined / Failed). */
const sent = computed(() => new Map((props.lobby.invitations ?? []).map((i) => [i.puuid, invitationStatus(i)])))
const rows = computed(() => invitableFriends(props.friends, props.lobby))

const pending = ref<string | null>(null)
async function invite(f: Friend) {
  if (pending.value || sent.value.get(f.puuid)?.pending) return
  tapFeedback()
  pending.value = f.puuid
  try {
    await inviteFriend(f)
  } finally {
    pending.value = null
  }
}
</script>

<template>
  <Sheet
    :open="open"
    modal
    :detents="['medium', 'large']"
    title="Invite to Party"
    body-class="lr-sheet-body--lists"
    @update:open="emit('update:open', $event)"
  >
    <EmptyState
      v-if="!lobby.canInvite"
      :icon="UsersIcon"
      title="Can't Invite"
      :message="lobby.isSearching ? 'Cancel the search to invite friends.' : 'Only the party leader can invite, and the party must have room.'"
    />
    <EmptyState
      v-else-if="rows.length === 0"
      :icon="UserPlusIcon"
      title="No Friends Online"
      message="Friends who are online in League show up here."
    />
    <ListSection v-else header="Online" bare>
      <TransitionGroup tag="ul" name="lr-rows" class="lr-list">
        <FriendRow
          v-for="f in rows"
          :key="f.puuid"
          :friend="f"
          tappable
          :value="sent.get(f.puuid)?.label"
          :value-tone="sent.get(f.puuid)?.tone"
          :busy="pending === f.puuid"
          :disabled="!!pending || !!sent.get(f.puuid)?.pending"
          :aria-label="sent.get(f.puuid) ? `${f.gameName}, ${sent.get(f.puuid)!.label}` : `Invite ${f.gameName}`"
          @click="invite(f)"
        />
      </TransitionGroup>
    </ListSection>
  </Sheet>
</template>
