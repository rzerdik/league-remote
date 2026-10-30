import { computed } from 'vue'
import { conversations, unreadOf } from './chat'
import { incomingRequests } from './friendRequests'
import type { Tab } from './navigation'

/**
 * Count badges per tab (0 hides it): unread messages in conversations that aren't muted (as the client counts them) on
 * Chat, requests waiting for an answer on Friends.
 */
export const badges = computed<Record<Tab, number>>(() => ({
  home: 0,
  profile: 0,
  chat: conversations.value.reduce((n, c) => n + unreadOf(c), 0),
  friends: incomingRequests.value.length,
  settings: 0,
}))
