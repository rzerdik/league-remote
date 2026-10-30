import { ref } from 'vue'

/** Guards an async action against double taps: `run` is a no-op while a previous run is pending. */
export function useBusy() {
  const busy = ref(false)
  async function run<T>(fn: () => Promise<T>): Promise<T | undefined> {
    if (busy.value) return undefined
    busy.value = true
    try {
      return await fn()
    } finally {
      busy.value = false
    }
  }
  return { busy, run }
}
