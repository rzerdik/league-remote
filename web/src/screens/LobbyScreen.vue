<script setup lang="ts">
import { CrownIcon, LogOutIcon, PlayIcon, PlusIcon, SearchIcon } from '@lucide/vue'
import { computed, ref, watch } from 'vue'
import { autoAcceptEnabled } from '../autoPick'
import { partyChat } from '../chat'
import { secondsSinceReading } from '../clock'
import ModeList from '../components/ModeList.vue'
import ChatButton from '../components/chat/ChatButton.vue'
import ChatSheet from '../components/chat/ChatSheet.vue'
import InviteSheet from '../components/invites/InviteSheet.vue'
import ActionBar, { type ActionBarAccessory } from '../components/lr/ActionBar.vue'
import Avatar from '../components/lr/Avatar.vue'
import LargeTitle from '../components/lr/LargeTitle.vue'
import ListRow from '../components/lr/ListRow.vue'
import ListSection from '../components/lr/ListSection.vue'
import LrAlert from '../components/lr/LrAlert.vue'
import LrButton from '../components/lr/LrButton.vue'
import Sheet from '../components/lr/Sheet.vue'
import Tag from '../components/lr/Tag.vue'
import { useBusy } from '../composables/useBusy'
import type { LobbyMember, LobbyState } from '../contracts'
import { profileIconUrl } from '../ddragon'
import { formatSeconds } from '../format'
import { friendOf } from '../friends'
import { tapFeedback } from '../haptics'
import { invitationStatus, invitingLobby } from '../invites'
import { isPracticeTool, lobbyMap, lobbyTitle, partyMax, positionLabel } from '../labels'
import { sendCommand } from '../hub'
import { playerShortName } from '../riotId'
import { liveFriends } from '../store'
import LobbyRoles from './lobby/LobbyRoles.vue'
import PartyMemberSheet from './lobby/PartyMemberSheet.vue'

const props = defineProps<{ lobby: LobbyState }>()
const { busy, run } = useBusy()
const modeOpen = ref(false)
const leaveAlert = ref(false)
/** The pending command is Leave: its spinner goes on Leave, not on Find Match. */
const leaving = ref(false)
const chatOpen = ref(false)
const inviteOpen = ref(false)

const title = computed(() => lobbyTitle(props.lobby))
/** "2/5", or the count alone where the party size isn't known (custom games, new queues). */
const partyCount = computed(() => {
  const max = partyMax(props.lobby)
  return max ? `${props.lobby.members.length}/${max}` : String(props.lobby.members.length)
})
const map = computed(() => lobbyMap(props.lobby))
const isPractice = computed(() => isPracticeTool(props.lobby))
const startsGame = computed(() => props.lobby.isCustom || isPractice.value)
const currentMode = computed<number | 'practice'>(() => (isPractice.value ? 'practice' : props.lobby.queueId))

const showRoles = computed(() => props.lobby.showPositionSelector && props.lobby.members.some((m) => m.isLocalPlayer))
const canChooseMode = computed(() => props.lobby.isLeader && !props.lobby.isSearching)
watch(canChooseMode, (ok) => {
  if (!ok) modeOpen.value = false
})

/** Seconds in queue, counted locally between snapshots. */
const inQueue = computed(() =>
  props.lobby.timeInQueue == null ? null : props.lobby.timeInQueue + secondsSinceReading('queue', props.lobby.timeInQueue),
)

const accessory = computed<ActionBarAccessory | null>(() => {
  const l = props.lobby
  if (l.isSearching) {
    const t = inQueue.value
    const estimate = l.estimatedQueueTime ? `~${formatSeconds(l.estimatedQueueTime)}` : ''
    return {
      tone: 'orange',
      pulsing: true,
      title: 'Finding Match',
      detail: [estimate, autoAcceptEnabled.value && 'Auto Accept'].filter(Boolean).join(' · ') || undefined,
      value: t != null ? formatSeconds(t) : undefined,
      valueLabel: t != null ? `Time in queue ${formatSeconds(t)}` : undefined,
    }
  }
  if (!l.isLeader) return { tone: 'gray', title: 'Waiting for Leader' }
  return null
})

function start() {
  tapFeedback()
  void run(() => sendCommand({ type: 'StartMatchmaking' }))
}
function cancel() {
  tapFeedback()
  void run(() => sendCommand({ type: 'CancelMatchmaking' }))
}
/**
 * Leave, next to Find Match (the client's ✕ there; labelled here, since a ✕ on iOS dismisses rather than leaves).
 * Leading a party with others asks first: everyone else is left without a leader.
 */
function leave() {
  tapFeedback()
  if (props.lobby.isLeader && props.lobby.members.length > 1) leaveAlert.value = true
  else confirmLeave()
}
function confirmLeave() {
  leaving.value = true
  void run(() => sendCommand({ type: 'LeaveLobby' })).finally(() => (leaving.value = false))
}
function openModes() {
  tapFeedback()
  modeOpen.value = true
}

/** "Invite Friends" in the party list: whenever you may invite (leader, or invites opened), there is room and no search. */
const canInvite = computed(() => !!invitingLobby.value)
watch(canInvite, (ok) => {
  if (!ok) inviteOpen.value = false
})
function openInvite() {
  tapFeedback()
  inviteOpen.value = true
}
/** Invites that haven't joined: shown under the members, as the client's party list does. */
const invited = computed(() => (props.lobby.invitations ?? []).map((i) => ({ ...i, status: invitationStatus(i) })))

/** The member whose profile sheet is open, by name: it follows lobby updates and closes when they leave. */
const memberName = ref<string | null>(null)
const memberOpen = ref(false)
const selectedMember = computed(() => props.lobby.members.find((m) => m.riotId === memberName.value) ?? null)
function openMember(m: LobbyMember) {
  memberName.value = m.riotId
  memberOpen.value = true
}

function roles(m: LobbyMember) {
  return [positionLabel(m.firstPositionPreference), positionLabel(m.secondPositionPreference)].filter(Boolean)
}
/** What VoiceOver reads for a member row: your own row is told apart by colour on screen, so it says "you" here. */
function memberLabel(m: LobbyMember) {
  return [playerShortName(m.riotId) || 'Player', m.isLocalPlayer && 'you', m.isLeader && 'party leader', ...roles(m)]
    .filter(Boolean)
    .join(', ')
}
</script>

<template>
  <div class="lr-content">
    <LargeTitle :title="title">
      <template v-if="map">{{ map }}</template>
      <!-- On the title row; docks into the nav bar once the title collapses, so it stays reachable while scrolling. -->
      <template v-if="partyChat" #trailing>
        <ChatButton :conversation="partyChat" @open="chatOpen = true" />
      </template>
    </LargeTitle>

    <ListSection v-if="canChooseMode">
      <ListRow tappable text pop-up title="Mode" :value="title" :disabled="busy" @click="openModes" />
    </ListSection>

    <LobbyRoles v-if="showRoles && !lobby.isSearching" :lobby="lobby" />

    <ListSection header="Party" :header-trailing="partyCount" bare>
      <TransitionGroup tag="ul" name="lr-rows" class="lr-list">
        <ListRow
          v-for="(m, i) in lobby.members"
          :key="m.riotId || `member-${i}`"
          tappable
          :aria-label="memberLabel(m)"
          @click="openMember(m)"
        >
          <template #lead><Avatar :name="m.riotId" :image-url="profileIconUrl(m.profileIconId)" /></template>
          <template #title>
            <span :class="{ 'lr-accent-text': m.isLocalPlayer }">{{ playerShortName(m.riotId) || 'Player' }}</span>
            <CrownIcon v-if="m.isLeader" class="lr-inline-symbol lr-accent-text" :stroke-width="2" role="img" aria-label="Party leader" />
          </template>
          <template v-if="roles(m).length" #trailing>
            <span class="lr-tags">
              <Tag v-for="r in roles(m)" :key="r">{{ r }}</Tag>
            </span>
          </template>
        </ListRow>
        <ListRow
          v-for="i in invited"
          :key="`invite-${i.puuid}`"
          :title="playerShortName(i.riotId)"
          :value="i.status.label"
          :value-tone="i.status.tone"
          :value-pulse="i.status.pending"
        >
          <template #lead><Avatar :name="i.riotId" :image-url="profileIconUrl(friendOf(i)?.profileIconId ?? 0)" /></template>
        </ListRow>
        <ListRow v-if="canInvite" key="invite" tappable title="Invite Friends" @click="openInvite">
          <template #lead><Avatar :icon="PlusIcon" /></template>
        </ListRow>
      </TransitionGroup>
    </ListSection>

    <PartyMemberSheet v-model:open="memberOpen" :member="selectedMember" />

    <InviteSheet v-model:open="inviteOpen" :lobby="lobby" :friends="liveFriends?.friends ?? []" />

    <Sheet v-model:open="modeOpen" modal :detents="['medium', 'large']" title="Game Mode" body-class="lr-sheet-body--lists">
      <ModeList mode="choose" :current="currentMode" :disabled="!canChooseMode" @done="modeOpen = false" />
    </Sheet>

    <ChatSheet v-model:open="chatOpen" :conversation="partyChat" />

    <!-- Searching or waiting, the one action belongs to the status: on its capsule, so the bar takes one row. -->
    <ActionBar :accessory="accessory">
      <template #accessory-action>
        <LrButton v-if="lobby.isSearching" variant="gray" size="sm" :loading="busy" @click="cancel">Cancel</LrButton>
        <LrButton v-else-if="!lobby.isLeader" variant="gray" size="sm" :loading="busy" @click="leave">Leave</LrButton>
      </template>
      <template v-if="lobby.isLeader && !lobby.isSearching">
        <LrButton
          variant="prominent"
          size="lg"
          block
          :leading="startsGame ? PlayIcon : SearchIcon"
          :loading="busy && !leaving"
          :disabled="leaving || !lobby.canStartActivity"
          @click="start"
        >
          {{ startsGame ? 'Start Game' : 'Find Match' }}
        </LrButton>
        <LrButton variant="glass" size="lg" :leading="LogOutIcon" :loading="busy && leaving" :disabled="busy" @click="leave">
          Leave
        </LrButton>
      </template>
    </ActionBar>

    <LrAlert v-model:open="leaveAlert" title="Leave Party?" action-label="Leave" destructive @action="confirmLeave" />
  </div>
</template>
