<script setup lang="ts">
import { MessageCircleIcon } from '@lucide/vue'
import { computed } from 'vue'
import { conversationTitle, unreadOf } from '../../chat'
import type { ChatConversation } from '../../contracts'
import { tapFeedback } from '../../haptics'
import Badge from '../lr/Badge.vue'
import LrButton from '../lr/LrButton.vue'

/**
 * Glass chat button with the room's unread count. Goes in the screen's `LargeTitle #trailing` slot: on the title row,
 * docked into the nav bar once the title collapses.
 */
const props = defineProps<{ conversation: ChatConversation }>()
const emit = defineEmits<{ open: [] }>()

const unread = computed(() => unreadOf(props.conversation))
const label = computed(
  () => `${conversationTitle(props.conversation)} chat${unread.value ? `, ${unread.value} unread` : ''}`,
)

function open() {
  tapFeedback()
  emit('open')
}
</script>

<template>
  <span class="chat-btn">
    <LrButton variant="glass" :icon="MessageCircleIcon" :aria-label="label" aria-haspopup="dialog" @click="open" />
    <Badge :count="unread" on aria-hidden="true" />
  </span>
</template>

<style scoped>
.chat-btn {
  position: relative;
  display: inline-flex;
}
</style>
