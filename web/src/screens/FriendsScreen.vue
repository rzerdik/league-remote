<script setup lang="ts">
import { SearchXIcon, UserPlusIcon, UsersIcon } from '@lucide/vue'
import { computed, ref } from 'vue'
import ConnectionEmptyState from '../components/ConnectionEmptyState.vue'
import AddFriendSheet from '../components/friends/AddFriendSheet.vue'
import FriendRequestsSection from '../components/friends/FriendRequestsSection.vue'
import FriendRow from '../components/friends/FriendRow.vue'
import FriendSheet from '../components/friends/FriendSheet.vue'
import EmptyState from '../components/lr/EmptyState.vue'
import LargeTitle from '../components/lr/LargeTitle.vue'
import ListRow from '../components/lr/ListRow.vue'
import ListSection from '../components/lr/ListSection.vue'
import LrButton from '../components/lr/LrButton.vue'
import type { MenuItem } from '../components/lr/LrMenu.vue'
import SearchField from '../components/lr/SearchField.vue'
import { connectionEmpty } from '../connection'
import type { Friend } from '../contracts'
import { incomingRequests, outgoingRequests } from '../friendRequests'
import {
  PRESENCE_GROUPS,
  customGroups,
  friendById,
  friendStatus,
  friends,
  presenceGroup,
  type PresenceGroup,
} from '../friends'
import { haptic } from '../haptics'
import { liveFriends } from '../store'

/**
 * Friends tab: League's friend list grouped by presence (In Game, Online, Away, Offline — offline folded away), each
 * with its status line ("In Game 12:30 · Ranked Solo/Duo"). Tap → profile sheet (Invite, Message, Remove). With the
 * user's own friend groups from the client, a menu narrows the list to one of them.
 */
const query = ref('')
const groupId = ref<number | null>(null)
const showOffline = ref(false)

const group = computed(() => customGroups.value.find((g) => g.id === groupId.value) ?? null)
const groupItems = computed<MenuItem[]>(() => [
  { label: 'All Friends', checked: groupId.value === null, onSelect: () => (groupId.value = null) },
  ...customGroups.value.map((g) => ({ label: g.name, checked: g.id === groupId.value, onSelect: () => (groupId.value = g.id) })),
])

const filtered = computed(() => {
  const q = query.value.trim().toLowerCase()
  return friends.value.filter(
    (f) =>
      (groupId.value === null || f.groupId === groupId.value) &&
      (!q || f.gameName.toLowerCase().includes(q) || f.tagLine.toLowerCase().includes(q)),
  )
})

const sections = computed(() =>
  PRESENCE_GROUPS.map((g) => ({ ...g, friends: filtered.value.filter((f) => presenceGroup(f) === g.id) })).filter(
    (s) => s.friends.length,
  ),
)

const onlineCount = computed(() => friends.value.filter((f) => presenceGroup(f) !== 'offline').length)

/** Offline friends stay folded unless searching (a search shows every match). */
function folded(id: PresenceGroup) {
  return id === 'offline' && !showOffline.value && !query.value.trim()
}

const openId = ref<string | null>(null)
const sheetOpen = ref(false)
const selected = computed(() => friendById(openId.value))
function choose(f: Friend) {
  haptic('selection')
  openId.value = f.id
  sheetOpen.value = true
}

const addFriendOpen = ref(false)
const hasRequests = computed(() => incomingRequests.value.length + outgoingRequests.value.length > 0)
</script>

<template>
  <div class="lr-content">
    <LargeTitle title="Friends">
      <template v-if="friends.length">{{ onlineCount }} online</template>
      <!-- On the title row; docks into the nav bar once the title collapses, so it stays reachable while scrolling. -->
      <template #trailing>
        <LrButton variant="glass" :icon="UserPlusIcon" aria-label="Add Friend" aria-haspopup="dialog" @click="addFriendOpen = true" />
      </template>
    </LargeTitle>

    <FriendRequestsSection direction="incoming" />

    <ConnectionEmptyState />
    <template v-if="connectionEmpty" />
    <EmptyState v-else-if="!liveFriends" busy title="Loading Friends" />
    <EmptyState
      v-else-if="!friends.length && !hasRequests"
      :icon="UsersIcon"
      title="No Friends Yet"
      message="Friends you add in League show up here."
    />

    <template v-else-if="friends.length">
      <ListSection bare>
        <SearchField v-model="query" placeholder="Search Friends" />
      </ListSection>

      <ListSection v-if="customGroups.length">
        <ListRow text title="Group" :value="group?.name ?? 'All Friends'" :menu="groupItems" menu-title="Show Group" />
      </ListSection>

      <EmptyState v-if="!sections.length" :icon="SearchXIcon" title="No Results" />

      <ListSection v-for="s in sections" :key="s.id" :header="s.title" :header-trailing="String(s.friends.length)">
        <ListRow
          v-if="folded(s.id)"
          tappable
          chevron
          :title="`Show ${s.friends.length} Offline`"
          variant="accent"
          @click="showOffline = true"
        />
        <template v-else>
          <FriendRow
            v-for="f in s.friends"
            :key="f.id"
            :friend="f"
            tappable
            :aria-label="`${f.gameName}, ${friendStatus(f)}`"
            @click="choose(f)"
          />
          <ListRow
            v-if="s.id === 'offline' && showOffline && !query.trim()"
            tappable
            variant="accent"
            title="Hide Offline"
            @click="showOffline = false"
          />
        </template>
      </ListSection>
    </template>

    <FriendRequestsSection direction="outgoing" />

    <FriendSheet v-model:open="sheetOpen" :friend="selected" />
    <AddFriendSheet v-model:open="addFriendOpen" />
  </div>
</template>
