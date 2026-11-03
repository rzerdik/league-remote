# Gap analysis — League Remote design system vs. Liquid Glass (iOS 27)

Snapshot read on 2026-11-02 (read-only) from the design-system artifact, version `1793657865-4a82`, synced from `rzerdik/league-remote@cf4415e + working tree`. Source of truth in the repo (from `tokens.json` → `meta.paths`): `web/design/tokens.json`, `web/CLAUDE.md`, components in `web/src/components/lr`. The stylesheet that becomes `components/bundle.css` lives somewhere under `web/` — locate it before editing.

Legend: ✅ already matches Apple guidance · 🟡 partly / needs tuning · ❌ missing · ⚪ not applicable to a phone PWA.

Every "Apple says" claim below is sourced in `reference/` (tags resolve in `reference/SOURCES.md`).

---

## 1. Material

| Area | Apple (iOS 26 → 27) | League Remote today | Status | Action |
|---|---|---|---|---|
| Layering | Glass only on the floating controls/navigation layer; content stays opaque; never glass on glass | Same rule in README ("Glass for controls, solid for content"; chrome hides while a modal sheet is up) | ✅ | Keep. Add a lint/review check (PLAN T1.6). |
| Regular vs thick | Bigger glass behaves thicker: more opaque, deeper shadow, stronger lensing | `glass` (small controls) and `glass-thick` (text-carrying, sheets, menus) | ✅ | Keep the two tiers. |
| iOS 27 retune | Busy backdrops diffused more; darkened edge; brighter specular highlights; more contrast; lower default transparency | `blur(14px) saturate(180%)` / thick `blur(28px) saturate(170%)`; `glass-rim` = 0.5px top highlight + hairline; no dark edge | 🟡 | Retune blur/alpha, add dark edge + brighter specular (T1.1–T1.3). |
| Clear variant | Only over rich media, with a 35% dark dimming layer over bright content; never mixed with regular | `glass-clear` only over the camera, with `scrim` | ✅ | Optionally split a dedicated `dim-clear` token (0.35) from the modal `scrim` (T1.4). |
| Tint | Tint only the primary action; tint maps to backdrop brightness; brand color belongs in content | Gold `accent` fills the ONE prominent action per screen; tab bar and nav are monochrome | ✅ | Decide whether the prominent button stays a solid fill or becomes near-opaque tinted glass (Decision D5). |
| User glass preference | 26.1 Clear/Tinted toggle → iOS 27 continuous slider "more clear ↔ more tinted", default middle. **Web pages cannot read it.** | Not modelled | ❌ | Add a `--lr-glass-amount` mechanism (+ optional Settings row, Decision D1) (T1.5). |
| Reduce Transparency | Glass becomes frostier / near-opaque | No `prefers-reduced-transparency` rule; iOS Safari doesn't expose that query anyway | ❌ | Add the media query for Chromium + make the in-app setting cover Safari (T1.5). |
| Increase Contrast | Mostly black/white with a contrasting border | `dark-hc` / `light-hc` themes with 1px rim and denser glass | ✅ | Keep; add `forced-colors` handling (T1.5). |
| Refraction / lensing | The defining optical effect; in iOS 27 refraction is an optional, selective effect | None (blur only) | ❌ | Optional Tier 2, Chromium only — Decision D2 (T5). |
| Fallback | Opaque surface when blur isn't available | `@supports not (backdrop-filter…)` → `bg-elevated` | ✅ | Keep. |
| Glass blur tokens | — | `--lr-glass-blur*` live in `bundle.css`, not in `tokens.json` | 🟡 | Move them into a `material` token family so the system documents them (T1.1). |

## 2. Interaction and motion

| Area | Apple | Today | Status | Action |
|---|---|---|---|---|
| Interactive glass press | Glass reacts instantly: flexes, scales, bounces, lights up from the touch point; light can spread to neighbouring glass | All buttons: `scale(0.96)` + `brightness(1.12)` on press; tabs `scale(0.94)` | 🟡 | Glass controls get a pointer-origin glow + spring response (direction of scale = Decision D4) (T2.1). |
| Knobs turn to glass while touched | Slider and toggle knobs become glass only during interaction | Switch thumb ✅ (stretches, `thumb-pressed`, blur); slider thumb only scales 1.15 + translucent | 🟡 | Give the slider thumb the same glass-while-dragging treatment; same for the segmented thumb while dragged (T2.3). |
| Materialize | Glass appears by ramping lensing, not a plain fade | Menus: `scale(0.6)` + opacity on a spring; sheets/banners slide | 🟡 | Add a shared "materialize" enter/exit (scale ~0.94→1 + opacity + small blur ramp) (T2.2). |
| Morph from source | Menus, popovers, action sheets emerge from the control that opened them | Menus scale from the trigger via `transform-origin` | ✅/🟡 | Keep; optional View-Transition morph button→menu (T2.2). |
| Springs | Apple presets: smooth bounce 0, snappy 0.15, bouncy 0.3 (duration 0.5 s); 26.2 made menus/controls bouncier | `spring-smooth` (0.4, 1), `spring-snappy` (0.3, 1), `spring-bouncy` (0.4, 0.8) as `linear()`; JS springs for gestures | ✅ | Keep the model. Optionally add `spring-interactive` and a slightly bouncy menu curve (T2.4). |
| Reduce Motion | Turns off elastic effects; cross-fades | Everything becomes a `dur-fade` cross-fade | ✅ | Extend to the new glow/materialize effects. |

## 3. Navigation chrome

| Area | Apple | Today | Status | Action |
|---|---|---|---|---|
| Floating tab bar | Inset glass capsule at the bottom, labels always shown | `lr-tabbar lr-glass`, 5 tabs, inset capsule, fixed height 62px | ✅ | Keep. |
| Selected tab | Selection is a pill that moves between tabs; dragging across the bar shows a glass lens | `is-selected` swaps background (`fill-tertiary`) and color with a fade | 🟡 | Sliding selection pill on a spring + optional drag-to-switch lens (T3.2). |
| Tab bar minimize | Optional: collapses on scroll down, expands on reverse scroll / tap | Never minimizes | ⚪/❌ | Decision D3 — Apple marks it optional; default: don't. |
| Bottom accessory | Persistent controls above the tab bar, inline when collapsed | `ActionBar` with `accessory` already sits above the tab bar | ✅ | Keep; only revisit if D3 = yes. |
| Toolbar grouping | Items in one functional group share one glass capsule; ≤ 3 groups; text and icons never mixed in a group; primary action separate and tinted | Nav bar has one leading and one trailing glass circle per screen; ActionBar puts each button in its own capsule | 🟡 | Add a `GlassGroup` / `.lr-glass-group` for adjacent icon buttons (T3.3). |
| Scroll edge effect | Replaces dividers; soft by default; iOS 27 shows a more uniform toolbar zone at the top when content scrolls under bars; one effect per edge | `lr-edge-bottom` (soft blur + mask) behind the action bar; **no top edge** | 🟡 | Add a top scroll-edge band that appears when content scrolls under the nav (T3.1). |
| Large title | Collapses to an inline title on scroll | `LargeTitle` docks into `NavBar` | ✅ | Keep. |

## 4. Sheets, menus, alerts, controls

| Area | Apple | Today | Status | Action |
|---|---|---|---|---|
| Sheet detents | Partial = inset glass; full height = opaque, flush; dragging up makes it more opaque and slightly larger | Partial = inset `glass-thick`; `large` = `bg-elevated` flush with `radius-sheet` top corners | ✅/🟡 | Interpolate opacity/inset continuously with drag progress instead of switching at the detent (T4.1). |
| Sheet buttons (HIG 2026-03-24) | Cancel/Close leading, Done trailing; never Cancel + Done + Back together | Head grid `control-md · title · control-md` | ❓ | Audit every sheet against the rule (T4.2). |
| Modal vs parallel | Modal tasks dim; parallel tasks use glass without dimming | Modal sheets have a scrim; picker sheet is non-modal | ✅ | Keep. |
| Alerts | Bolder, left-aligned title and message in iOS 26+; ≤ 3 buttons | `LrAlert` for irreversible confirmations | ❓ | Verify alignment/weight (T4.3). |
| Menus | Icons all-or-none per group; one submenu level; destructive last and red | `LrMenu` with items/separators | ❓ | Audit (T4.4). |
| Concentric corners | Fixed / capsule / concentric (outer − padding, with a fallback) | README states the rule; tokens give fixed radii | 🟡 | Add a CSS helper for concentric radii (T4.5). Optional `corner-shape: squircle` progressive enhancement. |
| System colors | iOS 26 palette (light/dark/IC light/IC dark) | Red, orange, green, blue, cyan, mint, indigo, purple, pink match the HIG values exactly | ✅ | Nothing to change. Yellow, teal and brown aren't used — add only if needed. |
| Typography | Dynamic Type sizes; emphasized weights; no ALL CAPS section headers | Matches (Large scale, bold titles, `maxFontSize`, no caps) | ✅ | Nothing to change. |
| Hit targets / contrast | 44×44 pt; 4.5:1 text, 3:1 large/bold | `hit-min` 44px; README requires checks in four themes | ✅ | Add worst-case backdrop checks for glass (T1.6). |

## 5. Docs

| Area | Today | Action |
|---|---|---|
| README "Materials" section | Recipe hard-codes blur values | Rewrite to name tokens + the new glass amount, tiers and a11y rules (T6.1). |
| `components/Materials` card | Glass and glass-thick over a flat preview ground | Add a worst-case backdrop (busy champion art + white area) and the tint range (T1.6). |
| `web/CLAUDE.md` | Unknown (not synced into the artifact) | Add a short "Liquid Glass rules" block pointing at `reference/` (T6.2). |
