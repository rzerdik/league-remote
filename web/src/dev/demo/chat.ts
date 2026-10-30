import type { ChatConversation, ChatMessage, ChatState } from '../../contracts'
import { store } from '../../store'
import { held, minutesAgo, scenario, wait } from './core'
import { KESTREL, ME, NIGHTOWL, TARN, VEXA } from './people'

let msgSeq = 0
function msg(body: string, from: string | null, at: string, kind = 'chat'): ChatMessage {
  return { id: `demo-${++msgSeq}`, kind, body, fromMe: from === null, senderName: from ?? ME.gameName, timestamp: at, senderCellId: null }
}
function room(id: string, kind: string, messages: ChatMessage[], unread: number, title = ''): ChatConversation {
  const lastMessage = [...messages].reverse().find((m) => m.kind === 'chat') ?? null
  const puuid = kind === 'direct' ? id.split('@')[0]! : ''
  return { id, kind, title, unread, isMuted: false, lastMessage, messages, puuid }
}

const PARTY_ID = 'demo-party@example.invalid'
const DRAFT_ID = 'demo-draft@example.invalid'

function partyRoom(unread: number) {
  return room(
    PARTY_ID,
    'party',
    [
      msg('', NIGHTOWL.gameName, minutesAgo(9), 'joined'),
      msg('yo', NIGHTOWL.gameName, minutesAgo(9)),
      msg('one more?', null, minutesAgo(8)),
      msg('', KESTREL.gameName, minutesAgo(3), 'joined'),
      msg('ready when you are', KESTREL.gameName, minutesAgo(1)),
      msg('I can play supp if needed', KESTREL.gameName, minutesAgo(1)),
    ],
    unread,
  )
}

/** A line from a hidden teammate of an anonymous (ranked) draft: no name, only their champ-select cell. */
function anon(body: string, cellId: number, at: string): ChatMessage {
  return { ...msg(body, '', at), senderCellId: cellId }
}

/** Anonymous ranked draft: senders are told apart by champion and position ("Lee Sin · Jungle"), runs split per cell. */
function draftRoom(unread: number) {
  return room(
    DRAFT_ID,
    'champ-select',
    [
      anon('jg', 1, minutesAgo(1.5)),
      anon('top pls', 0, minutesAgo(1.2)),
      anon('first time on this champ', 0, minutesAgo(1.2)),
      { ...msg('mid', null, minutesAgo(1)), senderCellId: 2 },
      anon('can I swap?', 3, minutesAgo(0.6)),
      anon('sure', 4, minutesAgo(0.5)),
      anon('ty', 3, minutesAgo(0.4)),
    ],
    unread,
  )
}

function directRooms(): ChatConversation[] {
  return [
    room(`${VEXA.puuid}@eu2.pvp.net`, 'direct', [msg('hi', null, minutesAgo(70)), msg('want to play later?', VEXA.gameName, minutesAgo(64))], 1, VEXA.gameName),
    room(`${TARN.puuid}@eu2.pvp.net`, 'direct', [msg('gg wp', TARN.gameName, minutesAgo(60 * 26)), msg('gg', null, minutesAgo(60 * 26))], 0, TARN.gameName),
    room('friend-3@eu1.pvp.net', 'direct', [msg('new patch is wild', 'Mirelle', minutesAgo(60 * 24 * 4))], 0, 'Mirelle'),
  ]
}

/** The scenario's conversations: the party room in a lobby, the draft room in champion select, a few DMs. */
export function chatState(): ChatState | null {
  const rooms: ChatConversation[] = []
  switch (scenario) {
    case 'chat':
    case 'lobby':
    case 'queue':
      rooms.push(partyRoom(2), ...directRooms())
      break
    case 'friends':
      rooms.push(...directRooms())
      break
    case 'planning':
    case 'pick':
    case 'ban':
    case 'enemypick':
    case 'final':
    case 'pick-final':
    case 'ban-final':
    case 'skins':
    case 'aram':
      rooms.push(draftRoom(2), ...directRooms())
      break
    default:
      return null
  }
  return { conversations: rooms, updatedAt: new Date().toISOString() }
}

function pushChat(update: (c: ChatConversation) => ChatConversation | void, id: string) {
  if (!held.chat) return
  held.chat = {
    updatedAt: new Date().toISOString(),
    conversations: held.chat.conversations.map((c) => (c.id === id ? (update({ ...c, messages: [...c.messages] }) ?? c) : c)),
  }
  store.chat = held.chat
}

const REPLIES: Record<string, [string, string]> = {
  'champ-select': ['', 'np'],
  party: [KESTREL.gameName, 'ok queue up'],
  direct: ['', 'sure'],
}

/** Demo friends without a conversation yet: OpenDirectConversation opens an empty one (like the connector). */
const DEMO_FRIENDS: Record<string, string> = { 'friend-4': 'Quill', 'friend-5': 'Brine' }
export async function demoOpenDirect(puuid: string) {
  if (!held.chat || held.chat.conversations.some((c) => c.puuid === puuid)) return true
  const title = held.friends?.friends.find((f) => f.puuid === puuid)?.gameName ?? DEMO_FRIENDS[puuid]
  if (!title) return false
  await wait(400)
  held.chat = { updatedAt: new Date().toISOString(), conversations: [...held.chat.conversations, room(`${puuid}@eu2.pvp.net`, 'direct', [], 0, title)] }
  store.chat = held.chat
  return true
}

/** SendChatMessage lands a moment later (like the connector's echo); someone answers the first message. */
const answered = new Set<string>()
export async function demoChat(cmd: { conversationId?: string | null; body?: string | null; type: string }) {
  const id = cmd.conversationId ?? ''
  if (cmd.type === 'MarkConversationRead') {
    pushChat((c) => ({ ...c, unread: 0 }), id)
    return
  }
  await wait(350)
  pushChat((c) => {
    c.messages.push(msg(cmd.body ?? '', null, new Date().toISOString()))
    c.lastMessage = c.messages[c.messages.length - 1]!
    return c
  }, id)
  if (answered.has(id)) return
  answered.add(id)
  await wait(1800)
  pushChat((c) => {
    const [who, text] = REPLIES[c.kind] ?? ['', 'ok']
    const at = new Date().toISOString()
    // The draft is anonymous: the answer comes from a hidden teammate (Support).
    c.messages.push(c.kind === 'champ-select' ? anon(text, 4, at) : msg(text, who || c.title, at))
    c.lastMessage = c.messages[c.messages.length - 1]!
    c.unread += 1
    return c
  }, id)
}
