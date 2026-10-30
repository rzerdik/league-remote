# C# coding standards

Derived from the code in `src/` and `tests/`. When this file and the code disagree, match the code around you and fix
this file.

## Language and project settings

- .NET 10 SDK (`global.json`, `rollForward: latestFeature`). `Directory.Build.props` sets `LangVersion latest`,
  `Nullable enable`, `ImplicitUsings enable` and `TreatWarningsAsErrors` for every project. The connector targets
  `net10.0-windows` (WinForms tray). Contracts targets `net10.0` and must stay cross-platform.
- `.editorconfig`: 4-space indent for `.cs`, LF line endings, final newline, **file-scoped namespaces** (warning),
  `var` for built-in and apparent types, `System` usings first.
- NuGet versions live in `Directory.Packages.props`. No new package without a reason in the commit message. Prefer
  the BCL (the push stack uses BCL crypto).

## Shape of the code

- **Primary constructors** for services and DI-created classes:
  `internal sealed class PhoneHub(PhonePairing pairing, PhoneConnections connections, ...) : Hub`.
- **`sealed`** by default. **`internal`** for everything in the connector (tests see it through `InternalsVisibleTo`).
  Contracts types are `public`.
- **Records** for DTOs and snapshots (`public sealed record RemoteCommand(...)`, `internal sealed record LcuSnapshot(...)`),
  optional members as defaulted parameters (`int? QueueId = null`).
- **Collection expressions** (`[]`, `[.. items]`) and `switch` expressions with property patterns
  (`command.QueueId is not > 0`). See `CommandWhitelist.Validate`.
- **`TimeProvider` for time and delays** (`Task.Delay(delay, time, ct)`), so tests can fake the clock.
- **Static pure mappers** turn LCU JSON into contract types (`GameStateMapper`, `LobbyMapper`, `ChatMapper`, …): no I/O,
  no logging, `now` passed in. **Readers** do the I/O and hand raw `JsonElement`s to a mapper (`GameStateReader`,
  `ChatReader`, `FriendsReader`, `ProfileReader`). Stores hold the result (`SliceStore<T>` and one subclass per slice).
- Read LCU JSON with the lenient `JsonElementExtensions` (`Str`, `Int`, `Long`, `Bool`, `Dbl`, `Arr`, `Obj`,
  `Objects`): missing or wrongly typed properties give defaults, never exceptions. The LCU changes shape between patches.
- Section comments in long classes: `// --- Lobby -----------------------------------------------------------`.

## LCU access

- `LcuClient` is the **only** place that talks to the LCU, with **one typed method per whitelisted endpoint**. Never add
  a method that takes an arbitrary path or method. There is deliberately no proxy.
- GETs return `JsonElement?` (null on 404, meaning "not in that phase"). Writes go through `SendAsync` and throw
  `LcuRequestException` on a non-success status.
- When a request body copies what the client's own front-end sends, say so and where it came from
  (`_generatePracticeToolPayload in rcp-fe-lol-parties, patch 16.21`).
- Topics and phase names are constants (`LcuTopics`, `GameflowPhases`). Event routing is `LcuConnectionService.RefreshesFor`.
- See [lcu-integration.md](lcu-integration.md) for verifying endpoints.

## Errors and results

- `CommandExecutor.ExecuteAsync` **never throws**: every failure becomes `CommandResult(Success: false, Error)`, or the
  phone waits for a timeout. A feature handler (`LobbyCommands`, `ChampSelectCommands`, …) returns a short, user-facing
  sentence for a failed precondition ("Not in a lobby.", "Cancel the search first.").
- `LcuRefusals` turns an LCU error into a fixed sentence. League's own text stays in the log.
- Catch narrowly and with `when` filters. Never swallow `OperationCanceledException` for the caller's token.
- Transient LCU errors must not drop the LCU connection (`LcuErrors.IsTransient`).

## Logging and privacy

- Structured logging with message templates (`logger.LogWarning("Command {Type} failed: {Message}", ...)`), never string
  interpolation in the template.
- **Never log** chat message bodies, pairing tokens, the LCU password or lockfile contents, or other players' identifiers.
- Privacy policy for state (see the `GameStateMapper` doc comment): own lobby and own team only. From `theirTeam` only
  what the client shows (champion, hover, position, bans). Never enemy names, puuids or summoner ids.

## Comments

- `/// <summary>` on types and non-obvious members, explaining **why** and the LCU behaviour behind it. `CommandType`
  members document their required fields.
- Inline comments only for the non-obvious.
- Plain English, short sentences. No commented-out code.

## Contracts

`src/LeagueRemote.Contracts` is shared by the connector and, by hand, `web/src/contracts/`. Any change there is a
contract change: see [contracts.md](contracts.md).
