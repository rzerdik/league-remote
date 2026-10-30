# League Remote

A phone remote for the League of Legends client: accept ready checks, queue, pick/ban, swaps, runes, chat.
One Windows tray app (.NET 10) that talks to the League client and serves a Vue 3 PWA to paired phones.
The LCU API is unofficial and changes with patches. Users: `README.md`. Contributors: `CONTRIBUTING.md`. Plan: `ROADMAP.md`.

## Safety — read first

@docs/standards/safety.md

## Map

| Path | What |
|---|---|
| `src/LeagueRemote.Contracts/` | Shared DTOs, hub method names, **`CommandType` + `CommandWhitelist`** |
| `src/LeagueRemote.Connector/` | The app: LCU access (`Lcu/`), state slices (`State/`), `Commands/CommandExecutor`, web server + hub (`Server/`), pairing, push, local CA (`Https/`), client sounds, auto pick, tray |
| `web/` | Vue 3 + Vite PWA with its own `lr-` design system — see [web/CLAUDE.md](web/CLAUDE.md) |
| `tests/LeagueRemote.Connector.Tests/` | xUnit, the only test project (LCU always mocked) |
| `docs/` | User docs and `docs/standards/` (coding standards, below) |

## Architecture

One process. It reads the League client through whitelisted calls, maps the JSON into privacy-filtered **state
slices**, and sends them to paired phones over SignalR. Phones send commands from a fixed list; the executor
validates each one and runs a typed `LcuClient` method. Details: [docs/architecture.md](docs/architecture.md).

**There is no raw LCU proxy, ever.** A new phone action is a new whitelisted `CommandType` and a typed `LcuClient` method.

Contracts exist twice, kept in sync by hand: `src/LeagueRemote.Contracts/*.cs` and `web/src/contracts/*.ts` (one file
per C# file, with `validateCommand` in `commandWhitelist.ts`). See [docs/standards/contracts.md](docs/standards/contracts.md).

## Commands

```bash
dotnet build LeagueRemote.sln                        # also runs npm ci/build in web/ when stale
dotnet test LeagueRemote.sln                         # add -p:SkipWebBuild=true when web/ didn't change
npm --prefix web run build                           # tokens + vue-tsc type-check + vite build
npm --prefix web run dev -- --port 5179 --strictPort # review screens at /?demo=<scenario>
```

Demo scenarios: the `switch (scenario)` in `web/src/dev/demo/index.ts` plus `offline` and `pair` (for example `play`,
`lobby`, `readycheck`, `pick`, `game`, `chat`). Check new UI at 375 px, light and dark.

Don't run the connector yourself while another copy may be running: both want ports 5080/5443. Ask first.
Release build: `/publish-connector`.

## Rules

- Verify before saying done: `dotnet test` for C#, `npm --prefix web run build` for web, a demo scenario for UI.
- Match the code around you. Standards: [C#](docs/standards/csharp.md) · [TypeScript/Vue](docs/standards/typescript-vue.md) ·
  [testing](docs/standards/testing.md) · [LCU integration](docs/standards/lcu-integration.md) ·
  [contracts](docs/standards/contracts.md). UI rules: [web/CLAUDE.md](web/CLAUDE.md).
- Every new LCU path: verified via `/help` (`/verify-lcu-endpoint`), anonymised sample JSON in
  `tests/LeagueRemote.Connector.Tests/Samples/`, tests.
- Keep `docs/commands.md` and the README config table accurate. Update `ROADMAP.md` in the same change as the work it describes.
- Privacy: own lobby/team only; never enemy names/puuids; never log chat bodies, tokens or the LCU password; keep
  message text out of push payloads.
- Branches `feat/…`, `fix/…`, `ui/…`; plain-English commit subjects without prefixes ([CONTRIBUTING.md](CONTRIBUTING.md)).
  Commit to your branch; never push or merge into `main` unless asked.

## Agents and skills

Subagents (`.claude/agents/`): `lcu-integrator`, `web-ui`, `test-writer`, `reviewer`. Skills (`.claude/skills/`):
`/add-remote-command`, `/add-state-slice`, `/verify-lcu-endpoint`, `/publish-connector`.
Pass the safety rules to every subagent you start.
