# Liquid Glass — the material (rules for a web implementation)

Condensed from Apple's developer documentation, the HIG and WWDC25/WWDC26 sessions, in our own words. Tags like `[V-219]` resolve in `SOURCES.md`. `[web]` marks our translation to CSS, not an Apple statement. Deeper notes with every citation: `../research/01-apple-apis.md`, `../research/02-hig.md`.

## 1. What it is

- A dynamic material for **controls and navigation** that floats above the app's content. It blurs what is behind it, picks up color and light from it, and reacts to touch in real time [TO-LG] [SUI-CUSTOM].
- Its signature is **lensing**: it bends and concentrates light at its edges, rather than only scattering it like older blur materials [V-219].
- **Size reads as thickness.** Larger glass refracts more, is more opaque, scatters more softly and casts a deeper shadow [V-219] [V-284].
- **Specular highlights** follow the shape's outline and, in some system moments, move with device motion [V-219].
- **Shadows adapt**: stronger over text, weaker over plain light backgrounds [V-219].
- **Small vs. large.** Small glass (tab bars, bar buttons) flips between light and dark to keep its symbols legible; large glass (menus, sidebars) adapts but does not flip [V-219].
- **Glass cannot sample glass.** Elements meant to look like one material share one container, which also renders in one pass [V-323] [V-284].
- **Materialize, don't fade.** Glass appears and disappears by ramping its lensing up or down, not by changing opacity alone [V-219].

`[web]` Blur + saturation covers the scatter; a rim highlight (inset shadow or masked gradient) covers the specular; an outer shadow covers depth; lensing needs an SVG displacement filter that only Chromium can apply to the backdrop (see `03-web-implementation.md`).

## 2. iOS 27 changes to the material

- Busy backgrounds are diffused more strongly behind glass, for readability [V-2026-102] [NEWS-WWDC26].
- A **darkened edge** and **brighter specular highlights** add depth and separation [V-2026-102].
- Refraction is more uniform and contrast is higher; Apple frames it as a readability fix [NEWS-WWDC26] [OS-IOS].
- Refraction became an **optional** effect apps can apply selectively [V-2026-102].
- Apps already using glass get the refinements automatically; the `UIDesignRequiresCompatibility` opt-out is ignored for apps built with the 27 SDKs, and from April 2027 App Store uploads must use them [PLIST-COMPAT] [DEV-NEWS-SDK] [FORUM-DTS].
- A system **Liquid Glass slider** (Settings › Appearance › Liquid Glass) runs from more clear to more tinted, defaults to the middle and replaces the 26.1 Clear/Tinted switch. It respects Reduce Transparency and Increase Contrast [OS-IOS] [SUPPORT-27] [MR-27-RELEASE]. **No app API exposes its value** [V-2026-102]; a web page certainly can't read it.

`[web]` Consequences for League Remote: blur a little more, raise default glass alpha a little, add a dark outer edge and a brighter top line, and model the slider in-app (`glass-amount`) because Safari can't see the OS setting.

## 3. Layering rules

1. Two layers: **content** (lists, cards, the draft board, art) and a **functional layer** of glass controls floating above it [TO-ADOPT] [HIG-MAT].
2. Glass only in the functional layer. Content uses opaque or standard (non-glass) surfaces [HIG-MAT].
3. One exception: knobs of content controls (sliders, switches) turn into glass **only while touched** [HIG-MAT] [TO-ADOPT].
4. **Never glass on glass.** Things placed on glass use fills, transparency and vibrancy — not another glass layer [V-219] [TO-ADOPT].
5. Apply glass to the control itself, not to its inner parts [V-356].
6. Avoid glass resting on top of content in a steady state (e.g. at launch); move or scale content so they don't collide at rest [V-219].
7. **Brand color lives in the content layer.** Don't paint bars with solid brand color [V-2026-251] [HIG-BRAND].

## 4. Variants

| Variant | Use | Rules |
|---|---|---|
| **Regular** (default) | Everything, at any size, over anything | Adaptive; use for text-heavy glass (alerts, popovers, sidebars) [V-219] [HIG-MAT] |
| **Clear** | Only over rich media (photo, video, camera) | All three must hold: rich media behind, the media tolerates dimming, the content on the glass is bold and bright. Add a **35% dark dimming layer** if the media is bright. Never mix with regular in one interface [V-219] [HIG-MAT] |
| **None** (`identity`) | Turning glass off without changing structure | — [SUI-GLASS] |

## 5. Tint

- Tint only **primary** actions and meaningful status; if everything is tinted, nothing stands out [V-219] [V-323].
- Real tint maps a color to a **range of tones** based on the backdrop's brightness — like stained glass — rather than an opaque paint [V-219].
- An opaque solid fill on a glass control breaks the material's character [V-219]. `[web]` A near-opaque tinted layer with the specular rim on top keeps the glass read while protecting label contrast.
- Toolbar and tab bar symbols are monochrome by default [V-323] [HIG-COLOR].
- Every custom color needs light, dark and increased-contrast variants (League Remote already has four themes) [HIG-COLOR] [TO-ADOPT].

## 6. Interaction

- On touch, glass responds instantly: it flexes like a gel, scales and bounces, and **lights up from under the finger**; the glow can spill onto neighbouring glass [V-219] [V-323].
- Elements can lift into glass only while being touched, so the resting UI stays quiet [V-219].
- Sliders keep momentum and stretch while dragged [V-284].
- iOS 26.4 added **Reduce Bright Effects** (dims tap highlights/flashes) and made Reduce Motion tone glass animation down further [9TO5-264] [SUPPORT-26].

## 7. Grouping and morphing

- Neighbouring glass that belongs together goes in **one container**; within the container's spacing, shapes blend like droplets and morph into each other [SUI-CONTAINER] [V-284].
- Morphs between a control and what it opens (menu, popover, sheet) keep a shared identity; far-apart elements use a materialize transition instead [SUI-CUSTOM] [SUI-TRANSITION].
- Menus, popovers, alerts and action sheets **emerge from the control that triggered them**; action sheets no longer rise from the bottom edge [TO-ADOPT] [V-356].

`[web]` One shared glass capsule per group (`.lr-glass-group`), View Transitions or FLIP for button→menu morphs, and a "materialize" keyframe (scale + opacity + small blur) everywhere else.

## 8. Shapes

- Three shape kinds: **fixed** radius, **capsule** (radius = half the height), **concentric** (child radius = parent radius − padding, with a fallback when standalone) [V-356].
- Pinched or flared nested corners are the typical mistake [V-356].
- On iPhone, prefer capsules with extra margin near the screen edge [V-356].
- Apple publishes no radius values; take any new ones from the iOS 27 UI Kit [RES].

## 9. Accessibility adaptations

| Setting | What glass does | `[web]` equivalent |
|---|---|---|
| Reduce Transparency | Frostier, hides more of the backdrop | `prefers-reduced-transparency: reduce` (Chromium only) **and** the in-app glass amount at "tinted" |
| Increase Contrast | Mostly black or white with a contrasting border | `dark-hc` / `light-hc` themes (already present); `forced-colors: active` → system-color border |
| Reduce Motion | Elastic/bounce effects off, fewer animated effects | `prefers-reduced-motion: reduce` → cross-fades; no glow, no stretch, no materialize scale |
| Liquid Glass slider (27) | Clear ↔ tinted | `--lr-glass-amount` (in-app) |
| Reduce Bright Effects (26.4) | Fewer flash/highlight effects on tap | No web query; keep the glow subtle and tie it to the same in-app setting if one is added |

Sources: [V-219] [HIG-MAT] [TO-ADOPT] [V-2026-102] [9TO5-264].

## 10. DON'T list (Apple, condensed)

- Glass in the content layer; glass on glass; regular mixed with clear.
- Clear glass over plain or low-detail backgrounds.
- Tinting everything; opaque solid fills on glass controls.
- Custom backgrounds or borders on bars, tab bars, sheets and popovers — they fight the glass and the scroll edge effect.
- Text and symbols mixed in one glass group.
- Screen-specific actions in the tab bar.
- Stacked or mixed soft/hard scroll edges.
- Hard-coded control metrics; pinched nested corners.
- Blur, shadow or highlight baked into app-icon artwork.

Sources: [V-219] [V-356] [V-323] [HIG-MAT] [TO-ADOPT] [HIG-ICON].
