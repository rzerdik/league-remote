import { computed } from 'vue'
import { conversationById, conversationTitle } from './chat'
import { isInGame, isLobbyPhase } from './gameflow'
import { lobbyTitle, queueLabel } from './labels'
import { closeConversation, closeMatch, closePlayer, closePlayerMatch, closeSettingsView, nav, SETTINGS_VIEW_TITLE } from './navigation'
import { postGamePhase } from './postGame'
import { matchById, playerMatchById, playerProfile, resultLabel } from './profile'
import { playerShortName } from './riotId'
import { liveGameState, liveState as live, store } from './store'

/** What the app shows: League's state picks the Home screen, `nav` the tab and any pushed view over it. */

export type GameView = 'pair' | 'play' | 'lobby' | 'champselect' | 'game' | 'postgame'

/** The game state picks the screen; there is no navigation of our own. */
export const view = computed<GameView>(() => {
  if (!store.grant) return 'pair'
  const s = live.value
  if (!s || !s.lcuConnected) return 'play'
  if (s.gameflowPhase === 'ChampSelect' && s.champSelect) return 'champselect'
  if (isLobbyPhase(s.gameflowPhase) && s.lobby) return 'lobby'
  // Reconnect too: the game is still running (the connector keeps sending the clock).
  if (isInGame(s.gameflowPhase)) return 'game'
  // From the Nexus falling until you leave the client's post-game screen: the scoreboard, Play Again and Leave.
  if (postGamePhase.value) return 'postgame'
  return 'play'
})

/** A ready check still waiting for your answer: the cover is up. */
export const readyCheck = computed(() => {
  const rc = live.value?.readyCheck
  return rc && rc.state === 'InProgress' && rc.playerResponse === 'None' ? rc : null
})

/** The Home tab is the game screen; the other tabs are their own screens. Unpaired, only the pair screen exists. */
export const screen = computed(() => (view.value === 'pair' || nav.tab === 'home' ? view.value : nav.tab))

/** The conversation open in the Chat tab (a pushed view: back button, no tab bar). */
export const openChat = computed(() => (screen.value === 'chat' ? conversationById(nav.chatConversationId) : null))
/** The game open in the Profile tab (a pushed view, like a conversation). */
export const openGame = computed(() => (screen.value === 'profile' ? matchById(nav.profileGameId) : null))
/** A Settings sub-view (Auto Pick & Ban), pushed like a game. */
export const openSettings = computed(() => (screen.value === 'settings' ? nav.settingsView : null))
/** Another player's profile (View Profile), pushed over whatever screen it was opened from; one of their games inside. */
export const openPlayer = computed(() => (screen.value !== 'pair' ? nav.player : null))
const openPlayerGame = computed(() =>
  openPlayer.value ? playerMatchById(openPlayer.value.puuid, openPlayer.value.gameId) : null,
)

/** A pushed view is open: Back in the nav bar, no tab bar. */
export const pushed = computed(() => !!openPlayer.value || !!openChat.value || !!openGame.value || !!openSettings.value)

export function goBack() {
  if (openPlayer.value) {
    if (openPlayerGame.value) closePlayerMatch()
    else closePlayer()
  } else if (openGame.value) closeMatch()
  else if (openSettings.value) closeSettingsView()
  else closeConversation()
}

/** The draft board has no large title: the nav bar always carries the title, with the game mode under it. ARAM has one. */
const draftBoardUp = computed(() => screen.value === 'champselect' && !pushed.value && !live.value?.champSelect?.benchEnabled)
export const navInline = computed(() => !!openChat.value || draftBoardUp.value)
export const navSubtitle = computed(() => (draftBoardUp.value ? queueLabel(live.value?.lobby?.queueId ?? null) : ''))

export const navTitle = computed(() => {
  if (openPlayer.value) {
    if (openPlayerGame.value) return resultLabel(openPlayerGame.value.result)
    return playerProfile(openPlayer.value.puuid)?.summoner.gameName || playerShortName(openPlayer.value.name) || 'Player'
  }
  switch (screen.value) {
    case 'chat':
      return openChat.value ? conversationTitle(openChat.value) : 'Chat'
    case 'profile':
      return openGame.value ? resultLabel(openGame.value.result) : 'Profile'
    case 'friends':
      return 'Friends'
    case 'settings':
      return openSettings.value ? SETTINGS_VIEW_TITLE[openSettings.value] : 'Settings'
    case 'lobby':
      return live.value?.lobby ? lobbyTitle(live.value.lobby) : 'Lobby'
    case 'champselect':
      return 'Champion Select'
    case 'game':
      // From the store, not liveGame/: that module belongs to the lazily loaded In Game screen.
      return liveGameState.value?.available && liveGameState.value.result ? 'Game Over' : 'In Game'
    case 'postgame':
      return 'Game Over'
    default:
      return 'Play'
  }
})
