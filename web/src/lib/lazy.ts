import { defineComponent, h, onBeforeUnmount, ref, shallowRef, type Component } from 'vue'
import ScreenLoadFailed from '../components/ScreenLoadFailed.vue'
import ScreenLoading from '../components/ScreenLoading.vue'
import { reloadForMissingCode } from './reload'
import { within } from './timeout'

/** A lazily loaded component that can be fetched ahead of its first use. */
export type LazyComponent = Component & { prefetch: () => void }

interface LazyOptions {
  /** Shown after LOADING_DELAY_MS while the chunk is on its way (default: a spinner in the content area); null: nothing. */
  loading?: Component | null
  /** Shown once loading failed (default: "Couldn't Load" with Try Again); null: nothing (use `onFailed`). */
  failed?: Component | null
  /** Called when a mounted copy couldn't load (a cover with nothing to show closes itself). */
  onFailed?: () => void
}

/** How long a screen may take before its loading view shows (a cached chunk never gets there). */
const LOADING_DELAY_MS = 200
/** After start-up, when the lazy screens are fetched in the background. */
const PREFETCH_AFTER_MS = 2000
/** Tries per load: a dropped request is tried once more before the screen says it couldn't load. */
const ATTEMPTS = 2
/** One try may take this long (a request that never answers must not leave a spinner up for good). */
const LOAD_TIMEOUT_MS = 15_000

// ----- which chunks failed ------------------------------------------------------------------------------------

/** The last chunk URL whose import failed (from the error, or from Vite's `vite:preloadError` payload), and a count. */
let lastFailedChunk: string | undefined
let chunkFailures = 0

/**
 * Remember which chunk a failed import was for. Chromium ("Failed to fetch dynamically imported module: <url>") and
 * Firefox name the URL in the error; WebKit doesn't ("Importing a module script failed."), so there the retry below
 * is a plain one.
 */
export function noteChunkFailure(error: unknown) {
  const message = error instanceof Error ? error.message : String(error)
  const url = /(https?:\/\/[^\s'"]+?\.m?js)\b/.exec(message)?.[1]
  if (!url) return
  lastFailedChunk = url
  chunkFailures++
}

// ----- lazy() ---------------------------------------------------------------------------------------------------

/**
 * A screen or view that isn't needed at start (Profile, In Game, Settings, Auto Pick, the QR scanner) in its own
 * chunk, so the first screen isn't held up by the rest (Vite warned at 500 kB).
 *
 * - Each try is bounded (LOAD_TIMEOUT_MS); a load gets ATTEMPTS tries. When a mounted copy's load has failed for good
 *   the app reloads once to pick up the current version (reload.ts: throttled, never during a ready check, the draft
 *   or a game); otherwise it shows `failed`. A background prefetch's failure does neither.
 * - Browsers remember a failed module fetch for its URL, so a plain retry can't recover: the second try imports the
 *   chunk that just failed with a `?retry=` query (a new URL), straight, past Vite's preload helper. That is fine for
 *   our chunks: their own imports are static (fetched as usual), and the helper only adds the chunk's CSS `<link>`,
 *   which its first try already put in the page. Without a URL in the error (WebKit) the retry goes through the
 *   loader again.
 * - A chunk Vite couldn't fetch resolves to nothing once main.ts handled `vite:preloadError`: a failure too.
 * - A failed load is never kept: every later load starts afresh, so a prefetch that failed in a dead spot doesn't
 *   break the screen later. `prefetch()` loads it ahead of use (not while offline), so its first appearance renders
 *   at once (with its transition).
 */
export function lazy(loader: () => Promise<{ default: Component } | undefined>, options: LazyOptions = {}): LazyComponent {
  const loading = options.loading === undefined ? ScreenLoading : options.loading
  const failed = options.failed === undefined ? ScreenLoadFailed : options.failed
  let resolved: Component | null = null
  let pending: Promise<Component> | null = null

  async function attempt(): Promise<Component> {
    let retryUrl: string | undefined
    for (let i = 0; i < ATTEMPTS; i++) {
      const mark = chunkFailures
      try {
        const load = retryUrl
          ? (import(/* @vite-ignore */ `${retryUrl}?retry=${Date.now()}`) as Promise<{ default: Component }>)
          : loader()
        const m = await within(load, LOAD_TIMEOUT_MS, undefined)
        if (m?.default) return m.default
      } catch (e) {
        noteChunkFailure(e)
      }
      // The chunk that failed during this try (main.ts records Vite's handled errors too); retry that one.
      if (chunkFailures > mark) retryUrl = lastFailedChunk
    }
    throw new Error('Chunk not loaded')
  }

  function load(): Promise<Component> {
    if (resolved) return Promise.resolve(resolved)
    pending ??= attempt().then(
      (c) => {
        resolved = c
        pending = null
        return c
      },
      (e: unknown) => {
        pending = null // the next load starts afresh
        throw e
      },
    )
    return pending
  }

  const Lazy = defineComponent({
    name: 'LazyView',
    inheritAttrs: false,
    setup(_, { attrs, slots }) {
      const comp = shallowRef<Component | null>(resolved)
      const error = ref(false)
      const slow = ref(false)
      let timer: ReturnType<typeof setTimeout> | undefined
      let alive = true
      function start() {
        error.value = false
        slow.value = false
        clearTimeout(timer)
        timer = setTimeout(() => (slow.value = true), LOADING_DELAY_MS)
        load()
          .then(
            (c) => {
              if (alive) comp.value = c
            },
            () => {
              if (!alive || reloadForMissingCode()) return
              error.value = true
              options.onFailed?.()
            },
          )
          .finally(() => clearTimeout(timer))
      }
      if (!comp.value) start()
      onBeforeUnmount(() => {
        alive = false
        clearTimeout(timer)
      })
      return () => {
        if (comp.value) return h(comp.value, attrs, slots)
        if (error.value) return failed ? h(failed, { onRetry: start }) : null
        return slow.value && loading ? h(loading) : null
      }
    },
  })

  return Object.assign(Lazy, {
    prefetch() {
      // Offline, a prefetch can only fail (and leave a failed module record behind): the screen loads when it opens.
      if (resolved || navigator.onLine === false) return
      void load().catch(() => undefined)
    },
  })
}

/** Fetch these once the app has settled after start-up (never in the way of the first screen). */
export function prefetchLater(...components: LazyComponent[]) {
  setTimeout(() => components.forEach((c) => c.prefetch()), PREFETCH_AFTER_MS)
}
