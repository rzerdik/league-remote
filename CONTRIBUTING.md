# Contributing

Thanks for helping. Bug reports, ideas and pull requests are welcome. For a big change, open an issue first.

## Setup

You need the .NET 10 SDK and Node 22 or later. The connector builds on Windows only. You do not need a League client
to develop or test: the client is always mocked.

```bash
dotnet build LeagueRemote.sln                  # also runs npm ci and npm run build in web/ when they are stale
dotnet test LeagueRemote.sln                   # add -p:SkipWebBuild=true if web/ did not change
npm --prefix web run build                     # tokens, vue-tsc type-check, vite build
npm --prefix web run dev -- --port 5179 --strictPort
```

Open `http://localhost:5179/?demo=<scenario>` to see a screen with fake data and no PC. The scenarios are the
`switch (scenario)` in `web/src/dev/demo/index.ts` plus `offline` and `pair` (for example `play`, `lobby`, `readycheck`,
`pick`, `game`, `chat`, `profile`, `autopick`). Check new UI at 375 px, in light and dark.

Running the real app uses ports 5080 and 5443. Do not start a second copy while one is running.

The app ships as one file, `LeagueRemote.exe`. Keep it that way: defaults belong in code, user settings in
`%LOCALAPPDATA%\league-remote\settings.json` (a new key also goes into `ConnectorSettings.DefaultJson` and the README).

## Where things are

| Path | What |
|---|---|
| `src/LeagueRemote.Contracts/` | Shared types, hub method names, `CommandType` and `CommandWhitelist`. |
| `src/LeagueRemote.Connector/` | The Windows app: League client access, state, commands, web server, tray. |
| `web/` | The phone app (Vue 3, Vite, TypeScript). |
| `tests/LeagueRemote.Connector.Tests/` | xUnit tests. |
| `docs/` | User docs and [coding standards](docs/standards/). |

How it fits together: [docs/architecture.md](docs/architecture.md).

## Rules

- **No raw LCU proxy, ever.** A new phone action is a new whitelisted `CommandType`, validated in
  `CommandWhitelist` and run by a typed `LcuClient` method. Commands that target a player, invite or chat only act on
  what the client lists right now.
- **Contracts live twice.** `src/LeagueRemote.Contracts/*.cs` and `web/src/contracts/*.ts` (one file per C# file,
  including `validateCommand`) are kept in sync by hand. See [docs/standards/contracts.md](docs/standards/contracts.md).
- **Verify LCU endpoints before using them.** The League client API is unofficial and changes. Check the endpoint
  against a real client with read-only requests, then record an anonymised sample in
  `tests/LeagueRemote.Connector.Tests/Samples/`. See [docs/standards/lcu-integration.md](docs/standards/lcu-integration.md).
- **Privacy.** Own lobby and team only. Never enemy names or puuids. Never log chat text, tokens or the League
  password. Keep message text out of push payloads. Samples must hold no real player names, Riot IDs, ids or messages.
- **Tests.** Every new command needs whitelist tests and executor tests. See [docs/standards/testing.md](docs/standards/testing.md).
- **Docs.** A new or changed command updates [docs/commands.md](docs/commands.md). A config key updates the README.
- **Dependencies.** Ask before adding one. NuGet versions live in `Directory.Packages.props`. Warnings are errors.
- **Never test against your own live account** with anything that writes or acts on other players.

Coding standards: [C#](docs/standards/csharp.md) | [TypeScript and Vue](docs/standards/typescript-vue.md) |
[testing](docs/standards/testing.md) | [LCU](docs/standards/lcu-integration.md) | [contracts](docs/standards/contracts.md).
UI design rules: [web/CLAUDE.md](web/CLAUDE.md).

## Branches and commits

- Branch from `main`: `feat/<name>`, `fix/<name>` or `ui/<name>`.
- Commit subjects are short plain sentences, capitalised, no trailing period, no `feat:` or `fix:` prefix.
  An area prefix with a colon is fine (`Lobby: Mode first, then Roles`). Keep them under about 72 characters.
- The body says what changed and why, and how you checked it.
- One logical change per commit. Put contract changes in their own commit when others depend on them.
- Before you open a pull request: `dotnet test LeagueRemote.sln` and `npm --prefix web run build` pass, and
  `git status` shows no secrets, `web/dist`, `publish/` or `.env`.

## Pull requests

Fill in the template. Add screenshots for UI changes. If you could not try something against a real client, say so.

## AI assistants

The repo has files for Claude Code: `CLAUDE.md` files, `.claude/agents`, `.claude/skills` and `.claude/hooks`. They are
the same rules as here, written for an assistant. You do not need them.
