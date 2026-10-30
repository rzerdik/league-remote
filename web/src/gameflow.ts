/** League's game-flow phases (ClientState.gameflowPhase) the app groups. */

/** The party screen: the lobby, the queue and the ready check over it. */
const LOBBY_PHASES: ReadonlySet<string> = new Set(['Lobby', 'Matchmaking', 'ReadyCheck'])

export function isLobbyPhase(phase: string | null | undefined): boolean {
  return !!phase && LOBBY_PHASES.has(phase)
}

/** The game itself: the loading screen, the running game, or reconnecting to it. */
const IN_GAME_PHASES: ReadonlySet<string> = new Set(['GameStart', 'InProgress', 'Reconnect'])

export function isInGame(phase: string | null | undefined): boolean {
  return !!phase && IN_GAME_PHASES.has(phase)
}

/** From the Nexus falling until you leave the client's post-game screen (the PC shows the honor vote first). */
const POST_GAME_PHASES: ReadonlySet<string> = new Set(['WaitingForStats', 'PreEndOfGame', 'EndOfGame'])

export function isPostGamePhase(phase: string | null | undefined): boolean {
  return !!phase && POST_GAME_PHASES.has(phase)
}

/** Phases a page reload would throw the user out of: the ready check, the draft, the game. */
const CRITICAL_PHASES: ReadonlySet<string> = new Set(['ReadyCheck', 'ChampSelect', ...IN_GAME_PHASES])

export function isCriticalPhase(phase: string | null | undefined): boolean {
  return !!phase && CRITICAL_PHASES.has(phase)
}
