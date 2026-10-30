# Contracts, state slices and the command whitelist

The phone and the connector share one set of types. They live in two places that you keep in sync **by hand**:

| C# (`src/LeagueRemote.Contracts/`) | TypeScript mirror (`web/src/contracts/`, re-exported by `index.ts`) |
|---|---|
| `Hub.cs`: `HubMethods` (route, method names) | `hub.ts`: `HubMethods` (`path`, `server`, `client`) |
| `Commands.cs`: `CommandType`, `RemoteCommand`, enums, constants, `CommandResult` | `commands.ts`: `CommandType` union, `RemoteCommand`, constant arrays |
| `CommandWhitelist.cs`: `Validate` | `commandWhitelist.ts`: `validateCommand` (the phone's early check) |
| One file per state slice: `State.cs` (`ClientState`), `Chat.cs`, `Friends.cs`, … | The same file name in lower camel case: `state.ts`, `chat.ts`, `friends.ts`, … |
| `Pairing.cs` | `pairing.ts`: `PairingGrant` |

`clientSounds.ts` mirrors the sound manifest from `ClientSoundCatalog.cs`; it has no file in `Contracts/`.

## State slices

The connector publishes several independent slices instead of one big state:

- **`ClientState`** (`StateUpdated`): game flow: ready check, lobby, champ select, runes, in-game clock, and small things
  tied to it (received invitations). Push triggers read it.
- **Feature slices** (`ChatState`, `FriendsState`, …): each has its own `XUpdated` message, store and refresh loop, so a
  chatty slice (presence) does not rebuild the others. They are replayed to a phone when it signs in and **not
  pushed**. One opt-in exception: a new friend request (name only, `PushPreferences.FriendRequest`).

Add a slice only for a stream with its own update rhythm. Otherwise extend an existing one. Steps: `/add-state-slice`.

## Rules

- SignalR's JSON protocol uses **camelCase** property names and enums as **string names**. The TS mirror uses the same
  spelling.
- `RemoteCommand` stays a **flat envelope**: add optional, nullable parameters at the end, don't nest objects, reuse a
  field when it means the same thing. Missing fields mean null.
- **Whitelist, not proxy.** A new phone action is a new `CommandType` member, validated in `CommandWhitelist.Validate`
  and run by a typed `LcuClient` method in its feature's handler (`Commands/`). `CommandExecutor` is the one place that
  validates. Never a "call this endpoint" command. A command that targets another player or object acts only on what
  the client lists right now: the handler re-reads it from the LCU and refuses anything else ("That invite is gone.").
  Never trust an id because the phone sent it.
- Document required fields on the enum member (`/// <summary>Requires <see cref="RemoteCommand.QueueId"/>.</summary>`)
  and in the TS union (`/** Needs conversationId. */`).
- A new hub message goes into `HubMethods` on both sides.
- Keep `docs/commands.md` in sync when you add or change a command.
- Put contract changes in their own small commit when other branches depend on them.

## Checklist for a contract change

1. C# type in `src/LeagueRemote.Contracts/` (with XML docs).
2. Validation in `CommandWhitelist.Validate`, plus whitelist tests in `tests/LeagueRemote.Connector.Tests/Whitelist/`.
3. TS mirror in `web/src/contracts/` (types and `validateCommand`).
4. `dotnet test LeagueRemote.sln` and `npm --prefix web run build` (type-checks the mirror).
5. `docs/commands.md`.

`/add-remote-command` and `/add-state-slice` walk through all of it.
