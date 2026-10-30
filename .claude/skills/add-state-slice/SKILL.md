---
name: add-state-slice
description: End-to-end checklist for a new state slice the connector sends to phones as its own message (like ChatState or FriendsState) — contract, connector store/mapper/refresh loop, broadcaster entry, PWA store and feature module, tests, demo. Use when a feature brings a new stream of LCU data with its own update rhythm.
argument-hint: "[Feature name, e.g. Friends]"
---

Add the **$ARGUMENTS** slice. First decide whether it needs to be a slice at all: data that changes with the game flow
belongs in `ClientState` (like invitations), and data that belongs to an existing slice extends it (like friend requests
in `FriendsState`). A new slice is for its own stream with its own events and rhythm.

A new slice is: **contract + store (`SliceStore<T>` subclass) + reader and mapper + refresh loop + one broadcaster entry +
web handler**. Copy a recent slice (friends: `Contracts/Friends.cs`, `State/FriendsStore.cs`, `State/FriendsReader.cs`,
`State/FriendsMapper.cs`) rather than inventing a new shape. Standards: `docs/standards/contracts.md`,
`docs/standards/lcu-integration.md`, `docs/standards/testing.md`.

## 0. Verify the LCU side (read-only)

`/verify-lcu-endpoint` for every GET and WAMP topic involved; record anonymised samples.

## 1. Contract — `src/LeagueRemote.Contracts/<Feature>.cs`

`<Feature>State` record (with `UpdatedAt` and a static `Empty()`), its item records, limits as constants. Add
`HubMethods.Client.<Feature>Updated` to `Hub.cs`, with an XML doc saying it is replayed on sign-in and never pushed.

## 2. Connector

- `LcuClient`: typed GETs in their own `// --- Section ---`.
- `State/<Feature>Mapper.cs`: static, pure, lenient accessors, privacy rules (nothing a player typed in logs).
- `State/<Feature>Reader.cs`: does the GETs and calls the mapper. Tests call it directly.
- `State/<Feature>Store.cs`: a `SliceStore<T>` subclass: latest value + version, fingerprint without `UpdatedAt` so
  identical rebuilds don't republish, `Changed` event, `Touch()` for `RefreshState`.
- `Lcu/LcuConnectionService.cs`: a `Request<Feature>Refresh()` on `ILcuSession`, its own `RefreshLoop` (debounce;
  throttle when events churn), reset to `Empty()` on disconnect. Add the WAMP topics to `LcuTopics` (and `All`) and
  route them in `RefreshesFor` to only this loop.
- `Program.cs`: register the store. `Server/PhoneBroadcaster.cs`: add the store to the slice table (it sends changes to
  signed-in phones and replays the current value on sign-in). `CommandExecutor`: `RefreshState` touches the store, and a
  handler's `Commands` entry names the slice to refresh after a command (add a `RefreshSlice` value if needed).
- No push unless it is planned (then a trigger in `Push/PushTriggers.cs`: transitions only, no typed text in the payload).

## 3. PWA — `web/src/`

`contracts/<feature>.ts` mirror (exported from `contracts/index.ts`) and the hub name in `contracts/hub.ts`; the slice in
`store.ts` (`Slices`, `SLICES`, a `store.<feature>` field and a `live<Feature>` computed) and in `SLICE_MESSAGES` in
`hub.ts` (it is cleared on unpair and disconnect with the others); a `<feature>.ts` module with the computeds the UI
needs; components under `components/<feature>/`; and a `?demo=<feature>` scenario in `dev/demo/`.

## 4. Tests and verify

`Samples/<Feature>Samples.cs` + `<Feature>/<Feature>Tests.cs` (built through the reader), store fingerprint behaviour,
the `RefreshesFor` routing, and a hub case in `Server/PhoneHubTests.cs` (sent to signed-in phones, replayed to a phone
that signs in later).

```bash
dotnet test LeagueRemote.sln -p:SkipWebBuild=true
npm --prefix web run build
```

Update `docs/architecture.md` if the slice list changes. Then the `reviewer` subagent.
