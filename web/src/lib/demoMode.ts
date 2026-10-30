/**
 * Dev only: a `?demo=` page (src/dev/demo/) plays the PC itself. It never opens the hub connection, because
 * `npm run dev` proxies /hub to the connector on this machine: a demo must not sign in, redeem codes or keep a socket
 * open there. Always false in a build.
 */
export const isDemo = import.meta.env.DEV && location.search.includes('demo=')
