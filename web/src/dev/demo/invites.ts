// ?demo=invite: two invites on Play (one League won't let you accept). ?demo=invite-lobby: a party with sent invites
// (pending, declined), the Invite Friends sheet and, on the Friends tab, the profile sheet's Invite.
import type { ClientState } from '../../contracts'
import { store } from '../../store'
import { lobby } from './client'
import { held, push, wait } from './core'
import { lobbyMembers, riotIdOf, TARN, VEXA } from './people'

export function receivedInvites(): NonNullable<ClientState['invitations']> {
  return [
    { id: 'inv-1', fromRiotId: riotIdOf(VEXA), queueId: 440, queueName: 'Ranked Flex', gameMode: 'CLASSIC', canAccept: true },
    { id: 'inv-2', fromRiotId: riotIdOf(TARN), queueId: 1700, queueName: 'Arena', gameMode: 'CHERRY', canAccept: false },
  ]
}

export function invitingLobby() {
  return {
    ...lobby(false),
    queueId: 440,
    members: lobbyMembers.slice(0, 2),
    canInvite: true,
    invitations: [
      { puuid: VEXA.puuid, riotId: riotIdOf(VEXA), state: 'Pending' },
      { puuid: 'demo-juniper', riotId: 'Juniper#EUW', state: 'Declined' },
    ],
  }
}

/** Accept joins a demo party; Decline drops the banner; SendInvitation adds a pending invite. */
export async function demoInvite(cmd: { type: string; invitationId?: string | null; puuid?: string | null }) {
  await wait(400)
  const s = store.state!
  if (cmd.type === 'SendInvitation' && s.lobby) {
    const f = held.friends?.friends.find((x) => x.puuid === cmd.puuid)
    const others = (s.lobby.invitations ?? []).filter((i) => i.puuid !== cmd.puuid)
    const invite = { puuid: cmd.puuid ?? '', riotId: f ? `${f.gameName}#${f.tagLine}` : 'Unknown', state: 'Pending' }
    push({ ...s, lobby: { ...s.lobby, invitations: [...others, invite] } })
    return
  }
  const rest = (s.invitations ?? []).filter((i) => i.id !== cmd.invitationId)
  if (cmd.type === 'AcceptInvitation') {
    const leader = {
      riotId: riotIdOf(VEXA),
      isLeader: true,
      firstPositionPreference: 'JUNGLE',
      secondPositionPreference: 'TOP',
      isLocalPlayer: false,
      puuid: VEXA.puuid,
    }
    const me = { ...lobbyMembers[0]!, isLeader: false }
    push({ ...s, gameflowPhase: 'Lobby', invitations: null, lobby: { ...lobby(false), queueId: 440, isLeader: false, members: [leader, me] } })
  } else push({ ...s, invitations: rest.length ? rest : null })
}
