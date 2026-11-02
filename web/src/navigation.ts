import { reactive } from 'vue'
import type { AutoPickQueueGroup, AutoPickRole } from './contracts'

/**
 * The bottom tab bar's tabs. Home is the game: Play / Lobby / Champion Select / In Game, picked by League's state.
 * Profile is your ranks, mastery and match history; Chat and Friends are the social side; Settings.
 */
export type Tab = 'home' | 'profile' | 'chat' | 'friends' | 'settings'

/** The In Game screen's segmented control. */
export type InGameView = 'overview' | 'scoreboard' | 'events'

/** A Settings sub-view pushed over the Settings tab (back button, no tab bar). */
export type SettingsView = 'autopick' | 'notifications' | 'sounds' | 'about'

/** The nav bar's inline title for each Settings sub-view (the same words as the view's large title). */
export const SETTINGS_VIEW_TITLE: Record<SettingsView, string> = {
  autopick: 'Auto Pick & Ban',
  notifications: 'Notifications',
  sounds: 'Sounds & Haptics',
  about: 'About',
}

export const nav = reactive({
  tab: 'home' as Tab,
  /** The conversation open in the Chat tab (its id), or null for the conversation list. */
  chatConversationId: null as string | null,
  /** The game open in the Profile tab (MatchSummary.gameId), or null for the profile itself. */
  profileGameId: null as number | null,
  /** What the In Game screen shows (kept while you look at another tab). */
  inGameView: 'overview' as InGameView,
  /** The Settings sub-view open, or null for Settings itself. */
  settingsView: null as SettingsView | null,
  /** The auto pick preset open in its editor sheet (Settings › Auto Pick & Ban), or null. */
  autoPickEdit: null as { group: AutoPickQueueGroup; role: AutoPickRole } | null,
  /**
   * Another player's profile pushed over the current screen (View Profile from a friend, a party member or a
   * champ-select teammate), and one of their games inside it; null when none is open. `name` titles the view until the
   * profile arrives. App.vue closes it when the game screen changes (lobby → champion select…) or your pick turn starts.
   */
  player: null as PlayerView | null,
  /**
   * The player views under `player`, oldest first: a teammate tapped in a player's game opens over that player, and Back
   * returns to them (and their game). At most MAX_PLAYER_TRAIL, so every view on it is still in the PC's cache.
   */
  playerTrail: [] as PlayerView[],
})

/** A pushed player profile: whose, the name to title it with until it arrives, and the game of theirs open in it. */
interface PlayerView {
  puuid: string
  name: string
  gameId: number | null
}

/** Player views kept under the open one (the connector keeps three profiles: these two and the open one). */
const MAX_PLAYER_TRAIL = 2

export function selectTab(tab: Tab) {
  // Tapping a tab while already on it goes back to its root, like iOS pops to the root.
  if (tab === 'chat' && nav.tab === 'chat') nav.chatConversationId = null
  if (tab === 'profile' && nav.tab === 'profile') nav.profileGameId = null
  if (tab === 'settings' && nav.tab === 'settings') closeSettingsView()
  nav.tab = tab
}

export function openConversation(id: string) {
  nav.chatConversationId = id
  nav.tab = 'chat'
}

export function closeConversation() {
  nav.chatConversationId = null
}

export function openMatch(gameId: number) {
  nav.profileGameId = gameId
  nav.tab = 'profile'
}

export function closeMatch() {
  nav.profileGameId = null
}

/** Pushes a player's profile; over another player's (a teammate in their game), that one goes on the trail. */
export function openPlayer(puuid: string, name = '') {
  const current = nav.player
  if (current && current.puuid.toLowerCase() !== puuid.toLowerCase())
    nav.playerTrail = [...nav.playerTrail, current].slice(-MAX_PLAYER_TRAIL)
  nav.player = { puuid, name, gameId: null }
}

/** Back from a player's profile: to the player it was opened from (with their game), or off the player views. */
export function closePlayer() {
  nav.player = nav.playerTrail.at(-1) ?? null
  nav.playerTrail = nav.playerTrail.slice(0, -1)
}

/** Closes every player view at once (the game screen moved on, your turn in champion select). */
export function closePlayers() {
  nav.player = null
  nav.playerTrail = []
}

export function openPlayerMatch(gameId: number) {
  if (nav.player) nav.player = { ...nav.player, gameId }
}

export function closePlayerMatch() {
  if (nav.player) nav.player = { ...nav.player, gameId: null }
}

export function openSettingsView(view: SettingsView) {
  nav.settingsView = view
  nav.tab = 'settings'
}

export function closeSettingsView() {
  nav.settingsView = null
  nav.autoPickEdit = null
}
