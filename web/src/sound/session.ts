import { watch } from 'vue'
import { onAudioGesture, playback, soundSettings } from './index'
import { BACKGROUND_MEDIA_ELEMENTS, silentClip } from './platform'

/**
 * Keeps League Remote's audio alive in the background (Settings › Play in Background), so a Match Found or a chat sound
 * still plays with the app minimised or the phone locked, until the app is closed.
 *
 * iOS suspends a Home Screen app shortly after it leaves the screen — no timers, no hub socket, no cues — unless it is
 * playing media. So while connected (`setBackgroundAudioActive`, from `useBackgroundAudio`) and some sound is on, a plain
 * `<audio>` element loops a near-silent clip (`keepAlive`): it holds the audio session, keeps the page running between
 * music tracks or with music off, and lets `index.ts` / `music.ts` start their background elements. It is primed in a
 * gesture like every iOS media element and stops when the connector goes away (after a grace period, so a short
 * connection drop doesn't end it) or the setting is off.
 *
 * - `navigator.audioSession.type = 'playback'` (Safari 16.4+) while it runs: the category iOS keeps playing in the
 *   background; `'auto'` again when it stops (Web Audio alone then mixes with other apps and follows the silent switch).
 * - Media Session: the lock screen and Control Center show "League Remote" with the app icon and where the client is;
 *   pause there quiets everything (`playback.userPaused`) until play is pressed or the app is touched.
 *
 * Trade-offs, all iOS: a playing media element takes the audio session, so other apps' audio (Spotify, podcasts) pauses
 * while League Remote is connected with Play in Background on, and its sounds ignore the silent switch; the loop costs a
 * little battery while connected; iOS can still end it (another app starts audio, a call — it restarts on the next tap).
 * Elsewhere (Chrome, Android, desktop) a hidden page keeps playing Web Audio, so none of this runs there.
 */

type AudioSessionType = 'auto' | 'playback' | 'transient' | 'transient-solo' | 'ambient' | 'play-and-record'

function audioSession(): { type: AudioSessionType } | null {
  return (navigator as Navigator & { audioSession?: { type: AudioSessionType } }).audioSession ?? null
}

let connected = false
let nowPlaying = ''
let keepAlive: HTMLAudioElement | null = null
let primed = false
let loggedOn = false

function anySound(): boolean {
  return !soundSettings.muted || (soundSettings.music && soundSettings.musicVolume > 0)
}

/** The keep-alive loop should be playing now. */
function wanted(): boolean {
  return BACKGROUND_MEDIA_ELEMENTS && connected && soundSettings.background && !playback.userPaused && anySound()
}

function element(): HTMLAudioElement {
  if (keepAlive) return keepAlive
  const el = new Audio()
  el.loop = true
  el.preload = 'auto'
  el.setAttribute('playsinline', '')
  el.src = silentClip(2, true)
  keepAlive = el
  return el
}

function setSessionType(type: AudioSessionType) {
  const s = audioSession()
  if (!s || s.type === type) return
  try {
    s.type = type
  } catch {
    /* unsupported value on this version */
  }
}

/** Bring the loop, the audio session and the Media Session state in line with `wanted()`. */
function update() {
  if (!BACKGROUND_MEDIA_ELEMENTS) return
  const on = wanted()
  if (on) {
    setSessionType('playback')
    const el = element()
    // Allowed outside a gesture once a gesture has played it (primed); otherwise the next tap starts it.
    if (primed && el.paused) void el.play().catch(() => {})
  } else {
    keepAlive?.pause()
    setSessionType('auto')
  }
  if (import.meta.env.DEV && on !== loggedOn) console.debug(`[sound] background keep-alive ${on ? 'on' : 'off'}`)
  loggedOn = on
  updateMediaSession()
}

/** In a gesture: start the loop, or play-then-pause it once so it may start later without one. */
function onGesture() {
  if (!BACKGROUND_MEDIA_ELEMENTS) return
  const el = element()
  const on = wanted()
  if (on) setSessionType('playback')
  if (!el.paused && on) return
  if (!on && primed) return
  void el.play()?.then(
    () => {
      primed = true
      if (!wanted()) el.pause()
    },
    () => {
      /* not this gesture: the next one tries again */
    },
  )
}

function updateMediaSession() {
  const ms = 'mediaSession' in navigator ? navigator.mediaSession : null
  if (!ms) return
  try {
    if (typeof MediaMetadata !== 'undefined')
      ms.metadata = new MediaMetadata({
        title: 'League Remote',
        artist: nowPlaying,
        artwork: [
          { src: `${import.meta.env.BASE_URL}icon-192.png`, sizes: '192x192', type: 'image/png' },
          { src: `${import.meta.env.BASE_URL}icon-512.png`, sizes: '512x512', type: 'image/png' },
        ],
      })
    ms.playbackState = playback.userPaused ? 'paused' : wanted() ? 'playing' : 'none'
  } catch {
    /* older Media Session implementation */
  }
}

function setHandlers() {
  const ms = 'mediaSession' in navigator ? navigator.mediaSession : null
  if (!ms) return
  const handlers: [MediaSessionAction, MediaSessionActionHandler | null][] = [
    ['play', () => (playback.userPaused = false)],
    ['pause', () => (playback.userPaused = true)],
    ['stop', () => (playback.userPaused = true)],
    // Not a player: no seeking or tracks on the lock screen.
    ['seekbackward', null],
    ['seekforward', null],
    ['previoustrack', null],
    ['nexttrack', null],
  ]
  for (const [action, handler] of handlers) {
    try {
      ms.setActionHandler(action, handler)
    } catch {
      /* action not supported here */
    }
  }
}

/**
 * Connected to the client (or not): the keep-alive only runs while there is something to hear about. `label` is the
 * second line on the lock screen (where the client is, e.g. "Champion Select").
 */
export function setBackgroundAudioActive(active: boolean, label = '') {
  connected = active
  nowPlaying = label
  update()
}

/** Hook the keep-alive to gestures, the lock screen and the settings (install.ts). */
export function installAudioSession() {
  onAudioGesture(onGesture)
  if (BACKGROUND_MEDIA_ELEMENTS) setHandlers()
  watch(
    () => [soundSettings.background, soundSettings.muted, soundSettings.music, soundSettings.musicVolume, playback.userPaused],
    update,
  )
}
