# Plan — update League Remote to Liquid Glass (iOS 27)

Executor: Claude Code, working in `rzerdik/league-remote`. Read `README.md` first, then `GAP-ANALYSIS.md`. References live in `reference/`; every Apple claim is sourced there.

## Ground rules

1. **The repo is the source of truth.** Edit `web/design/tokens.json`, the stylesheet that builds `components/bundle.css`, the Vue components in `web/src/components/lr`, and `web/CLAUDE.md`. Don't hand-edit the design-system artifact; it is re-synced from the repo at the end (Phase 6).
2. **Find before you edit.** Paths beyond the three above are not known from the design system. Locate the stylesheet, the tokens→CSS generator and the JS spring table first and list them in your first message.
3. **Tokens keep their shape.** Families are `{"tokens":[{name,value,usage}]}` lists; colors are keyed `dark`, `light`, `dark-hc`, `light-hc` (dark first); no named colors, `var()` or `color-mix()` inside token values. Every new token gets a `usage` sentence.
4. **No invented Apple numbers.** Apple publishes no blur/opacity/radius/duration values. Use `reference/06-token-proposals.md` as starting points, tune by eye, and say so in commit messages.
5. **Every change works in four themes, at 375 and 430px, at the largest text size, with Reduce Motion** — the README's existing bar.
6. **Small commits per task**, conventional messages, on a branch `feat/liquid-glass-27`.
7. Stop and ask Robert before acting on any **Decision** below that isn't answered yet.

## Decisions for Robert (ask before the dependent task)

| ID | Question | Default if unanswered | Blocks |
|---|---|---|---|
| D1 | Add a "Liquid Glass" slider (More Clear ↔ More Tinted) to Settings › This Phone? iPhone Safari can't read the iOS 27 system slider or Reduce Transparency, so this is the only way to get frostier glass on the web. | Build the mechanism (`--lr-glass-amount`, persisted per device), **no Settings row** | T1.5 UI |
| D2 | Add Chromium-only refraction (Tier 2) to the tab bar and action-bar capsule? iPhone users won't see it. | No | Phase 5 |
| D3 | Minimize the tab bar on scroll? Apple makes it optional. | No | T3.4 |
| D4 | Glass controls on press: Apple-style (scale up ~1.04 + glow + bounce) or keep the current press-down (0.96) and only add the glow? | Keep press-down + add glow | T2.1 |
| D5 | Prominent (gold) button: keep a solid fill, or near-opaque tinted glass (same color, rim + specular, ~92% alpha)? `on-accent` contrast must stay ≥ 7:1 either way. | Keep solid + rim (today) | T1.3 |
| D6 | Vendor some of Emil Kowalski's MIT skills (`apple-design`, `animation-performance`, `review-animations`) into `.claude/skills/`? | No | T6.3 |

---

## Phase 0 — Prepare (no visual change)

**T0.1 Inventory.** Locate and list: tokens source and generator, the stylesheet behind `bundle.css` (look for `.lr-glass`, `--lr-glass-blur`), the JS spring table, `Sheet`, `TabBar`, `NavBar`, `LrMenu`, `LrSlider`, `LrSegmented`, `LrSwitch`, `LrButton`, the demo mode used for the component cards, and how `data-theme` (incl. `-hc`) is chosen.
*Done when:* one message lists every file you'll touch and how to run demo mode.

**T0.2 Baseline.** Capture screenshots of the demo screens (Play, Lobby, Champion Select, Settings, a modal sheet, a menu open, Scanner) in four themes at 390px.
*Done when:* baseline images are saved outside `web/` (or in a gitignored folder) for before/after comparison.

## Phase 1 — Material (P0)

**T1.1 Material tokens.** Add the `material` family from `reference/06-token-proposals.md` §4. Replace the hard-coded `--lr-glass-blur`, `--lr-glass-blur-thick`, `--lr-blur-soft`, `--lr-blur-thin` in the stylesheet with the token-generated variables (keep the old variable names as aliases for one release if they're used widely).
*Done when:* no blur radius or saturation literal remains in glass rules; the preview cards render unchanged except for the intentional blur bump.

**T1.2 iOS 27 retune.** Apply the `glass` / `glass-thick` alpha changes and the new `glass-rim` (brighter specular line, darker outer edge) from §1 and §3; add `glass-specular` and `glass-edge` colors if you build the rim from them.
*Done when:* over a busy backdrop the glass reads as more diffused with a crisper edge in dark and light; IC themes are unchanged.

**T1.3 Prominent button (D5).** Default: keep the solid accent with `glass-rim`. If D5 = tinted glass, render the fill as accent at ~92% alpha over a `glass-thick` blur and verify `on-accent` ≥ 7:1 over white and black backdrops.

**T1.4 Clear glass dimming.** Add `dim-clear` (35%, HIG) and use it under clear glass in the scanner; keep `scrim` for modal dimming.
*Done when:* scanner controls sit on a localized 35% dim over a bright feed.

**T1.5 Glass amount + accessibility.**
- Add `glass-ultraclear`, `glass-tinted` (§2) and the `color-mix()` recipe from `reference/03-web-implementation.md` §3, driven by `--lr-glass-amount` (default from the `glass-amount` token).
- `@media (prefers-reduced-transparency: reduce)` and `-hc` themes force amount 1.
- `@media (forced-colors: active)`: no backdrop filter, `1px solid CanvasText` border.
- Persist a per-device amount (same storage the app already uses for This Phone settings). Settings row only if D1 = yes: a `LrSlider` labelled "Liquid Glass" with "More Clear" / "More Tinted" end labels and a live preview of the tab bar.
*Done when:* amount 0 / 0.5 / 1 visibly change all glass; amount 1 equals the Reduce Transparency look; IC themes ignore it.

**T1.6 Worst-case checks + Materials card.** Extend the `Materials` preview with a busy backdrop (champion splash art with a white region) under `glass`, `glass-thick`, `glass-clear` + `dim-clear`, at amounts 0 / 0.5 / 1. Measure `label` and `label-secondary` contrast on glass at amount 0 over the white region; fix alphas until ≥ 4.5:1 (3:1 for ≥ 24px or bold). Add the review checklist from `reference/03-web-implementation.md` §13 to `web/CLAUDE.md`.
*Done when:* contrast numbers are recorded in the PR description.

## Phase 2 — Interaction and motion (P1)

**T2.1 Interactive glass press (D4).** Add `.lr-glass-interactive` (pointer-origin glow, `glass-glow`, `glass-glow-size`, `dur-glow`) to `LrButton` variants `glass`, `clear`, `prominent`, the tab bar items and nav circles. Write `--lr-px/--lr-py` on the pressed element only. Scale per D4; release on `spring-bouncy`. Reduce Motion: no scale, no glow.
*Done when:* the glow starts under the finger on iPhone, nothing changes for `gray`/`plain`, no layout shift.

**T2.2 Materialize.** Replace menu/banner/popover enter/exit with the shared `lr-materialize-in/out` keyframes (scale ≥ 0.9, own-pixel blur ≤ 5px, from the trigger origin). Optional: View-Transition morph from trigger to menu (`reference/03-web-implementation.md` §7).
*Done when:* nothing animates a backdrop blur; exits are faster than entries; Reduce Motion cross-fades.

**T2.3 Knobs become glass while dragged.** Slider thumb: widen, `thumb-pressed`, `knob-blur`, `glass-rim` while active (match the switch). Segmented control: thumb becomes a glass lens while dragged between segments, settles on `spring-snappy` with velocity.
*Done when:* at rest the controls look exactly as today.

**T2.4 Springs.** Add `spring-menu` + `dur-menu` (response 0.35, damping 0.85) generated with the same tool as the existing curves, and `spring-interactive` (0.15, 0.86) to the JS spring table. Use `spring-menu` for menus, popovers and banners.

## Phase 3 — Navigation chrome (P1)

**T3.1 Top scroll edge.** Add `lr-edge-top` (`reference/03-web-implementation.md` §8): soft blur band under the nav, shown only once content scrolls under it; in dark themes a faint dim. One soft edge per edge.
*Done when:* scrolling Settings and Friends shows a uniform, legible zone under the nav buttons and collapsed title; no band at rest.

**T3.2 Tab selection pill.** One pill element slides between tabs on `spring-snappy` (`transform` only); optional drag-to-switch with the pill as a glass lens while held.
*Done when:* switching tabs moves one pill; VoiceOver still reads the selected tab (`aria-current`).

**T3.3 Glass groups.** Add `.lr-glass-group` (`reference/03-web-implementation.md` §9). Wherever two or more icon buttons are adjacent on glass, render one group. Never mix text and icon buttons in a group.

**T3.4 Tab bar minimize (only if D3 = yes).** Collapse on scroll down to the current tab at the leading edge, expand on scroll up or tap; `ActionBar` stays above. Never during Champion Select or Ready Check.

## Phase 4 — Sheets, menus, alerts, shapes (P2)

**T4.1 Sheet opacity follows drag.** Interpolate background (glass-thick → `bg-elevated`), inset (space-2 → 0) and radius (`radius-xl` → `radius-sheet`) from drag progress between the top partial detent and `large`, via a variable on the sheet element.

**T4.2 Sheet header audit.** Close/Cancel leading, Done trailing; Back replaces Cancel after step one; never all three (HIG 2026-03-24). List any sheet that breaks it and fix.

**T4.3 Alert audit.** Bold, left-aligned title and message (iOS 26); ≤ 3 buttons; destructive always paired with Cancel; Cancel never default.

**T4.4 Menu audit.** Icons all-or-none per group; destructive last and red; one submenu level; title-case verbs.

**T4.5 Concentric helper.** Add `.lr-concentric` (`--lr-r-outer`, `--lr-pad`, fallback) and use it where an inner rounded element sits inside a card or sheet. Optional `corner-shape: squircle` behind `@supports`.

## Phase 5 — Optional refraction (P3, only if D2 = yes)

**T5.1** Implement Tier 2 per `reference/03-web-implementation.md` §11: JS gate, per-size displacement map, `refraction-scale` / `refraction-bezel` tokens, applied only to the tab bar and the action-bar capsule; off for Reduce Transparency, Reduce Motion, IC themes and amount ≥ 0.75. Animate the filter `scale` to materialize.
*Done when:* Chrome Android shows rim lensing; Safari and Firefox look exactly like Tier 1; no frame drops while scrolling on a mid-range Android.

## Phase 6 — Docs and re-sync (P0, last)

**T6.1 README.** Rewrite the design system README sections *Materials* (tokens instead of literals, the glass amount, tiers, a11y rules), *Motion* (materialize, glow, knobs, `spring-menu`) and *Principles* (iOS 27 notes) — keep Robert's voice and structure; update component READMEs touched (`Materials`, `TabBar`, `NavBar`, `Sheet`, `LrButton`, `LrSlider`, `LrSegmented`, `LrMenu`).

**T6.2 `web/CLAUDE.md`.** Add a "Liquid Glass rules" block: the layering rules, the lint checklist, and pointers to this pack's `reference/` (copy the pack into the repo, e.g. `web/design/liquid-glass/`, if Robert agrees).

**T6.3 Skills (only if D6 = yes).** Copy chosen skills into `.claude/skills/` with the MIT notice.

**T6.4 Re-sync the design system.** Re-sync the artifact `https://claude.ai/artifact/UcLm49d6wjKfVfx8on8L7i` from the repo the same way it was last synced (its `lastChange.via` is "GitHub · rzerdik/league-remote@… + working tree"), following the Design System type's re-sync rules: read the index and each file you'll change right before saving, publish only changed `project/` files, keep usage notes and assets, list tokens the code no longer defines and ask before removing them, write the index last with `lastChange` set (`by` Robert, `via` the new commit, a one-line `note`). Never write `tokens.css`, `api/` or `manifest.json` by hand.

## Phase 7 — Verify (P0)

- Before/after screenshots vs T0.2 in four themes at 375 and 430px.
- Largest text size and Reduce Motion on every changed screen.
- Contrast numbers from T1.6 in the PR.
- iPhone Safari (iOS 27) as the primary target; Chrome Android; Firefox Android scroll smoothness with the tab bar visible.
- Glass surfaces visible at once ≤ 4 on every screen; no glass inside glass; no `opacity`/`filter` on glass ancestors.
- A separate reviewer pass (fresh agent, or Emil's `review-animations` if D6) that hasn't seen the implementation, checking the diff against `reference/01-material.md` §10 and `reference/03-web-implementation.md` §13.
