import { onScopeDispose, watch } from 'vue'
import type { ClientState } from '../contracts'
import { champSelectMusic, newDraftMusicFlags } from '../sound/draftMusic'
import { playMusicStinger, setMusic, type MusicTrack } from '../sound/music'
import { liveState } from '../store'

/**
 * Plays the League client's music for where the client is, like the client does (see ClientSoundCatalog.cs):
 *
 * - Home (no lobby): the home theme `music-ac-home-main`, looped (CLIENT_AMBIENCE.AC_MAIN, 1 s fades).
 * - Lobby: the map's pre-game ambience loop; Matchmaking and the ready check: the map's in-queue music
 *   (`rcp-fe-lol-parties` determineState: looped, 3 s fade-in, stopped quickly).
 * - Champion Select: see `champSelectMusic` (draft layers by phase and progress, or the map's track).
 * - Loading, in game, post-game: silence (the client's own music there is the game's).
 *
 * Summoner's Rift unless the lobby's game mode is ARAM (Howling Abyss). Mute, the music switch, volume and playing on in
 * the background (Play in Background) are handled by the music engine.
 */
export function useMusic() {
  let flags = newDraftMusicFlags()
  /** The first snapshot after (re)connecting joins the music where it is: no transition stinger. */
  let baseline = true

  function tracksFor(s: ClientState | null): MusicTrack[] {
    if (!s) return []
    const aram = s.lobby?.gameMode === 'ARAM'
    switch (s.gameflowPhase) {
      case 'None':
        return [{ key: 'musicHome', volume: 1, fadeInMs: 1000, fadeOutMs: 1000 }]
      case 'Lobby':
        return [{ key: aram ? 'ambienceLobbyAram' : 'ambienceLobby', volume: 1, fadeInMs: 3000, fadeOutMs: 100 }]
      case 'Matchmaking':
      case 'ReadyCheck':
        return [{ key: aram ? 'musicQueueAram' : 'musicQueue', volume: 1, fadeInMs: 3000, fadeOutMs: 100 }]
      case 'ChampSelect': {
        if (!s.champSelect) return []
        const m = champSelectMusic(s.champSelect, s.lobby?.gameMode, flags)
        if (m.stinger && !baseline) playMusicStinger(m.stinger)
        return m.tracks
      }
      default:
        return []
    }
  }

  watch(
    liveState,
    (s) => {
      if (s?.gameflowPhase !== 'ChampSelect') flags = newDraftMusicFlags()
      setMusic(tracksFor(s))
      baseline = !s
    },
    { immediate: true, flush: 'post' },
  )

  onScopeDispose(() => setMusic([]))
}
