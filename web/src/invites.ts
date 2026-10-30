import { computed } from 'vue'
import type { Friend, LobbyState, SentInvitation } from './contracts'
import { isFriend, isInvitable } from './friends'
import { haptic } from './haptics'
import type { StatusTone } from './labels'
import { sendCommand } from './hub'
import { riotId } from './riotId'
import { liveState } from './store'

/**
 * Game invites both ways. Received ones are banners (components/invites/InviteBanners); sending happens from the
 * Lobby's party list (InviteSheet) or a friend's profile sheet (inviteAction / inviteFriend).
 */

/** Friends the invite sheet lists: invitable ones not in the party yet, by name (rows don't jump when invited). */
export function invitableFriends(friends: Friend[], lobby: LobbyState): Friend[] {
  return friends.filter((f) => isInvitable(f) && !inParty(f, lobby)).sort((a, b) => riotId(a).localeCompare(riotId(b)))
}

/** A lobby member is this friend (`isFriend`: by puuid, else by Riot ID). */
function inParty(f: Friend, lobby: LobbyState): boolean {
  return lobby.members.some((m) => isFriend(f, m))
}

/** The current lobby when you can invite from it (leader or open invites, room left, not searching). */
export const invitingLobby = computed<LobbyState | null>(() => {
  const lobby = liveState.value?.lobby
  return lobby?.canInvite && !lobby.isSearching ? lobby : null
})

/** The invite this lobby already sent to the friend, if any. */
function sentInvitation(f: Friend): SentInvitation | null {
  return liveState.value?.lobby?.invitations?.find((i) => i.puuid === f.puuid) ?? null
}

/**
 * How a sent invite reads everywhere it shows (party list, invite sheet, profile sheet): "Invited" (orange, still
 * pending: can't be sent again), "Declined" or "Failed" (red: may be sent again).
 */
export function invitationStatus(i: SentInvitation): { label: string; tone: StatusTone; pending: boolean } {
  if (i.state === 'Declined') return { label: 'Declined', tone: 'red', pending: false }
  if (i.state === 'Error') return { label: 'Failed', tone: 'red', pending: false }
  return { label: 'Invited', tone: 'orange', pending: true }
}

/**
 * The friend profile sheet's invite row: offered while you are in a lobby you can invite from and the friend is free;
 * "Invited" (disabled) while their invite is pending. Null: don't offer it.
 */
export function inviteAction(f: Friend): { label: string; disabled: boolean } | null {
  const lobby = invitingLobby.value
  if (!lobby || !isInvitable(f) || inParty(f, lobby)) return null
  const sent = sentInvitation(f)
  const pending = !!sent && invitationStatus(sent).pending
  return { label: pending ? 'Invited' : 'Invite to Party', disabled: pending }
}

/** Sends the invite; the connector refuses it when it can't work (no lobby, full, searching, not a friend). */
export async function inviteFriend(f: Friend): Promise<boolean> {
  const r = await sendCommand({ type: 'SendInvitation', puuid: f.puuid })
  if (r?.success) haptic('success')
  return !!r?.success
}
