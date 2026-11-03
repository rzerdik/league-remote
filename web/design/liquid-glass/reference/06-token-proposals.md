# Token proposals (starting values)

Apple publishes **no numbers** for blur radius, glass opacity, highlight strength, shadow size or container spacing. The only official number is the 35% dimming layer under clear glass over bright content [HIG-MAT]. Every value below is therefore a **starting point to tune by eye** on a real iPhone (Safari, iOS 27) and on Chrome Android, in all four themes, over the worst-case backdrop described in PLAN T1.6. Values marked `[tune]` are guesses informed by the iOS 27 direction (more diffusion, darker edge, brighter specular, less transparency by default) [V-2026-102].

All entries follow the shape `web/design/tokens.json` already uses (a `{"tokens":[{name,value,usage}]}` list per family; color values keyed by theme `dark`, `light`, `dark-hc`, `light-hc`, primary theme first). Never use named colors, `var()` or `color-mix()` inside tokens — the design-system page drops them; do mixing in the stylesheet.

## 1. `color` family — changed

| Token | dark | light | dark-hc | light-hc | Note |
|---|---|---|---|---|---|
| `glass` | `rgba(48,48,52,0.62)` (was 0.56) | `rgba(255,255,255,0.66)` (was 0.58) | unchanged | unchanged | iOS 27 default is less transparent [tune] |
| `glass-thick` | `rgba(38,38,42,0.84)` (was 0.8) | `rgba(250,250,252,0.86)` (was 0.82) | unchanged | unchanged | [tune] |

## 2. `color` family — new

| Token | dark | light | dark-hc | light-hc | Usage |
|---|---|---|---|---|---|
| `glass-ultraclear` | `rgba(48,48,52,0.48)` | `rgba(255,255,255,0.5)` | `rgba(40,40,44,0.86)` | `rgba(255,255,255,0.9)` | Clearest end of the Liquid Glass amount setting. Equals or exceeds today's iOS 26-like alpha floor, so text on glass never gets worse than the shipped look. IC themes ignore the setting. [tune] |
| `glass-tinted` | `rgba(30,30,33,0.95)` | `rgba(250,250,252,0.96)` | `rgba(30,30,32,0.95)` | `rgba(255,255,255,0.97)` | Most tinted end of the setting, and the Reduce Transparency surface. Effectively opaque. |
| `glass-specular` | `rgba(255,255,255,0.5)` | `#ffffff` | `rgba(255,255,255,0.45)` | `rgba(0,0,0,0.55)` | Bright top/leading highlight line of glass (iOS 27 brighter speculars). |
| `glass-edge` | `rgba(0,0,0,0.55)` | `rgba(0,0,0,0.12)` | `rgba(255,255,255,0.45)` | `rgba(0,0,0,0.55)` | The darkened outer edge iOS 27 added for separation. |
| `glass-glow` | `rgba(255,255,255,0.22)` | `rgba(255,255,255,0.6)` | `rgba(255,255,255,0)` | `rgba(255,255,255,0)` | Light that blooms from the touch point while glass is pressed. Zero in IC themes. |
| `dim-clear` | `rgba(0,0,0,0.35)` | `rgba(0,0,0,0.35)` | `rgba(0,0,0,0.5)` | `rgba(0,0,0,0.5)` | Dimming under clear glass over bright camera content — the HIG's 35%. Keeps `scrim` for modal dimming only. |

## 3. `shadow` family — changed

`glass-rim` (iOS 27: brighter specular on top, darker edge around):

```json
{"name": "glass-rim",
 "value": {
  "dark": "inset 0 0.5px 0 rgba(255,255,255,0.42), inset 0 0 0 0.5px rgba(255,255,255,0.12), inset 0 -0.5px 0 rgba(0,0,0,0.35), 0 0 0 0.5px rgba(0,0,0,0.7)",
  "light": "inset 0 1px 0 rgba(255,255,255,1), inset 0 0 0 0.5px rgba(255,255,255,0.7), inset 0 -0.5px 0 rgba(0,0,0,0.06), 0 0 0 0.5px rgba(0,0,0,0.14)",
  "dark-hc": "inset 0 0 0 1px rgba(255,255,255,0.45)",
  "light-hc": "inset 0 0 0 1px rgba(0,0,0,0.55)"},
 "usage": "Every glass surface: bright specular line on top, hairline, darkened outer edge (iOS 27). Combine with glass-lift."}
```

`[tune]` all four alphas. Keep the IC values as they are.

## 4. `material` family — new (moves values out of `bundle.css`)

Lengths, percentages and plain numbers only.

| Token | Value | Usage |
|---|---|---|
| `glass-blur` | `18px` (was 14px in CSS) | Backdrop blur of regular glass. iOS 27 diffuses busy content more. Keep ≤ 20px. [tune] |
| `glass-blur-thick` | `28px` (unchanged) | Backdrop blur of `glass-thick` (sheets, menus, alerts). Never animate it. |
| `glass-saturate` | `180%` | Saturation boost on regular glass. |
| `glass-saturate-thick` | `170%` | Saturation boost on thick glass. |
| `edge-blur` | `6px` (was `--lr-blur-soft`) | Scroll-edge bands, top and bottom. |
| `knob-blur` | `2px` (was `--lr-blur-thin`) | Switch/slider/segment knobs while they are glass. |
| `glass-amount` | `0.5` | Default position of the Liquid Glass amount: 0 = ultraclear, 0.5 = default, 1 = tinted. Mirrors iOS 27's default-middle slider. |
| `glass-press-scale` | `1.04` | Scale glass controls reach while pressed if Decision D4 = "Apple-style". Unused otherwise. [tune] |
| `glass-glow-size` | `120%` | Radius of the touch glow relative to the control. [tune] |
| `refraction-scale` | `40` | Tier 2 only: `feDisplacementMap` scale. Published demos use 20–70. [tune] |
| `refraction-bezel` | `12px` | Tier 2 only: width of the refracting rim band. [tune] |

## 5. `duration` / `easing` — optional additions

Generate curves with the same tool that produced the existing `linear()` easings (response + damping ratio → samples), so the family stays consistent.

| Token | Spring | Usage |
|---|---|---|
| `spring-menu` + `dur-menu` | response 0.35, damping 0.85 | Menus, popovers and action sheets materializing from their control — slightly bouncier, matching the iOS 26.2 change to pop-up menus. |
| `spring-interactive` (JS only) | response 0.15, damping 0.86 | Finger-tracking (SwiftUI `interactiveSpring` defaults). Lives in the JS spring table, not `tokens.json`. |
| `dur-glow` | `180ms` | Fade-in of the touch glow (press feedback stays under 200 ms). |

## 6. `size` family — none required

`tabbar-h`, `switch-*`, `control-*` stay. Apple doesn't publish these metrics; if you want to re-measure them, use Apple's iOS 27 UI Kit (Figma/Sketch) from https://developer.apple.com/design/resources/ — do not invent new numbers.
