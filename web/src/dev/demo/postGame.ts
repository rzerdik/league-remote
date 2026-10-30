// ?demo=postgame: the client's post-game screen after a ranked Solo/Duo win (+21 LP, a mastery level up), with Play Again
// (back to the lobby) and Leave (back to Play). `&game=` picks the game: `promoted` (a win that promotes), `loss`
// (−18 LP), `normal` (a Draft Pick loss: no Ranked section), `arena` (2nd place of eight duos), `remake`; `&lp=pending`
// is a ranked game whose LP hasn't been reported yet; `&again=off` is a game the client offers no Play Again for;
// `&wait=1` starts as the Nexus falls (Waiting for Stats, then the honor screen's Game Ending) and the scoreboard
// arrives at 4 s. A few seconds in, the game shows up in the match history ("Open in Match History").
import type {
  ClientState,
  CommandResult,
  MatchParticipant,
  MatchSummary,
  PostGameSummary,
  PostGameTeam,
  RemoteCommand,
} from '../../contracts'
import { store } from '../../store'
import { base, lobby } from './client'
import { held, params, push, seeded, wait } from './core'
import { lobbyMembers, ME, VEXA } from './people'
import { ALLIES, BUILDS, ENEMIES, mate, player, pushProfile } from './profile'

const postKind = params.get('game') ?? 'ranked'
const POST_GAME_ID = 7_312_009_990
const POST_WAIT_STEP_MS = 2000
const POST_HISTORY_MS = 5000

function postTeam(teamId: number, isMyTeam: boolean, win: boolean, players: MatchParticipant[]): PostGameTeam {
  const sum = (f: (p: MatchParticipant) => number) => players.reduce((n, p) => n + f(p), 0)
  return {
    teamId,
    isMyTeam,
    win,
    kills: sum((p) => p.kills),
    deaths: sum((p) => p.deaths),
    assists: sum((p) => p.assists),
    gold: sum((p) => p.gold),
    participants: players,
  }
}

function postGameSummary(): PostGameSummary {
  const rnd = seeded(POST_GAME_ID)
  const result = postKind === 'remake' ? 'remake' : postKind === 'loss' || postKind === 'normal' ? 'loss' : 'win'
  const remake = result === 'remake'
  const won = result === 'win'
  const me = player(103, 'mage', rnd, {
    isMe: true,
    gameName: ME.gameName,
    tagLine: ME.tagLine,
    kills: remake ? 0 : won ? 11 : 4,
    deaths: remake ? 0 : won ? 3 : 7,
    assists: remake ? 1 : 8,
    cs: remake ? 18 : 213,
    champLevel: remake ? 3 : 17,
    gold: remake ? 1400 : 14200,
    items: remake ? [1056, 2003, 0, 0, 0, 0, 3340] : BUILDS.mage,
  })
  const canPlayAgain = params.get('again') !== 'off'
  if (postKind === 'arena') {
    const duo = (place: number, a: number, b: number, mine: boolean) =>
      postTeam(place, mine, place <= 4, [
        mine ? { ...me, placement: place, cs: 0, items: [3089, 3157, 4645, 3020, 0, 0, 0] } : player(a, 'mage', rnd, { placement: place }),
        player(b, 'adc', rnd, mine ? { ...mate(VEXA.gameName, VEXA.tagLine), placement: place } : { placement: place }),
      ])
    return {
      gameId: POST_GAME_ID,
      queueId: 1750,
      gameMode: 'CHERRY',
      mapId: 30,
      durationSeconds: 1104,
      result: 'win',
      isRanked: false,
      rank: null,
      mastery: { championId: 103, pointsGained: 860, level: 12, leveledUp: false, grade: '', pointsUntilNextLevel: 4300 },
      teams: [
        duo(2, 0, 51, true),
        duo(1, 157, 777, false),
        duo(3, 92, 875, false),
        duo(4, 38, 69, false),
        duo(5, 1, 22, false),
        duo(6, 99, 64, false),
        duo(7, 86, 412, false),
        duo(8, 266, 89, false),
      ],
      canPlayAgain,
    }
  }
  const allies = ALLIES.map(([id, build, gameName, tagLine]) => player(id, build, rnd, mate(gameName, tagLine)))
  allies.splice(2, 0, me)
  const enemies = ENEMIES.map(([id, build]) => player(id, build, rnd))
  const solo = 'RANKED_SOLO_5x5'
  const rank: PostGameSummary['rank'] =
    postKind === 'normal' || remake || params.get('lp') === 'pending'
      ? null
      : postKind === 'promoted'
        ? { queueType: solo, tier: 'EMERALD', division: 'II', leaguePoints: 0, leaguePointsDelta: 22, provisionalGamesRemaining: 0, change: 'promoted' }
        : postKind === 'loss'
          ? { queueType: solo, tier: 'EMERALD', division: 'III', leaguePoints: 29, leaguePointsDelta: -18, provisionalGamesRemaining: 0, change: '' }
          : { queueType: solo, tier: 'EMERALD', division: 'III', leaguePoints: 68, leaguePointsDelta: 21, provisionalGamesRemaining: 0, change: '' }
  return {
    gameId: POST_GAME_ID,
    queueId: postKind === 'normal' ? 400 : 420,
    gameMode: 'CLASSIC',
    mapId: 11,
    durationSeconds: remake ? 212 : 1512,
    result,
    isRanked: postKind !== 'normal' && !remake,
    rank,
    mastery: remake
      ? null
      : { championId: 103, pointsGained: won ? 1240 : 610, level: won ? 12 : 11, leveledUp: won, grade: won ? 'S' : 'B+', pointsUntilNextLevel: 9760 },
    teams: [postTeam(100, true, won, allies), postTeam(200, false, result === 'loss', enemies)],
    canPlayAgain,
  }
}

export function postGameStart(): ClientState {
  const ended: ClientState = { ...base('EndOfGame'), postGame: postGameSummary() }
  if (params.get('wait') !== '1') return ended
  setTimeout(() => push(base('PreEndOfGame')), POST_WAIT_STEP_MS)
  setTimeout(() => push(ended), POST_WAIT_STEP_MS * 2)
  return base('WaitingForStats')
}

/** The finished game lands in the match history a moment later, as the PC rereads it after a game. */
export function postGameToHistory() {
  setTimeout(() => {
    const s = postGameSummary()
    const me = s.teams.flatMap((t) => t.participants).find((p) => p.isMe)
    if (!held.profile || !me) return
    const match: MatchSummary = {
      gameId: s.gameId,
      queueId: s.queueId,
      gameMode: s.gameMode,
      gameType: 'MATCHED_GAME',
      mapId: s.mapId,
      startedAt: new Date(Date.now() - s.durationSeconds * 1000).toISOString(),
      durationSeconds: s.durationSeconds,
      result: s.result,
      position: s.mapId === 11 ? 'MIDDLE' : '',
      me,
    }
    pushProfile({ ...held.profile, matches: [match, ...held.profile.matches], updatedAt: new Date().toISOString() })
  }, POST_HISTORY_MS)
}

/** Play Again: the client rebuilds the lobby of that queue (you alone); Leave: back to Play. Both refused elsewhere. */
export async function demoPostGame(cmd: RemoteCommand): Promise<CommandResult> {
  await wait(700)
  const s = store.state
  if (s?.gameflowPhase !== 'EndOfGame') return { requestId: cmd.requestId, success: false, error: 'Not on the post-game screen.' }
  if (cmd.type === 'PlayAgain') {
    if (s.postGame && !s.postGame.canPlayAgain)
      return { requestId: cmd.requestId, success: false, error: "Play Again isn't available for this game." }
    const queueId = s.postGame?.queueId || 420
    const arena = s.postGame?.gameMode === 'CHERRY'
    push({
      ...base('Lobby'),
      lobby: { ...lobby(false), queueId, gameMode: arena ? 'CHERRY' : 'CLASSIC', members: lobbyMembers.slice(0, 1), showPositionSelector: !arena },
    })
  } else {
    push(base('None'))
  }
  return { requestId: cmd.requestId, success: true }
}
