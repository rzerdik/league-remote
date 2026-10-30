import { computed, ref, watch } from 'vue'
import { useBusy } from '../../composables/useBusy'
import { whenReady } from '../../lib/timeout'
import { detailsFor, loadMatchDetails, MATCH_DETAILS_TIMEOUT_MS } from '../../profile'

export type MatchDetailsStatus = 'loading' | 'ready' | 'error'

/**
 * A game's details for the pushed match view: asks the connector for them ('LoadMatchDetails') unless they are already
 * in the profile, then waits for the ProfileUpdated that brings them. Both waits are bounded, so the view always ends
 * in 'ready' or 'error' (with the connector's reason or ours); `load` retries. `puuid`: one of another player's games
 * (their profile view), seen from their side.
 */
export function useMatchDetails(gameId: () => number, puuid: () => string | null = () => null) {
  const { busy, run } = useBusy()
  const error = ref('')
  const details = computed(() => detailsFor(gameId(), puuid()))

  const status = computed<MatchDetailsStatus>(() => (details.value ? 'ready' : error.value && !busy.value ? 'error' : 'loading'))

  function load() {
    error.value = ''
    void run(async () => {
      const r = await loadMatchDetails(gameId(), puuid())
      if (!r.ok) {
        if (!details.value) error.value = r.error
        return
      }
      if (!(await whenReady(() => details.value, MATCH_DETAILS_TIMEOUT_MS))) error.value = "This game didn't come through. Try again."
    })
  }

  watch(
    gameId,
    () => {
      if (!details.value) load()
    },
    { immediate: true },
  )

  return { details, status, error, load }
}
