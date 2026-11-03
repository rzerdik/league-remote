# 02 — Apple HIG for Liquid Glass (iOS 26 to iOS 27): implementable rules for a web design system

Research date: 2026-11-02. All HIG pages were read through their JSON endpoints (`https://developer.apple.com/tutorials/data/design/human-interface-guidelines/<page>.json`) with WebFetch, which renders the pages and summarizes them. Everything below is paraphrased. Direct quotes are kept short and are marked with quotation marks.

## Source keys (every claim is tagged with one or more of these)

| Key | URL |
|---|---|
| [hig] | https://developer.apple.com/design/human-interface-guidelines (JSON: .../human-interface-guidelines.json) |
| [new] | https://developer.apple.com/design/whats-new/ (entries stop at 2025-12-16) |
| [mat] | https://developer.apple.com/design/human-interface-guidelines/materials |
| [col] | https://developer.apple.com/design/human-interface-guidelines/color |
| [typ] | https://developer.apple.com/design/human-interface-guidelines/typography |
| [lay] | https://developer.apple.com/design/human-interface-guidelines/layout |
| [mot] | https://developer.apple.com/design/human-interface-guidelines/motion |
| [dark] | https://developer.apple.com/design/human-interface-guidelines/dark-mode |
| [a11y] | https://developer.apple.com/design/human-interface-guidelines/accessibility |
| [icon] | https://developer.apple.com/design/human-interface-guidelines/app-icons |
| [ico] | https://developer.apple.com/design/human-interface-guidelines/icons |
| [sfs] | https://developer.apple.com/design/human-interface-guidelines/sf-symbols |
| [brand] | https://developer.apple.com/design/human-interface-guidelines/branding |
| [btn] | https://developer.apple.com/design/human-interface-guidelines/buttons |
| [tab] | https://developer.apple.com/design/human-interface-guidelines/tab-bars |
| [tb] | https://developer.apple.com/design/human-interface-guidelines/toolbars |
| [side] | https://developer.apple.com/design/human-interface-guidelines/sidebars |
| [split] | https://developer.apple.com/design/human-interface-guidelines/split-views |
| [sheet] | https://developer.apple.com/design/human-interface-guidelines/sheets |
| [pop] | https://developer.apple.com/design/human-interface-guidelines/popovers |
| [menu] | https://developer.apple.com/design/human-interface-guidelines/menus |
| [ctx] | https://developer.apple.com/design/human-interface-guidelines/context-menus |
| [alert] | https://developer.apple.com/design/human-interface-guidelines/alerts |
| [as] | https://developer.apple.com/design/human-interface-guidelines/action-sheets |
| [search] | https://developer.apple.com/design/human-interface-guidelines/search-fields |
| [seg] | https://developer.apple.com/design/human-interface-guidelines/segmented-controls |
| [tog] | https://developer.apple.com/design/human-interface-guidelines/toggles |
| [sld] | https://developer.apple.com/design/human-interface-guidelines/sliders |
| [step] | https://developer.apple.com/design/human-interface-guidelines/steppers |
| [list] | https://developer.apple.com/design/human-interface-guidelines/lists-and-tables |
| [scroll] | https://developer.apple.com/design/human-interface-guidelines/scroll-views |
| [page] | https://developer.apple.com/design/human-interface-guidelines/page-controls |
| [prog] | https://developer.apple.com/design/human-interface-guidelines/progress-indicators |
| [win] | https://developer.apple.com/design/human-interface-guidelines/windows |
| [ios] | https://developer.apple.com/design/human-interface-guidelines/designing-for-ios |
| [adopt] | https://developer.apple.com/documentation/technologyoverviews/adopting-liquid-glass (JSON: /tutorials/data/documentation/technologyoverviews/adopting-liquid-glass.json) |
| [lgov] | https://developer.apple.com/documentation/technologyoverviews/liquid-glass |
| [w219] | https://developer.apple.com/videos/play/wwdc2025/219/ ("Meet Liquid Glass") |
| [w356] | https://developer.apple.com/videos/play/wwdc2025/356/ ("Get to know the new design system") |
| [w251] | https://developer.apple.com/videos/play/wwdc2026/251/ ("Communicate your brand identity on iOS") |
| [res] | https://developer.apple.com/design/resources/ |
| [vid] | https://developer.apple.com/videos/design/ |
| [cult27] | https://www.cultofmac.com/news/liquid-glass-changes-ios-27-macos-27 (press, not Apple) |
| [cultsotu] | https://www.cultofmac.com/news/wwdc26-platforms-state-of-the-union (press) |
| [neo] | https://www.neowin.net/news/apple-finally-brings-the-slider-for-liquid-glass-and-many-other-changes/ (press) |
| [sa] | https://siliconangle.com/2026/06/08/apple-refines-liquid-glass-design-expands-child-safety-tools-wwdc/ (press) |
| [pipe] | https://pipedot.org/765S9 (press) |
| [impl] | My own web-implementation suggestion. This is not an Apple claim. |

Press sources (cult27, cultsotu, neo, sa, pipe) describe iOS 27 at a feature level. Treat them as secondary sources and check them against Apple pages where possible.

---

## 0. What's new / change log (timeline)

The What's New page ([new]) has dated entries only up to 2025-12-16. The 2026 dates below come from each HIG page's own change log.

| Date | Change | Source |
|---|---|---|
| 2025-06-09 | Liquid Glass launch (iOS 26). New guidance in Materials, App icons (layered), Color (updated system color values), Layout, Toolbars (bar-item grouping, symbol guidance, navigation-bar guidance folded in), Buttons (styles and content), Search fields (iOS placement, tokens), Sidebars (content under the sidebar), Split views (iOS and iPadOS), Icons (standard-symbol table), Windows (resizable iPad windows). iOS 26 and macOS 26 UI kits, Icon Composer and SF Symbols 7 beta released. | [new] |
| 2025-07-28 | Tab bars: Liquid Glass guidance. Scroll views: scroll edge effects. Menus: icons for menu items. SF Symbols 7: Draw and gradients. | [new], [tab], [scroll], [menu], [sfs] |
| 2025-09-09 | Materials updated for Liquid Glass. Motion: Liquid Glass guidance added. Layout: iPhone 17 / Air specs. | [new], [mot] |
| 2025-12-16 | Color, Buttons, Toolbars and Tab bars updated for Liquid Glass. Typography gained emphasized weights. | [new], [btn], [tb], [tab], [typ] |
| 2026-03-24 | Sheets: button placement updated. Scroll views: visionOS Look to Scroll. | [sheet], [scroll] |
| 2026-06-08 (WWDC26, iOS 27) | Tab bars: terminology and art. Search fields: search-as-a-tab guidance refined. Menus: menu item icons. Sidebars: icon colors, adaptable style. Scroll views: scroll edge effects updated. App icons: Liquid Glass guidance refined. | [tab], [search], [menu], [side], [scroll], [icon] |
| 2026-09-09 | Branding: brand-color guidance refined. | [brand] |
| 2026-10-28 | App icons: icon format options (bitmap vs layered Icon Composer file). | [icon] |

iOS 27 platform changes reported by press, not stated in the HIG:
- A user-facing Liquid Glass slider in Settings that runs from "ultra clear to fully tinted". At the opaque end the glass is effectively off and toolbars become opaque. [cult27], [sa], [pipe], [neo]
- Glass is more legible, and less transparent, by default. Complex backgrounds behind toolbars are more diffused. [cultsotu], [pipe]
- Developers can no longer opt out of Liquid Glass in the 27 releases. In iOS 26 the opt-out was the `UIDesignRequiresCompatibility` key. [cultsotu], [adopt]
- macOS 27: one uniform window corner radius, frosted toolbars again, sidebars that run to the window edge, and sidebar icons that regain color. [cult27], [neo], [cultsotu]
- App icons are crisper, with optional refraction layers in the artwork. Icon Composer was updated to match. [cult27], [cultsotu]
- iOS apps can now be resized freely. [cultsotu]

Web takeaway [impl]: build a user-adjustable "glass intensity" token (clear to tinted/opaque) and expose it as a CSS custom property, e.g. `--glass-opacity` from 0 to 1. Make the default somewhat more opaque than the iOS 26 look. At the opaque extreme, fall back to solid surfaces.

---

## FOUNDATIONS

### 1. Materials ([mat])

**What changed:** Liquid Glass is a new material for a functional layer of controls and navigation (tab bars, sidebars, toolbars) that floats above the content layer. The older blur materials (ultra-thin, thin, regular, thick) stay, but only for the content layer. [mat]

**Rules**
- DON'T use Liquid Glass in the content layer. App backgrounds and content surfaces use standard materials. [mat]
- Exception: transient interactive parts of content-layer controls, such as slider and toggle knobs, turn into glass while they are being touched. [mat], [adopt]
- Use glass sparingly. On custom controls, apply it only to the most important functional elements. [mat], [adopt]
- DON'T stack glass on glass. Elements placed on top of glass should use fills, transparency and vibrancy, not more glass. [w219], [adopt]
- In steady states, such as at launch, avoid overlap between content and glass. [w219]

**Variants** [mat], [w219]
- **Regular** (the default, used by most system components): blurs the background and adjusts its luminosity. It looks darker over dark content and lighter over light content. Use it for text-heavy components such as alerts, sidebars and popovers.
- **Clear**: very translucent and without adaptive behaviors. Use it only over visually rich media (photos, video). Over bright content it needs a dark **dimming layer at 35% opacity** behind it. That dimming isn't needed over dark content or when standard AVKit controls already dim. [mat]
- Use Clear only when all three hold: the element is over rich media, a dimming layer won't harm that content, and the content on the glass is bold and bright. Never mix Regular and Clear in one interface. For small elements the dimming can be localized behind the element only. [w219]

**Optical behaviors** [w219]
- Lensing (bending and concentrating light) is the main edge cue. Larger elements get stronger lensing and refraction.
- Highlights respond to geometry and sometimes to device motion.
- "Materialization": glass appears by ramping its lensing up, not by a plain fade.
- When touched, the glass flexes like gel and glows from the touch point. The glow can spread to nearby glass.
- Shadow opacity rises over text and falls over light solid backgrounds.
- Small elements (tab bars, nav bars) flip between light and dark to keep contrast. Large elements (menus, sidebars) don't flip.
- When glass grows larger, for example a menu expanding from a button, it behaves like a thicker material: deeper shadow, more refraction, softer scatter.
- On iPad and Mac, ambient color from nearby content spills onto large glass. A glass window that loses focus recedes.

**Standard materials (content layer)** [mat]
- iOS order by opacity: ultraThin, thin, regular (default), thick.
- Label vibrancy levels: label, secondaryLabel, tertiaryLabel and quaternaryLabel. Avoid quaternaryLabel on thin and ultraThin.
- Fill vibrancy levels: fill, secondaryFill, tertiaryFill. There is one separator vibrancy.
- Choose by semantic purpose, not by the color a material gives. Thicker materials give better text contrast. Thinner ones keep more context visible.

**Accessibility modifiers** (applied automatically) [w219], [mat], [adopt]
- Reduce Transparency: the glass becomes frostier and hides more of the background.
- Increase Contrast: elements go mostly black or white, with a contrasting border.
- Reduce Motion: some effects are less intense and the elastic behavior is off.
- The user's chosen Liquid Glass look in Settings also changes appearance (iOS 26.x had Clear and Tinted; iOS 27 has a slider, per [cult27]).

**Web mapping** [impl]
- `.glass` token sets: `--glass-bg: color-mix(in oklab, var(--surface) N%, transparent)`, `backdrop-filter: blur(var(--glass-blur)) saturate(var(--glass-sat))`, a 1px inner highlight (inset box-shadow, top-left lighter), and an adaptive outer shadow.
- Make lensing optional: an SVG `feDisplacementMap` filter or a canvas/WebGL shader, used only on the hero navigation chrome.
- `.glass--clear` uses a lower bg alpha and requires a `.dim` sibling, `rgba(0,0,0,.35)`.
- Under `@media (prefers-reduced-transparency: reduce)`, use near-opaque frosted surfaces. Under `(prefers-contrast: more)`, use solid black/white with a 1px contrasting border. Under `(prefers-reduced-motion: reduce)`, turn off the press stretch and glow.
- Use a "no glass inside glass" lint rule: children of `.glass` use `--fill-*` tokens, not `.glass`.

### 2. Color ([col], [w356], [w219], [brand])

**What changed:** System color values were updated in 2025 for better hue separation alongside glass [new], [w356]. Glass has no color of its own and picks it up from the content behind it [col].

**System colors (sRGB 0–255), from the HIG color page** [col]. Apple says values can change between releases and should not be hard-coded in native apps. For the web they are the reference palette.

| Color | Light | Dark | Increased contrast, light | Increased contrast, dark |
|---|---|---|---|---|
| Red | 255,56,60 | 255,66,69 | 233,21,45 | 255,97,101 |
| Orange | 255,141,40 | 255,146,48 | 197,83,0 | 255,160,86 |
| Yellow | 255,204,0 | 255,214,0 | 161,106,0 | 254,223,67 |
| Green | 52,199,89 | 48,209,88 | 0,137,50 | 74,217,104 |
| Mint | 0,200,179 | 0,218,195 | 0,133,117 | 84,223,203 |
| Teal | 0,195,208 | 0,210,224 | 0,129,152 | 59,221,236 |
| Cyan | 0,192,232 | 60,211,254 | 0,126,174 | 109,217,255 |
| Blue | 0,136,255 | 0,145,255 | 30,110,244 | 92,184,255 |
| Indigo | 97,85,245 | 109,124,255 | 86,74,222 | 167,170,255 |
| Purple | 203,48,224 | 219,52,242 | 176,47,194 | 234,141,255 |
| Pink | 255,45,85 | 255,55,95 | 231,18,77 | 255,138,196 |
| Brown | 172,127,94 | 183,138,102 | 149,109,81 | 219,166,121 |

- The system gray values (gray through gray6) were not readable; the page shows them only as images. [unverified: use values from the iOS 27 UI Kit in Figma/Sketch, [res]]
- Semantic names to mirror as tokens: systemBackground plus secondary and tertiary; systemGroupedBackground plus secondary and tertiary; label, secondaryLabel, tertiaryLabel, quaternaryLabel; placeholderText; separator (translucent); opaqueSeparator; link. [col]

**Liquid Glass color rules** [col], [w219]
- Tinting makes glass look like stained glass. Use it only to emphasize a primary call to action or a status. The system's prominent button style works this way.
- For primary actions, color the background, not the symbol or text.
- DON'T color the backgrounds of several controls.
- On small elements (toolbars, tab bars), symbols and text default to monochrome that goes dark over light content and light over dark content.
- Large glass (sidebars) is more opaque to stay legible.
- Tinting maps a chosen color to a tonal range based on the brightness behind the glass, so hue, brightness and saturation shift the way real tinted glass does. [w219]
- Decorative color belongs in the content layer. An opaque solid fill on a glass control "breaks" the material, so use the built-in tint instead. [w219]
- Provide light, dark and increased-contrast variants for every custom color. Provide both light and dark even if the app ships in one appearance, because glass adapts. [col], [adopt]
- DON'T use color alone to convey meaning. DON'T reuse the semantic system colors for other purposes. [col]

**Web mapping** [impl]
- Tokens are `--sys-<color>` with `light`/`dark`/`hc-light`/`hc-dark` values, switched by `prefers-color-scheme` and `prefers-contrast: more`.
- `.glass--tinted` uses `background: color-mix(in oklab, var(--tint) 70–85%, transparent)` and approximates the brightness mapping with `mix-blend-mode: luminosity` or with overlay layers.
- Monochrome glass labels: `color: var(--glass-label)`, which is computed per region; use a JS luminance sampler or a fixed adaptive choice per theme.

### 3. Typography ([typ], [a11y], [w356])

**What changed:** Type is bolder and left-aligned in key places such as alerts and onboarding [w356]. Emphasized weights were added to the Dynamic Type specs on 2025-12-16 [typ], [new].

**Default and minimum sizes** [typ], [a11y]: iOS/iPadOS 17/11 pt, macOS 13/10, tvOS 29/23, visionOS 17/12, watchOS 16/12.

**iOS Dynamic Type, Large (default)** [typ]

| Style | Weight | Size/Leading (pt) | Emphasized |
|---|---|---|---|
| Large Title | Regular | 34/41 | Bold |
| Title 1 | Regular | 28/34 | Bold |
| Title 2 | Regular | 22/28 | Bold |
| Title 3 | Regular | 20/25 | Semibold |
| Headline | Semibold | 17/22 | Semibold |
| Body | Regular | 17/22 | Semibold |
| Callout | Regular | 16/21 | Semibold |
| Subhead | Regular | 15/20 | Semibold |
| Footnote | Regular | 13/18 | Semibold |
| Caption 1 | Regular | 12/16 | Semibold |
| Caption 2 | Regular | 11/13 | Semibold |

Body size and leading at each Dynamic Type step: xS 14/19, S 15/20, M 16/21, **L 17/22**, xL 19/24, xxL 21/26, xxxL 23/29, AX1 28/34, AX2 33/40, AX3 40/48, AX4 47/56, AX5 53/62. [typ]

macOS styles: Large Title 26/32, Title1 22/26, Title2 17/22, Title3 15/20, Headline Bold 13/16, Body 13/16, Callout 12/15, Subheadline 11/14, Footnote 10/13, Caption1 10/13, Caption2 Medium 10/13. [typ]

**Rules** [typ]
- Avoid Ultralight, Thin and Light weights. Prefer Regular, Medium, Semibold and Bold.
- Use as few typefaces as possible. Keep the hierarchy intact at every size.
- Use loose leading for long text and tight leading only in rows of fewer than 3 lines.
- Let text be enlarged by at least 200% (140% on watchOS). [a11y]
- At large sizes, stack items vertically and reduce the number of columns. Avoid truncation.
- SF is variable with optical sizes, and tracking changes with size. The tracking table was not in the fetched text. [unverified: tracking values]
- Section headers in lists now use title-style capitalization automatically. [adopt]

**Web mapping** [impl]
- Use `font-family: system-ui, -apple-system, "SF Pro Text", ...`. Define rem-based type tokens from the Large table (1 pt = 1 CSS px), and scale them with the user's font size via `rem` and `clamp`.
- Provide an `--emphasized` weight token per style.
- Left-align alert and dialog titles and body text, and use bolder title weights.

### 4. Layout ([lay], [w356], [adopt], [win], [ios])

**What changed:** Controls float on glass above content. Content runs edge to edge under bars, sidebars and inspectors. Shapes are concentric with the hardware and window corners. [lay], [w356], [adopt]

**Concentricity and corner radii** [w356], [adopt]
- There are three shape types:
  1. **Fixed**: a constant radius.
  2. **Capsule**: radius equals half the height.
  3. **Concentric**: child radius equals parent radius minus the padding between them. [w356]
- On iPhone, prefer capsules with extra margin from the screen edge. On iPad and Mac, prefer concentric shapes aligned to the window corners. [w356]
- Give nested-or-standalone components a concentric shape with a fallback radius. The fallback applies when the component has no parent. [w356]
- Avoid corners that look pinched or flared. [w356]
- Make custom corner radii in toolbars concentric with the bar's corners. [tb]
- Apple publishes **no numeric radii** for sheets, windows or bars. Sheets have a larger radius than before. Lists and forms have larger rows and section radii. [adopt] [unverified: exact values; measure from the iOS 27 UI Kit]
- Capsules are used across sliders, switches, bars, buttons and grouped tables. Use them sparingly in dense desktop UI. On macOS, Large controls are capsules and the new X-Large size uses glass. Mini, Small and Medium stay as rounded rectangles. [w356]

**Safe areas, margins and background extension** [lay], [side], [adopt]
- Keep content and controls inside the safe area: away from the Dynamic Island and the status, tool and tab bars. [lay]
- DON'T put solid or semi-opaque backgrounds under controls. Use the scroll edge effect instead. [lay]
- Extend full-screen backgrounds under sidebars, toolbars and tab bars. [lay]
- The background extension effect mirrors and blurs the adjacent image under a sidebar or inspector so a hero image fills the window while staying centered. [lay], [side], [adopt]
- Horizontal scroll views extend under the sidebar by default. [w356]
- DON'T let an image stop at the sidebar edge. [side]
- Base layout on size classes (compact/regular per axis), not on device. Change how much is shown, not what the app can do; for example, a tab bar can become a sidebar. [lay]
- Put controls in the middle or bottom of the screen for reach. [ios]
- Numbers that were found: tvOS safe zone 60 pt top/bottom and 80 pt sides; visionOS button centers at least 60 pt apart. [lay]
- iPhone/iPad dimension tables were not in the fetched text. [unverified]
- Windows have rounder corners and resize continuously. In iPad windowed mode, window controls sit at the leading edge of the toolbar, so move leading toolbar buttons inward. [adopt], [win]

**Web mapping** [impl]
- Tokens: `--radius-outer` (window/sheet), `--pad`, and `--radius-inner: max(calc(var(--radius-outer) - var(--pad)), var(--radius-fallback))`.
- A `.capsule` utility sets `border-radius: 9999px`.
- Use `env(safe-area-inset-*)` with `viewport-fit=cover`.
- Background extension: duplicate the hero image into a mirrored, blurred pseudo-element under the sidebar (`transform: scaleX(-1); filter: blur()`), or blur an overflow area.
- Use container queries for compact vs regular size classes.

### 5. Motion ([mot], [w219], [a11y])

**What changed:** Liquid Glass motion was added 2025-09-09. Glass responds more strongly to direct touch and more subtly to a trackpad. [mot]

**Rules**
- Add motion only with a purpose. Keep feedback motion brief and precise and consistent with the gesture; for example, something that slides down should be dismissed by sliding up. [mot]
- Motion must be cancelable. DON'T make people wait. DON'T add custom motion to frequent interactions. Motion can't be the only carrier of information. [mot]
- Controls morph between states on one floating plane. Menus pop out of the control that opened them. Buttons morph into menus and popovers. [w219], [adopt]
- Glass appears by materializing (lensing ramps up), not by a plain fade. [w219]
- Under Reduce Motion: tighten springs to reduce bounce, track gestures directly, avoid z-axis depth animation, replace x/y/z transitions with fades, and avoid animating in or out of blur. [a11y]
- No durations or easing curves are published. [unverified: durations]

**Web mapping** [impl]
- Morph popovers and menus from the trigger with the View Transitions API, or with FLIP that animates `clip-path`/`border-radius` from the button rect to the panel rect.
- Use a spring easing via `linear()` easing.
- Press state: `scale(1.02–1.05)` plus a radial highlight at the pointer (`--x`/`--y`).
- Under reduced motion, use opacity fades only.

### 6. Dark Mode ([dark])

- The HIG Dark Mode page has no Liquid Glass-specific changes; its last change is 2024-08-06. [dark]
- iOS backgrounds have two sets: **base** (dimmer) and **elevated** (brighter, used for foreground layers such as sheets and popovers). [dark]
- Contrast: at least **4.5:1**. Aim for **7:1** with custom colors, especially for small text. [dark]
- Test with Increase Contrast and Reduce Transparency, both separately and together. DON'T offer an app-only appearance switch. [dark]
- Slightly darken white-background images in dark mode. [dark]
- Glass needs both light and dark color variants even in a single-appearance app. [col]

Web mapping [impl]: Use `--bg-base` and `--bg-elevated` tokens, and apply elevated surfaces to modal layers in dark mode. Follow `prefers-color-scheme`. An in-app override is acceptable on the web only if it defaults to the system setting.

### 7. Accessibility ([a11y])

| Platform | Default hit target | Minimum |
|---|---|---|
| iOS/iPadOS | 44×44 pt | 28×28 pt |
| macOS | 28×28 | 20×20 |
| tvOS | 66×66 | 56×56 |
| visionOS | 60×60 | 28×28 |
| watchOS | 44×44 | 28×28 |

- Padding: about **12 pt** around bezeled elements and about **24 pt** around visible edges of unbezeled ones. [a11y]
- Contrast (WCAG AA): text up to 17 pt needs **4.5:1**. 18 pt and larger needs **3:1**. Bold text at any size needs **3:1**. [a11y]
- If the defaults fail, provide a high-contrast scheme under Increase Contrast. [a11y]
- Offer on-screen alternatives to gestures. Minimize time limits. Support Full Keyboard Access. [a11y]
- The page body has no Liquid Glass content. The glass accessibility modifiers are described in [w219] (see §1).

Web mapping [impl]: Use a `--hit-min: 44px` token, and enlarge hit areas invisibly with a `::before` inset. Test glass labels for 4.5:1 against the worst-case backdrop, or use the opaque fallback. Mirror the system settings through `prefers-contrast`, `prefers-reduced-transparency` and `prefers-reduced-motion`.

### 8. App icons ([icon], [adopt], [res])

**What changed:** Icons are now layered: one background layer plus one or more foreground layers. The system renders them as Liquid Glass, with specular highlights, refraction and translucency that adapt to size. Icon Composer is the authoring tool. [icon]

- Appearances: default, dark, clear light, clear dark, tinted light, tinted dark (6 variants). The system generates any variants you don't provide. [icon]
- Canvas: iOS/iPadOS/macOS 1024×1024 px, square, masked by the system to a rounded rectangle. watchOS 1088×1088, circular. visionOS 1024×1024, circular, 3D. tvOS 800×480, 2 to 5 parallax layers. [icon]
- DO: use crisp edges (no feathering), vary layer opacity in Icon Composer, and use solid or gradient backgrounds (any image must be full-bleed and opaque). Use vector SVG/PDF, or PNG for mesh gradients. Keep content centered on the template grid. Keep core features the same across all variants. [icon]
- DON'T: pre-mask layers, bake in blur, shadows, highlights or bevels (the system adds these dynamically), rely on outline-only shapes, use thin lines, or add text except a mnemonic letter. [icon], [adopt]
- A bitmap icon is auto-split into 2 layers. Glass applies only to large, simple foreground shapes. Use a layered Icon Composer file to control this. [icon] (format guidance added 2026-10-28)
- iOS 27 (press): crisper edges and optional refraction layers in the artwork. [cult27], [cultsotu]
- Icon Composer needs macOS Sequoia or later. It exports to Xcode and can flatten an icon for marketing. [res]

Web mapping [impl]: For PWA and favicon sets, export flattened variants (default/dark/tinted-mono) from Icon Composer. In UI, imitate the look with layered SVG: a background gradient plus foreground shapes with a specular `linearGradient` overlay. Use `prefers-color-scheme` to swap in the dark icon.

### 9. SF Symbols ([sfs], [res], [ico])

- SF Symbols 7 (2025) added **Draw On/Off** animations and **gradient** rendering, and Magic Replace became the default replace. [sfs]
- The current download is labeled **SF Symbols 27**, with more than 7,000 symbols. [res] (The HIG page itself still describes features through version 7. [sfs])
- Rendering modes: monochrome, hierarchical, palette, multicolor. There are 9 weights and 3 scales (small, medium default, large, relative to cap height). [sfs]
- Variants: outline (toolbars, lists, next to text), **fill (iOS tab bars**, swipe actions, selection), slash, enclosed. [sfs]
- Use variable color for changing values and hierarchical mode for depth. Use animation sparingly and with a purpose. [sfs]
- Standard action symbols [ico]: Done `checkmark`, Cancel `xmark`, Delete `trash`, Add `plus`, More `ellipsis`, Share `square.and.arrow.up`, Search `magnifyingglass`, Filter `line.3.horizontal.decrease`, Compose `square.and.pencil`, Undo/Redo `arrow.uturn.backward`/`forward`, Copy `document.on.document`, Paste `document.on.clipboard`, Select `checkmark.circle`, Account `person.crop.circle`.
- Use the standard Back and Close symbols, not the words "Back" or "Close". [tb]
- The primary action in iOS bars is often a tinted checkmark. [w356]

Web mapping [impl]: SF Symbols are licensed only for Apple platforms, so on the web use a look-alike set (for example Lucide or Phosphor) mapped to the same semantic names. Match icon stroke weight to the adjacent text weight. Use filled icons for the selected tab bar item.

### 10. Branding ([brand], [w251])

- Use accent color sparingly: for primary actions, status, badges and the selected tab. Use it on as few controls as possible. [brand]
- **Move brand color into the content layer**, where it scrolls under the glass and the glass picks it up. Don't paint toolbars or tab bars with solid brand color; solid bars look heavy and shrink the content area. [brand], [w251]
- Customize only standard components that matter. Keep standard sizes, placement and popover behavior. Utility components such as context menus stay unbranded. [w251]
- Custom fonts are fine for headlines, with system fonts for body text. They must support Dynamic Type and Bold Text. [brand], [w251]
- Show the logo sparingly, for example only on the home screen, fading on scroll. DON'T brand the launch screen. [brand], [w251]
- Use full-bleed brand content (gradients, video) behind the glass. [w251]

Web mapping [impl]: Navigation chrome takes no brand fill by default. Apply `--brand` to hero/content backgrounds and to `.btn--prominent` only.

---

## COMPONENTS

### 11. Buttons ([btn], [adopt], [w356])

**What changed:** New glass button styles: glass, glass prominent (tinted), clear glass, and prominent clear glass. There is also an extra-large control size. Buttons can morph into menus and popovers. Bars in the system use rounder, capsule shapes. [adopt], [w356]

**Rules** [btn]
- The hit region must be at least **44×44 pt** (60×60 on visionOS).
- Always include a pressed state.
- Use one or two prominent buttons per view at most, for the most likely action. Show preference with style, not size; buttons in a set share one size.
- DON'T give button labels a color similar to bright content behind them. Prefer the monochrome default.
- Labels start with a verb and use title case ("Add to Cart").
- Roles: normal, primary (accent color, responds to Return), cancel, destructive (system red). Never give the primary role to a destructive button.
- On iOS, show an activity indicator inside the button for slow actions; the label can change to something like "Checking out…".
- visionOS sizes: mini 28, small 32, regular 44, large 52, XL 64 pt. Use a circle for icon-only buttons and a capsule for text or icon plus text. [btn]
- watchOS: inline buttons are capsules, and primary actions are full width. [btn]

**Web component** [impl]: `<button class="btn" data-variant="glass|glass-prominent|clear|clear-prominent|plain" data-size="sm|md|lg|xl">`. Capsule by default on touch layouts, with a minimum height of 44px. Prominent buttons use a tinted glass background and white/monochrome label. Includes `:active` scale and highlight and an `aria-busy` spinner slot.

### 12. Tab bars ([tab], [adopt], [w356])

**What changed:** On iOS the tab bar floats on glass above the content at the bottom. It can minimize on scroll. It can carry a bottom accessory, such as a mini player. Search is a dedicated tab at the trailing end. On iPad the tab bar is at the top and can turn into a sidebar. [tab], [adopt]

**Behaviors**
- **Minimize on scroll** (`.onScrollDown`). When the user scrolls down, the bar collapses to the current tab at the leading bottom corner. The accessory moves inline in the center and search stays in the trailing corner. Tapping a tab or scrolling to the top (the API also expands on reverse scroll) restores it. [tab], [adopt]
- The **bottom accessory** sits above the expanded tab bar and inline when the bar is minimized. Use it for persistent features such as playback, not for screen-specific actions like checkout. [tab], [w356]
- **Search tab** (role search) sits at the trailing end. As of iOS 27 it has two appearances: a **standard tab** (opens a search landing page with the field at the top, for discovery) or a **button** (focuses the field and opens the keyboard right away, then returns to the previous tab on exit). [search], [adopt]

**Rules** [tab]
- Use the tab bar only for navigation; actions belong in toolbars.
- Always show labels (single words). Icons go above labels in compact layouts and beside them in regular layouts. Use filled SF Symbols.
- Keep the bar visible except in modal views.
- DON'T hide or disable tabs when content is empty; explain why the section is empty instead.
- Avoid the overflow "More" tab. Keep customizable defaults to 5 or fewer.
- Badges are red ovals with white text (a number or "!"), for critical information only.
- Over colorful content, use a monochrome bar or an accent with enough contrast.
- On iPad, choose a fixed tab bar (tabBarOnly) or one that can become a sidebar (sidebarAdaptable).
- tvOS: 68 pt tall, 46 pt from the top. [tab]

**Web component** [impl]: `<nav class="tabbar glass capsule">` fixed at `bottom: max(env(safe-area-inset-bottom), 8px)` with horizontal margins (an inset capsule, not edge to edge). An IntersectionObserver or scroll-direction hook toggles `data-minimized`, which animates width to the current tab, slides the accessory inline and keeps the search button separate. At the regular breakpoint, move it to the top or turn it into a sidebar.

### 13. Toolbars / navigation bars ([tb], [adopt], [w356])

The navigation bar page was merged into Toolbars (2025-06-09); its standalone JSON returned an error. [tb], [new]

**What changed:** Bar items float as glass groups (pill-shaped clusters) with no bar background. Custom bar backgrounds and decoration are removed. The primary action is separated and tinted. [tb], [adopt], [w356]

**Rules**
- There are three areas: leading (back, title, document menu), center (common controls, which collapse to overflow first), and trailing (essential items, search, More, primary action). [tb]
- Group items by function and frequency, **at most 3 groups**. Keep Done, Close and Save in their own distinct sections. [tb]
- Items in one group share one glass background. Separate groups with fixed spacers. [adopt]
- Prefer symbols. DON'T mix text and symbols in the same group, because they read as one button. Text buttons get their own container. [tb], [w356], [adopt]
- Use symbols without borders or circles; the group supplies the container. [tb]
- **One** primary action, placed trailing, with the prominent (tinted) style. [tb]
- Titles should be under **15 characters**, and never the app name. Large titles collapse to inline on scroll. [tb]
- Hide whole items, not their contents. The system adds overflow automatically; don't build an overflow menu by hand. [adopt], [tb]
- Reduce custom backgrounds and tints, and let the content set the bar's color. Labels should not match content-layer colors. [tb]
- On iPad, the toolbar and tab bar can share the top area. [tb]
- iOS 27 SwiftUI adds toolbar priority and overflow APIs for resizing windows (press). [cultsotu]

**Web component** [impl]: `<header class="toolbar">` is transparent. Inside it, `.toolbar-group.glass.capsule` holds icon buttons at 44px each, there is a centered title (or a large title in the content that shrinks via scroll-driven animation), and a trailing `.btn--prominent` holds the primary action. Use a ResizeObserver to move center items into a "…" menu. Combine with the scroll edge effect (§24).

### 14. Sidebars ([side], [w356], [w219], [cult27])

**What changed:** Sidebars float on glass, inset from the window edges, and content flows behind them through the background extension effect. On iPad and Mac the sidebar and tab bar act as one navigation element. [side], [w356], [w219]

**iOS 27 / macOS 27** (press): sidebars run to the window edge, and sidebar icons regain color in the active app. [cult27], [neo]. The HIG notes a 2026-06-08 change to sidebar icon colors and says icons use the accent color by default; fixed colors are used sparingly, for example a yellow VIP icon. [side]

**Rules** [side]
- No more than **2 levels** of hierarchy; use a split view for deeper structures.
- Use disclosure controls for groups. Let people customize contents.
- DON'T hide the sidebar by default.
- Use SF Symbols, or custom symbols rather than bitmaps.
- On macOS, keep critical actions out of the sidebar's bottom area.
- Prefer a tab bar first on iPhone/iPad, using a sidebar-adaptable tab view.
- No widths are published. [unverified]

**Web component** [impl]: `<aside class="sidebar glass">` inset by `--pad` with a concentric radius against the window/viewport container. The main content's hero uses a mirrored, blurred extension under the sidebar. Collapses to the tab bar below the regular breakpoint.

### 15. Split views ([split], [adopt])

- 2 or 3 panes. Persistently highlight the selection in each pane that leads to a detail view. [split]
- Support narrow, intermediate and wide widths, because iPad windows resize fluidly. [split]
- macOS dividers are 1 pt thin by default. Set minimum and maximum pane sizes. [split]
- Each pane can have its own scroll edge effect, but at consistent heights. [scroll], [w356]
- An inspector column is a standard part of split views. [adopt]
- The page has no Liquid Glass content. [split]

Web mapping [impl]: CSS grid with `minmax()` columns and container queries. The sidebar is a glass pane; the content and detail panes are content layer.

### 16. Sheets ([sheet], [adopt], [w356])

**What changed** [adopt], [w356]
- Larger corner radius.
- **Partial-height (half) sheets are inset** from the screen edges, so content shows around them, and they are glass.
- **At full height they become more opaque** and grow slightly, signaling deeper engagement.
- Use a dimming layer for interrupting (modal) tasks. For parallel tasks, use glass without dimming.
- Check that sheet content does not crowd the rounder corners.

**Rules** [sheet]
- Detents: `large` (full) and `medium` (about half).
- Include a grabber on resizable sheets; tapping it cycles through detents.
- Support swipe to dismiss, and confirm unsaved changes with an action sheet.
- Show one sheet at a time.
- Buttons (updated 2026-03-24): Cancel or Close goes leading and Done goes trailing in the top toolbar. In multi-step flows, Back replaces Cancel after the first step and Done is enabled only on the last step. Never show Cancel, Done and Back together. Pair Done with Cancel or Back.
- Use Close as an `xmark` symbol and Done as a `checkmark` symbol. [ico], [tb] [unverified: that sheets specifically use these symbols, though the toolbar guidance implies it]

**Web component** [impl]: A `<dialog class="sheet">` with `data-detent="medium|large"`.
- At medium: `inset-inline: 8px; bottom: 8px`, a concentric radius with the device corner (for example 32–38px) [unverified: radius], and glass.
- At large: flush to the edges (top radius only), with a near-opaque background.
- Transition the opacity and inset with the detent.
- `::backdrop` dims only for modal use.
- Includes a drag handle and a pointer-driven drag between detents.

### 17. Popovers ([pop], [adopt])

- Keep functionality small. The popover is only as big as its content, its arrow points at the source, and it avoids covering the source. [pop]
- Animate size changes. Never cascade popovers or show more than one. DON'T use popovers for warnings. [pop]
- Avoid popovers in compact width; use a sheet. [pop]
- Liquid Glass: remove custom visual effect views from popover content (the system supplies the glass). Buttons morph into popovers. [adopt]
- The HIG page itself has no Liquid Glass content. [pop]

Web mapping [impl]: Use the HTML `popover` attribute with CSS anchor positioning (`position-anchor`). The panel is glass with a thicker material (stronger shadow) because it is larger. It morphs from the trigger's rect and falls back to the sheet component under the compact breakpoint.

### 18. Menus ([menu], [w219], [w356], [adopt])

**What changed:** Menus pop open from the control that triggered them, morphing out of it on the same glass plane. Expanded glass gets thicker-material qualities. Menus across platforms share an anatomy: selection indicator, icon, label, accessory. Icon guidance was added in 2025 and refined 2026-06-08. [w219], [w356], [menu]

**Rules** [menu]
- Labels are verbs in title case, without articles. Append an ellipsis when more input is needed.
- Unavailable items are dimmed.
- Icons: either every item in a group has one or none does. Use standard icons for Copy, Share and so on. For closely related actions, one symbol introduces the group and text distinguishes the items. [menu], [w356]
- Separate groups with separators or short gaps. Put frequent items first.
- One submenu level only, about 5 items or fewer; mark it with a chevron.
- Use one toggle item whose label changes, and checkmarks for attributes in effect.
- iOS layouts: small (a row of 4 icon-only items above the list), medium (a row of 3 items with icon and label), large (the default list).

**Web component** [impl]: A `menu` popover anchored to its button that morphs from the button's capsule into a rounded panel (FLIP on width, height and radius). Optional `.menu-row--small` (4 icon cells) and `.menu-row--medium` (3 icon+label cells). Rows at least 44px tall. Supports separators, submenus with a chevron, and a destructive red row.

### 19. Context menus ([ctx])

- Keep them short, with about **3 groups** or fewer.
- Order items so the most frequent ones are nearest the finger; reverse the order if the menu opens above.
- Destructive items go **last** and red.
- One submenu level.
- Hide unavailable items rather than dimming them (macOS Cut/Copy/Paste are exceptions).
- Every item must also be available elsewhere in the UI.
- No keyboard shortcuts in context menus.
- A preview can appear on iOS; its clipping path must match during the animation.
- The top actions should match the item's swipe actions. [adopt]
- The page has no Liquid Glass content (last change 2023-12-05). [ctx]

Web mapping [impl]: Open on `contextmenu` and long-press. Reuse the glass menu panel and add an optional scaled preview card above it.

### 20. Alerts and action sheets ([alert], [as], [adopt], [w356])

**What changed:** Alert typography is bolder and **left-aligned** [w356]. **Action sheets now come from the element that triggered them** (popover-like), not from the bottom edge, and the rest of the UI stays interactive [adopt], [w356]. The Alerts HIG page itself has not changed since 2024-02-02 [alert].

**Alert rules** [alert]
- A title, optional message, and **up to 3 buttons**.
- Titles at most 2 lines, never vague ("Error").
- Button labels are 1–2 words, title case, preferably verbs. Use "OK" only for purely informational alerts, and never Yes or No.
- The most likely or default button goes trailing in a row or on top in a stack. Cancel goes leading or at the bottom.
- Destructive style only for actions people did not deliberately choose. Always include Cancel with a destructive action, and never make Cancel the default.
- Avoid alerts at launch and for undoable actions. Avoid scrolling alerts.

**Action sheet rules** [as]
- Titles fit on one line. Messages are optional and rare.
- Destructive buttons go on top with the destructive style. Cancel goes at the bottom.
- No scrolling.
- Use an action sheet for choices tied to an intentional action, and a menu when people open it on purpose.

**Web component** [impl]
- `<dialog class="alert glass">`: left-aligned bold title, 2–3 buttons (side by side when there are 2 short labels, stacked otherwise), a dimmed backdrop, and Escape mapped to Cancel.
- Action sheet: anchored to its source with anchor positioning, a glass panel, non-modal.

### 21. Search fields ([search], [adopt], [w356])

**What changed:**
- iOS 26: search moved to the bottom on iPhone, either as a search tab at the trailing end of the tab bar or in a bottom toolbar. On iPad it is at the upper trailing side of the toolbar. When focused, the field slides up above the keyboard. [adopt], [search]
- iOS 27 (2026-06-08): search as a tab is refined into a **standard tab** (landing page) or a **button appearance** (instant field). [search]

**Placement rules** [search]
- iPhone:
  - A tab bar search tab.
  - Or a bottom toolbar, as an expanded field or a button that expands. Use this if there is room and search is a priority (Settings, Mail, Notes).
  - Or a top toolbar button, when bottom placement would cover a primary function (Wallet).
  - Or an inline field above the list it filters, which can pin to the top bar on scroll.
- iPad/Mac: the trailing toolbar for searching across columns, the top of the sidebar for filtering the sidebar, or a sidebar/tab item for a dedicated discovery area.
- Focus immediately in dedicated search areas, except on iPad with only a virtual keyboard.
- Show recent searches before typing and live suggestions while typing. Search as people type.
- Scope bars go from broad to narrow scope, with the broad scope as the default. Tokens represent filters; pair them with suggestions.
- Placeholder text names what can be searched.

**Web component** [impl]
- `.search-field.glass.capsule` at 44px tall, with a magnifier icon and a clear (x) button.
- Mobile: dock it at the bottom above the tab bar or in the bottom toolbar, and use the `visualViewport` API to ride above the on-screen keyboard.
- It can collapse to a circular glass button that expands into the field.
- Desktop: put it trailing in the toolbar.

### 22. Segmented controls ([seg])

- 2 to about 5 segments on iPhone, about 5–7 on wide layouts. Segments have equal width.
- Labels are nouns in title case. Use text or icons, never both in one control.
- Use a segmented control for switching closely related views. Use a tab bar for app sections.
- No Liquid Glass guidance on the page; the last change was 2023-06-21. [seg]
- The system control appears with glass in iOS 26 kits. [unverified]

Web mapping [impl]: A capsule track (fill token) with a sliding selection thumb that turns glass while it is being dragged (by analogy with toggles and sliders [mat]). Built as `role="radiogroup"`.

### 23. Toggles, sliders and steppers ([tog], [sld], [step], [mat], [adopt])

- **Liquid Glass**: slider and toggle knobs transform into glass while being touched, then return to their solid look at rest. [mat], [adopt]
- Neither the toggles page nor the sliders page discusses glass. [tog], [sld]
- **Toggles**: use a switch only inside a list row on iOS. The default on-color is green; an accent color is OK if it contrasts with the off state. Outside lists, use a toggle-style button (its highlighted background shows state). Never show state with color alone. [tog]
- **Sliders**: the track fills from minimum to the thumb. Minimum is leading, maximum is trailing. Optional min/max icons. Pair with a text field or stepper for precise input. DON'T use a slider for iOS volume. [sld]
- **Steppers**: put the stepper next to the value it changes, and pair it with a field for large ranges. [step]
- Don't hard-code the metrics of these controls; their dimensions changed in iOS 26. [adopt]

Web mapping [impl]
- Switch: a 51×31-ish capsule [unverified: classic iOS switch size] whose knob scales up and gets a `.glass` class on `:active`/pointerdown, with a spring back.
- Slider: a capsule track with an accent fill, and a knob that enlarges into a glass lens while dragging.
- Stepper: a glass-group capsule with − and + buttons, each at least 44px.

### 24. Lists, tables and scroll views ([list], [scroll], [adopt], [w356], [w219])

**Lists** [adopt], [list]
- Larger row heights, more padding, and larger section corner radii. Section headers in title case. The table content stays in the content layer, not glass.
- Persistently highlight selected rows in navigation lists. Use a checkmark for option lists. Use a disclosure indicator to drill in.
- Don't combine an A–Z index with trailing controls.
- Row heights are not published. [unverified]

**Scroll edge effect** [scroll], [w356], [w219]
- Purpose: separation between floating glass bars and the content scrolling under them. It is not decorative and does not darken like an overlay.
- **Soft**: a variable blur fading out downward, the iOS default under glass buttons and fields.
- **Hard**: a more opaque blur with a defined edge, mostly on macOS. Use it for text outside glass controls, backgroundless controls, or pinned table headers.
- **Automatic** is preferred. Apple's 2026 wording says automatic gives the more opaque separation for top bars with many controls, text outside glass, and pinned headers.
- One effect per view. Don't mix or stack soft and hard. In split views each pane can have its own effect, at consistent heights.
- Use it only where floating UI overlaps scrolling content.
- When dark content scrolls under the bar, the effect turns into subtle dimming. [w219]
- DON'T put solid or semi-opaque backgrounds under bars. [lay]
- Other rules: make it obvious content scrolls (show partial content at the edge), and don't nest same-axis scroll views. [scroll]

**Web component** [impl]
- Soft: a pseudo-element under the sticky bar with `backdrop-filter: blur()` and a `mask-image: linear-gradient(to bottom, #000, transparent)`, height about the bar height plus 20px.
- Hard: the same with no mask, a higher-opacity bg and a hairline.
- Show it only when `scrollTop > 0`, using a scroll-driven animation (`animation-timeline: scroll()`).

### 25. Page controls ([page])

- Dots, one per page. More than about **10** gets hard to count, so use a grid instead.
- Center horizontally at the bottom.
- iOS background styles: automatic (background only during interaction), prominent (always, only when it is the primary navigation), and minimal (never; no scrubbing allowed).
- Don't color the indicators. Use at most 2 custom indicator images.
- Tap the left or right side of the current dot to step, and scrub to move continuously. Don't animate while scrubbing.
- Edge dots shrink when there are many (about 1/2 and 1/4 size, from the image alt text only). [page]
- No Liquid Glass content on the page. [page]

Web mapping [impl]: `.page-control`, with an optional `.glass.capsule` background shown on hover or interaction. Dots are 8px with 8px gaps [unverified: sizes]; the current dot uses `--label`, the others `--tertiary-label`.

### 26. Progress indicators ([prog])

- Determinate when possible: bars fill from leading to trailing, circular indicators fill clockwise. Indeterminate means a spinner (and a bar on macOS only).
- Keep the indicator moving. Switch from indeterminate to determinate once the duration is known.
- Don't jump to 90% and then crawl. Don't switch between bar and circle mid-task.
- Offer Cancel and Pause where it makes sense.
- Refresh control: pull to refresh. Still update automatically too.
- No Liquid Glass content on the page. [prog]

Web mapping [impl]: Capsule tracks with an accent fill, `role="progressbar"` with `aria-valuenow`, and a spinner that respects `prefers-reduced-motion`.

---

## 27. Design resources ([res], [new], [vid])

- iOS/iPadOS **27** UI Kit: Figma and Sketch. macOS 27 UI Kit: Figma and Sketch. App Icon Template for iOS, iPadOS and watchOS 27: Figma, Sketch and PS/AI. watchOS 26 and visionOS 26 kits. tvOS 18 kits. [res]
- The iOS 26 kit (June 2025) was rebuilt with Liquid Glass materials, updated colors and components. Figma kits for visionOS and watchOS were added 2025-08-20. [new]
- Icon Composer: needs macOS Sequoia or later; for layered Liquid Glass icons for iPhone, iPad, Mac and Watch, with appearance annotations. [res]
- SF Symbols 27 (download `SF-Symbols-27.dmg`): more than 7,000 symbols, 9 weights, 3 scales. [res]
- Fonts: SF Pro, SF Compact, SF Mono, New York, plus localized SF fonts. [res]
- WWDC26 design sessions: 250 "Principles of great design", 251 "Communicate your brand identity on iOS", 290 naming and labels, 292 "Design intuitive search experiences", and 8012 Icon Composer lab. [vid]
- WWDC25 sessions: 219 "Meet Liquid Glass", 356 "Get to know the new design system", 220 icons, 361 Icon Composer. [new]

Recommendation [impl]: take the missing numbers (corner radii, bar heights, gray values, switch and segment sizes, list row heights) from the iOS 27 Figma UI Kit. The HIG does not publish them.

---

## 28. Pages that could not be loaded

- `https://developer.apple.com/tutorials/data/design/human-interface-guidelines/whats-new.json`: client error. I used `https://developer.apple.com/design/whats-new/` instead, which only lists entries through 2025-12-16.
- `.../human-interface-guidelines/changelog.json` and `.../tutorials/data/design/whats-new.json`: client error.
- `.../human-interface-guidelines/navigation-bars.json`: client error. The content was merged into Toolbars on 2025-06-09.
- `.../human-interface-guidelines/liquid-glass.json`: no such HIG page (client error). Liquid Glass guidance lives in Materials plus the developer docs.
- `https://developer.apple.com/documentation/technologyoverviews/liquid-glass` (HTML): returned only metadata. The JSON at `/tutorials/data/documentation/...` worked.
- WWDC26 sessions 292 and 269: the transcript pages came back empty.
- Partly read: the gray values on the Color page (images only), the tracking table on Typography, and the tvOS type table, which was cut off.
- `curl` to developer.apple.com is blocked by the sandbox proxy (403), so everything went through WebFetch.

## 29. Key gaps, marked [unverified]

These numbers are not in any fetched page and have to come from the UI kits or be measured:
- sheet, window, bar and list corner radii
- tab bar and toolbar heights and insets on iOS
- glass blur radius and opacity values
- system gray values
- SF tracking values
- iPhone/iPad dimension tables
- switch, segment and slider dimensions
- list row heights
- animation durations and springs
