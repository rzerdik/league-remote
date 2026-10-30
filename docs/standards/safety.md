# Safety rules (every session, every agent, every subagent)

Development machines run a live League client with a real account. **Never affect anything outside the scope of the
task**, not yourself and not any subagent. When you start a subagent, pass these rules on or point it at this file.

## Processes and ports

- **No blanket kills.** No `taskkill /IM`, `Stop-Process -Name`, `pkill`, `killall`, and no piping a process list into
  a kill.
- **Stop only what you started**, by PID or exact name, and keep track of what you started (dev servers, test hosts).
  Other sessions may run in parallel.
- **Don't stop, restart or modify** the League client, a running connector, or anything listening on 5080, 5443 or your
  dev port. If a port is taken, use another (`--port <n> --strictPort`); don't free it.
- Don't touch `%LOCALAPPDATA%\league-remote\` (real pairings, certificates, CA). Tests use temp directories.

## Live League client

- **Read-only by default:** `GET` and `/help` only (see [lcu-integration.md](lcu-integration.md)). A write needs an
  explicit OK in chat that names the exact request.
- **Never act on other players** without asking: invites, friend requests, messages, swaps, trades, kicks.
- Never accept or decline, queue, lock in, dodge or leave a lobby on your own.
- Smoke-test live systems with read-only requests only. Anything with side effects: use a mock, or ask first.

## Secrets and privacy

- Never print, log or commit the lockfile password, pairing tokens, VAPID private keys, `.env` files or chat text.
- Recorded LCU samples are anonymised (names, Riot IDs, puuids, summoner ids, message text) before they are committed.

## Git

- Commit only to your own branch. Never push, merge into `main`, force-push, rebase shared branches, delete branches or
  worktrees, or use the shared stash unless the user asks.
