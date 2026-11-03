# TypeScript / Vue standards (`web/`)

Code conventions for the PWA. **Visual design, the `lr-` component library, tokens, motion and copy rules live in
[`web/CLAUDE.md`](../../web/CLAUDE.md)**. This file does not repeat them.

## Stack

Vue 3.5 (`<script setup lang="ts">` single-file components only), Vite 8, TypeScript 6 (strict, `vue-tsc -b` in the
build), `@microsoft/signalr`, `reka-ui` primitives, `@lucide/vue` icons. No Tailwind, no shadcn-vue, no UI kit, no
router, no Pinia, on purpose. Ask before adding a dependency.

## Formatting

2-space indent, LF, **no semicolons**, single quotes, trailing commas in multi-line literals, long lines are fine up to
~120 characters (`.editorconfig`; match the surrounding file).

## Structure

| Where | What |
|---|---|
| `src/contracts/*.ts` | Hand-written mirror of `src/LeagueRemote.Contracts/*.cs`, one file per C# file, re-exported by `index.ts`. See [contracts.md](contracts.md) |
| `src/hub.ts` | The only SignalR connection: `start`, pairing, `sendCommand`, push hub calls, one handler per state slice message |
| `src/store.ts` | One `reactive` store: a field per slice (`state`, `chat`, `friends`, …) and a `liveX` computed for each (null while unpaired or signed out). UI reads `liveX`, never the raw field. Also `toast` and `saveGrant`. No other global state containers |
| `src/connection.ts` | `connectionPhase` and the status texts built from it. Screens use `connectionEmpty` |
| `src/navigation.ts`, `appView.ts`, `badges.ts` | Tabs, pushed views, which view the game state shows, tab badges |
| `src/<feature>.ts` (`chat.ts`, `friends.ts`, `invites.ts`, `push.ts`, `pairing.ts`, `draft.ts`, `gameflow.ts`, …) | Feature modules (plain TS, no components): computeds over `liveX`, command helpers, badge counts |
| `src/liveGame/`, `src/sound/` | Larger features with several files and an `index.ts` |
| `src/labels.ts`, `format.ts`, `riotId.ts`, `teams.ts`, `clock.ts`, `countdown.ts`, `haptics.ts` | Names and phase labels, formatting, Riot ID display, time, haptics |
| `src/lib/` | Helpers with no app state: `lazy`, `reload`, `timeout` (`within`), `storage` (`KEYS`), `spring`, `pressStates`, `glassAmount`, `qrDetector`, `vImgLoaded`, `demoMode` |
| `src/composables/use*.ts` | Behaviour: `useBusy`, `useAnswers`, `useSheet`, `useModalLayer`, `useTimerTicks`, `useRefraction`, … |
| `src/components/lr/` | Design-system primitives (owned by `web/CLAUDE.md`) |
| `src/components/<feature>/` | Feature components shared across screens (sheets, banners, sections) |
| `src/screens/<Name>Screen.vue` + `src/screens/<name>/` | One screen per game phase or tab, sub-parts in a folder |
| `src/styles/` | `tokens.css` and `tokens.ts` (generated), `components.css` importing `components/*.css` |
| `src/dev/demo/` | `?demo=<scenario>` fake states and a dev command handler. Dev only, never in a build |

## Rules

- **Send commands only through `sendCommand({ type, ... })`** from `hub.ts`. It validates client-side, shows the error
  toast, never hangs (`{ silent?, timeoutMs? }`), and returns `null` when the command is invalid, the phone is offline
  or the PC does not answer in time. Wrap user-triggered commands in `useBusy().run(...)` so buttons disable while the
  command is in flight.
- **The game state drives the UI.** Screens render from `liveState` / `liveChat`. Optimistic local values (`pending`)
  are cleared when the next state echoes back (see `screens/lobby/LobbyRoles.vue`). Never assume a command succeeded.
- Props with `defineProps<{ ... }>()` (type-based), computed over watchers, `watch` only for side effects.
- Type imports with `import { type X }` / `import type`. Narrow unknown strings with guards like `isPosition`.
- Doc-comment the *why* on non-obvious logic (`/** Optimistic values while a change is on its way; ... */`), same
  tone as the C# side.
- Never log or toast chat message bodies or tokens. The pairing grant lives only in `localStorage` through `saveGrant`.
  Every `localStorage` key is in `KEYS` (`lib/storage.ts`).
- Never await a network call without a bound: race it with `within(...)` (`src/lib/timeout.ts`) so the UI can't hang.
- Every new screen or state gets a **demo scenario** in `src/dev/demo/` (fake slice data, and its commands in the dev
  command handler), so it can be reviewed at 375 px without a League client.

## Verify

```bash
npm --prefix web run build     # gen tokens + vue-tsc type-check + vite build
npm --prefix web run dev       # then /?demo=<scenario>
```

There is no JS unit-test runner. The type-check is the gate. Do not add a runner as a side effect of another task.
