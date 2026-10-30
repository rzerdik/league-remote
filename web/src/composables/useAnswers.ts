import { shallowRef } from 'vue'

/**
 * One answer at a time to a list of requests (swap requests, game invites): while one is in flight every answer is
 * locked (`inFlight`), and `busyFor(id)` says which action of that request shows the spinner. `kind` is the caller's
 * name for the action (a command type, 'accept' / 'decline'…).
 */
export function useAnswers<K extends string>() {
  const inFlight = shallowRef<{ id: string; kind: K } | null>(null)

  /** Runs `send` for request `id` unless another answer is in flight; resolves its result (undefined if skipped). */
  async function answer<T>(id: string, kind: K, send: () => Promise<T>): Promise<T | undefined> {
    if (inFlight.value) return undefined
    inFlight.value = { id, kind }
    try {
      return await send()
    } finally {
      inFlight.value = null
    }
  }

  function busyFor(id: string): K | null {
    return inFlight.value?.id === id ? inFlight.value.kind : null
  }

  return { inFlight, answer, busyFor }
}
