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
    // As the client lists them (League's recommended positions, patch 16.21).
    championLanes: {
      top: [2, 6, 8, 9, 10, 11, 13, 14, 17, 19, 23, 24, 27, 31, 34, 36, 39, 41, 42, 48, 54, 57, 58, 62, 67, 68, 69, 72, 74, 75, 77, 78, 79, 80, 82, 83, 84, 85, 86, 92, 98, 106, 110, 114, 122, 126, 133, 150, 154, 157, 164, 223, 240, 266, 420, 429, 516, 777, 799, 875, 887, 893, 897, 904],
      jungle: [2, 5, 9, 11, 17, 19, 20, 23, 24, 28, 30, 31, 32, 33, 35, 36, 48, 54, 56, 57, 59, 60, 62, 64, 72, 75, 76, 77, 78, 79, 80, 91, 98, 102, 104, 105, 106, 107, 113, 120, 121, 126, 131, 133, 141, 143, 154, 163, 200, 203, 233, 234, 238, 245, 246, 254, 266, 421, 427, 517, 799, 805, 876, 887, 904, 950],
      middle: [1, 3, 4, 7, 8, 10, 13, 14, 26, 34, 38, 39, 41, 42, 45, 50, 54, 55, 61, 63, 69, 74, 79, 80, 84, 85, 90, 91, 92, 96, 99, 101, 103, 105, 112, 115, 126, 127, 131, 133, 134, 136, 142, 157, 161, 163, 166, 238, 245, 246, 268, 517, 518, 711, 777, 800, 805, 887, 893, 901, 910, 950],
      bottom: [15, 18, 21, 22, 29, 30, 42, 45, 50, 51, 67, 69, 81, 96, 110, 112, 115, 119, 134, 136, 145, 157, 202, 221, 222, 236, 360, 429, 498, 523, 800, 804, 895, 901, 910],
      utility: [1, 3, 7, 9, 12, 16, 17, 22, 25, 26, 32, 34, 35, 37, 40, 43, 44, 50, 53, 54, 57, 60, 63, 72, 76, 78, 80, 89, 98, 99, 101, 111, 115, 117, 142, 143, 147, 161, 164, 201, 223, 235, 267, 350, 412, 427, 432, 497, 518, 526, 555, 800, 888, 902, 910],
    },
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
