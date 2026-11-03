<script setup lang="ts">
import { ArrowUpIcon } from '@lucide/vue'
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { sendChat } from '../../chat'
import { ChatMaxMessageLength } from '../../contracts'
import { haptic } from '../../haptics'

/**
 * Message field + send button, with optional quick replies above it (tap = send). `docked` pins it to the bottom of
 * the screen above the keyboard (Chat tab) and publishes its height as --lr-composer-h so the messages clear it;
 * otherwise it sits where it is placed (a sheet's footer, which rides the keyboard itself).
 */
const props = defineProps<{ conversationId: string; quickReplies?: readonly string[]; docked?: boolean }>()

const text = ref('')
const root = ref<HTMLElement | null>(null)

function send(value: string) {
  const body = value.trim()
  if (!body) return
  haptic('light')
  if (value === text.value) text.value = ''
  void sendChat(props.conversationId, body)
}

let observer: ResizeObserver | null = null
onMounted(() => {
  if (!props.docked || !root.value) return
  const el = root.value
  const publish = () => document.documentElement.style.setProperty('--lr-composer-h', `${Math.round(el.offsetHeight)}px`)
  observer = new ResizeObserver(publish)
  observer.observe(el)
  publish()
})
onBeforeUnmount(() => {
  observer?.disconnect()
  if (props.docked) document.documentElement.style.removeProperty('--lr-composer-h')
})
</script>

<template>
  <div ref="root" class="chat-composer" :class="{ 'chat-composer--docked': docked }">
    <div v-if="quickReplies?.length" class="chat-quick" role="group" aria-label="Quick replies">
      <button
        v-for="q in quickReplies"
        :key="q"
        type="button"
        class="chat-chip subheadline"
        @pointerdown.prevent
        @click="send(q)"
      >
        {{ q }}
      </button>
    </div>
    <form class="chat-field" @submit.prevent="send(text)">
      <input
        v-model="text"
        class="message"
        type="text"
        :maxlength="ChatMaxMessageLength"
        placeholder="Message"
        aria-label="Message"
        enterkeyhint="send"
        autocomplete="off"
      />
      <!-- pointerdown.prevent: keep the keyboard up after sending. -->
      <button type="submit" class="chat-send" :disabled="!text.trim()" aria-label="Send" @pointerdown.prevent>
        <ArrowUpIcon :stroke-width="2.5" aria-hidden="true" />
      </button>
    </form>
  </div>
</template>

<style scoped>
.chat-composer {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  padding: var(--space-2) var(--space-4) var(--space-3);
}
.chat-composer--docked {
  position: fixed;
  left: max(var(--safe-left), calc((100% - var(--lr-column)) / 2));
  right: max(var(--safe-right), calc((100% - var(--lr-column)) / 2));
  bottom: 0;
  z-index: var(--z-chrome);
  padding-bottom: max(var(--space-3), var(--safe-bottom));
  transform: translateY(calc(-1 * var(--lr-keyboard, 0px)));
  background: linear-gradient(to bottom, transparent, var(--bg) 40%);
}
.chat-quick {
  display: flex;
  gap: var(--space-2);
  overflow-x: auto;
  scrollbar-width: none;
  margin: 0 calc(-1 * var(--space-4));
  padding: 0 var(--space-4);
}
.chat-quick::-webkit-scrollbar {
  display: none;
}
.chat-chip {
  position: relative;
  flex: none;
  height: var(--control-sm);
  padding: 0 var(--space-4);
  border: 0;
  border-radius: var(--radius-full);
  background: var(--fill-tertiary);
  color: var(--label);
  cursor: pointer;
}
/* A 44-pt hit region around the chip. */
.chat-chip::after {
  content: '';
  position: absolute;
  inset: calc((var(--control-sm) - var(--hit-min)) / 2) 0;
}
.chat-chip:active {
  background: var(--fill-secondary);
}
.chat-field {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  min-height: var(--control-lg);
  /* The send button sits concentric in the capsule. */
  padding: 0 calc((var(--control-lg) - var(--control-md)) / 2) 0 var(--space-5);
  border-radius: var(--radius-full);
  background: var(--lr-glass-fill);
  -webkit-backdrop-filter: var(--lr-glass-blur);
  backdrop-filter: var(--lr-glass-blur);
  box-shadow: var(--glass-rim), var(--glass-lift);
}
.chat-field input {
  flex: 1;
  min-width: 0;
  height: var(--control-lg);
  border: 0;
  background: transparent;
  color: var(--label);
  /* iOS zooms the page into a focused field under 16px (a smaller Dynamic Type size). */
  font-size: max(16px, var(--font-size-message));
  outline: none;
}
.chat-field input::placeholder {
  color: var(--label-tertiary);
}
.chat-send {
  flex: none;
  display: grid;
  place-items: center;
  width: var(--control-md);
  height: var(--control-md);
  border: 0;
  border-radius: var(--radius-full);
  background: var(--accent);
  color: var(--on-accent);
  cursor: pointer;
  transition:
    opacity var(--dur-fade) var(--ease-fade),
    transform var(--dur-snappy) var(--spring-snappy);
}
.chat-send:disabled {
  opacity: 0.35;
  cursor: default;
}
.chat-send:not(:disabled):active {
  transform: scale(0.9);
  transition-duration: var(--dur-press);
}
.chat-send svg {
  width: var(--symbol);
  height: var(--symbol);
}
</style>
