# Components — Apple behavior mapped to League Remote

For each Apple component: what Liquid Glass changed, the rules that matter on iPhone, and which `lr-` component it maps to. Tags resolve in `SOURCES.md`. Full per-page notes: `../research/02-hig.md`.

---

## Tab bar → `TabBar` (`lr-tabbar`, `lr-tab`)

Apple:
- Floats on glass over content at the bottom; can minimize on scroll (`onScrollDown`: collapses to the current tab, expands on reverse scroll or tap) — iPhone only, optional [HIG-TAB] [SUI-TABMIN].
- Bottom accessory sits above the expanded bar, inline when minimized; for persistent features (playback), never screen-specific actions like checkout [HIG-TAB] [V-356].
- Labels always shown; filled symbols for tabs; badges for critical info only; ≤ 5 tabs; keep visible except in modal views; tabs never hidden or disabled when empty [HIG-TAB].
- Search, if any, is a separate trailing tab; iOS 27 adds a prominent tab role and two search-tab appearances (landing page vs instant field) [HIG-SEARCH] [V-2026-269].
- Navigation only — actions belong in toolbars [HIG-TAB].

League Remote today: inset glass capsule, 5 tabs, labels, badges, fixed height, `is-selected` = `fill-tertiary` background + `accent-text` color, `scale(0.94)` on press.

Do:
- Make the selection a **single pill that slides** between tabs on `spring-snappy` (one element, `transform` only), instead of each tab toggling its own background.
- Optional: drag across the bar to switch tabs, with the pill turning into a glass lens (`glass` fill + `glass-rim` + `knob-blur`) while the finger is down; release snaps on `spring-bouncy` with the release velocity.
- Selected icon: heavier stroke (already 2.25) — Lucide has no fill variants for most glyphs, so stroke weight stands in for Apple's filled symbol.
- Minimize on scroll: **Decision D3**, default no.

## Navigation bar / toolbar → `NavBar` (`lr-nav`), `LargeTitle`, `ActionBar`

Apple:
- No bar background; items float as glass groups. Items in one group share one capsule; separate groups with fixed space; at most 3 groups [HIG-TB] [TO-ADOPT].
- Don't mix text and symbols in one group; text buttons get their own capsule [HIG-TB] [V-356].
- One primary action, trailing, tinted [HIG-TB].
- Large titles collapse inline on scroll; titles short (< 15 chars), never the app name [HIG-TB].
- Hide whole items, not their contents [TO-ADOPT].
- iOS 27: toolbar items get visibility priorities and an automatic overflow menu [UPD-SUI] [V-2026-269].

League Remote today: one glass circle leading (`#lr-nav-leading`) and one trailing (`#lr-nav-trailing`); `LargeTitle` docks its trailing buttons into the nav once collapsed; `ActionBar` holds the one prominent action at the bottom.

Do:
- When two or more icon buttons sit side by side (e.g. a future Chat + More in the nav), render them in **one** `.lr-glass-group` capsule with plain icon buttons inside, not two glass circles.
- Keep text buttons (Leave, Cancel) as their own capsule.
- Add the **top scroll edge** (below).

## Scroll edge effect → `lr-edge-bottom` (exists), `lr-edge-top` (new)

Apple:
- Replaces dividers where floating UI overlaps scrolling content: soft (progressive blur + fade, the iOS default) or hard (near-opaque band, mainly macOS) [V-356] [SUI-SCROLLEDGE].
- One effect per edge; never stack soft and hard; only where floating UI overlaps content [V-356].
- Over dark content it becomes a subtle dimming [V-219].
- Don't put solid or semi-opaque backgrounds behind bars [HIG-LAYOUT] [V-323].
- **iOS 27:** when content scrolls under the bars, a more uniform toolbar zone appears across the top for legibility; `automatic` has new visuals [V-2026-102] [V-2026-278] [MR-27-LG].

Do: a fixed `lr-edge-top` band (`safe-top` + nav height + ~20px), `backdrop-filter: blur(var(--edge-blur))`, masked `linear-gradient(#000 60%, transparent)`, `z-index: var(--z-edge)`, shown only once the content has scrolled (scroll-driven animation or an `IntersectionObserver` sentinel). In dark themes add a faint dim (`rgba(0,0,0,0.25)`, tune) instead of brightening.

## Sheets → `Sheet` (`lr-sheet`)

Apple:
- Partial-height sheets are **inset glass**, bottom corners nesting inside the display corners; at full height they become **opaque** and anchor to the edges; dragging up makes the sheet more opaque and slightly larger [V-323] [V-356] [TO-ADOPT].
- Modal tasks dim the background; parallel tasks use glass without dimming [V-356].
- Grabber on resizable sheets (tap cycles detents); swipe to dismiss; confirm unsaved changes; one sheet at a time [HIG-SHEET].
- Button placement (HIG update 2026-03-24): Cancel or Close **leading**, Done **trailing**; in multi-step flows Back replaces Cancel after step one; never Cancel + Done + Back together [HIG-SHEET].
- Remove custom sheet backgrounds [V-323].

League Remote today: matches the inset-glass ↔ opaque behavior by switching at the `large` detent; modal/non-modal; ✕ / swipe / Escape; `confirmClose`.

Do:
- Drive background alpha, inset and corner radius from **drag progress** between the highest partial detent and `large` (a CSS variable written on the sheet element only), so the sheet gets steadily more opaque and slightly larger as it rises.
- Audit every sheet header against the leading-Close / trailing-Done rule.

## Menus, popovers, action sheets → `LrMenu`, `ListRow menu`

Apple:
- Menus pop out of their control and morph from it on the same glass plane; larger glass behaves thicker [V-219] [TO-ADOPT].
- Icons: every item in a group has one, or none does; one leading icon can introduce a group of related items [HIG-MENU] [V-356].
- One submenu level; destructive last and red; frequent items first; labels are title-case verbs [HIG-MENU] [HIG-CTX].
- iOS 26.2 made pop-up menu and control animation bouncier [MR-262] [9TO5-262].
- Action sheets now come from the triggering element, not the bottom edge, and the rest of the UI stays interactive [TO-ADOPT] [V-356].

League Remote today: `lr-menu` on `glass-thick`, `scale(0.6)` + opacity on `spring-snappy` from the Reka trigger origin.

Do: keep origin scaling; switch the enter curve to `spring-menu` (slight bounce); exit stays a fast fade; audit icon consistency per group.

## Alerts → `LrAlert`

Apple: title + optional message + ≤ 3 buttons; bolder and **left-aligned** text in iOS 26; destructive style only for actions people didn't choose deliberately; always pair destructive with Cancel; never Cancel as default [HIG-ALERT] [V-356].

Do: verify title weight/alignment; buttons side by side for two short labels, stacked otherwise.

## Buttons → `LrButton`

Apple:
- Styles: glass, glass prominent (tinted), clear, prominent clear; an extra-large size exists for prominent actions [TO-ADOPT] [UIK-BTN] [V-356].
- ≥ 44×44 pt hit region; always a pressed state; at most one or two prominent buttons per view; verbs in title case; an activity indicator inside the button for slow actions [HIG-BTN].
- Glass buttons react on touch (scale, bounce, glow) [V-323].

League Remote today: `prominent`, `glass`, `gray`, `destructive`, `success`, `plain`, `clear`; sizes sm/md/lg; `loading`.

Do: add the interactive-glass press to `glass`, `clear` and `prominent` only (Decision D4 for direction); `gray`/`plain` in content keep the current press.

## Switches, sliders, segmented controls → `LrSwitch`, `LrSlider`, `LrSegmented`

Apple: knobs become glass while touched, then return to solid [HIG-MAT]; switches only inside list rows on iOS; never state by color alone [HIG-TOGGLE]; slider fills leading → thumb [HIG-SLIDER]; segmented: 2–5 equal segments, text or icons not both [HIG-SEG]. Don't hard-code these controls' metrics — they changed in iOS 26 [TO-ADOPT].

League Remote today: switch ✅ (thumb stretches to `switch-thumb-pressed`, `thumb-pressed`, blur); slider thumb only scales and turns translucent; segmented thumb dims pressed segments.

Do: slider thumb gets the switch's glass-while-dragging treatment (wider, `thumb-pressed`, `knob-blur`, `glass-rim`); segmented thumb becomes a glass lens while being dragged between segments.

## Search → `SearchField`

Apple: on iPhone, search lives at the bottom (tab or bottom toolbar) or inline above the list it filters; the field rides above the keyboard; placeholder names what's searchable [HIG-SEARCH] [TO-ADOPT].

League Remote: inline field above the champion grid in the picker — matches the inline pattern. No change.

## Lists → `ListSection`, `ListRow`

Apple: content layer, never glass; larger rows, more padding, larger section radii; title-case section headers [TO-ADOPT] [HIG-LIST].

League Remote: matches (`radius-card` 28px, `row-min`, title-case headers). No change.

## Banners, tips → `ActionBanner`, `BannerHost`, `Tip`

Banners float above content → glass (already). Tips are content → opaque (already). Banner enter/exit should use the shared materialize keyframe.

## App icon → `assets/Logos`

Apple: icons are layered and rendered as glass by the system; don't bake blur, shadows or highlights into the artwork; crisp edges [HIG-ICON]. iOS 27 icons are sharper with more layer separation [MR-27-LG].

League Remote: the PWA icons are flat images and the Signal L mark must not be replaced. Only rule to apply: don't add faux-glass highlights to the mark.
