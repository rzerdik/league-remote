## What and why

## How I checked it

- [ ] `dotnet test LeagueRemote.sln`
- [ ] `npm --prefix web run build`
- [ ] UI changes: checked a `?demo=` scenario at 375 px, light and dark (screenshots below)
- [ ] Tried against a real client or phone (say what, or say not tried)

## Checklist

- [ ] Contracts changed in C# and in `web/src/contracts/` (including `validateCommand`)
- [ ] New or changed command: whitelist tests, executor tests, `docs/commands.md`
- [ ] New LCU endpoint: verified read-only, anonymised sample added
- [ ] No real player names, Riot IDs, ids, chat text or tokens in samples, logs or screenshots
