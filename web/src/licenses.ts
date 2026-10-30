import { within } from './lib/timeout'

/** The PC app's license and third-party notices as plain text, embedded in the exe and served by it. */
const LICENSES_PATH = '/licenses.txt'
const FETCH_TIMEOUT_MS = 8000

/** This build's release version (`dev` for local builds). */
export const appVersion = __APP_VERSION__

/** Dev only: `?demo=` pages answer from here instead of asking the PC (src/dev/demo/about.ts). */
export const devLicenses: { load: (() => Promise<string | null>) | null } = { load: null }

/** The licenses text, or null when the PC can't be reached in time. */
export async function loadLicenses(): Promise<string | null> {
  if (import.meta.env.DEV && devLicenses.load) return devLicenses.load()
  try {
    const r = await within(fetch(LICENSES_PATH, { cache: 'no-cache' }), FETCH_TIMEOUT_MS, null)
    if (!r?.ok) return null
    return await within(r.text(), FETCH_TIMEOUT_MS, null)
  } catch {
    return null
  }
}
