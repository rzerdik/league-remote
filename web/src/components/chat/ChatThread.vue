<script setup lang="ts">
import { MessageCircleIcon } from '@lucide/vue'
import { conversationTitle, pendingFor } from '../../chat'
import type { ChatConversation } from '../../contracts'
import EmptyState from '../lr/EmptyState.vue'
import ChatComposer from './ChatComposer.vue'
import ChatMessages from './ChatMessages.vue'

/**
 * A conversation as a pushed view (Chat tab): messages from under the nav bar down to the composer docked above the
 * keyboard; few messages sit at the bottom, next to the composer. Both follow the finger on an edge swipe back.
 */
defineProps<{ conversation: ChatConversation; quickReplies?: readonly string[] }>()
</script>

<template>
  <div class="lr-content chat-thread">
    <!-- A DM just opened from a friend (nothing written yet). -->
    <EmptyState
      v-if="!conversation.messages.length && !pendingFor(conversation.id).length"
      class="lr-swipe-follow"
      :icon="MessageCircleIcon"
      title="No Messages"
      :message="`Say hi to ${conversationTitle(conversation)}.`"
    />
    <ChatMessages class="lr-swipe-follow" :conversation="conversation" />
    <ChatComposer class="lr-swipe-follow" :conversation-id="conversation.id" :quick-replies="quickReplies" docked />
  </div>
</template>

<style scoped>
.chat-thread {
  padding-top: calc(var(--lr-nav-top) + var(--lr-nav-h) + var(--space-3));
  padding-left: var(--lr-inset-left);
  padding-right: var(--lr-inset-right);
  /* --lr-composer-h: published by the docked composer; --lr-keyboard: by useKeyboardInset. */
  padding-bottom: calc(var(--lr-composer-h, var(--control-lg)) + var(--lr-keyboard, 0px) + var(--space-2));
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
}
.chat-thread > .chat-log {
  margin-top: auto;
}
</style>
