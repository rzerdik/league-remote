<script setup lang="ts">
import { CircleUserIcon } from '@lucide/vue'
import { computed, watch } from 'vue'
import FriendSheet from '../../components/friends/FriendSheet.vue'
import Avatar from '../../components/lr/Avatar.vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import Sheet from '../../components/lr/Sheet.vue'
import type { LobbyMember } from '../../contracts'
import { profileIconUrl } from '../../ddragon'
import { friendOf } from '../../friends'
import { haptic } from '../../haptics'
import { positionLabel } from '../../labels'
import { openPlayer } from '../../navigation'
import { playerName, playerShortName } from '../../riotId'

/**
 * A party member's profile, opened from the Lobby's party list. A friend gets the friend profile sheet (presence,
 * rank, Message, View Profile…) with their party details added; yourself and anyone else get what the lobby knows: the
 * full Riot ID, leader and the two roles they picked, plus View Profile for the others (their ranks, form and match
 * history, pushed over the lobby) when the PC app sends their puuid.
 */
const props = defineProps<{ member: LobbyMember | null }>()
const open = defineModel<boolean>('open', { required: true })

// Left the party (here or on the PC): close.
watch(
  () => props.member,
  (m) => {
    if (!m) open.value = false
  },
)

const friend = computed(() => (props.member && !props.member.isLocalPlayer ? friendOf(props.member) : null))
const primary = computed(() => positionLabel(props.member?.firstPositionPreference))
const secondary = computed(() => positionLabel(props.member?.secondPositionPreference))
const canView = computed(() => !!props.member && !props.member.isLocalPlayer && !!props.member.puuid)

function viewProfile() {
  const m = props.member
  if (!m?.puuid) return
  haptic('selection')
  open.value = false
  openPlayer(m.puuid, m.riotId)
}
</script>

<template>
  <FriendSheet v-if="friend" v-model:open="open" :friend="friend">
    <template v-if="member">
      <ListRow title="Party" :value="member.isLeader ? 'Leader' : 'Member'" />
      <ListRow v-if="primary" title="Primary Role" :value="primary" />
      <ListRow v-if="secondary" title="Secondary Role" :value="secondary" />
    </template>
  </FriendSheet>
  <Sheet
    v-else
    v-model:open="open"
    modal
    :detents="['medium', 'large']"
    body-class="lr-sheet-body--lists"
    :title="member ? playerShortName(member.riotId) || 'Player' : 'Player'">
    <ListSection v-if="member">
      <ListRow :title="playerName(member.riotId) || 'Player'" :subtitle="member.isLocalPlayer ? 'You' : undefined" wrap>
        <template #lead><Avatar :name="member.riotId" :image-url="profileIconUrl(member.profileIconId)" /></template>
      </ListRow>
      <ListRow title="Party" :value="member.isLeader ? 'Leader' : 'Member'" />
      <ListRow v-if="primary" title="Primary Role" :value="primary" />
      <ListRow v-if="secondary" title="Secondary Role" :value="secondary" />
    </ListSection>
    <ListSection v-if="canView">
      <ListRow tappable :icon="CircleUserIcon" tile-color="indigo" title="View Profile" @click="viewProfile" />
    </ListSection>
  </Sheet>
</template>
