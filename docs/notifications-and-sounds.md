# Notifications and sounds

## Push notifications

The PC sends Web Push messages so your phone can alert you while the app is in the background.

- **Android:** any modern browser.
- **iPhone and iPad:** only the app added to the Home Screen, iOS 16.4 or later.
- The PC needs internet access to reach the push services (Google, Mozilla, Apple).

| Trigger | Notification | Default |
|---|---|---|
| Ready check starts | "Match found". Stays until tapped. | on |
| Champ select starts | "Champion select" | off |
| Your pick or ban turn starts | "Your turn to pick" or "Your turn to ban" | on |
| The game has loaded | "Game started" | on |
| A new lobby invitation | "Game invite" | on |
| A new friend request | "Friend request" | off |

Choose which ones you get in Settings, Notifications. There is also **Send Test Notification**.

Rules:

- A push fires only when the state changes between two connected states. Reconnects, restarts and refreshes never
  repeat one.
- A ready check notifies at most every 3 s, a game start at most every 60 s. Friend requests that are already open when
  the app starts do not notify.
- No push is sent while the phone has the app open.
- Messages hold names and queue only. Never chat text.

Technical notes:

- Subscriptions must be `https` URLs of the Google, Mozilla, Apple or Windows push services. At most 5 on the PC; the oldest is
  dropped. One is removed when the push service says it is gone.
- The key pair is in `vapid.dpapi` (DPAPI-protected) and the subscriptions in `push-subscriptions.json` in the data folder.
  A phone can change, remove or test-push only the subscription it registered.
- The hub methods are in `src/LeagueRemote.Contracts/Hub.cs`. The service worker is `web/public/sw.js`.
- Sending uses [Lib.Net.Http.WebPush](https://github.com/tpeczek/Lib.Net.Http.WebPush) (VAPID and RFC 8291).

## Client sounds and music

The phone plays the League client's **own** sounds and music, at the moments and with the timing the client uses:
ready check, champ select timer ticks, bans and locks, lobby, invites, chat and friend requests, and the music for the
home screen, lobby, queue and draft.

No Riot audio is in this repository or in the release. The PC reads it from your own League install:

- Sounds come from the client's plugin archives (`{Lcu:InstallPath}\Plugins\rcp-fe-*\assets.wad`).
- Map music and ambience are fetched once from the running client with a read-only request.
- Files are extracted on first use into `%LOCALAPPDATA%\league-remote\client-sounds\` (about 50 MB) and extracted again
  when a patch changes the source.
- The phone gets them from `/client-sounds/manifest.json` and `/client-sounds/{key}.ogg`.

Without an install or cache the manifest is empty, and the phone uses synthesized sounds for the main cues (no music).

Settings › Sounds & Haptics has switches for sounds, music, both volumes, each kind of sound, and **Play in Background**. It is on by
default and keeps sound going while the app is minimised or the phone is locked. On iPhone this pauses other apps' audio
while connected.

The mapping from sound to file, and where each timing comes from, is in
[`ClientSoundCatalog.cs`](../src/LeagueRemote.Connector/ClientSounds/ClientSoundCatalog.cs). After a League patch, run the
test `Catalog_matches_a_real_installation_when_one_is_configured` with `LEAGUEREMOTE_REAL_INSTALL` set to your install
folder.
