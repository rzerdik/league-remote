# Testing

```bash
dotnet test LeagueRemote.sln                          # everything (also builds web/ via npm, see below)
dotnet test LeagueRemote.sln -p:SkipWebBuild=true     # faster when web/ didn't change
dotnet test tests/LeagueRemote.Connector.Tests --filter "FullyQualifiedName~CommandExecutorTests"
```

Building the connector (and so its test project) runs `npm ci` when `web/node_modules` is missing and `npm run build`
when `web/dist` is stale. A fresh worktree's first test run takes longer.

**The real League client is never needed for tests.** A test that needs a real install is opt-in through an
environment variable and skips otherwise (`LEAGUEREMOTE_REAL_INSTALL` for the client-sounds catalog).

## Project

One project, `tests/LeagueRemote.Connector.Tests` (`net10.0-windows`), with a folder per feature: `Whitelist/`, `Lcu/`,
`GameState/`, `ChampSelect/`, `Commands/`, `Chat/`, `Friends/`, `Profile/`, `PostGame/`, `LiveGame/`, `AutoPick/`,
`ClientSounds/`, `Https/`, `Server/`, `Pairing/`, `Push/`, `Logging/`. Shared helpers are in `Fakes/` and recorded JSON in `Samples/`.

The `Server/` tests start the real phone server with `PhoneServerHost`: Kestrel on loopback, random ports, a temp data
folder, a fake command runner and a fake push sender. Only `LanHttpMiddlewareTests` uses an in-memory test server.

## Conventions

- **xunit** (`[Fact]`, `[Theory]` + `[InlineData]`). `using Xunit` is global. Test classes are `sealed`. No shared
  mutable statics: each test builds what it needs.
- **Test names are sentences** with underscores that describe behaviour:
  `LockChampion_falls_back_to_the_complete_endpoint`, `Stale_champ_select_room_of_an_earlier_game_is_ignored`.
- **Recorded LCU JSON** lives in `Samples/<Feature>Samples.cs` (`GameStateSamples`, `FriendsSamples`, `ProfileSamples`, …)
  as `const string` raw literals (`"""`), trimmed to the fields we read, each with a `/// <summary>` saying which
  endpoint it is and what is special about it. A samples class can offer a ready fake with the slice's routes
  (`FriendsSamples.Handler()`). Record from a real client (read-only GET), then **anonymise**: replace names, Riot IDs,
  puuids, summoner ids, notes and message bodies with obvious placeholders (`me-puuid`, `Friend`, `Enemy`,
  `aaaa0001-…`). Vary a sample inline with `.Replace(...)` instead of copying it.
- **One fake League client:** `FakeLcu` (`Fakes/FakeLcu.cs`). GETs answer from `Routes` (404 otherwise), writes answer
  204 unless listed in `Failures` with a status and LCU message, and every request is recorded as
  `"METHOD /path body"` in `Requests`. A fake with its own behaviour derives from it (`DraftLcu` for a live draft) or
  uses `Intercept`. `FakeLcuSession` wraps it in an `ILcuSession` and runs a command
  (`FakeLcuSession.ExecuteAsync`). `TestLcu` has helpers (parse a sample, build a state or profile from a fake).
  Assert on the **exact request list** for commands: that is the contract with the client. Read a whole slice through
  its real reader (`TestLcu.BuildStateAsync`, `FriendsReader.ReadAsync`) instead of calling a mapper piecemeal.
- One test class per feature: `<Feature>Tests.cs` (mapper and commands), `<Feature>WhitelistTests.cs` when a feature adds
  several commands. A new hub message gets a case in `Server/PhoneHubTests.cs` (sent to signed-in phones, replayed to a
  phone that signs in later).
- Time: `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`). No sleeps. `ChampSelectLock` takes the
  `TimeProvider` and `ConfirmDelay`: `FakeLcuSession.InstantLock(time)` sets it to zero.
- Logging: `NullLogger<T>.Instance`.
- Every new command: whitelist tests (valid case and each rejection), command tests (happy-path request list and each
  refusal message), and mapper tests for any new state slice. Privacy rules get tests too (enemy names never leak).
- Network tests bind only to `127.0.0.1` on random ports and delete their temp folders.

## Web

`npm --prefix web run build` (type-check) plus a look at the affected `?demo=` scenarios at 375 px, in light and dark.
