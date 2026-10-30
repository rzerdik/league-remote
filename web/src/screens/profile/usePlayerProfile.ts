import { computed, ref, watch } from 'vue'
import { useBusy } from '../../composables/useBusy'
import { whenReady } from '../../lib/timeout'
import { loadPlayerProfile, playerProfile, PLAYER_PROFILE_TIMEOUT_MS } from '../../profile'

export type PlayerProfileStatus = 'loading' | 'ready' | 'error'

/**
 * Another player's profile for the pushed Player Profile view: asks the connector ('LoadPlayerProfile') every time the
 * view opens for someone — it re-checks they may be opened and rereads at most every 30 s — then waits for the
 * ProfileUpdated that brings it. A profile already on the phone shows at once (and refreshes underneath); both waits
 * are bounded, so the view ends in 'ready' or 'error' (the connector's refusal, or ours).
 */
export function usePlayerProfile(puuid: () => string) {
  const { busy, run } = useBusy()
  const error = ref('')
  const profile = computed(() => playerProfile(puuid()))

  const status = computed<PlayerProfileStatus>(() =>
    error.value && !busy.value ? 'error' : profile.value ? 'ready' : 'loading',
  )

  function load() {
    error.value = ''
    void run(async () => {
      const r = await loadPlayerProfile(puuid())
      // A refusal wins over a cached copy: they may have left the party or been removed as a friend.
      if (!r.ok) error.value = r.error
      else if (!(await whenReady(() => profile.value, PLAYER_PROFILE_TIMEOUT_MS)))
        error.value = "This profile didn't come through. Try again."
    })
  }

  watch(puuid, load, { immediate: true })

  return { profile, status, error, load }
}
