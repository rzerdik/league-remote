// Mirror of src/LeagueRemote.Connector/ClientSounds/ClientSoundCatalog.cs: GET /client-sounds/manifest.json.

/**
 * Sounds: key → URL of the client's own sound (what each one is and when the client plays it is documented in the
 * catalog).
 */
export type ClientSoundKey =
  | 'matchFound'
  | 'readyCountdown'
  | 'readyPortal'
  | 'readyPortalAram'
  | 'readyAccept'
  | 'readyDecline'
  | 'readyAcceptedLoop'
  | 'readySuccess'
  | 'readyFailed'
  | 'queueStart'
  | 'queueCancel'
  | 'memberJoined'
  | 'memberLeft'
  | 'inviteSent'
  | 'leaderGranted'
  | 'inviteReceived'
  | 'chatMessage'
  | 'friendRequest'
  | 'banPhase'
  | 'pickPhase'
  | 'yourTurn'
  | 'yourBan'
  | 'timerTick'
  | 'timerTickSmall'
  | 'hover'
  | 'lockIn'
  | 'ban'
  | 'allyBan'
  | 'serialAllyBan'
  | 'serialEnemyBan'
  | 'allyPick'
  | 'enemyPick'
  | 'allyFirstPick'
  | 'enemyFirstPick'
  | 'pickSniped'
  | 'swapRequest'
  | 'positionSwapRequest'
  | 'swapAccepted'
  | 'swapDeclined'
  | 'benchUpdate'
  | 'reroll'
  | 'gameStart'
  | 'click'

/** Music: the client's music and ambience loops (streamed, only while music is on). */
export type ClientMusicKey =
  | 'musicHome'
  | 'ambienceLobby'
  | 'ambienceLobbyAram'
  | 'musicQueue'
  | 'musicQueueAram'
  | 'musicBlindPick'
  | 'musicAram'
  | 'musicAramBan'
  | 'musicDraftIntent'
  | 'musicDraftIntentToBan'
  | 'musicDraftBan'
  | 'musicDraftBanLayer1'
  | 'musicDraftBanLayer2'
  | 'musicDraftBanToPick'
  | 'musicDraftPick'
  | 'musicDraftPickLayer1'
  | 'musicDraftPickLayer2'
  | 'musicDraftPickLayer3'
  | 'musicDraftPickLayer4'
  | 'musicDraftFinal'

export interface ClientSoundManifest {
  sounds: Partial<Record<ClientSoundKey, string>>
  music: Partial<Record<ClientMusicKey, string>>
}
