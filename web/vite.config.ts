import { createHash } from 'node:crypto'
import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { fileURLToPath, URL } from 'node:url'
import vue from '@vitejs/plugin-vue'
import { defineConfig, type Plugin } from 'vite'

/**
 * Stamps dist/sw.js with a hash of this build's output (VERSION, so old caches are dropped on activate) and the list
 * of its /assets/*.js|css files (ASSETS, precached on install so lazy chunks never go missing after an update).
 */
function serviceWorkerVersion(): Plugin {
  let outDir = 'dist'
  let base = '/'
  let assets: string[] = []
  let version = ''
  return {
    name: 'league-remote:sw-version',
    apply: 'build',
    configResolved(config) {
      outDir = resolve(config.root, config.build.outDir)
      base = config.base
    },
    generateBundle(_options, bundle) {
      const hash = createHash('sha256')
      const files = Object.keys(bundle).sort()
      for (const f of files) {
        const item = bundle[f]
        hash.update(f)
        hash.update(item.type === 'chunk' ? item.code : typeof item.source === 'string' ? item.source : Buffer.from(item.source))
      }
      version = hash.digest('hex').slice(0, 12)
      assets = files.filter((f) => f.startsWith('assets/') && /\.(js|css)$/.test(f)).map((f) => `${base}${f}`)
    },
    closeBundle() {
      const sw = resolve(outDir, 'sw.js')
      if (!version || !existsSync(sw)) return
      const src = readFileSync(sw, 'utf8')
      const out = src
        .replace(/const VERSION = '[^']*'/, `const VERSION = '${version}'`)
        .replace(/const ASSETS = \[\]/, `const ASSETS = ${JSON.stringify(assets)}`)
      if (out === src || !out.includes(version)) this.error('sw.js: VERSION/ASSETS placeholders not found')
      writeFileSync(sw, out)
    },
  }
}

/**
 * Where `npm run dev` proxies /hub and /client-sounds: the PC app on this machine by default. `?demo=` pages never
 * open the hub (src/hub.ts); LR_DEV_CONNECTOR points the proxy elsewhere (another PC, or a closed port to keep a dev
 * session away from the running PC app entirely).
 */
const devConnector = process.env.LR_DEV_CONNECTOR?.trim() || 'http://localhost:5080'

/** Shown in Settings › About. The release workflow sets LR_VERSION from the tag, so it matches the PC app's version. */
const appVersion = process.env.LR_VERSION?.trim() || 'dev'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), serviceWorkerVersion()],
  define: { __APP_VERSION__: JSON.stringify(appVersion) },
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    host: true, // reachable from a phone on the same LAN
    proxy: {
      // The SignalR hub.
      '/hub': {
        target: devConnector,
        ws: true,
        changeOrigin: true,
      },
      // The PC app's license and third-party notices (Settings › About).
      '/licenses.txt': {
        target: devConnector,
        changeOrigin: true,
      },
      // The League client's own sounds and music (manifest + files).
      '/client-sounds': {
        target: devConnector,
        changeOrigin: true,
      },
      // The League client's own rank emblems.
      '/rank-emblems': {
        target: devConnector,
        changeOrigin: true,
      },
    },
  },
})
