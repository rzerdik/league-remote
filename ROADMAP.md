# Roadmap

✅ works · 🔧 built, needs a live check · 🗓️ planned · 💡 idea

Update this file in the same change as the work it describes.

## Works ✅

- **Game flow:** lobbies for any queue, Practice Tool, role preferences, ready check, queue and cancel, leave.
- **Champion select:** hover, lock, bans, enemy team and bans, pick-order and role swaps, trades, spells, runes.
- **Social:** chat, friends and presence, friend requests, lobby invites.
- **Profile:** rank, mastery, form, roles, match history and game details. Teammates' profiles and Riot ID search.
- **Live game:** game clock, scoreboard, objective and buff timers, events.
- **Alerts:** Web Push with per-phone choices, and the client's own sounds and music. See the live check below.
- **Phone app:** iOS-style design system, light and dark, large text, Reduce Motion, demo scenarios for every screen.
- **Open-source clean-up:** one process, one test project, shared package versions, warnings as errors, and docs
  checked against the code.
- **Single exe (v0.2.0):** the release is just `LeagueRemote.exe` + `SHA256SUMS.txt`; no `appsettings.json`, optional
  `settings.json` in the data folder, licenses served at `/licenses.txt` and shown in Settings › About.

## Needs a live check 🔧

Built and tested with mocks. Not yet tried against a real client or phone.

- **Phone server in the connector:** the app has not run as one process on a real PC with a phone yet. That covers
  HTTPS with the local CA, pairing by QR, code or link, several phones, unpair, Web Push and client sounds.
  Tests run the real server on loopback.
- **Tray setup items:** Start with Windows (Run key, follows a moved exe), Allow on Private Networks (hides once a rule
  lets phones in; UAC prompt, cancel is fine) and Open Settings File on a real PC.
- **Log file:** a tray run writes `logs\connector.log` (rolling, 5 × 2 MB) and the tray's Open Log Folder opens it.
- **Hardening:** the security headers (CSP included) don't block anything on iPhone or Android, and push still works
  with the DPAPI-protected VAPID key (phones re-subscribe once).
- **ARAM bench:** bench swaps and rerolls. Record a real bench session.
- **Dodge:** the quit call in a matchmade champ select, and that custom games hide it.
- **Auto pick and ban:** auto ban, auto lock with cancel, spells and rune page in a real draft. Custom blind and draft games.
- **Skins:** the picker after lock-in in draft and ARAM, chromas, Random, and the favourite suggestion.
- **Champ select timer:** the phone's countdown against the client's, within about 0.5 s.
- **Anonymous draft chat:** hidden teammates get a champion or position label.
- **Post-game:** Play Again solo and in a party, LP and mastery from a real game.
- **Live game:** a real `allgamedata` sample, one event per grub, timers against the game clock.
- **Social:** friend request and invite flows with real players, opening a new direct chat.
- **Profile:** friend, party and teammate profiles. How soon a finished game appears in the history.
- **iPhone:** VoiceOver for the main flows, the largest text size, Increase Contrast, haptics, sound in the background
  and on the lock screen, music memory use, the final-seconds glow around the Dynamic Island.

## Next 🗓️

- Code signing for the exe.
- Check the League client's certificate against Riot's chain instead of by host.
- End-to-end encryption between phone and PC.
- Trends over more than the last 20 games.
- Chroma preview art in the skin picker.

## Ideas 💡

- Power spikes and item gold difference in the live game view.
- Reaching the PC away from home, for example through a VPN. Not supported today.
- Translations.
- Push for more events.
