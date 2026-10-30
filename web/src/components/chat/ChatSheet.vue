<script setup lang="ts">
import { computed, watch } from 'vue'
import { conversationTitle } from '../../chat'
import type { ChatConversation } from '../../contracts'
import Sheet from '../lr/Sheet.vue'
import ChatComposer from './ChatComposer.vue'
import ChatMessages from './ChatMessages.vue'

/**
 * A conversation in a modal sheet over the current screen: the draft chat in Champion Select (the tab bar is hidden
 * there) and the party chat in the Lobby. The composer is the sheet's footer, so it rides the keyboard.
 */
const props = defineProps<{ conversation: ChatConversation | null; quickReplies?: readonly string[] }>()
const open = defineModel<boolean>('open', { required: true })

// The room is gone (draft over, left the party): close.
watch(
  () => props.conversation,
  (c) => {
    if (!c) open.value = false
  },
)

const title = computed(() => (props.conversation ? conversationTitle(props.conversation) : 'Chat'))
</script>

<template>
  <Sheet v-model:open="open" modal :detents="['medium', 'large']" detent="large" :title="title" body-class="chat-sheet-body">
    <ChatMessages v-if="conversation" :conversation="conversation" />
    <template v-if="conversation" #footer>
      <ChatComposer :conversation-id="conversation.id" :quick-replies="quickReplies" />
    </template>
  </Sheet>
</template>

<style>
.chat-sheet-body {
  display: flex;
  flex-direction: column;
  padding: 0 var(--space-4);
}
/* Few messages sit at the bottom, next to the composer (margin, not justify-content, so it still scrolls). */
.chat-sheet-body > .chat-log {
  margin-top: auto;
}
</style>
