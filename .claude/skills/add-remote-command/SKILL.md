---
name: add-remote-command
description: End-to-end checklist for adding a new whitelisted phone → League client command (CommandType) across contracts, whitelist, LcuClient, CommandExecutor, the TypeScript mirror, tests and docs. Use whenever the phone needs to trigger a new action in the client.
argument-hint: "[CommandName and what it does]"
---

Add the command **$ARGUMENTS**. Work through every step; skipping one breaks the C# ↔ TypeScript contract or the
whitelist. Standards: `docs/standards/contracts.md`, `docs/standards/csharp.md`, `docs/standards/testing.md`.

## 0. Verify the LCU call (read-only)

Run `/verify-lcu-endpoint` for each endpoint involved. Note the `/help` name, method, path, body. Copy the request body
the client's own front-end sends. Do **not** try the write against the live client; that's for the user to try later.

## 1. Contracts — `src/LeagueRemote.Contracts/Commands.cs`

- New `CommandType` member **at the end** of the enum, with `/// <summary>Requires <see cref="RemoteCommand.X"/>. …</summary>`.
- New parameters: optional nullable fields **appended** to the flat `RemoteCommand` record (`long? FooId = null`).
  Reuse existing fields when they mean the same thing (`ConversationId`, `SwapId`, …).

## 2. Whitelist — `src/LeagueRemote.Contracts/CommandWhitelist.cs`

Add `switch` arms that reject every invalid shape with a clear message (positive ids, length limits, no control chars).
Tests in `tests/LeagueRemote.Connector.Tests/Whitelist/`: one valid command + each rejection.

## 3. LCU — `src/LeagueRemote.Connector/Lcu/LcuClient.cs`

One typed method in the matching `// --- Section ---` (`Task FooAsync(…, CancellationToken ct) => SendAsync(...)`),
XML doc with what the client does and where the body comes from. Never a generic path/verb method.

## 4. Handler — `src/LeagueRemote.Connector/Commands/<Feature>Commands.cs`

`CommandExecutor` is the **one place that validates** a command (`CommandWhitelist.Validate`) and hands it to the
`CommandHandler` of its feature (`LobbyCommands`, `ChampSelectCommands`, `SocialCommands`, …). Add the command to the
handler's `Commands` dictionary with the slice to refresh after success (`RefreshSlice.State`, `Chat`, `Friends` or
`None`), and a `case CommandType.X:` in its `ExecuteAsync`: read the current state needed for preconditions
(`GetLobbyAsync`, `GetChampSelectSessionAsync`, …), return a short user-facing sentence when refused ("Not in a lobby.",
"Cancel the search first."), else call the `LcuClient` method and `return null`. A command that targets a player,
invitation or conversation re-reads the client's current list and refuses ids that aren't on it ("That invite is
gone."). A new feature gets a new `CommandHandler`, added in the `CommandExecutor` constructor.
Command tests: exact request list on success, each refusal.

## 5. TypeScript mirror — `web/src/contracts/commands.ts` + `commandWhitelist.ts`

- Add the name to the `CommandType` union with a `/** Needs … */` comment; new `RemoteCommand` fields (camelCase,
  `?: T | null`).
- Mirror the validation in `validateCommand` (`commandWhitelist.ts`).
- If the phone uses it now: call `sendCommand({ type: 'X', … })` (from `hub.ts`) inside `useBusy().run`, and handle it
  in the dev command handler in `web/src/dev/demo/index.ts` so the demo reacts.

## 6. Verify

```bash
dotnet test LeagueRemote.sln -p:SkipWebBuild=true
npm --prefix web run build
```

## 7. Docs

Add the command to `docs/commands.md`: command, LCU call(s), preconditions. Say in the PR how the endpoint was verified
and what still needs a live try. Then hand the diff to the `reviewer` subagent before committing.
