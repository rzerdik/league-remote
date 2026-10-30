import type { ClientState, LobbyState } from '../../contracts'
import { lobbyMembers } from './people'

/** The client state every scenario builds on: League running, three rune pages, nothing going on. */
export function base(phase: string): ClientState {
  return {
    lcuConnected: true,
    gameflowPhase: phase,
    readyCheck: null,
    lobby: null,
    champSelect: null,
    runePages: [
      { id: 1, name: 'Electrocute Mid', isCurrent: true, isEditable: true },
      { id: 2, name: 'Arcane Comet', isCurrent: false, isEditable: true },
      { id: 3, name: 'First Strike', isCurrent: false, isEditable: true },
    ],
    updatedAt: new Date().toISOString(),
    inGame: null,
    // As a live client reported it (Quickplay and the old Arena queue disabled).
    availableQueueIds: [400, 420, 440, 450, 480, 1750, 2400, 3140],
    invitations: null,
    postGame: null,
  }
}

/** A four-player party, so a queue that holds five: Ranked Flex (Solo/Duo takes two at most, LobbyScreen's "4/2"). */
export function lobby(searching: boolean): LobbyState {
  return {
    queueId: 440,
    isLeader: true,
    canStartActivity: true,
    isSearching: searching,
    timeInQueue: searching ? 83 : null,
    estimatedQueueTime: searching ? 120 : null,
    members: lobbyMembers,
    gameMode: 'CLASSIC',
    isCustom: false,
    showPositionSelector: true,
    canInvite: false,
    invitations: null,
  }
}
