# Motion for Liquid Glass

League Remote already uses Apple's spring model (response + damping ratio) as CSS `linear()` curves plus JS springs for gestures — keep it. This file lists what Apple and Emil Kowalski's skills add for glass. Sources in `SOURCES.md`; deeper notes in `../research/04-web-implementation.md` §7–8.

## 1. Apple's spring facts

- SwiftUI's default spring is duration 0.5 s, bounce 0 (critically damped). Presets: `.smooth` bounce 0, `.snappy` 0.15, `.bouncy` 0.3, all 0.5 s [SUI-SPRING] [SUI-SMOOTH] [SUI-SNAPPY] [SUI-BOUNCY].
- Legacy response/damping: default (0.5, 0.825); `interactiveSpring` (0.15, 0.86) for finger tracking [SUI-SPRING-LEGACY] [SUI-INTERACTIVE-SPRING].
- Conversion (bounce ≥ 0): mass 1, stiffness = (2π / duration)², damping = 4π(1 − bounce) / duration — checked against Apple's own example (0.5 s, 0.3 → stiffness 157.9, damping 17.6) [WWDC23-SPRINGS].
- Start at bounce 0; add bounce only when the gesture carried momentum [WWDC23-SPRINGS] [EMIL-APPLE].
- HIG motion: purposeful, brief, cancelable; never make people wait; dismiss the way something arrived; motion is never the only signal [HIG-MOTION].
- Glass: materializes by ramping lensing; menus pop out of their control; controls morph between states; touch makes glass flex and glow [V-219].
- iOS 26.2 made pop-up menus and controls bouncier [MR-262].

League Remote's current set maps cleanly: `spring-smooth` (0.4, 1) ≈ `.smooth`; `spring-snappy` (0.3, 1); `spring-bouncy` (0.4, 0.8) ≈ between `.snappy` and `.bouncy`, used only after momentum. Add `spring-menu` (0.35, 0.85) for menus/popovers and a JS `spring-interactive` (0.15, 0.86).

## 2. Glass-specific motion rules

| Moment | Do | Reduce Motion |
|---|---|---|
| Press glass control | Respond on pointer-down; glow from the touch point; scale per D4; release on `spring-bouncy` | No scale, no glow, color/opacity change only |
| Knob dragged (switch, slider, segment, tab pill) | Knob widens and becomes glass while held; tracks the finger 1:1; settles on `spring-snappy` with release velocity | Knob changes fill only; position jumps or fades |
| Menu / popover / banner appears | Materialize: scale 0.94→1 + opacity + small own-pixel blur, from the trigger origin, on `spring-menu` | Cross-fade `dur-fade` |
| Menu / banner leaves | Faster than entry, `dur-fade`, reverse of entry | Cross-fade |
| Sheet dragged | Opacity, inset and radius follow drag progress; release with velocity on `spring-bouncy` | Detent jumps with a fade |
| Top scroll edge appears | Opacity fade only | Same |
| Tab selection | Pill slides on `spring-snappy` | Pill cross-fades between tabs |

## 3. Emil Kowalski's skills — rules worth adopting

Repo: https://github.com/emilkowalski/skills (MIT, © 2026 Emil Kowalski) [EMIL-REPO]. Relevant skills: `apple-design`, `emil-design-eng`, `animate`, `animation-performance`, `review-animations`, `improve-animations`, `mobile-native`.

- **Easing:** ease-out for enter/exit, ease-in-out for on-screen movement, never ease-in for UI; custom curves over keywords [EMIL-DESIGN-ENG].
- **Durations:** press 100–160ms, tooltip 125–200ms, dropdown 150–250ms, modal/drawer 200–500ms; exits faster than entrances [EMIL-DESIGN-ENG].
- **Frequency gate:** things people do 100+ times a day, or from the keyboard, get no animation [EMIL-DESIGN-ENG].
- **Press:** respond on pointer-down; scale 0.95–0.98 for ordinary buttons; never `scale(0)` [EMIL-DESIGN-ENG].
- **Gestures:** hand off release velocity; project momentum; rubber-band past limits; animate from the on-screen value; CSS `linear()` springs are not velocity-aware, so anything a finger touches uses a JS spring [EMIL-APPLE].
- **Material:** bigger surfaces = stronger blur and deeper shadow; never light translucent on light translucent; text on glass a bit heavier with higher contrast; scroll-edge fade instead of 1px dividers; materialize with blur + scale, not opacity alone [EMIL-APPLE].
- **Performance:** animate transform/opacity only; animated blur < 20px (lint at 10px); `will-change` last; 8ms frame budget at 120Hz; don't drive per-frame motion through an inherited CSS variable [EMIL-PERF].
- **A11y trio:** reduced motion → cross-fade; reduced transparency → more opacity, no blur; more contrast → near-solid with a defined border [EMIL-APPLE].

Using them in Claude Code: copy the folders you want into the repo's `.claude/skills/` (e.g. `apple-design`, `animation-performance`, `review-animations`) and keep the MIT license notice. Decision D6.
