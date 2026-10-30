// League Remote service worker (hand-written; `vite build` stamps VERSION and ASSETS into dist/sw.js, see vite.config.ts).
// - /hub (SignalR) and anything non-GET: network only (never intercepted).
// - Navigations (incl. the QR deep link /?pair=123456): network-first so a new build's index.html is picked up at
//   once; the cached shell (keyed WITHOUT the query string, so codes never pile up in the cache) is the offline
//   fallback. The page itself reads ?pair= from location.
// - /assets/* (content-hashed): the current build's JS/CSS are precached on install (so lazy chunks such as the QR
//   fallback work offline and never go missing); cache-first. The previous build's cache is kept for one more version:
//   this worker takes over at once, and a page still running that build (mid-game, it doesn't reload) must still find
//   its not-yet-loaded chunks after the server dropped them. Older builds' caches are dropped on activate.
// - Other same-origin files (manifest, icons): cache-first, revalidated in the background.
// - Data Dragon images: cache-first (immutable per version). Skin art (/cdn/img/champion/loading|splash/, unversioned and
//   large): cache-first in its own cache, kept across app versions and trimmed to the newest SKIN_ART_MAX images.
// - /client-sounds/manifest.json (the League client's own sounds, served by the PC app): network-first, cached copy
//   offline; /client-sounds/*.ogg: cache-first in their own cache (kept across app versions).
// - Web Push (push.ts): `push` shows the notification from the JSON payload { title, body, tag, url, renotify,
//   requireInteraction }; `notificationclick` focuses an open app window (navigating it to `url`) or opens one. The
//   app icon badge counts the notifications still shown.
const VERSION = 'dev' // replaced with a build hash at build time
const ASSETS = [] // replaced with the build's /assets/* JS and CSS at build time
const SHELL_PREFIX = 'league-remote-shell-'
const SHELL_CACHE = `${SHELL_PREFIX}${VERSION}`
const IMG_CACHE = 'league-remote-ddragon-img-v2'
const SOUND_CACHE = 'league-remote-client-sounds-v1'
const SKIN_ART_CACHE = 'league-remote-skin-art-v1'
/** Loading art is ~50 KB, a splash ~300 KB: a few champions' worth of skins. */
const SKIN_ART_MAX = 80
const SHELL = ['/', '/index.html', '/manifest.webmanifest', '/favicon.svg', '/icon-192.png', '/icon-512.png', '/apple-touch-icon.png']
const NAV_TIMEOUT_MS = 4000

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(SHELL_CACHE)
      .then((c) => c.addAll([...SHELL, ...ASSETS]))
      .then(() => self.skipWaiting()),
  )
})

/** Drop /assets/* entries that are not part of the current build (only meaningful when ASSETS is stamped). */
async function pruneAssets() {
  if (ASSETS.length === 0) return
  const keep = new Set(ASSETS)
  const cache = await caches.open(SHELL_CACHE)
  const keys = await cache.keys()
  await Promise.all(
    keys
      .filter((req) => {
        const path = new URL(req.url).pathname
        return path.startsWith('/assets/') && !keep.has(path) && !/\.(woff2?|wasm)$/.test(path)
      })
      .map((req) => cache.delete(req)),
  )
}

/**
 * The caches to keep: this build's shell, the previous build's (the newest other shell cache — caches.keys() lists
 * caches in creation order), and the cross-version ones. At most two builds' assets, so the cache stays bounded.
 */
function cachesToKeep(keys) {
  const previous = keys.filter((k) => k.startsWith(SHELL_PREFIX) && k !== SHELL_CACHE).at(-1)
  return new Set([SHELL_CACHE, previous, IMG_CACHE, SOUND_CACHE, SKIN_ART_CACHE].filter(Boolean))
}

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => {
        const keep = cachesToKeep(keys)
        return Promise.all(keys.filter((k) => !keep.has(k)).map((k) => caches.delete(k)))
      })
      .then(pruneAssets)
      .then(() => self.clients.claim()),
  )
})

function isCacheable(res) {
  return res && (res.ok || res.type === 'opaque')
}

/** Drops the oldest entries (cache.keys() lists them in insertion order) until at most `max` are left. */
async function trimCache(cacheName, max) {
  const cache = await caches.open(cacheName)
  const keys = await cache.keys()
  await Promise.all(keys.slice(0, Math.max(0, keys.length - max)).map((k) => cache.delete(k)))
}

async function cacheFirst(request, cacheName, { revalidate = false, max = 0 } = {}) {
  const cache = await caches.open(cacheName)
  const cached = await cache.match(request)
  const fetchAndStore = () =>
    fetch(request)
      .then((res) => {
        if (isCacheable(res)) {
          const stored = cache.put(request, res.clone())
          if (max > 0) void stored.then(() => trimCache(cacheName, max)).catch(() => undefined)
        }
        return res
      })
      .catch(() => undefined)
  if (cached) {
    if (revalidate) void fetchAndStore() // fire-and-forget refresh
    return cached
  }
  const res = await fetchAndStore()
  return res || Response.error()
}

/**
 * /assets/* (content-hashed, never change): this build's cache, then any kept cache (the previous build's, for a page
 * that still runs it), then the network (stored in this build's cache, under the URL without its query).
 */
async function assetFirst(request) {
  // The query is ignored: a file's name is its content hash; lazy.ts retries a failed chunk as `…js?retry=<n>`.
  const key = new URL(request.url)
  key.search = ''
  const cache = await caches.open(SHELL_CACHE)
  const cached = (await cache.match(key.href)) || (await caches.match(key.href))
  if (cached) return cached
  try {
    const res = await fetch(request)
    if (res.ok) void cache.put(key.href, res.clone())
    return res
  } catch {
    return Response.error()
  }
}

/** Network-first for page loads: always try the server (bounded), fall back to the cached shell when offline. */
async function networkFirstNavigation(request, shellKey) {
  const cache = await caches.open(SHELL_CACHE)
  try {
    const res = await Promise.race([
      fetch(request),
      new Promise((_, reject) => setTimeout(() => reject(new Error('timeout')), NAV_TIMEOUT_MS)),
    ])
    if (res.ok) {
      void cache.put(shellKey, res.clone())
      return res
    }
    // A server error page: prefer the cached shell if there is one.
    return (await cache.match(shellKey)) || (await cache.match('/index.html')) || res
  } catch {
    return (await cache.match(shellKey)) || (await cache.match('/index.html')) || Response.error()
  }
}

/** Network-first for small JSON that must be fresh when online (the sound manifest). */
async function networkFirst(request, cacheName) {
  const cache = await caches.open(cacheName)
  try {
    const res = await fetch(request)
    if (res.ok) void cache.put(request, res.clone())
    return res
  } catch {
    return (await cache.match(request)) || Response.error()
  }
}

self.addEventListener('fetch', (event) => {
  const req = event.request
  if (req.method !== 'GET') return
  const url = new URL(req.url)

  if (url.origin === self.location.origin) {
    if (url.pathname === '/hub' || url.pathname.startsWith('/hub/')) return // network only
    if (url.pathname === '/client-sounds/manifest.json') {
      event.respondWith(networkFirst(req, SOUND_CACHE))
      return
    }
    if (url.pathname.startsWith('/client-sounds/')) {
      // Range requests (media elements) go to the network; fetch()/decodeAudioData requests are cached.
      if (req.headers.has('range')) return
      event.respondWith(cacheFirst(req, SOUND_CACHE))
      return
    }
    if (req.mode === 'navigate') {
      const shellKey = new Request(url.origin + url.pathname) // drop ?pair=… and other query params
      event.respondWith(networkFirstNavigation(req, shellKey))
      return
    }
    if (url.pathname.startsWith('/assets/')) {
      event.respondWith(assetFirst(req))
      return
    }
    event.respondWith(cacheFirst(req, SHELL_CACHE, { revalidate: true }))
    return
  }

  if (url.hostname === 'ddragon.leagueoflegends.com' && /^\/cdn\/img\/champion\/(loading|splash)\//.test(url.pathname)) {
    event.respondWith(cacheFirst(req, SKIN_ART_CACHE, { max: SKIN_ART_MAX }))
    return
  }
  if (url.hostname === 'ddragon.leagueoflegends.com' && /\/cdn\/[^/]+\/img\//.test(url.pathname)) {
    event.respondWith(cacheFirst(req, IMG_CACHE))
  }
  // Everything else (Data Dragon JSON): default network handling.
})

// --- Web Push ---------------------------------------------------------------------------------------------------

self.addEventListener('push', (event) => {
  let data = {}
  try {
    data = event.data ? event.data.json() : {}
  } catch {
    data = { body: event.data ? event.data.text() : '' }
  }
  const title = data.title || 'League Remote'
  const tag = data.tag || undefined
  const options = {
    body: data.body || '',
    icon: '/icon-192.png',
    badge: '/icon-192.png',
    tag,
    // renotify needs a tag; without it browsers throw.
    renotify: Boolean(tag && data.renotify !== false),
    requireInteraction: Boolean(data.requireInteraction),
    data: { url: appPath(data.url) },
  }
  // Always show something: iOS revokes push permission from sites that receive pushes without a notification.
  event.waitUntil(self.registration.showNotification(title, options).then(updateBadge))
})

/**
 * Where a notification click goes: a path on this origin from the payload's `url`; anything else (another host, a
 * protocol-relative `//host/x`, a non-string) opens the app's root. Only the PC can send a payload, still: never off-origin.
 */
function appPath(value) {
  if (typeof value !== 'string') return '/'
  try {
    const u = new URL(value, self.location.origin)
    return u.origin === self.location.origin ? u.pathname + u.search + u.hash : '/'
  } catch {
    return '/'
  }
}

/** App icon badge (iOS 16.4+ home-screen app, desktop Chromium) = notifications still shown; push.ts clears it. */
async function updateBadge() {
  if (!('setAppBadge' in self.navigator)) return
  try {
    const shown = await self.registration.getNotifications()
    await (shown.length ? self.navigator.setAppBadge(shown.length) : self.navigator.clearAppBadge())
  } catch {
    /* badging not allowed here */
  }
}

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  event.waitUntil(updateBadge())
  const target = new URL((event.notification.data && event.notification.data.url) || '/', self.location.origin)
  event.waitUntil(
    (async () => {
      const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true })
      const existing = windows.find((c) => new URL(c.url).origin === target.origin)
      if (existing) {
        await existing.focus()
        if (new URL(existing.url).pathname !== target.pathname && 'navigate' in existing) {
          await existing.navigate(target.href).catch(() => undefined)
        }
        return
      }
      await self.clients.openWindow(target.href)
    })(),
  )
})
