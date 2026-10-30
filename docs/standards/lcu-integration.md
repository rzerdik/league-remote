# LCU integration

The League Client Update API is **unofficial and changes with patches**. Everything we call is verified against a real
client first, recorded as sample JSON, and reached only through typed `LcuClient` methods.

## Safety

The rules in [safety.md](safety.md) apply. In short: only `GET` and `/help` against a live client, a write needs the
user's OK naming the exact request, never act on other players, never print the lockfile password.

## Verify an endpoint (read-only)

The client describes itself at `/help`. Use the helper (GET only, never prints the password):

```bash
node .claude/skills/verify-lcu-endpoint/lcu.mjs search "LobbyV2.*Invit"     # names matching a regex
node .claude/skills/verify-lcu-endpoint/lcu.mjs describe PostLolLobbyV2LobbyInvitations
node .claude/skills/verify-lcu-endpoint/lcu.mjs get /lol-lobby/v2/received-invitations
```

- Function names encode **method + path**: `DeleteLolLobbyV2Lobby` = `DELETE /lol-lobby/v2/lobby`. `describe` lists the
  arguments; look up the body type with `describe <TypeName>`.
- Events are WAMP topics: `OnJsonApiEvent` + the URI with `/` → `_` (see `LcuTopics`).
- `/help` says an endpoint *exists*, not how the client *uses* it. For write bodies copy what the client's own front-end
  sends (the `rcp-fe-lol-*` plugins) and cite it in the XML doc (`CreatePracticeToolLobbyAsync` is the example).
- Say in the PR description and the sample's `<summary>` how you verified it (`/help` name, read-only GET).
- No client running? Say so, work from names already in the code, and mark the result unverified.

## Adding an endpoint

1. `LcuClient`: one typed method in the right `// --- Section ---`. GETs return `JsonElement?`, writes use `SendAsync`.
2. Reader / mapper: the reader does the GET, the mapper turns the raw JSON into a contract type with the lenient
   `Str`/`Int`/`Arr` accessors and applies the privacy policy.
3. Events: add the WAMP topic to `LcuTopics` (and `LcuTopics.All`) if the state must update live, and route it in
   `LcuConnectionService.RefreshesFor`. Throttle churny slices (presence).
4. Anonymised sample JSON in `tests/LeagueRemote.Connector.Tests/Samples/`, plus mapper and command tests.
5. If the phone can trigger it: a whitelisted command, see [contracts.md](contracts.md).

## Client sounds and music

The phone plays **the League client's own sounds and music**, extracted by the connector from the user's installation
(`Plugins/rcp-fe-*/assets.wad`, see `ClientSoundCatalog` / `ClientSoundLibrary`). Nothing is copied into the repository.
For any moment where the client plays a sound, use that asset and the client's timing, found by reading the plugin JS
bundles. Synthesize only where the client has no sound, and note that in the catalog.
