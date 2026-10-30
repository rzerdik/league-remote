# src/ — .NET (connector and contracts)

@../docs/standards/csharp.md
@../docs/standards/contracts.md

## Where things go

- **New LCU read**: `Connector/Lcu/LcuClient.cs` (typed GET) → raw JSON into the slice it belongs to: the game state
  (`State/GameStateReader.cs` fills `LcuSnapshot`, `GameStateMapper` maps it) or a feature slice's reader and mapper
  (`ChatReader`/`ChatMapper`, `FriendsReader`/`FriendsMapper`, `ProfileReader`/`ProfileMapper`) → contract type in that
  slice's file in `Contracts/`. Live updates: WAMP topic in `Lcu/LcuTopics.cs` (and `LcuTopics.All`), routed in
  `LcuConnectionService.RefreshesFor` (which loops it wakes).
- **New state slice** (a separate stream like chat or friends): contract in `Contracts/<Feature>.cs`, a store
  (`SliceStore<T>` subclass) in `State/`, reader and mapper, its own `RefreshLoop` in `LcuConnectionService`, one entry
  in the `Server/PhoneBroadcaster` slice table, `XUpdated` in `Hub.cs`, and the web handler. Small additions ride an
  existing slice. Use `/add-state-slice`.
- **New phone action**: `Contracts/Commands.cs` (`CommandType` + flat `RemoteCommand` field) → `Contracts/CommandWhitelist.cs`
  → typed write in `LcuClient` → an entry and a `case` in the feature's `CommandHandler` in `Commands/`
  (`LobbyCommands`, `ChampSelectCommands`, `SocialCommands`, …). A precondition returns a short user-facing string;
  `LcuRefusals` words LCU errors. `CommandExecutor` is the single place that validates. Use `/add-remote-command`.
- **New hub method / push trigger**: `Contracts/Hub.cs` → `Server/PhoneHub.cs`; push triggers in `Push/PushTriggers.cs`
  (transitions between two connected states only, never replays).

## Gotchas

- `LeagueRemote.Connector` targets `net10.0-windows`; Contracts targets `net10.0` and must stay cross-platform.
- Building the connector builds `web/` (MSBuild runs `npm ci` / `npm run build`); `-p:SkipWebBuild=true` skips it.
- The connector's `appsettings.json` is read from the exe's folder, not the working directory.
- LCU GETs return null on 404 — that means "not in that phase", not an error.
- Each slice has its own refresh loop: one slice's events must not rebuild another (`RefreshesFor` decides).
- Hubs are transient: no instance fields. Anything that outlives a call goes through `IHubContext<PhoneHub>` or a singleton.
- Store `Changed` events fire on LCU threads: handlers only signal, they never await a hub send.
- Push notifications only for transitions between two connected states, never for replays, and never with anything a
  player typed (names and queue only).
- Live-client verification rules: [docs/standards/lcu-integration.md](../docs/standards/lcu-integration.md).
