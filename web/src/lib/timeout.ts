import { watch } from 'vue'

/** Settle with `promise`, or with `fallback` once `ms` have passed (the promise itself keeps running). */
export function within<T, F>(promise: Promise<T>, ms: number, fallback: F): Promise<T | F> {
  let timer: ReturnType<typeof setTimeout> | undefined
  const timeout = new Promise<F>((resolve) => {
    timer = setTimeout(() => resolve(fallback), Math.max(0, ms))
  })
  return Promise.race([promise, timeout]).finally(() => clearTimeout(timer))
}

/**
 * Resolves with the reactive `read()` once it is set (it may already be), or null after `ms`: for data a command asked
 * the connector for, which arrives later with a state message.
 */
export async function whenReady<T>(read: () => T | null | undefined, ms: number): Promise<T | null> {
  const ready = read()
  if (ready) return ready
  let stop = () => {}
  const arrived = new Promise<T>((resolve) => {
    stop = watch(read, (v) => {
      if (v) resolve(v)
    })
  })
  try {
    return await within(arrived, ms, null)
  } finally {
    stop()
  }
}
