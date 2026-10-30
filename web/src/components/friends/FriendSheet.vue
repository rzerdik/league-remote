<script setup lang="ts">
import { CircleUserIcon, MessageCircleIcon, UserMinusIcon, UserPlusIcon } from '@lucide/vue'
import { computed, ref, watch } from 'vue'
import { openDirectMessage } from '../../chat'
import type { Friend } from '../../contracts'
import {
  customGroups,
  friendActivity,
  friendChampion,
  friendQueue,
  friendRank,
  friendRankLabel,
  presenceTone,
  removeFriend,
} from '../../friends'
import { haptic } from '../../haptics'
import { inviteAction, inviteFriend } from '../../invites'
import { openPlayer } from '../../navigation'
import { riotId } from '../../riotId'
import { toast } from '../../store'
import Avatar from '../lr/Avatar.vue'
import ChampionIcon from '../lr/ChampionIcon.vue'
import ListRow from '../lr/ListRow.vue'
import ListSection from '../lr/ListSection.vue'
import LrAlert from '../lr/LrAlert.vue'
import Sheet from '../lr/Sheet.vue'

/**
 * A friend's profile in a modal sheet: presence, rank, note, then Invite / Message / View Profile / Remove. Invite is
 * offered while you can invite from your lobby (invites.ts `inviteAction`), Message opens the DM (chat.ts
 * `openDirectMessage`), View Profile pushes their full profile (ranks, form, champions, match history) over the current
 * screen, Remove asks first. The default slot adds detail rows under the presence (the party sheet adds the member's roles).
 */
const props = defineProps<{ friend: Friend | null }>()
const open = defineModel<boolean>('open', { required: true })

// Removed (here or on the PC): close.
watch(
  () => props.friend,
  (f) => {
    if (!f) open.value = false
  },
)

const f = computed(() => props.friend)
const group = computed(() => customGroups.value.find((g) => g.id === f.value?.groupId)?.name ?? '')
const invite = computed(() => (f.value ? inviteAction(f.value) : null))

const busy = ref<'invite' | 'message' | 'remove' | null>(null)

async function run(kind: 'invite' | 'message', action: () => unknown) {
  if (busy.value) return
  haptic('selection')
  busy.value = kind
  try {
    await action()
  } finally {
    busy.value = null
  }
}

function sendInvite() {
  const friend = f.value
  if (!friend || invite.value?.disabled !== false) return
  void run('invite', async () => {
    if (await inviteFriend(friend)) toast(`Invited ${riotId(friend)}`, 'success')
  })
}

function message() {
  const friend = f.value
  if (!friend) return
  void run('message', async () => {
    // Opens (or starts) the DM in the Chat tab; on failure the sheet stays (the command already toasted).
    if (await openDirectMessage(friend.puuid)) open.value = false
  })
}

function viewProfile() {
  const friend = f.value
  if (!friend) return
  haptic('selection')
  open.value = false
  openPlayer(friend.puuid, riotId(friend))
}

const removeAlert = ref(false)
async function confirmRemove() {
  const friend = f.value
  if (!friend || busy.value) return
  busy.value = 'remove'
  try {
    if (await removeFriend(friend)) {
      haptic('success')
      open.value = false
    }
  } finally {
    busy.value = null
  }
}
</script>

<template>
  <Sheet v-model:open="open" modal :detents="['medium', 'large']" :title="f?.gameName ?? 'Friend'" body-class="lr-sheet-body--lists">
    <template v-if="f">
      <ListSection>
        <ListRow :title="riotId(f)" wrap>
          <template #lead>
            <ChampionIcon v-if="f.activity === 'in-game' && f.championId" :champion-id="f.championId" decorative />
            <Avatar v-else :name="f.gameName" />
          </template>
        </ListRow>
        <ListRow title="Status" :value="friendActivity(f)" :value-tone="presenceTone(f)" />
        <ListRow v-if="f.activity && friendQueue(f)" title="Queue" :value="friendQueue(f)" />
        <ListRow v-if="friendChampion(f)" title="Playing" :value="friendChampion(f)" />
        <ListRow v-if="friendRank(f)" :title="friendRankLabel(f)" :value="friendRank(f)" />
        <ListRow v-if="group" title="Group" :value="group" />
        <ListRow v-if="f.note" title="Note" :subtitle="f.note" wrap />
        <slot />
      </ListSection>

      <ListSection>
        <ListRow
          tappable
          variant="accent"
          :icon="UserPlusIcon"
          tile-color="accent"
          :title="invite?.label ?? 'Invite to Party'"
          :disabled="!invite || invite.disabled"
          :busy="busy === 'invite'"
          @click="sendInvite"
        />
        <ListRow
          tappable
          variant="accent"
          :icon="MessageCircleIcon"
          tile-color="blue"
          title="Message"
          :busy="busy === 'message'"
          @click="message"
        />
        <ListRow
          tappable
          variant="accent"
          :icon="CircleUserIcon"
          tile-color="indigo"
          title="View Profile"
          @click="viewProfile"
        />
      </ListSection>

      <ListSection>
        <ListRow
          tappable
          variant="danger"
          :icon="UserMinusIcon"
          tile-color="red"
          title="Remove Friend"
          :busy="busy === 'remove'"
          @click="removeAlert = true"
        />
      </ListSection>

      <LrAlert
        v-model:open="removeAlert"
        :title="`Remove ${f.gameName}?`"
        message="They’ll be removed from your friends in League. You’d need to send a new friend request."
        action-label="Remove"
        destructive
        @action="confirmRemove"
      />
    </template>
  </Sheet>
</template>
