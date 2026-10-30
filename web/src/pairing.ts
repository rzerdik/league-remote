import { HubMethods } from './contracts'

const CODE_RE = /^\d{6}$/

/**
 * Pull a 6-digit pairing code out of user input or a scanned QR payload.
 * Accepts a bare code ("123456", "123 456", "123-456") or a link carrying `?pair=123456`.
 */
export function extractPairCode(text: string): string | null {
  const t = text.trim()
  const compact = t.replace(/[\s-]/g, '')
  if (CODE_RE.test(compact)) return compact
  try {
    const p = new URL(t).searchParams.get(HubMethods.pairQueryParameter)?.trim()
    if (p && CODE_RE.test(p)) return p
  } catch {
    /* not an absolute URL */
  }
  const m = t.match(new RegExp(String.raw`[?&]${HubMethods.pairQueryParameter}=(\d{6})(?!\d)`))
  return m ? m[1]! : null
}

/**
 * Deep link: read `?pair=` from the current URL and remove it (history.replaceState) so a reload or
 * a home-screen shortcut never re-submits a used code. Returns the code, `'invalid'` for a malformed value,
 * or null when absent.
 */
export function takePairCodeFromUrl(): string | 'invalid' | null {
  const url = new URL(window.location.href)
  const raw = url.searchParams.get(HubMethods.pairQueryParameter)
  if (raw === null) return null
  url.searchParams.delete(HubMethods.pairQueryParameter)
  history.replaceState(history.state, '', url.pathname + url.search + url.hash)
  const code = raw.trim()
  return CODE_RE.test(code) ? code : 'invalid'
}
