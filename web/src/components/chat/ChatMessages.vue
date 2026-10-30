<script setup lang="ts">
import { RotateCwIcon } from '@lucide/vue'
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { draftSender, dropPending, markRead, pendingFor, sendChat, senderLabel, type DraftSender } from '../../chat'
import type { ChatConversation, ChatMessage } from '../../contracts'
import { messageTime } from '../../format'
import { DURATION } from '../../styles/tokens'
import Avatar from '../lr/Avatar.vue'
import ChampionIcon from '../lr/ChampionIcon.vue'

/**
 * A conversation's messages as bubbles (yours on the right, tinted), grouped by sender, with a time line after a
 * pause. In the draft room, teammates' runs carry their champion portrait (or a position / slot monogram) so hidden
 * senders of an anonymous draft can be told apart. Sent-but-not-yet-echoed messages show dimmed; a failed one can be
 * tapped to retry. Keeps itself scrolled to the newest message (while you are at the bottom) and marks the conversation read while it is on screen.
 */
const props = defineProps<{ conversation: ChatConversation }>()

const root = ref<HTMLElement | null>(null)

interface Row {
  key: string
  kind: 'time' | 'event' | 'bubble'
  text: string
  mine?: boolean
  sender?: string
  /** Draft room: who wrote it, for the avatar next to the run's last bubble. */
  draft?: DraftSender | null
  /** First / last bubble of a run from one sender (bubble corners, sender label). */
  first?: boolean
  last?: boolean
  pendingKey?: number
  failed?: boolean
}

const GAP_MS = 15 * 60 * 1000
const showSenders = computed(() => props.conversation.kind !== 'direct')
const showAvatars = computed(() => props.conversation.kind === 'champ-select')

const rows = computed<Row[]>(() => {
  const c = props.conversation
  const out: Row[] = []
  let prevAt = 0
  let prevSender: string | null = null
  const push = (m: ChatMessage) => {
    const at = m.timestamp ? Date.parse(m.timestamp) : 0
    if (at && (!prevAt || at - prevAt > GAP_MS)) {
      out.push({ key: `t:${m.id}`, kind: 'time', text: messageTime(m) })
      prevSender = null
    }
    if (at) prevAt = at
    if (m.kind === 'joined' || m.kind === 'left') {
      out.push({ key: `e:${m.id}`, kind: 'event', text: `${senderLabel(m, c)} ${m.kind}` })
      prevSender = null
      return
    }
    // Hidden draft senders all have an empty name: their cell keeps their runs apart.
    const sender = m.fromMe
      ? '\u0000me'
      : m.senderCellId != null
        ? `\u0000cell:${m.senderCellId}`
        : m.senderName || '\u0000them'
    out.push({
      key: `m:${m.id}`,
      kind: 'bubble',
      text: m.body,
      mine: m.fromMe,
      sender: m.fromMe ? undefined : senderLabel(m, c),
      draft: m.fromMe || !showAvatars.value ? undefined : draftSender(m),
      first: sender !== prevSender,
    })
    prevSender = sender
  }
  c.messages.forEach(push)
  for (const p of pendingFor(c.id)) {
    out.push({
      key: `p:${p.key}`,
      kind: 'bubble',
      text: p.body,
      mine: true,
      first: prevSender !== '\u0000me',
      pendingKey: p.key,
      failed: p.failed,
    })
    prevSender = '\u0000me'
  }
  // `last` = the next row isn't a bubble from the same sender.
  for (let i = 0; i < out.length; i++) {
    const r = out[i]!
    if (r.kind !== 'bubble') continue
    const next = out[i + 1]
    r.last = !next || next.kind !== 'bubble' || next.first === true
  }
  return out
})

function retry(r: Row) {
  if (r.pendingKey == null) return
  dropPending(r.pendingKey)
  void sendChat(props.conversation.id, r.text)
}

// --- Scrolling ------------------------------------------------------------------------------------------------

function scroller(): HTMLElement | null {
  return (root.value?.closest('.lr-sheet-body') as HTMLElement | null) ?? document.getElementById('app')
}
function atBottom(el: HTMLElement) {
  return el.scrollHeight - el.scrollTop - el.clientHeight < 120
}
function toBottom(smooth: boolean) {
  const el = scroller()
  if (el) el.scrollTo({ top: el.scrollHeight, behavior: smooth ? 'smooth' : 'auto' })
}

watch(
  () => rows.value.length,
  async (n, before) => {
    const el = scroller()
    const follow = !el || atBottom(el) || rows.value[n - 1]?.mine === true
    await nextTick()
    if (follow) toBottom(before !== undefined)
  },
)

// The keyboard opening shrinks the visible area: keep the newest message in view.
const vv = window.visualViewport
function onViewport() {
  const el = scroller()
  if (el && atBottom(el)) requestAnimationFrame(() => toBottom(false))
}
onMounted(async () => {
  await nextTick()
  toBottom(false)
  // Sheets slide in; settle once more when the sheet is at its height.
  setTimeout(() => toBottom(false), DURATION.smooth)
  vv?.addEventListener('resize', onViewport)
})
onBeforeUnmount(() => vv?.removeEventListener('resize', onViewport))

// --- Read state ---------------------------------------------------------------------------------------------

function markIfVisible() {
  if (document.visibilityState === 'visible') void markRead(props.conversation)
}
watch(() => [props.conversation.id, props.conversation.unread], markIfVisible, { immediate: true })
onMounted(() => document.addEventListener('visibilitychange', markIfVisible))
onBeforeUnmount(() => document.removeEventListener('visibilitychange', markIfVisible))
</script>

<template>
  <div ref="root" class="chat-log" role="log" aria-live="polite" aria-relevant="additions">
    <template v-for="r in rows" :key="r.key">
      <p v-if="r.kind === 'time'" class="chat-time caption-1">{{ r.text }}</p>
      <p v-else-if="r.kind === 'event'" class="chat-event caption-1">{{ r.text }}</p>
      <div
        v-else
        class="chat-row"
        :class="{
          'is-mine': r.mine,
          'is-first': r.first,
          'is-last': r.last,
          'is-pending': r.pendingKey != null && !r.failed,
          'has-avatar': showAvatars && !r.mine,
        }"
      >
        <span v-if="showAvatars && !r.mine && r.last" class="chat-avatar">
          <ChampionIcon v-if="r.draft?.championId" :champion-id="r.draft.championId" size="ban" decorative />
          <Avatar v-else :name="r.draft?.monogram ?? ''" />
        </span>
        <span v-if="showSenders && !r.mine && r.first" class="chat-sender caption-1">{{ r.sender }}</span>
        <button v-if="r.failed" type="button" class="chat-bubble chat-bubble--failed message" @click="retry(r)">
          {{ r.text }}
          <span class="chat-failed caption-1"><RotateCwIcon :stroke-width="2.25" aria-hidden="true" />Not sent</span>
        </button>
        <p v-else class="chat-bubble message">
          <span v-if="!showSenders && !r.mine" class="lr-sr">{{ r.sender }}: </span>{{ r.text }}
        </p>
      </div>
    </template>
  </div>
</template>

<style scoped>
.chat-log {
  display: flex;
  flex-direction: column;
  gap: var(--space-0-5);
  padding: var(--space-2) 0;
}
.chat-time,
.chat-event {
  align-self: center;
  margin: var(--space-3) 0 var(--space-1);
  color: var(--label-secondary);
  text-align: center;
}
.chat-time {
  font-weight: 600;
}
.chat-row {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  max-width: 82%;
}
.chat-row.is-mine {
  align-self: flex-end;
  align-items: flex-end;
}
/* Draft room: a gutter for the sender's avatar, which sits beside the last bubble of their run (like Messages). */
.chat-row.has-avatar {
  position: relative;
  padding-left: calc(var(--champ-ban) + var(--space-2));
}
.chat-avatar {
  position: absolute;
  left: 0;
  bottom: 0;
}
.chat-avatar .lr-avatar {
  --s: var(--champ-ban);
  font-size: var(--font-size-caption-2);
}
.chat-row.is-first:not(:first-child) {
  margin-top: var(--space-2);
}
.chat-sender {
  margin: 0 var(--space-3) var(--space-0-5);
  color: var(--label-secondary);
}
.chat-bubble {
  margin: 0;
  padding: var(--space-2) var(--space-4);
  border-radius: var(--radius-bubble);
  background: var(--fill-secondary);
  color: var(--label);
  overflow-wrap: anywhere;
  white-space: pre-wrap;
  text-align: start;
}
/* Runs: the corners between bubbles of one sender tighten, like Messages. */
.chat-row:not(.is-mine):not(.is-last) .chat-bubble {
  border-bottom-left-radius: var(--radius-sm);
}
.chat-row:not(.is-mine):not(.is-first) .chat-bubble {
  border-top-left-radius: var(--radius-sm);
}
.chat-row.is-mine .chat-bubble {
  background: var(--accent);
  color: var(--on-accent);
}
.chat-row.is-mine:not(.is-last) .chat-bubble {
  border-bottom-right-radius: var(--radius-sm);
}
.chat-row.is-mine:not(.is-first) .chat-bubble {
  border-top-right-radius: var(--radius-sm);
}
.chat-row.is-pending .chat-bubble {
  opacity: 0.55;
}
.chat-bubble--failed {
  border: 0;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: var(--space-0-5);
}
.chat-row.is-mine .chat-bubble--failed {
  background: var(--fill-secondary);
  color: var(--label);
}
.chat-failed {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  color: var(--red-text);
  font-weight: 600;
}
.chat-failed svg {
  width: 1em;
  height: 1em;
}
</style>
