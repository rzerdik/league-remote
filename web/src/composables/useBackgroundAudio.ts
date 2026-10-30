import { computed, onScopeDispose, watch } from 'vue'
import { phaseLabel } from '../labels'
import { setBackgroundAudioActive } from '../sound/session'
import { liveState } from '../store'

/**
 * Keeps the app's audio alive in the background while it is connected to the client (`sound/session.ts`): on while
 * `liveState` is there, and for a grace period after it goes (a connection hiccup while the phone is locked shouldn't let iOS
 * suspend the app before it reconnects), then off so a PC that was switched off doesn't keep the phone's audio busy.
 * The lock screen's second line is where the client is.
 */
const GRACE_MS = 2 * 60_000

export function useBackgroundAudio() {
  let timer: ReturnType<typeof setTimeout> | undefined
  const label = computed(() => {
    const s = liveState.value
    if (!s) return ''
    return s.lcuConnected ? phaseLabel(s.gameflowPhase) : 'Waiting for League'
  })

  watch(
    [() => !!liveState.value, label],
    ([live, text]) => {
      clearTimeout(timer)
      if (live) setBackgroundAudioActive(true, text)
      else timer = setTimeout(() => setBackgroundAudioActive(false), GRACE_MS)
    },
    { immediate: true },
  )

  onScopeDispose(() => {
    clearTimeout(timer)
    setBackgroundAudioActive(false)
  })
}
