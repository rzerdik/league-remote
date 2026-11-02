// Mirror of src/LeagueRemote.Contracts/CommandWhitelist.cs: the phone checks a command before sending it so a mistake
// shows at once; the connector validates again.

import { AutoPickMaxBans, AutoPickMaxPicks, isAutoPickQueueGroup, isAutoPickRoleAllowed } from './autoPick'
import { ChatMaxConversationIdLength, ChatMaxMessageLength } from './chat'
import { SwapKinds, isPosition, type RemoteCommand } from './commands'
import { FriendMaxIdLength } from './friends'
import { isPuuid, RiotIds, validateGameName, validateTagLine } from './riotIds'

const MaxRequestIdLength = 64

/** Longest invitation id accepted (letters, digits, '-', '_', '.', ':' and '@'). */
const MaxLcuIdLength = 128
const isLcuId = (id: string | null | undefined) => !!id && id.length <= MaxLcuIdLength && /^[A-Za-z0-9_.:@-]+$/.test(id)

const hasControlChars = (s: string) => /[\u0000-\u001f\u007f-\u009f]/.test(s)

/** Null when the command is valid, otherwise the error to show. */
export function validateCommand(c: RemoteCommand): string | null {
  if (!c.requestId || c.requestId.length > MaxRequestIdLength) return `RequestId is required (max ${MaxRequestIdLength} chars).`
  const pos = (v: number | null | undefined) => typeof v === 'number' && v > 0
  const nonNegative = (v: number | null | undefined) => typeof v === 'number' && v >= 0
  switch (c.type) {
    case 'CreateLobby':
      return pos(c.queueId) ? null : 'QueueId must be a positive integer.'
    case 'HoverChampion':
    case 'LockChampion':
      if (!pos(c.championId)) return 'ChampionId must be a positive integer.'
      // League numbers a draft's actions from 0, so 0 is a real action (the first ban of a custom game).
      return nonNegative(c.actionId) ? null : 'ActionId must not be negative.'
    case 'BenchSwap':
      return pos(c.championId) ? null : 'ChampionId must be a positive integer.'
    case 'SetSummonerSpells':
      if (!pos(c.spell1Id) || !pos(c.spell2Id)) return 'Spell1Id and Spell2Id must be positive integers.'
      return c.spell1Id === c.spell2Id ? 'Summoner spells must differ.' : null
    case 'SelectRunePage':
      return pos(c.runePageId) ? null : 'RunePageId must be a positive integer.'
    case 'RequestSwap':
    case 'AcceptSwap':
    case 'DeclineSwap':
    case 'CancelSwap':
      if (!c.swapKind || !SwapKinds.includes(c.swapKind)) return 'SwapKind must be PickOrder, Position or Trade.'
      return pos(c.swapId) ? null : 'SwapId must be a positive integer.'
    case 'SetPositionPreferences':
      if (!isPosition(c.firstPosition)) return 'FirstPosition must be TOP, JUNGLE, MIDDLE, BOTTOM, UTILITY or FILL.'
      if (c.firstPosition === 'FILL')
        return c.secondPosition == null || c.secondPosition === 'UNSELECTED'
          ? null
          : 'SecondPosition must be empty when FirstPosition is FILL.'
      if (!isPosition(c.secondPosition)) return 'SecondPosition must be TOP, JUNGLE, MIDDLE, BOTTOM, UTILITY or FILL.'
      return c.firstPosition === c.secondPosition ? 'The two positions must differ.' : null
    case 'SendChatMessage':
    case 'MarkConversationRead': {
      const id = c.conversationId
      if (!id || !id.trim() || id.length > ChatMaxConversationIdLength || hasControlChars(id))
        return `ConversationId is required (max ${ChatMaxConversationIdLength} chars).`
      if (c.type === 'MarkConversationRead') return null
      const body = c.body
      if (!body || !body.trim()) return 'Body is required.'
      if (body.length > ChatMaxMessageLength) return `Body must be at most ${ChatMaxMessageLength} characters.`
      return hasControlChars(body) ? 'Body must be a single line of text.' : null
    }
    case 'AcceptInvitation':
    case 'DeclineInvitation':
      return isLcuId(c.invitationId) ? null : `InvitationId is required (max ${MaxLcuIdLength} chars: letters, digits, '-', '_', '.', ':' or '@').`
    case 'RemoveFriend': {
      const id = c.friendId
      return !id || !id.trim() || id.length > FriendMaxIdLength || hasControlChars(id)
        ? `FriendId is required (max ${FriendMaxIdLength} chars).`
        : null
    }
    case 'SendFriendRequest':
    case 'SearchPlayer':
      return validateGameName(c.gameName) ?? validateTagLine(c.tagLine)
    case 'SendInvitation':
    case 'OpenDirectConversation':
    case 'AcceptFriendRequest':
    case 'DeclineFriendRequest':
    case 'CancelFriendRequest':
    case 'LoadPlayerProfile':
      return isPuuid(c.puuid) ? null : `Puuid is required (max ${RiotIds.maxPuuidLength} chars).`
    case 'LoadMatchDetails':
      if (!(typeof c.gameId === 'number' && Number.isSafeInteger(c.gameId) && c.gameId > 0)) return 'GameId must be a positive integer.'
      // Optional: empty = a game of your own history.
      return c.puuid && !isPuuid(c.puuid) ? `Puuid must be a puuid (max ${RiotIds.maxPuuidLength} chars).` : null
    case 'SetAutoPickEnabled':
    case 'SetAutoAcceptEnabled':
      return typeof c.enabled === 'boolean' ? null : 'Enabled is required.'
    case 'SetAutoPickPreset':
      return validateAutoPickPreset(c)
    case 'CancelAutoPickAction':
      return c.actionId == null || nonNegative(c.actionId) ? null : 'ActionId must not be negative.'
    case 'SelectSkin':
      return pos(c.skinId) ? null : 'SkinId must be a positive integer.'
    default:
      return null
  }
}

/** ValidateAutoPickPreset */
function validateAutoPickPreset(c: RemoteCommand): string | null {
  const group = c.queueGroup
  if (!isAutoPickQueueGroup(group)) return 'QueueGroup must be draft, blind or aram.'
  if (!isAutoPickRoleAllowed(group, c.role))
    return group === 'aram' ? 'Role must be ANY for aram.' : 'Role must be ANY, TOP, JUNGLE, MIDDLE, BOTTOM or UTILITY.'
  const bans = c.banChampionIds ?? []
  const picks = c.pickChampionIds ?? []
  if (group === 'aram' && (bans.length > 0 || picks.length > 0)) return 'ARAM presets have no ban or pick list.'
  const listError = (ids: number[], max: number, name: string) => {
    if (ids.length > max) return `${name} can have at most ${max} champions.`
    if (!ids.every((id) => Number.isInteger(id) && id > 0 && id <= 0x7fffffff)) return `${name} must be positive integers.`
    return new Set(ids).size !== ids.length ? `${name} must not repeat a champion.` : null
  }
  const error = listError(bans, AutoPickMaxBans, 'BanChampionIds') ?? listError(picks, AutoPickMaxPicks, 'PickChampionIds')
  if (error) return error
  if (c.spell1Id != null || c.spell2Id != null) {
    const ok = (v: number | null | undefined) => typeof v === 'number' && Number.isInteger(v) && v > 0
    if (!ok(c.spell1Id) || !ok(c.spell2Id)) return 'Spell1Id and Spell2Id must both be positive integers, or both empty.'
    if (c.spell1Id === c.spell2Id) return 'Summoner spells must differ.'
  }
  return c.runePageId != null && !(c.runePageId > 0) ? 'RunePageId must be a positive integer.' : null
}
