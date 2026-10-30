---
name: publish-connector
description: Build the release exe — the single-file LeagueRemote.Connector.exe with the web app embedded — into publish/connector. Use when asked to publish, release or build the exe.
disable-model-invocation: true
---

Build the exe. Read `docs/standards/safety.md` first. Never stop a process by name.

## Preflight

1. `git status` clean and on the branch you mean; `dotnet test LeagueRemote.sln` and `npm --prefix web run build` pass.
2. If a copy of the connector runs **from `publish/connector`**, the build can't replace the exe. Don't kill it: ask the
   user to quit it, or stop it by PID only when they said to.

## Build

This is the same command the release workflow (`.github/workflows/release.yml`) runs:

```bash
dotnet publish src/LeagueRemote.Connector -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish/connector
```

- Builds `web/` first (MSBuild runs `npm ci` / `npm run build` when stale) and embeds `web/dist/**`. Pass
  `-p:SkipWebBuild=true` only after building `web/` yourself.
- Output: `LeagueRemote.Connector.exe` (self-contained, compressed), `appsettings.json` and `.pdb` files.
  `appsettings.json` stays next to the exe. `publish/` is git-ignored.
- Don't launch the exe to check it: it binds 5080/5443, which a running connector may use. Confirm it exists and its size.

A release is made by pushing a tag `v<version>`: the workflow tests, publishes, zips the exe with `appsettings.json`,
and creates the GitHub release with a SHA256 file.

## Report

What was built, where, the size, the commit it was built from, and anything the user has to do (quit the old exe,
copy `appsettings.json` changes).
