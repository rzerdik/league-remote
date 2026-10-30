// Mirror of src/LeagueRemote.Contracts/Chat.cs.

/** ChatState.MaxMessageLength (the client's own chat input allows 200). */
export const ChatMaxMessageLength = 200
export const ChatMaxConversationIdLength = 256

/** ChatKinds */
export type ChatKind = 'party' | 'champ-select' | 'direct'

/** Published as its own hub message (ChatUpdated), not inside ClientState. */
export interface ChatState {
  /** Party first, then draft, then direct conversations (most recent first). */
  conversations: ChatConversation[]
  updatedAt: string
}

export interface ChatConversation {
  /** LCU conversation id; what the chat commands take. */
  id: string
  kind: ChatKind | string
  /** The friend's name for direct conversations; '' for party and draft. */
  title: string
  unread: number
  /** Muted in the client: no badge, no sound. */
  isMuted: boolean
  lastMessage: ChatMessage | null
  /** Recent messages, oldest first. */
  messages: ChatMessage[]
  /** The friend's puuid for direct conversations; '' for party and draft. */
  puuid: string
}

export interface ChatMessage {
  id: string
  /** 'chat', or 'joined' / 'left' in party rooms. */
  kind: 'chat' | 'joined' | 'left' | string
  body: string
  fromMe: boolean
  /** '' when League hides the name (anonymous draft). */
  senderName: string
  timestamp: string | null
  /**
   * Draft room only: the sender's cell in ChampSelectState.myTeam (matched by the connector, also for hidden teammates in
   * anonymous drafts). Null when unknown (other rooms, or no match).
   */
  senderCellId: number | null
}
