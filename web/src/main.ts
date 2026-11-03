import '@fontsource-variable/inter'
import { createApp } from 'vue'
import './styles/tokens.css'
import './styles/components.css'
import App from './App.vue'
import { installClock } from './clock'
import { isDemo } from './lib/demoMode'
import { installGlassAmount } from './lib/glassAmount'
import { noteChunkFailure } from './lib/lazy'
import { installPressStates } from './lib/pressStates'
import { installUpdateReload } from './lib/reload'
import { installPushLifecycle } from './push'
import { installHubLifecycle } from './hub'
import { installSound } from './sound/install'

// Every global listener and timer starts here, so importing a module (the demo, a test) has no effects of its own.
installPressStates()
installGlassAmount()
installClock()
installSound()
installHubLifecycle()
installPushLifecycle()
// No pinch zoom, like a native app: iOS ignores user-scalable=no in the viewport meta, but not a cancelled gesture.
for (const type of ['gesturestart', 'gesturechange']) document.addEventListener(type, (e) => e.preventDefault(), { passive: false })
// Dev-only fake client states for reviewing screens without a PC: ?demo=play|lobby|queue|readycheck|pick|ban|final|game…
// (`import.meta.env.DEV` here too, so a build drops the demo chunk.)
if (import.meta.env.DEV && isDemo) await import('./dev/demo')
createApp(App).mount('#app')

/**
 * A lazy chunk couldn't be fetched (an older build's chunk gone after an update, or no network). Only recorded here:
 * the import resolves to nothing and lazy.ts takes it from there — a retry of the chunk's URL (noted here) with a new
 * query, and only once a screen's load has failed for good a reload (reload.ts: throttled, never during a ready
 * check, the draft or a game) or "Couldn't Load". A background prefetch never reloads anything.
 */
window.addEventListener('vite:preloadError', (event) => {
  event.preventDefault()
  noteChunkFailure((event as Event & { payload?: unknown }).payload)
})

if (import.meta.env.PROD && 'serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register(`${import.meta.env.BASE_URL}sw.js`).catch((e) => {
      console.warn('Service worker registration failed', e)
    })
  })
  installUpdateReload()
}
