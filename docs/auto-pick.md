# Auto pick and ban

The PC can ban, pick and set summoner spells and a rune page for you when your turn starts. It runs on the PC, so it
works while your phone sleeps. The phone edits presets, and can pause or cancel.

It is **off by default** and has no presets. Nothing is written to the client until you turn it on.
Use it in line with Riot's rules.

## Where it acts

Only on your **own** in-progress ban or pick, during the ban and pick phase. The queue decides the group:

| Group | Queues | Does |
|---|---|---|
| `draft` | 400, 420, 440, 700, custom Summoner's Rift Draft Pick (3110), Tournament Draft (3130) | Bans, picks, spells, rune page |
| `blind` | 430, 480, 490, 870, 880, 890, custom Summoner's Rift Blind Pick (3100) | Picks, spells, rune page |
| `aram` | 450, 720, 2400 | Spells and rune page only |

Arena, Practice Tool, other custom games, a legacy custom lobby without a queue id and other modes are never
automated.

## Presets

One preset per group and role. Roles are `ANY`, TOP, JUNGLE, MIDDLE, BOTTOM and UTILITY. ARAM and custom games use
`ANY` only.

A preset holds up to 5 bans, up to 10 picks in order, two summoner spells (or none) and a rune page (optional).
Fields a role preset leaves empty come from the group's `ANY` preset. A preset with nothing set is removed.
The PC saves the rune page's name with the preset, so the phone can show it while League is closed.

## What happens in champ select

1. Spells and rune page are set once per champ select, only if they differ. After a position swap they follow the
   new role, unless you changed them.
2. When your ban or pick turn starts, the PC hovers the first valid choice. A choice is valid if it is available and
   not already banned, taken or hovered by a teammate.
3. It locks that choice after 3 seconds, or 1.5 seconds before the timer ends if that is sooner. With less time left it
   only hovers.
4. The phone shows "Auto-locking Ahri in 3 s" with **Cancel**.
5. If your choice becomes invalid before the lock, it hovers the next valid one and waits again.

Your own hover or lock always wins. Nothing is retried in a loop. If a rune page is gone or a spell is unknown, it logs
that and moves on.

## Pause and cancel

- **Pause for this game** stops all writes in the current champ select, or in the next one if sent before it starts.
  It ends when that champ select ends (the game starts or you dodge), when the client goes back to the home screen,
  when the League client disconnects, or when you resume.
- **Cancel** stops the pending auto ban or lock-in. The hover stays.
- A pending action is also cancelled if its preset is removed, no longer lists that champion, or your role changes to one
  without a preset.

Trades and pick-order swaps after locking are never touched.

## Storage

Presets are saved in `%LOCALAPPDATA%\league-remote\auto-pick.json`:

```json
{ "version": 1, "settings": { "enabled": true, "presets": [ { "queueGroup": "draft", "role": "ANY",
  "banChampionIds": [238], "pickChampionIds": [103, 99], "spell1Id": 4, "spell2Id": 14,
  "runePageId": 0, "runePageName": "" } ] } }
```

Writes are atomic. An unreadable file or an unknown version is moved to `auto-pick.json.corrupt-<time>` and the
defaults are used. Invalid presets are dropped on load.

## For contributors

The contract is `src/LeagueRemote.Contracts/AutoPick.cs`, mirrored in `web/src/contracts/autoPick.ts`. The rules live
in the `AutoPickState` XML docs and `AutoPickRules`. It is its own state slice (`AutoPickState`).

- It reuses the existing typed LCU calls for hover, lock, spells and rune page. It adds none.
- The settings commands work without a running client. Their rules are in [commands.md](commands.md).
- Timing uses `TimeProvider`, so tests run on a fake clock.
- The phone hides the feature until it receives an `AutoPickState`.
- Logs name actions and champion ids only, never player names.
