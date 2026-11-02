<script setup lang="ts">
import { MessageCircleIcon, SwordsIcon, UsersIcon } from '@lucide/vue'
import { computed, watch } from 'vue'
import { conversationById, conversationTitle, conversations, draftQuickReplies, senderLabel, unreadOf } from '../chat'
import ConnectionEmptyState from '../components/ConnectionEmptyState.vue'
import ChatThread from '../components/chat/ChatThread.vue'
import Avatar from '../components/lr/Avatar.vue'
import Badge from '../components/lr/Badge.vue'
import EmptyState from '../components/lr/EmptyState.vue'
import LargeTitle from '../components/lr/LargeTitle.vue'
import ListRow from '../components/lr/ListRow.vue'
import ListSection from '../components/lr/ListSection.vue'
import { connectionEmpty } from '../connection'
import type { ChatConversation } from '../contracts'
import { profileIconUrl } from '../ddragon'
import { listTime } from '../format'
import { friendOf } from '../friends'
import { haptic } from '../haptics'
import { nav, openConversation } from '../navigation'
import { liveChat, liveState, store } from '../store'

/**
 * Chat tab: the party room, the draft room, then direct conversations (as League has them) → one conversation with a
 * composer docked above the keyboard. The open conversation lives in `nav.chatConversationId` (App.vue shows the back
 * button and hides the tab bar while one is open).
 */
const open = computed(() => conversationById(nav.chatConversationId))
// The room is gone (left the lobby, draft over): back to the list.
watch(
  () => !!nav.chatConversationId && !open.value && !!liveChat.value,
  (gone) => {
    if (gone) nav.chatConversationId = null
  },
  { immediate: true },
)

const rooms = computed(() => conversations.value.filter((c) => c.kind !== 'direct'))
const direct = computed(() => conversations.value.filter((c) => c.kind === 'direct'))

/** A direct conversation's summoner icon, when the other player is a friend (Chat doesn't carry icons). */
function conversationIcon(c: ChatConversation) {
  return profileIconUrl(friendOf({ puuid: c.puuid, riotId: c.title })?.profileIconId ?? 0)
}

function preview(c: ChatConversation) {
  const m = c.lastMessage
  if (!m) return ''
  if (m.fromMe) return `You: ${m.body}`
  if (c.kind === 'direct') return m.body
  return c.kind === 'champ-select' || m.senderName ? `${senderLabel(m, c)}: ${m.body}` : m.body
}
function choose(c: ChatConversation) {
  haptic('selection')
  openConversation(c.id)
}

const quickReplies = computed(() =>
  open.value?.kind === 'champ-select' ? draftQuickReplies(liveState.value?.champSelect ?? null) : undefined,
)
</script>

<template>
  <ChatThread v-if="open" :key="open.id" :conversation="open" :quick-replies="quickReplies" />

  <div v-else class="lr-content">
    <LargeTitle title="Chat" />
    <ConnectionEmptyState />
    <template v-if="!connectionEmpty">
      <EmptyState v-if="!liveChat && store.syncing.chat" busy title="Loading Chat" />
      <EmptyState v-else-if="!conversations.length" :icon="MessageCircleIcon" title="No Conversations" />
    </template>

    <ListSection v-if="rooms.length">
      <ListRow
        v-for="c in rooms"
        :key="c.id"
        tappable
        :title="conversationTitle(c)"
        :icon="c.kind === 'party' ? UsersIcon : SwordsIcon"
        :tile-color="c.kind === 'party' ? 'blue' : 'orange'"
        @click="choose(c)"
      >
        <template v-if="preview(c)" #subtitle>{{ preview(c) }}</template>
        <template #trailing><Badge :count="unreadOf(c)" /></template>
      </ListRow>
    </ListSection>

    <ListSection v-if="direct.length" header="Messages">
      <ListRow v-for="c in direct" :key="c.id" tappable :title="conversationTitle(c)" @click="choose(c)">
        <template #lead><Avatar :name="c.title" :image-url="conversationIcon(c)" /></template>
        <template v-if="preview(c)" #subtitle>{{ preview(c) }}</template>
        <template #trailing>
          <span class="lr-row-meta">
            <span class="caption-1">{{ listTime(c.lastMessage) }}</span>
            <Badge :count="unreadOf(c)" />
          </span>
        </template>
      </ListRow>
    </ListSection>
  </div>
</template>
