import { computed, reactive, watch } from 'vue'
import type { ChampSelectState, ChatConversation, ChatMessage } from './contracts'
import { championName } from './ddragon'
import { positionLabel } from './labels'
import { openConversation } from './navigation'
import { sendCommand } from './hub'
import { play } from './sound'
import { liveChat, liveState, toast } from './store'

/**
 * League's chat on the phone: the party room, the draft room and recent direct conversations, as the connector
 * publishes them (ChatUpdated). Message bodies are shown here and nowhere else: never logged, never in a push.
 */

export const conversations = computed<ChatConversation[]>(() => liveChat.value?.conversations ?? [])
export const partyChat = computed(() => conversations.value.find((c) => c.kind === 'party') ?? null)
export const draftChat = computed(() => conversations.value.find((c) => c.kind === 'champ-select') ?? null)

export function conversationById(id: string | null | undefined): ChatConversation | null {
  return (id && conversations.value.find((c) => c.id === id)) || null
}

/** The direct conversation with this friend, if the connector lists one. */
function directConversationWith(puuid: string): ChatConversation | null {
  const p = puuid.toLowerCase()
  return conversations.value.find((c) => c.kind === 'direct' && c.puuid.toLowerCase() === p) ?? null
}

/** How long opening a new DM waits for the connector's chat to list it. */
const OPEN_DM_TIMEOUT_MS = 8000

/**
 * "Message" on a friend (Friends tab / profile sheet): shows the direct conversation with them in the Chat tab. The
 * connector opens it in the client first when there is none yet (friends only; nothing is sent to the friend, no window
 * opens on the PC) and keeps it listed. Resolves false when it could not be opened (a toast says why).
 */
export async function openDirectMessage(puuid: string): Promise<boolean> {
  const existing = directConversationWith(puuid)
  if (existing) {
    openConversation(existing.id)
    // Keeps it listed while it is on screen, even if newer DMs would push it past the connector's cap.
    void sendCommand({ type: 'OpenDirectConversation', puuid }, { silent: true })
    return true
  }
  const result = await sendCommand({ type: 'OpenDirectConversation', puuid })
  if (!result?.success) return false
  const opened = await whenListed(puuid)
  if (!opened) {
    toast('Could not open that chat.')
    return false
  }
  openConversation(opened.id)
  return true
}

/** The conversation once the connector's chat lists it (often before the command's result: it refreshes chat first). */
function whenListed(puuid: string): Promise<ChatConversation | null> {
  const listed = directConversationWith(puuid)
  if (listed) return Promise.resolve(listed)
  return new Promise((resolve) => {
    const stop = watch(
      () => directConversationWith(puuid),
      (c) => c && done(c),
    )
    const timer = setTimeout(() => done(null), OPEN_DM_TIMEOUT_MS)
    function done(c: ChatConversation | null) {
      clearTimeout(timer)
      stop()
      resolve(c)
    }
  })
}

export function conversationTitle(c: ChatConversation): string {
  if (c.kind === 'party') return 'Party'
  if (c.kind === 'champ-select') return 'Draft'
  return c.title || 'Chat'
}

export function unreadOf(c: ChatConversation | null | undefined): number {
  return c && !c.isMuted ? c.unread : 0
}

/** What players type for each assigned position. */
const ROLE_CALLS: Record<string, string> = { top: 'top', jungle: 'jg', middle: 'mid', bottom: 'adc', utility: 'supp' }

/** Draft quick replies (tap to send): your role call (when League assigned one), a swap ask, gl hf. */
export function draftQuickReplies(cs: ChampSelectState | null): string[] {
  const position = cs?.myTeam.find((m) => m.isLocalPlayer)?.assignedPosition?.toLowerCase() ?? ''
  const call = ROLE_CALLS[position]
  return call ? [call, 'can I swap?', 'gl hf'] : ['can I swap?', 'gl hf']
}

// --- Sending ------------------------------------------------------------------------------------------------

/** A message on its way (or that failed): shown as a bubble until the connector's chat has it. */
interface PendingMessage {
  key: number
  conversationId: string
  body: string
  failed: boolean
  /** Own messages the conversation already had when this was sent: its echo is a message not in here. */
  seen: string[]
}

export const pending = reactive<PendingMessage[]>([])
let pendingSeq = 0

export function pendingFor(conversationId: string): PendingMessage[] {
  return pending.filter((p) => p.conversationId === conversationId)
}

/** How long a sent message waits for the connector's chat to show it before the bubble is dropped anyway. */
const PENDING_TTL_MS = 15000

export async function sendChat(conversationId: string, text: string): Promise<boolean> {
  const body = text.trim()
  if (!body) return false
  const seen = conversationById(conversationId)?.messages.filter((m) => m.fromMe).map((m) => m.id) ?? []
  const p: PendingMessage = { key: ++pendingSeq, conversationId, body, failed: false, seen }
  pending.push(p)
  const result = await sendCommand({ type: 'SendChatMessage', conversationId, body })
  const entry = pending.find((x) => x.key === p.key)
  if (!result?.success) {
    if (entry) entry.failed = true
    return false
  }
  play('chatSent')
  setTimeout(() => dropPending(p.key), PENDING_TTL_MS)
  return true
}

export function dropPending(key: number) {
  const i = pending.findIndex((x) => x.key === key)
  if (i >= 0) pending.splice(i, 1)
}

/**
 * A pending message is done once the conversation has a new own message with the same text. Each echo settles one
 * pending message, so sending "gg" twice (or again after an earlier "gg") waits for its own echo.
 */
watch(conversations, (list) => {
  const claimed = new Set<string>()
  for (const p of [...pending]) {
    if (p.failed) continue
    const c = list.find((x) => x.id === p.conversationId)
    if (!c) continue
    const echo = c.messages.find((m) => m.fromMe && m.body === p.body && !p.seen.includes(m.id) && !claimed.has(m.id))
    if (!echo) continue
    claimed.add(echo.id)
    dropPending(p.key)
  }
})

// --- Reading ------------------------------------------------------------------------------------------------

const marking = new Set<string>()

/** Marks the conversation read in the client (only when it has unread messages; one request at a time). */
export async function markRead(c: ChatConversation | null | undefined) {
  if (!c || c.unread <= 0 || marking.has(c.id)) return
  marking.add(c.id)
  try {
    await sendCommand({ type: 'MarkConversationRead', conversationId: c.id }, { silent: true })
  } finally {
    marking.delete(c.id)
  }
}

// --- Display helpers ----------------------------------------------------------------------------------------

/** A draft-room sender as their champ-select teammate: label plus what their avatar shows. */
export interface DraftSender {
  /** Their name when League shows it, else "Ahri · Mid", "Mid", "Ahri", or "Teammate 3" (slot order). */
  label: string
  /** Picked (or else hovered/intended) champion for the portrait; 0 when none. */
  championId: number
  /** Monogram when there is no champion: name, position, or slot number. */
  monogram: string
}

/**
 * Who wrote a draft message, from the connector's senderCellId and the current team: in anonymous (ranked) drafts League
 * hides names, so teammates are told apart by champion and position instead. Follows the draft live (a new hover
 * relabels earlier lines too, so one player always reads the same). Null without a match (other rooms).
 */
export function draftSender(
  m: ChatMessage,
  cs: ChampSelectState | null = liveState.value?.champSelect ?? null,
): DraftSender | null {
  if (m.senderCellId == null || !cs) return null
  const team = [...cs.myTeam].sort((a, b) => a.cellId - b.cellId)
  const slot = team.findIndex((t) => t.cellId === m.senderCellId)
  const member = team[slot]
  if (!member) return null
  const championId = member.championId > 0 ? member.championId : Math.max(0, member.championPickIntent)
  const role = positionLabel(member.assignedPosition)
  const named = [championName(championId), role].filter(Boolean).join(' · ')
  return {
    label: m.senderName || named || `Teammate ${slot + 1}`,
    championId,
    monogram: m.senderName || role || String(slot + 1),
  }
}

/** The sender as the bubble shows it (League hides names in anonymous drafts). */
export function senderLabel(m: ChatMessage, c: ChatConversation): string {
  if (m.fromMe) return 'You'
  if (c.kind === 'champ-select') return draftSender(m)?.label || m.senderName || 'Teammate'
  if (m.senderName) return m.senderName
  return c.kind === 'direct' ? conversationTitle(c) : 'Player'
}
