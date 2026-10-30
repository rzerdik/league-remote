# Allowed commands

This is the whole list of things the phone can ask the PC to do. The PC checks each command with `CommandWhitelist`
before it runs it (the phone checks too, to answer faster). There is no way to send any other request to the League
client. See [security.md](security.md).

Refusals come back to the phone as a short sentence, for example "Not in a lobby." League's own error text stays in
the PC's log.

## Game flow

| Command | LCU call | Rule |
|---|---|---|
| `AcceptReadyCheck`, `DeclineReadyCheck` | `POST /lol-matchmaking/v1/ready-check/accept` or `decline` | |
| `CreateLobby(queueId)` | `POST /lol-lobby/v2/lobby` | |
| `CreatePracticeTool` | `GET /lol-game-queues/v1/queues`, then `POST /lol-lobby/v2/lobby` | Finds the `PRACTICETOOL` queue and sends the same private custom-lobby config as the client. |
| `StartMatchmaking`, `CancelMatchmaking` | `POST` or `DELETE /lol-lobby/v2/lobby/matchmaking/search` | Custom games start the same way. A legacy custom lobby without a queue id uses `POST /lol-lobby/v1/lobby/custom/start-champ-select`. |
| `SetPositionPreferences(firstPosition, secondPosition)` | `PUT /lol-lobby/v2/lobby/members/localMember/position-preferences` | Only in a lobby with a role selector, and not while searching. `FILL` first sends `UNSELECTED` second. |
| `LeaveLobby` | `DELETE /lol-lobby/v2/lobby` | Only in a lobby, not while searching. The phone asks first if you lead a party. |
| `DodgeChampSelect` | `POST /lol-lobby-team-builder/champ-select/v1/session/quit` | Only in champ select. Not in custom games, not once the game is starting. League's lockout and ranked LP loss apply. The phone asks first. |
| `PlayAgain` | `POST /lol-lobby/v2/play-again` | Only after a game, and only if League offers it. |
| `LeavePostGame` | `POST /lol-end-of-game/v1/state/dismiss-stats` | Only after a game. |

## Champion select

| Command | LCU call | Rule |
|---|---|---|
| `HoverChampion(championId, actionId)` | `PATCH /lol-champ-select/v1/session/actions/{id}` | Your current pick or ban action, also before your turn. Only when `actionId` is that action. |
| `LockChampion(championId, actionId)` | `PATCH .../actions/{id}` with `completed: true` | Only on your turn and when `actionId` is your current action. Confirmed against the session. Falls back to `POST .../actions/{id}/complete`. |
| `RequestSwap(swapKind, swapId)` | `POST /lol-champ-select/v1/session/{pick-order-swaps\|position-swaps\|trades}/{id}/request` | Only if the session lists it as `AVAILABLE`. |
| `AcceptSwap`, `DeclineSwap(swapKind, swapId)` | `POST .../{id}/accept` or `decline` | Only if `RECEIVED`. |
| `CancelSwap(swapKind, swapId)` | `POST .../{id}/cancel` | Only if `SENT`. |
| `BenchSwap(championId)` | `POST /lol-champ-select/v1/session/bench/swap/{championId}` | ARAM-style benches. Only while the client shows the bench, only a champion on it, and only one you can play. |
| `Reroll` | `POST /lol-champ-select/v1/session/my-selection/reroll` | Same rules as the client's button: rerolls left, finalization, no trade in progress. |
| `SetSummonerSpells(spell1Id, spell2Id)` | `PATCH /lol-champ-select/v1/session/my-selection` | |
| `SelectRunePage(runePageId)` | `PUT /lol-perks/v1/currentpage` | |
| `SelectSkin(skinId)` | `PATCH /lol-champ-select/v1/session/my-selection` | Only after you lock in, only if the mode allows skins, and only a skin or chroma that `GET /lol-champ-select/v1/skin-carousel-skins` lists as unlocked. Your current skin sends nothing. |

## Profile and search

| Command | LCU call | Rule |
|---|---|---|
| `RefreshState` | none | Sends the current state again. |
| `RefreshProfile` | `GET` current summoner, ranked stats, champion mastery (+ score), match history | At most every 30 s. Without a running client it sends the profile it has. |
| `LoadMatchDetails(gameId[, puuid])` | `GET /lol-match-history/v1/games/{gameId}` | Only a game in the history that was sent to the phone. Cached, up to 3 games. With `puuid`, a game of that player's loaded profile. |
| `LoadPlayerProfile(puuid)` | `GET` summoner, ranked stats, mastery and match history by puuid | See the access rules below. |
| `SearchPlayer(gameName, tagLine)` | `GET /lol-summoner/v1/alias/lookup`, then the same reads as `LoadPlayerProfile` | Riot ID is checked like Add Friend. Your own ID is refused. The player stays viewable for 15 minutes. |

**Access rules for other players' profiles.** A profile opens only for:

- a friend, a member of your lobby, or a teammate in champ select whose name League shows;
- a teammate the PC itself named in a game it sent to the phone (a puuid only ever goes out next to a teammate's name);
- a player a search or such a game opened less than 15 minutes ago.

Anything else is refused: "You can only view friends, your party and your team." Profiles are kept for 15 minutes
(up to 3 players) and reread at most every 30 s. Enemy names and puuids are dropped on the PC.

## Social

These act only on what the client lists right now. The PC reads the list again before it acts and refuses anything
else ("That invite is gone.", "Only friends can be invited.").

| Command | LCU call |
|---|---|
| `SendChatMessage(conversationId, body)` | `POST /lol-chat/v1/conversations/{id}/messages` |
| `MarkConversationRead(conversationId)` | `PUT` then `DELETE /lol-chat/v1/conversations/active` (how the client marks a chat read) |
| `OpenDirectConversation(puuid)` | `POST /lol-chat/v1/conversations` (friends only) |
| `SendInvitation(puuid)` | `POST /lol-lobby/v2/lobby/invitations` (you lead the lobby, not searching, party not full) |
| `AcceptInvitation`, `DeclineInvitation(invitationId)` | `POST /lol-lobby/v2/received-invitations/{id}/accept` or `decline` |
| `RemoveFriend(friendId)` | `DELETE /lol-chat/v1/friends/{id}` |
| `SendFriendRequest(gameName, tagLine)` | `POST /lol-chat/v2/friend-requests` |
| `AcceptFriendRequest(puuid)` | `PUT /lol-chat/v2/friend-requests/{puuid}` |
| `DeclineFriendRequest`, `CancelFriendRequest(puuid)` | `DELETE /lol-chat/v2/friend-requests/{puuid}` |

## Auto pick and ban

These need no running client. They are saved on the PC. See [auto-pick.md](auto-pick.md).

| Command | Effect |
|---|---|
| `SetAutoPickEnabled(enabled)` | Turns it on or off. Off cancels a pending auto action. |
| `SetAutoPickPreset(queueGroup, role, banChampionIds, pickChampionIds, spell1Id, spell2Id, runePageId)` | Saves one preset. Up to 5 bans and 10 picks. Both spells or neither. Nothing set removes the preset. |
| `PauseAutoPick`, `ResumeAutoPick` | No automation for this champ select (or the next, if sent before it starts). |
| `CancelAutoPickAction(actionId?)` | Does not complete the pending auto ban or lock-in. The hover stays. |

## Keeping this list true

Any new or changed command updates this file, `CommandWhitelist`, the TypeScript mirror and the tests in the same
change. Steps: [CONTRIBUTING.md](../CONTRIBUTING.md).
