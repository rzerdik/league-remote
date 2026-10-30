---
name: verify-lcu-endpoint
description: Verify a League client (LCU) endpoint or WAMP event read-only against the running client's /help and record its real JSON shape as an anonymised test sample. Use before adding or changing any LCU call.
argument-hint: "[endpoint path or /help name, e.g. /lol-lobby/v2/received-invitations]"
allowed-tools: Bash(node .claude/skills/verify-lcu-endpoint/lcu.mjs *), Read, Grep
---

Verify `$ARGUMENTS` against a running League client. **Read-only**: the helper only sends `GET`s and never
prints the lockfile password. Do not send any write to the live client from this skill; never act on other players.

1. **Find it in `/help`.** Function names encode method + path (`DeleteLolLobbyV2Lobby` = `DELETE /lol-lobby/v2/lobby`;
   path parameters drop out of the name). Search with a regex:

   ```bash
   node .claude/skills/verify-lcu-endpoint/lcu.mjs search "<NamePartRegex>"
   ```

   Exit code 2 = no lockfile (client not running): say so, and continue from names already in the code,
   marking the result **unverified**. Non-default install: set `LEAGUE_INSTALL_PATH`.

2. **Read its signature**: `node .claude/skills/verify-lcu-endpoint/lcu.mjs describe <FunctionName>` — arguments
   (path params, body type). Describe the body type the same way (`describe <TypeName>`).

3. **See the real shape** (GET endpoints only): `node .claude/skills/verify-lcu-endpoint/lcu.mjs get <path>`.
   404 usually means "not in that phase" (no lobby, not in champ select) — ask the user to get into that state if the
   shape matters, rather than creating it yourself.

4. **Events**: the WAMP topic is `OnJsonApiEvent` + the path with `/` → `_` (see `LcuTopics`). Check that the
   path exists in `search` output under `functions`/`events`.

5. **Record**: trim the JSON to the fields the code reads, **anonymise** (names → `Me`/`Friend`/`Enemy`, puuids →
   `me-puuid`…, summoner ids → small fake numbers, message bodies → neutral text), and add it to
   `tests/LeagueRemote.Connector.Tests/Samples/` (the `<Feature>Samples.cs` of its feature) with a `/// <summary>`
   naming the endpoint.
   Never paste the unredacted response into a commit, a note or a message.

6. **Write down the result** in the PR description and the sample's `<summary>`: "verified in the client's `/help`:
   `<Name>`" and/or "verified read-only against a live client (patch X)". Write bodies: copy what the client's own
   front-end sends and cite it; trying a write live needs the user's explicit OK first.

Report: the `/help` names found, method + path, arguments/body type, whether a live GET was seen, and the sample added.
