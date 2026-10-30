#!/usr/bin/env node
// Read-only access to the running League client (LCU) for verifying endpoints and recording sample JSON.
// It can only send GET requests: there is deliberately no way to POST/PUT/PATCH/DELETE from here.
// The LCU password from the lockfile is never printed.
//
//   node lcu.mjs search <regex>        function/event names in the client's /help matching <regex> (case-insensitive)
//   node lcu.mjs describe <Name>       full /help entry of one function, event or type (e.g. DeleteLolLobbyV2Lobby)
//   node lcu.mjs get </lol-.../path>   GET an endpoint and print its JSON (record samples from this, then anonymise)
//
// Install folder: LEAGUE_INSTALL_PATH, default C:\Riot Games\League of Legends (same default as Lcu:InstallPath).
import { readFileSync } from 'node:fs'
import { request } from 'node:https'
import { join } from 'node:path'

const [cmd, arg] = process.argv.slice(2)
if (!['search', 'describe', 'get'].includes(cmd) || !arg) {
  console.error('usage: node lcu.mjs search <regex> | describe <Name> | get </path>')
  process.exit(64)
}

const installPath = process.env.LEAGUE_INSTALL_PATH || 'C:\\Riot Games\\League of Legends'
let lockfile
try {
  lockfile = readFileSync(join(installPath, 'lockfile'), 'utf8').trim()
} catch {
  console.error(`No lockfile in ${installPath}: the League client is not running (or set LEAGUE_INSTALL_PATH).`)
  process.exit(2)
}
const [, , port, password] = lockfile.split(':')
const auth = 'Basic ' + Buffer.from(`riot:${password}`).toString('base64')

function get(path) {
  return new Promise((resolve, reject) => {
    const req = request(
      // The LCU uses a self-signed certificate; accepted only for this loopback request (as the connector does).
      { host: '127.0.0.1', port: Number(port), path, method: 'GET', headers: { Authorization: auth, Accept: 'application/json' }, rejectUnauthorized: false },
      (res) => {
        let body = ''
        res.setEncoding('utf8')
        res.on('data', (c) => (body += c))
        res.on('end', () => resolve({ status: res.statusCode, body }))
      },
    )
    req.on('error', reject)
    req.end()
  })
}

function parse(body) {
  try {
    return JSON.parse(body)
  } catch {
    return body
  }
}

if (cmd === 'get') {
  if (!arg.startsWith('/')) {
    console.error('path must start with /')
    process.exit(64)
  }
  const { status, body } = await get(arg)
  console.log(`HTTP ${status}`)
  console.log(JSON.stringify(parse(body), null, 2))
} else if (cmd === 'describe') {
  const { status, body } = await get(`/help?target=${encodeURIComponent(arg)}&format=Full`)
  console.log(`HTTP ${status}`)
  console.log(JSON.stringify(parse(body), null, 2))
} else {
  const { status, body } = await get('/help')
  const help = parse(body)
  if (status !== 200 || typeof help !== 'object') {
    console.error(`HTTP ${status}: ${String(body).slice(0, 300)}`)
    process.exit(1)
  }
  const re = new RegExp(arg, 'i')
  for (const section of ['functions', 'events', 'types']) {
    const entries = Object.entries(help[section] ?? {}).filter(([name]) => re.test(name))
    if (!entries.length) continue
    console.log(`# ${section} (${entries.length})`)
    for (const [name, description] of entries) console.log(`${name}${description ? ` — ${String(description).slice(0, 160)}` : ''}`)
  }
}
