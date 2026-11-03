# Kickoff prompt for Claude Code

Copy this pack into the repo (suggested: `web/design/liquid-glass/`), open Claude Code at the repo root, and paste:

---

We're updating the League Remote design system to Apple's Liquid Glass as of iOS 27. Everything you need is in `web/design/liquid-glass/`:

1. Read `README.md`, `PLAN.md` and `GAP-ANALYSIS.md` in that folder, then `reference/06-token-proposals.md` and `reference/03-web-implementation.md`.
2. Do Phase 0 only: find and list every file you'll touch (tokens source and generator, the stylesheet behind `components/bundle.css`, the JS spring table, the `lr` components named in the plan), tell me how to run demo mode, and take the baseline screenshots.
3. Then ask me the open decisions D1–D6 from `PLAN.md` in one message, with your recommendation for each.
4. After I answer, work phase by phase on branch `feat/liquid-glass-27`, one commit per task, checking each task's "Done when" before moving on. Show me before/after screenshots at the end of each phase.

Rules: the repo is the source of truth — don't edit the design-system artifact until Phase 6. Keep `tokens.json` in its list shape with all four themes. Never invent Apple numbers; the proposals are starting values to tune. Every change must work in dark, light, dark-hc and light-hc, at 375 and 430px, at the largest text size and with Reduce Motion.
