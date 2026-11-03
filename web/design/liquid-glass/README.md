# Liquid Glass (iOS 27) update pack — League Remote design system

A research-backed brief for Claude Code to bring the League Remote design system (`rzerdik/league-remote`, published at https://claude.ai/artifact/UcLm49d6wjKfVfx8on8L7i) in line with Apple's Liquid Glass as it stands in iOS 27 (released 2026-09-14; current 27.2).

Prepared 2026-11-02. The existing design system was only **read**; nothing in it or the repo was changed.

**Status (2026-11-03): done, and kept as a dated reference.** `reference/` still explains the rules; `web/CLAUDE.md` holds what applies today, and the code wins over this pack. Where it differs from the plan:

- Decisions: D1 no Settings row, D2 refraction yes, D3 tab bar minimizes yes, D4 Apple-style press, D5 solid prominent button, D6 skills vendored.
- Themes have no `data-theme`: `-hc` recipes became `@media (prefers-contrast: more)`.
- `components/bundle.css` exists only in the design-system artifact; here the stylesheet is `web/src/styles/components.css`.
- The proposed alphas failed 4.5:1 over white, so dark glass also dims its backdrop (`glass-brightness`) and text on glass uses `label-secondary-glass`.
- Not built: drag-to-switch on the tab pill. `glass-specular` and `glass-edge` weren't needed (the rim is one token).

## Start here (reading order for Claude Code)

1. `PLAN.md` — ground rules, six decisions for Robert, phased tasks with acceptance criteria.
2. `GAP-ANALYSIS.md` — what already matches Apple and what doesn't, area by area.
3. `reference/06-token-proposals.md` — exact token additions/changes in the repo's `tokens.json` shape.
4. `reference/03-web-implementation.md` — CSS/JS recipes using the `lr-` class names.
5. `reference/01-material.md`, `02-components.md`, `04-motion.md` — the rules behind each task.
6. `reference/05-ios27-changelog.md` — what changed after iOS 26.0.
7. `reference/SOURCES.md` — every source tag → URL.
8. `research/` — the full source-tagged research notes (long; consult when a reference file isn't enough).

`prompts/claude-code-kickoff.md` is a ready-to-paste first message for a Claude Code session.

## The short version

League Remote already follows most Liquid Glass rules: glass only on the floating controls layer, opaque content, never glass on glass, clear glass only over the camera with a dim, inset glass sheets that turn opaque at full height, a floating capsule tab bar, Apple's iOS 26 system colors and Dynamic Type, four themes including Increase Contrast, and Apple-model springs.

What iOS 26.x and 27 changed that the system doesn't reflect yet:

- **Material retune (27):** more diffusion behind glass, a darkened edge, brighter specular highlights, less transparency by default.
- **User control (26.1 → 27):** a system slider from more clear to more tinted. Safari can't read it (or Reduce Transparency), so the web needs its own glass-amount setting.
- **Interactive glass:** light that blooms from the touch point; knobs (slider, segmented) turning to glass while dragged.
- **Materialize, don't fade:** menus and banners appear by scale + blur + opacity from their control; 26.2 made menus bouncier.
- **Top scroll edge (27):** a uniform legible zone under the nav when content scrolls beneath it.
- **Tab bar:** a sliding selection pill (optional drag lens, optional minimize).
- **Sheets:** opacity and size follow the drag continuously; HIG 2026-03 button placement.
- **Optional:** Chromium-only refraction — iPhone Safari can't render it (WebKit bug 245510).

## What Apple does not publish

No blur radii, glass opacity, highlight strength, corner radii, bar heights or animation curves. The only official number is the 35% dim under clear glass. All proposed values are starting points to tune on device; take any new metric from Apple's iOS 27 UI Kit, never invent it.

## Copyright and licensing

All reference text is paraphrased from the sources with short quotes at most. Code snippets are original. Emil Kowalski's skills are MIT-licensed — keep the notice if you vendor them. SF Symbols and SF Pro are licensed for Apple platforms only; the system keeps Lucide and the system font stack.
