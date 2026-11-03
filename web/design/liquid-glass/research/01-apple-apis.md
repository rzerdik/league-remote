# 01 — Apple Liquid Glass: official API & behavior reference (for a WEB design system)

Compiled 2026-11-02 from Apple developer documentation (JSON data endpoints), the Human Interface Guidelines, and WWDC25/WWDC26 session transcripts. Everything here is paraphrased. Each claim carries a source tag; the tags resolve in the **Sources** table at the end.

**Confidence notes**
- Pages were read through a fetch tool that summarizes them. Facts tagged with a source were stated on that page. Inferences are tagged **[inference]**. Anything not confirmed on a fetched Apple page is tagged **[unverified]**.
- Web-equivalent columns and the "Web translation" notes are this author's mapping, not Apple guidance.
- Apple gives **no numeric values** for blur radius, refraction strength, highlight intensity, shadow size, or the default container `spacing`. The only official number found is the **35% dark dimming layer** for clear glass [HIG-MAT]. Every other value in a web implementation has to be tuned by eye.

---

## 1. What the material is

| Concept | What Apple says | Source |
|---|---|---|
| Definition | A dynamic material that combines the optical properties of glass with a sense of fluidity. | [TO-LG] [TO-ADOPT] |
| What it does to the backdrop | It blurs the content behind it, reflects color and light from surrounding content, and reacts to touch and pointer input in real time. | [SUI-CUSTOM] |
| Lensing, not scattering | The defining optical effect is *lensing*: the material bends and concentrates light in real time. Apple contrasts this with earlier materials, which scattered light. Lensing separates the glass from the background while keeping it light and transparent. | [V-219] |
| Thickness and size | Larger or thicker glass shows stronger lensing and refraction, softer light scattering, and deeper, richer shadows. | [V-219] |
| Opacity by size | Larger glass is more opaque. Smaller glass is clearer and flips between light and dark more readily to keep contrast. | [V-284] |
| Specular highlights | Light sources in the environment produce highlights that follow the element's geometry. During system interactions such as lock and unlock, the lights move across the material and trace its silhouette. In some cases the highlights respond to device motion. | [V-219] |
| Adaptive shadow | Shadow opacity rises over text and falls over solid light backgrounds, so the element stays easy to spot. | [V-219] |
| Continuous adaptation | Each layer of the material keeps adapting to what is behind it. It has no fixed light or dark look. Dynamic range and tint shift to keep controls legible while letting as much content through as possible. | [V-219] |
| Small vs. large elements | Small elements (navigation bars, tab bars) flip between light and dark depending on the background. Large elements (menus, sidebars) adapt but do **not** flip, because switching over a large area would be distracting. | [V-219] |
| Foreground on glass | Symbols and glyphs on regular glass flip light and dark automatically for contrast. Text on glass adapts for legibility. UIKit labels in the glass `contentView` become vibrant automatically based on their semantic text color. | [V-219] [V-323] [V-284] |
| Color spill | Sidebars on iPad and Mac pick up light from nearby colorful content. The color spills onto the surface and into its shadow. | [V-219] |
| Appearance plumbing | The glass follows the light/dark user interface style (UIKit) or `NSAppearance` (AppKit), and responds to the colors behind it. | [V-284] [V-310] |
| What the SwiftUI modifier renders | `glassEffect` draws a shape filled with the material, anchored **behind** the view and sized to the view's bounds including padding. It then applies the material's *foreground* effects over the view's content. | [SUI-GLASSEFFECT] |
| Glass can't sample glass | Glass samples a region larger than itself, but it cannot sample other glass. Glass elements in separate containers therefore render inconsistently. A shared container gives them one sampling region and a uniform appearance. It also needs only one render pass. | [V-323] [V-310] [V-284] [APPKIT-CONTAINER] |
| Materialize and dematerialize | Glass appears and disappears by gradually changing how much it bends and lenses light, not by a plain fade. | [V-219] |
| Focus loss | When a window loses focus on Mac or iPad, the glass visually recedes. | [V-219] |
| iOS 27 refinements | The material diffuses complex content behind it more effectively, for readability. A darkened edge and brighter specular highlights add depth and separation. Refraction becomes an **optional** effect that apps can apply selectively. The specific API name was not given in the transcript **[unverified]**. | [V-2026-102] |

**Web translation [inference]:** `backdrop-filter: blur() saturate()` covers the scatter and frost part. Lensing needs an SVG `feDisplacementMap` (or a WebGL/canvas shader) whose strength grows toward the edges. Specular highlights work as a `::before` gradient or an inset highlight (`box-shadow: inset 0 1px …`) that can follow the pointer or device orientation. The adaptive shadow is a `box-shadow` whose opacity is set per context. Apple's iOS 27 "darkened edge" maps to a subtle inner dark rim.

---

## 2. Layering model (the core architectural rule)

- **There are two layers.** The content layer holds the app's own content. A functional or navigation layer of Liquid Glass floats above it and holds controls and navigation: tab bars, sidebars, toolbars. Content can scroll beneath that layer. [TO-ADOPT] [HIG-MAT] [V-219] [V-356]
- **Glass belongs only in the navigation layer.** Apple says glass is "best reserved for the navigation layer that floats above the content" [V-219]. A table view, for example, stays in the content layer [V-219]. The HIG says not to use Liquid Glass in the content layer, and to use standard materials there instead, for example for app backgrounds [HIG-MAT].
- **Exception:** transient content-layer controls such as sliders and toggles take on Liquid Glass *only while someone is interacting with them* [HIG-MAT]. Their knobs turn into glass during interaction [TO-ADOPT].
- **Never stack glass on glass.** It becomes cluttered and confusing [V-219]. Avoid layering Liquid Glass elements on top of each other [TO-ADOPT].
- **Elements on top of glass** should use fills, transparency, and vibrancy, so they read as a thin overlay that belongs to the material [V-219].
- **Nested glass in UIKit:** a glass view added to another glass view's `contentView` adapts its appearance automatically [V-284]. This is the framework handling it. The design rule against glass on glass still stands.
- **Apply glass to the control, not to its inner views** [V-356]. In AppKit, put content *inside* `NSGlassEffectView.contentView`, not beside the glass view as a sibling placed behind content [V-310].
- **Brand identity belongs in the content layer.** Apple calls the content layer the best place to express brand identity. Remove solid bar backgrounds and let color live in the scrolling content [V-2026-251].
- **Steady states:** avoid content and glass intersecting at rest, for example at launch. Reposition or scale content to keep them apart [V-219].

---

## 3. Variants

| Variant | Behavior | When to use | Source |
|---|---|---|---|
| **regular** (default) | Adaptive and legible in any context, at any size, over any content. It blurs the background and adjusts its luminosity. Most system components use it. | The default. Use it whenever the background could hurt legibility, or when the component holds a lot of text (alerts, sidebars, popovers). | [V-219] [HIG-MAT] [SUI-GLASS] |
| **clear** | No adaptive behavior. It is permanently more transparent, so content shows through more vividly. It **requires a dimming layer** for legibility. | Only over media-rich content, and only when the content layer can tolerate dimming and the content on top of the glass is bold and bright. All three conditions must hold. | [V-219] [HIG-MAT] |
| **identity** | Content renders as if no glass were applied. | A no-op. Useful for toggling glass off conditionally without changing the view tree. The use case is an **[inference]**. | [SUI-GLASS] |

**Rules for clear glass**
- Don't mix regular and clear glass [V-219].
- Use clear glass only over visually rich backgrounds such as photos and video [HIG-MAT].
- **Dimming:** if the underlying content is bright, add a dark dimming layer at **35% opacity**. Skip it if the content is already dark enough, or if you use the standard AVKit playback controls, which provide their own dimming [HIG-MAT].
- For small elements, a *localized* dimming layer keeps more of the content's vibrancy than a full-screen one [V-219].

**Variants by framework:** SwiftUI has `Glass.regular`, `.clear`, and `.identity` [SUI-GLASS]. UIKit's `UIGlassEffect.Style` has `.regular` and `.clear` [UIK-GLASS-STYLE]. AppKit's `NSGlassEffectView.Style` has `.regular` and `.clear` [APPKIT-GLASS-STYLE]. UIKit buttons come in four configurations: `glass()`, `prominentGlass()`, `clearGlass()`, and `prominentClearGlass()` [UIK-BTN].

**Web translation [inference]:** use a `variant` prop with `regular`, `clear`, or `none`. Clear should require, or automatically add, a scrim at `rgba(0,0,0,.35)` when the backdrop is bright. Lint against mixing `regular` and `clear` within one cluster.

---

## 4. Tint

- Tinting generates a **range of tones** from the chosen color, mapped to the brightness of the content behind the glass. This mimics real colored glass, whose hue, brightness, and saturation shift with what is behind it [V-219].
- A solid opaque fill breaks the material's character. The built-in tint stays transparent and grounded [V-219].
- **Tint only primary elements and actions.** If everything is tinted, nothing stands out [V-219]. Use tint "to convey meaning and not just for visual effect" [V-323], for example a call to action.
- For broader color, put it in the **content layer**, not on the glass [V-219] [V-2026-251].
- In UIKit, tinted glass adapts automatically to a vibrant version of the color. Animate the tint change together with related content changes such as label color [V-284].
- Toolbars: icons use **monochrome** rendering to reduce visual noise [V-323]. In UIKit, `tintColor` on a bar button colors only the symbol, and `style = .prominent` tints the background [V-284]. In AppKit, `NSToolbarItem.style = .prominent` uses the accent color and `backgroundTintColor` sets a specific color [V-310].
- AppKit's `tintProminence` has four levels: `automatic`, `none`, `secondary`, and `primary`. Destructive buttons use a red tint at a level that doesn't overpower nearby controls. The default button (Return key) gets primary prominence automatically [V-310].
- Bars: keep the primary action (such as Done) separate and tinted. On iOS this is usually a blue checkmark [V-356].
- Controls: use color sparingly. Use system colors, or define custom colors with light, dark, and increased-contrast variants [TO-ADOPT].

**Web translation [inference]:** tint is a translucent color layer blended over the backdrop (for example `color-mix()` at low alpha, combined with `mix-blend-mode: overlay` or `soft-light`) rather than an opaque `background-color`. Expose it only on `prominent` or `primary` variants.

---

## 5. Interaction (`interactive`)

- `Glass.interactive(_:)` (default `true`) makes custom glass respond to touch and pointer input the same way the standard `.glass` button style does [SUI-CUSTOM] [SUI-INTERACTIVE].
- On iOS, interactive glass **scales, bounces, and shimmers** on touch, like toolbar buttons and sliders [V-323].
- In UIKit, `UIGlassEffect.isInteractive = true` makes glass scale and bounce on tap [V-284] [UIK-GLASS]. AppKit uses `NSGlassEffectView.effectIsInteractive` [APPKIT-GLASS].
- Apple's design description of the behavior [V-219]:
  - The glass responds instantly, flexing and energizing with light.
  - It illuminates from within, starting under the fingertip. The glow spreads across the element and onto nearby glass elements.
  - It behaves like a flexible gel that moves in tandem with input.
  - Elements can lift into glass only while touched, so the resting state stays quiet.
- **macOS 27:** a new interactive glass effect makes glass bounce subtly when clicked. Use it on controls, buttons, or glass containers of interactive controls. Apple says "a little goes a long way" [V-2026-289]. Custom glass on macOS can be marked interactive so it responds fluidly to clicks and works well with the pointer [V-2026-269].
- Sliders preserve momentum and stretch when dragged [V-284].

**Web translation [inference]:**
- `:active` or `pointerdown`: `transform: scale(~1.03–1.08)` with spring easing.
- A radial-gradient glow that follows the pointer position and grows from the press point.
- A bounce-back overshoot on release.
- Optionally, light bleeding onto sibling glass in the same group.
- All of it gated behind `prefers-reduced-motion`.

---

## 6. Grouping, merging, and morphing

### Containers and spacing
- A `GlassEffectContainer` (SwiftUI), `UIGlassContainerEffect` (UIKit), or `NSGlassEffectContainerView` (AppKit) renders several glass shapes together. This improves performance and lets the shapes blend and morph [SUI-CONTAINER] [UIK-CONTAINER] [APPKIT-CONTAINER].
- **`spacing` is the merge threshold.** It is the distance at which neighboring shapes begin to blend. A larger value starts the blending sooner as shapes approach [SUI-CONTAINER] [UIK-CONTAINER] [APPKIT-CONTAINER].
- **Blending at rest:** if the container's `spacing` is larger than the gap between items in the inner layout (for example `HStack` spacing), the shapes blend even when nothing is moving [SUI-CUSTOM].
- As views animate in or out and the gaps change, the shapes merge or separate "like water droplets" [V-284] [SUI-CUSTOM].
- In UIKit, you merge by animating children into overlapping frames. You split by adding views at the same position without animation, then animating them out to their final frames [V-284].
- The container also makes all its child glass adapt uniformly [V-284] [V-310].
- **The default spacing value is not documented** [SUI-CONTAINER] [UIK-CONTAINER].

### Union
- `glassEffectUnion(id:namespace:)` combines views that share an ID, a similar shape, and the same glass into **one** shape even at rest. It suits dynamically created views or views outside a single stack [SUI-CUSTOM].

### Morph identity and transitions
- `glassEffectID(_:in:)` gives each effect a stable identity within a namespace. SwiftUI uses that identity, together with the container's spacing and the shapes' geometry, to decide what morphs into what when views appear or disappear [SUI-CUSTOM].
- `GlassEffectTransition` [SUI-TRANSITION] [SUI-CUSTOM]:
  - `matchedGeometry` is the default when the effect is within the container's spacing. Shapes morph into one another.
  - `materialize` fades the content in and animates the material in or out, without matching geometry. Use it for effects farther apart than the spacing, or for a simpler transition. Pair it with an explicit animation.
  - `identity` makes no change.
  - Apple notes that both transitions do more than change opacity, and asks that they be used consistently.
- IDs and transitions only take effect during hierarchy changes or animations [SUI-CUSTOM].
- **Ordering rule:** apply `glassEffect` *after* other appearance modifiers, because the modifier captures the content it sends to the container [SUI-CUSTOM].
- **UIKit animation rule:** to materialize, set `UIVisualEffectView.effect` inside an animation block. To dematerialize, set it to `nil`. Apple says "Always prefer setting the effect property over the alpha" [V-284].

### System morphing
- Buttons morph fluidly into menus and popovers [TO-ADOPT].
- Menus, alerts, popovers, and confirmation dialogs emerge from the control that triggered them [V-323] [V-219].
- Action sheets come from the source element, not the bottom edge [TO-ADOPT] [V-356].
- Sheets can zoom from a toolbar button [V-323] [V-284].

**Web translation [inference]:**
- Use a group wrapper element with a `--glass-merge-distance` token. Render all glass in the group on one shared backdrop layer, either one `backdrop-filter` element shaped by a union mask or an SVG gooey filter (`feGaussianBlur` + `feColorMatrix` alpha threshold) for the metaball merge.
- For morphing, use the View Transitions API with `view-transition-name` as the equivalent of `glassEffectID`, or a FLIP animation.
- For materialize, animate blur, refraction strength, and opacity together, not opacity alone.
- Don't place separate `backdrop-filter` elements over each other. This mirrors "glass can't sample glass."

---

## 7. Shape and concentricity

- **The default glass shape is a capsule** in SwiftUI (`DefaultGlassEffectShape`) [SUI-GLASSEFFECT] and in UIKit [V-284]. Use a rounded rectangle for larger components where a capsule or circle would look odd, and keep shapes consistent across custom components [SUI-CUSTOM].
- **Three kinds of shape** [V-356]:
  - *Fixed*: a constant radius.
  - *Capsule*: radius equals half the height.
  - *Concentric*: radius equals the parent's radius minus the padding.
- Pinched or flared corners in nested shapes are the typical failure. The fix is to make the inner shape concentric [V-356].
- **Device edges:** on iPhone, use a capsule with extra margin near the screen edge. On iPad and Mac, use a concentric shape aligned to the window edge. Standalone components use concentric corners with a fallback radius [V-356].
- **`ConcentricRectangle`** computes each corner's radius relative to the container shape [SUI-CONCENTRIC]:
  - Corners far from the container's corners can resolve to 0, which is square.
  - `concentric(minimum:)` guarantees a minimum radius.
  - Uniform initializers take the largest computed radius for the group.
  - The container shape is set with `containerShape(_:)`. System views such as sheets and popovers provide it automatically.
  - SwiftUI usage: `.rect(corner: .containerConcentric)` [V-323].
- **UIKit `UICornerConfiguration`** [UIK-CORNER]:
  - `.corners(radius:)`, `.uniformCorners(radius:)`, and per-edge or per-corner variants.
  - `.capsule(maximumRadius:)`: a capsule that may stretch once the needed radius exceeds the maximum.
  - `UICornerRadius.fixed(_)` and `.containerConcentric(minimum:)`.
  - Squared corners are the default.
  - The WWDC session shows `.containerRelative()` and `.fixed(8)`, animated together with frame changes [V-284].
- **AppKit (macOS 27):** override `NSView.cornerConfiguration` with `.uniformCorners(radius: .containerConcentric(min))`. The closer a view sits to its container's corner, the more its radius should match [V-2026-289]. `borderShape` overrides button, pop-up, and segmented control shapes [V-310].
- **Control shapes on macOS:** mini, small, and medium controls are rounded rectangles. Large and extra-large controls are capsules [V-356] [V-310].
- Window corners are larger and wrap concentrically around the toolbar glass [V-310]. In macOS 27, all windows share a tighter, consistent radius [V-2026-102].

**Web translation [inference]:**
- Use `--radius-inner: max(var(--radius-min), calc(var(--radius-outer) - var(--padding)))`.
- A capsule is `border-radius: 9999px`, or `calc(height / 2)`.
- Propagate `--radius-outer` down the component tree as the equivalent of `containerShape`.

---

## 8. Scroll edge effect and background extension

### Scroll edge effect
- The effect replaces hard dividers. Content blurs and fades where it meets pinned UI, keeping floating controls legible [V-356] [V-219].
- Over dark content, the effect becomes a subtle dimming [V-219].
- System bars adopt it automatically [TO-ADOPT].
- Styles [SUI-SCROLLEDGE]:
  - `automatic`: the system picks the style based on platform and context.
  - `soft`: a subtle blurred transition. This is the default on iOS and iPadOS in most cases [V-356].
  - `hard`: a linear, nearly opaque boundary. Use it for dense UIs, pinned column headers, interactive text, or controls without backgrounds. It is mostly used on macOS [V-356] [V-219] [V-323].
- **Rules** [V-356]:
  - One effect per view.
  - Don't mix or stack soft and hard styles.
  - In split views, each pane can have its own effect. Keep their heights consistent.
  - Apply the effect only where floating UI overlaps scrolling content.
- APIs:
  - SwiftUI: `scrollEdgeEffectStyle(_:for:)` and `scrollEdgeEffectHidden(_:for:)` [SUI-SCROLLEDGE].
  - UIKit: `UIScrollView.topEdgeEffect`, `bottomEdgeEffect`, and the other edges, each with `.style` and `.isHidden` (default `false`) [UIK-SCROLLEDGE].
  - Custom bars: register them with `safeAreaBar(edge:alignment:spacing:content:)` in SwiftUI, or `UIScrollEdgeElementContainerInteraction` in UIKit [TO-ADOPT] [V-284].
- **Custom bar backgrounds or darkening interfere with the effect. Remove them** [V-323] [TO-ADOPT].
- **iOS 27 change:** `.automatic` no longer switches between soft and hard and has its own new visuals. Apps that overrode the style, especially to `.soft`, should re-evaluate [V-2026-278]. When content scrolls under floating bars, a uniform toolbar appears at the top for legibility [V-2026-102]. On macOS 27, automatic resolves to hard when free-floating text such as the window title is present [V-2026-289].

### Background extension
- `backgroundExtensionEffect()` (SwiftUI), `UIBackgroundExtensionView`, and `NSBackgroundExtensionView` mirror the view into copies on edges where safe area is available, then blur those copies. Content appears to continue under a sidebar or inspector while the real content stays unobscured [SUI-BGEXT] [TO-ADOPT] [V-323] [V-310].
- It clips the view so the copies don't overlap.
- Use it sparingly, on a **single** instance of background content [SUI-BGEXT].
- Keep text and controls layered *above* the extension, as siblings rather than subviews [V-356] [V-284].

**Web translation [inference]:**
- Scroll edge: a sticky header with a `mask-image: linear-gradient()` combined with `backdrop-filter`. Soft is a progressive blur and fade. Hard is a near-opaque band with a 1px line.
- Background extension: mirror a hero image with `transform: scaleX(-1)` copies under the sidebar, plus a blur.

---

## 9. Navigation components (system behavior to model)

- **Tab bar:** floats over content [V-323].
  - `tabBarMinimizeBehavior`: `.automatic`, `.never`, `.onScrollDown`, or `.onScrollUp`. Minimizing is iPhone only. The bar re-expands when scrolling the other way [SUI-TABMIN] [V-284].
  - The search tab (`Tab(role: .search)` or `UISearchTab`) is separated at the trailing end [TO-ADOPT].
  - **iOS 27:** `Tab(role: .prominent)` sits at the bottom trailing edge, apart from the other tabs [V-2026-269]. In UIKit, `prominentTabIdentifier` keeps a tab visible even when the bar collapses [V-2026-278].
- **Bottom accessory:** `tabViewBottomAccessory` (or the `isEnabled:` form from 26.1) holds persistent controls such as playback above the tab bar. It displays **inline** when the bar is collapsed. Read `tabViewBottomAccessoryPlacement` to switch to a compact layout [SUI-TABACC] [V-323] [V-284].
  - Don't put screen-specific actions such as checkout in the tab bar [V-356].
- **Toolbars:** items sit on floating glass and are grouped automatically [V-323].
  - UIKit groups image buttons on one shared background. Text buttons, Done and Close, and prominent buttons each get their own [V-284].
  - `ToolbarSpacer(.fixed)` splits groups. `.flexible` pushes groups apart [V-323] [SUI-SPACER].
  - `sharedBackgroundVisibility(.hidden)` removes an item's shared glass and puts it in its own group [SUI-SHAREDBG].
  - In UIKit, `.fixedSpace(0)` splits groups. Setting `hidesSharedBackground = false` on a flexible space keeps one shared background [V-284].
  - In AppKit, set `isBordered = false` for non-interactive items such as titles and status, so they are not on glass [V-310].
  - **iOS 27:** `visibilityPriority(_:)`, `ToolbarOverflowMenu`, `.topBarPinnedTrailing`, and `toolbarMinimizeBehavior(_:for:)` [UPD-SUI] [V-2026-269]. UIKit adds `barMinimizationBehavior` [V-2026-278].
- **Sidebars:** inset, floating glass panes that content flows behind [V-356] [V-310].
  - Large surfaces that don't flip between light and dark [V-219].
  - **iOS/macOS 27:** they extend to the window edges, still refract content and wallpaper, and sidebar icons regain accent color [V-2026-102] [V-2026-289].
- **Sheets:**
  - Partial-height sheets are inset with a glass background, and their bottom corners pull in to nest within the display corners.
  - At full height they become opaque and anchor to the screen edge [V-323].
  - Dragging a sheet upward makes it more opaque and slightly larger [V-356] [TO-ADOPT].
  - Sheets have a larger corner radius [TO-ADOPT].
  - Remove custom `presentationBackground` and sheet backgrounds [V-323] [V-284].
  - Modal tasks pair glass with a dimming layer. Parallel tasks use glass for separation without dimming [V-356].
- **Menus:** they adopt glass and use leading icons in a single scannable column [V-310] [V-323].
  - Introduce a related group with one icon and don't repeat it [V-356].
  - **iOS 27:** iPad and Mac menu bars show a minimal set of icons by default. `.labelStyle(.titleAndIcon)` (SwiftUI) or `preferredImageVisibility` (UIKit) shows an item's icon [V-2026-269] [V-2026-278].
- **Lists and forms:** larger row heights and padding, and larger section corner radii. Section headers use title case, not all caps [TO-ADOPT].
- **Typography:** bolder and left-aligned in key moments such as alerts and onboarding [V-356].
- **Controls:**
  - Shapes follow hardware curvature, so they are rounder.
  - A new extra-large size exists for prominent actions.
  - Don't hard-code control metrics [TO-ADOPT] [V-323] [V-310].
  - Slider tick marks and `neutralValue` (fill starting from a non-minimum point) [V-323] [V-284].

---

## 10. API table (with web equivalents)

| API (platform) | Behavior (one line) | Web-equivalent concept [inference] | Source |
|---|---|---|---|
| `View.glassEffect(_:in:)` (SwiftUI) | Draws glass in a shape behind the view, plus foreground effects on its content. Defaults to `.regular` in a capsule. | `.glass` class: `backdrop-filter` + refraction filter + highlight, with `border-radius: 9999px` | [SUI-GLASSEFFECT] |
| `Glass` (SwiftUI) | The material configuration value passed to `glassEffect`. | Props or tokens object `{variant, tint, interactive}` | [SUI-GLASS] |
| `Glass.regular` | The adaptive, legible default variant. | `data-variant="regular"` | [SUI-GLASS] [V-219] |
| `Glass.clear` | More transparent, not adaptive. Needs a dimming layer. | `data-variant="clear"` + required scrim | [SUI-GLASS] [HIG-MAT] |
| `Glass.identity` | Leaves content unaffected, as if no glass were applied. | `data-variant="none"` (glass disabled, same DOM) | [SUI-GLASS] |
| `Glass.tint(_:)` | Copy of the glass with a tint color that maps tones to the backdrop's brightness. | `--glass-tint`, blended rather than painted opaque | [SUI-GLASS] [V-219] |
| `Glass.interactive(_:)` | Copy of the glass that responds to touch and pointer (scale, bounce, shimmer). Default `true`. | `:active`/pointer spring scale + glow | [SUI-INTERACTIVE] [V-323] |
| `DefaultGlassEffectShape` | The default capsule shape. | `border-radius: 9999px` | [SUI-CONTAINER] |
| `GlassEffectContainer(spacing:content:)` | Renders child glass together, sharing sampling. Shapes blend within `spacing`. | Group wrapper, shared backdrop layer, `--glass-merge-distance` | [SUI-CONTAINER] |
| `glassEffectUnion(id:namespace:)` | Merges views with the same ID into one glass shape, even at rest. | Single backdrop element spanning grouped items (union mask) | [SUI-CUSTOM] |
| `glassEffectID(_:in:)` | Stable identity for morphing across hierarchy changes. | `view-transition-name` / FLIP key | [SUI-CUSTOM] |
| `glassEffectTransition(_:)` + `GlassEffectTransition` (`matchedGeometry`, `materialize`, `identity`) | Chooses between a morph, a fade-plus-material transition, or none. | Shared-element morph vs. blur/opacity "materialize" keyframes | [SUI-TRANSITION] |
| `.buttonStyle(.glass)` / `GlassButtonStyle`; `.glass(_:)` | Standard glass button. | `<Button variant="glass">` | [TO-ADOPT] [V-323] |
| `.buttonStyle(.glassProminent)` / `GlassProminentButtonStyle` | Prominent glass button, tinted with the accent color. | `<Button variant="glass-prominent">` | [TO-ADOPT] [V-323] |
| `backgroundExtensionEffect()` / `(isEnabled:)` | Mirrors and blurs a view into the safe area under sidebars or inspectors. | Mirrored blurred hero copy under the sidebar | [SUI-BGEXT] |
| `ScrollEdgeEffectStyle` (`automatic`, `soft`, `hard`), `scrollEdgeEffectStyle(_:for:)`, `scrollEdgeEffectHidden(_:for:)` | Blur or opaque transition where pinned controls overlap scrolling content. | Sticky bar mask: progressive blur (soft) or opaque band (hard) | [SUI-SCROLLEDGE] |
| `safeAreaBar(edge:alignment:spacing:content:)` | Custom bar that participates in the scroll edge effect. | Custom sticky bar opts into the edge-effect mask | [TO-ADOPT] [SUI-SCROLLEDGE] |
| `TabBarMinimizeBehavior` / `tabBarMinimizeBehavior(_:)` | Tab bar shrinks on scroll (iPhone). | Scroll-direction-driven compact nav state | [SUI-TABMIN] |
| `tabViewBottomAccessory(content:)` / `(isEnabled:content:)` (26.1) + `tabViewBottomAccessoryPlacement` | Persistent accessory above the tab bar, inline when the bar collapses. | Mini-player slot with `expanded`/`inline` states | [SUI-TABACC] |
| `ToolbarSpacer(_:placement:)` (`.fixed`, `.flexible`) | Splits toolbar glass groups or pushes them apart. | `<ToolbarSpacer fixed\|flex>` that breaks the shared pill | [SUI-SPACER] [V-323] |
| `sharedBackgroundVisibility(_:)` | Hides an item's shared glass group background. | `shared={false}` prop on a toolbar item | [SUI-SHAREDBG] |
| `ConcentricRectangle`, `.rect(corner: .containerConcentric)`, `containerShape(_:)` | Corners computed concentric to the container, with optional minimum. | `calc(var(--r-outer) - var(--pad))` with `max()` floor | [SUI-CONCENTRIC] [V-323] |
| `Tab(role: .search)` / `Tab(role: .prominent)` (27) | Search tab separated at trailing end / prominent trailing tab. | Detached trailing pill in the tab bar | [TO-ADOPT] [V-2026-269] |
| iOS 27 toolbar: `visibilityPriority`, `ToolbarOverflowMenu`, `.topBarPinnedTrailing`, `toolbarMinimizeBehavior(_:for:)` | Priority-based overflow, pinned trailing item, nav bar minimize on scroll. | Priority+ overflow pattern; collapsing header | [UPD-SUI] [V-2026-269] |
| `UIGlassEffect(style:)` (`.regular`/`.clear`), `.tintColor`, `.isInteractive` (UIKit) | Glass as a `UIVisualEffect`. Content goes in `UIVisualEffectView.contentView`. | Same as `.glass` | [UIK-GLASS] [UIK-GLASS-STYLE] [V-284] |
| `UIGlassContainerEffect.spacing` | Combines nested glass views. `spacing` is the merge distance. | Glass group | [UIK-CONTAINER] |
| `UIScrollEdgeEffect` (`style`, `isHidden`) via `UIScrollView.topEdgeEffect` etc. | Per-edge scroll edge configuration. | Per-edge sticky mask config | [UIK-SCROLLEDGE] |
| `UIScrollEdgeElementContainerInteraction` | Registers a custom overlay bar with the edge effect. | Same as `safeAreaBar` | [TO-ADOPT] [V-284] |
| `UIButton.Configuration.glass()` / `prominentGlass()` / `clearGlass()` / `prominentClearGlass()` | Glass button configurations. | Button variants × {regular, clear} × {default, prominent} | [UIK-BTN] |
| `UIView.cornerConfiguration` / `UICornerConfiguration` / `UICornerRadius` (`fixed`, `containerConcentric(minimum:)`), `.capsule(maximumRadius:)` | Declarative corner model: fixed, concentric, or capsule. | Radius tokens: `fixed`, `concentric`, `capsule` | [UIK-CORNER] |
| `UIBackgroundExtensionView` | UIKit background extension. | Same as `backgroundExtensionEffect` | [TO-ADOPT] [V-284] |
| `UITabAccessory`, `tabBarMinimizeBehavior`, `prominentTabIdentifier` (27) | Tab accessory, minimize, prominent tab. | See above | [V-284] [V-2026-278] |
| `UIBarButtonItem.fixedSpace`, `hidesSharedBackground`, `style = .prominent` | Toolbar glass grouping and tint. | Toolbar group props | [V-284] |
| `NSGlassEffectView` (`contentView`, `cornerRadius`, `tintColor`, `style`, `effectIsInteractive`) (AppKit) | Embeds content in glass. Put content *in* `contentView`. | `.glass` wrapper element | [APPKIT-GLASS] [V-310] |
| `NSGlassEffectView.Style` (`.regular`, `.clear`) | AppKit variants. | variant prop | [APPKIT-GLASS-STYLE] |
| `NSGlassEffectContainerView` (`contentView`, `spacing`) | Merges descendant glass within `spacing`. Fewer render passes. | Glass group | [APPKIT-CONTAINER] |
| `NSBackgroundExtensionView` | AppKit background extension. | See above | [V-310] |
| `NSButton.BezelStyle.glass`, `bezelColor`, `tintProminence`, `borderShape`, `controlSize .extraLarge` | Glass bezel, tint strength, shape override, XL size. | Button variant, tint-strength token, size scale | [TO-ADOPT] [V-310] |
| `NSToolbarItem.style = .prominent`, `backgroundTintColor`, `isBordered = false` | Prominent or tinted toolbar items. Remove glass from non-interactive items. | Toolbar item props | [V-310] |
| `UIDesignRequiresCompatibility` (Info.plist) | Temporary opt-out to the pre-glass look. **Ignored when building for 27+.** | n/a (glass is mandatory in 27) | [PLIST-COMPAT] [V-2026-102] |

---

## 11. DO / DON'T (explicitly stated by Apple)

**DO**
- Use standard system components. They adopt glass automatically and adapt to overlap and focus [TO-ADOPT].
- Reserve glass for the navigation and functional layer floating above content [V-219] [HIG-MAT].
- Use glass sparingly on custom controls, only for the most important functional elements [TO-ADOPT] [HIG-MAT] [V-310].
- Wrap multiple custom glass elements in a container, for performance and morphing [TO-ADOPT] [SUI-CUSTOM].
- Apply `glassEffect` after other appearance modifiers [SUI-CUSTOM].
- Use unique IDs in a shared namespace for morphing, and use morph and materialize consistently [SUI-CUSTOM].
- Use regular glass by default. Use clear glass only when all three conditions hold, with dimming (35% over bright content) [V-219] [HIG-MAT].
- Tint only primary actions, to convey meaning [V-219] [V-323].
- Put broad color and brand expression in the content layer [V-219] [V-2026-251].
- Use concentric or capsule shapes for nesting [V-356] [TO-ADOPT].
- Group toolbar items by function and frequency, use fixed spacers to separate groups, keep the primary action separate and tinted, and move secondary actions to a menu when crowded [V-356] [TO-ADOPT].
- Give every icon-only control an accessibility label [TO-ADOPT].
- Hide the whole toolbar item, not just its content [TO-ADOPT].
- Use one scroll edge effect per view [V-356].
- Use system or vibrant colors on glass. Give custom colors light, dark, and increased-contrast variants [HIG-MAT] [TO-ADOPT].
- Set the source for action sheets and popovers so they emerge from their control [TO-ADOPT] [V-284].
- Prefer setting or removing the effect over animating alpha (UIKit) [V-284].
- Test under Reduce Transparency, Reduce Motion, Increase Contrast, and the user's Liquid Glass preference [TO-ADOPT] [HIG-MAT].
- Limit the number of glass effects onscreen at once, and profile performance [SUI-CUSTOM] [TO-ADOPT].

**DON'T**
- Don't put glass in the content layer (lists, tables, app backgrounds). Transient sliders and toggles while dragging are the only exception [V-219] [HIG-MAT].
- Don't stack glass on glass [V-219] [TO-ADOPT].
- Don't mix regular and clear glass [V-219].
- Don't use clear glass over low-richness backgrounds [HIG-MAT].
- Don't tint everything. Don't use solid opaque fills on glass [V-219].
- Don't add custom backgrounds or borders to bars, toolbars, tab bars, split views, sheets, or popovers. They fight the glass and the scroll edge effect [TO-ADOPT] [V-356] [V-323] [V-284] [V-310].
- Don't group symbols with text in one shared background, and don't mix text and icons within one group [V-356] [TO-ADOPT].
- Don't put screen-specific actions in the tab bar [V-356].
- Don't mix or stack soft and hard scroll edge styles [V-356].
- Don't use pinched or flared nested corners [V-356].
- Don't hard-code control heights or layout metrics [TO-ADOPT] [V-310].
- Don't put non-interactive toolbar items such as titles and status on glass (macOS) [V-310].
- Don't bake blur, masking, or effects into app icon artwork [TO-ADOPT].
- Don't create too many containers or uncontained effects [SUI-CUSTOM].
- Don't place glass behind content as a sibling (AppKit). Use `contentView` [V-310].
- Don't let extended backgrounds distort overlaid text. Keep text and controls above them [V-356].
- Don't leave content and glass intersecting in steady states [V-219].

---

## 12. Accessibility and user-preference adaptations

| Setting | Effect on glass | Source |
|---|---|---|
| **Reduce Transparency** | Glass becomes frostier and hides more of the content behind it. Standard components adapt automatically. Test custom elements. | [V-219] [TO-ADOPT] [HIG-MAT] |
| **Increase Contrast** | Elements become predominantly black or white, with a contrasting border. | [V-219] |
| **Reduce Motion** | Lowers the intensity of some effects and **disables the elastic (gel/bounce) properties**. | [V-219] [TO-ADOPT] |
| **Show Borders** (macOS 27) | A new environment value lets custom macOS controls adapt to the "show borders" setting, as iOS apps already could. The exact API name was not given **[unverified]**. | [V-2026-102] |
| **User Liquid Glass preference — iOS/iPadOS/macOS 26.1** | A **Clear / Tinted** choice. On iOS it is in Settings > Display & Brightness, on Mac in System Settings > Appearance. Tinted increases opacity and contrast. Apple's docs confirm only that "users can choose a Liquid Glass look in device settings," which changes how variants appear. Location and option names come from third-party press: **[unverified on Apple pages]** (MacRumors). | [TO-ADOPT] [HIG-MAT] [3P-MACRUMORS] |
| **User Liquid Glass slider — iOS/macOS 27** | A continuous slider from "ultra clear" to "fully tinted" [V-2026-102]. The material responds to it automatically by adjusting its tint [V-2026-269]. **No app-facing API to read the value was described** [V-2026-102]. Press reports put it in Settings > Appearance with Default in the middle, replacing the Clear/Tinted toggle **[unverified on Apple pages]** (9to5Mac). | [V-2026-102] [V-2026-269] [3P-9TO5] |
| VoiceOver / Voice Control | Every icon needs an accessibility label, even when only the icon is shown. | [TO-ADOPT] |
| Colors | Use system colors, which were retuned for Light, Dark, and Increased Contrast, or give custom colors all three variants. | [V-356] [TO-ADOPT] |

**Web translation [inference]:**
- Map `prefers-reduced-transparency: reduce` to frosted, near-opaque glass.
- Map `prefers-contrast: more` (and `forced-colors: active`) to solid black or white with a 1px contrasting border.
- Map `prefers-reduced-motion: reduce` to no spring, bounce, stretch, or shimmer, and a shorter or crossfaded morph.
- Expose a user-facing `--glass-opacity` / "tint amount" slider, from clear to fully tinted, mirroring the iOS 27 slider. Default it to the middle.
- Treat clear-variant scrims as mandatory under reduced transparency.

---

## 13. Version and compatibility notes

- All core glass APIs were introduced in iOS, iPadOS, macOS, tvOS, and watchOS 26.0 (UIKit glass: iOS, iPadOS, Catalyst, and tvOS 26.0). `ConcentricRectangle`, `ScrollEdgeEffectStyle`, and `backgroundExtensionEffect` also list visionOS 26 [SUI-GLASS] [SUI-CONCENTRIC] [SUI-SCROLLEDGE] [SUI-BGEXT] [UIK-GLASS].
- `tabViewBottomAccessory(isEnabled:content:)` is 26.1+ [SUI-TABACC].
- No new `Glass` members for 26.x or 27 appear on the `Glass` page [SUI-GLASS].
- **27 (WWDC26):**
  - Liquid Glass is mandatory. `UIDesignRequiresCompatibility` is ignored when building for 27+ [PLIST-COMPAT] [V-2026-102].
  - Existing adopters get the refinements without recompiling [V-2026-102].
  - The material is refined: better diffusion, a dark edge, brighter speculars, and optional refraction [V-2026-102].
  - New behavior for the `automatic` scroll edge style [V-2026-278].
  - Prominent tab, toolbar overflow and priority, and minimal menu icons [V-2026-269] [V-2026-278].
  - AppKit gets concentric corners for views and an interactive glass bounce [V-2026-289].
- tvOS: glass appears on focus, on Apple TV 4K (2nd generation) and later [TO-ADOPT] [HIG-MAT].
- watchOS: changes are minimal [TO-ADOPT].
- visionOS: windows use a separate, unmodifiable system glass [HIG-MAT].

---

## Sources

All Apple doc pages were fetched through their JSON form `https://developer.apple.com/tutorials/data/documentation/<path>.json`. The human URL is listed below.

| Tag | URL |
|---|---|
| TO-LG | https://developer.apple.com/documentation/technologyoverviews/liquid-glass |
| TO-ADOPT | https://developer.apple.com/documentation/technologyoverviews/adopting-liquid-glass |
| SUI-CUSTOM | https://developer.apple.com/documentation/swiftui/applying-liquid-glass-to-custom-views |
| SUI-GLASS | https://developer.apple.com/documentation/swiftui/glass |
| SUI-INTERACTIVE | https://developer.apple.com/documentation/swiftui/glass/interactive(_:) |
| SUI-GLASSEFFECT | https://developer.apple.com/documentation/swiftui/view/glasseffect(_:in:) |
| SUI-CONTAINER | https://developer.apple.com/documentation/swiftui/glasseffectcontainer |
| SUI-TRANSITION | https://developer.apple.com/documentation/swiftui/glasseffecttransition |
| SUI-BGEXT | https://developer.apple.com/documentation/swiftui/view/backgroundextensioneffect() |
| SUI-SCROLLEDGE | https://developer.apple.com/documentation/swiftui/scrolledgeeffectstyle |
| SUI-CONCENTRIC | https://developer.apple.com/documentation/swiftui/concentricrectangle |
| SUI-SPACER | https://developer.apple.com/documentation/swiftui/toolbarspacer |
| SUI-SHAREDBG | https://developer.apple.com/documentation/swiftui/toolbarcontent/sharedbackgroundvisibility(_:) |
| SUI-TABMIN | https://developer.apple.com/documentation/swiftui/tabbarminimizebehavior |
| SUI-TABACC | https://developer.apple.com/documentation/swiftui/view/tabviewbottomaccessory(isenabled:content:) |
| UIK-GLASS | https://developer.apple.com/documentation/uikit/uiglasseffect |
| UIK-GLASS-STYLE | https://developer.apple.com/documentation/uikit/uiglasseffect/style |
| UIK-CONTAINER | https://developer.apple.com/documentation/uikit/uiglasscontainereffect |
| UIK-SCROLLEDGE | https://developer.apple.com/documentation/uikit/uiscrolledgeeffect |
| UIK-CORNER | https://developer.apple.com/documentation/uikit/uicornerconfiguration-swift.struct |
| UIK-BTN | https://developer.apple.com/documentation/uikit/uibutton/configuration-swift.struct/glass() |
| APPKIT-GLASS | https://developer.apple.com/documentation/appkit/nsglasseffectview |
| APPKIT-GLASS-STYLE | https://developer.apple.com/documentation/appkit/nsglasseffectview/style-swift.enum |
| APPKIT-CONTAINER | https://developer.apple.com/documentation/appkit/nsglasseffectcontainerview |
| HIG-MAT | https://developer.apple.com/design/human-interface-guidelines/materials (change log: Liquid Glass added 2025-06-09, updated 2025-09-09) |
| PLIST-COMPAT | https://developer.apple.com/documentation/bundleresources/information-property-list/uidesignrequirescompatibility |
| UPD-SUI | https://developer.apple.com/documentation/updates/swiftui |
| UPD-UIK | https://developer.apple.com/documentation/updates/uikit (June 2026 section had no glass changes) |
| V-219 | https://developer.apple.com/videos/play/wwdc2025/219/ — Meet Liquid Glass |
| V-356 | https://developer.apple.com/videos/play/wwdc2025/356/ — Get to know the new design system |
| V-323 | https://developer.apple.com/videos/play/wwdc2025/323/ — Build a SwiftUI app with the new design |
| V-284 | https://developer.apple.com/videos/play/wwdc2025/284/ — Build a UIKit app with the new design |
| V-310 | https://developer.apple.com/videos/play/wwdc2025/310/ — Build an AppKit app with the new design |
| V-2026-102 | https://developer.apple.com/videos/play/wwdc2026/102/ — Platforms State of the Union 2026 |
| V-2026-269 | https://developer.apple.com/videos/play/wwdc2026/269/?time=60 — What's new in SwiftUI (2026); the bare URL returned empty |
| V-2026-278 | https://developer.apple.com/videos/play/wwdc2026/278/ — Modernize your UIKit app |
| V-2026-289 | https://developer.apple.com/videos/play/wwdc2026/289/ — Modernize your AppKit app |
| V-2026-251 | https://developer.apple.com/videos/play/wwdc2026/251/ — Communicate your brand identity on iOS |
| V-2026-250 | https://developer.apple.com/videos/play/wwdc2026/250/ — Principles of great design (no glass content) |
| 3P-MACRUMORS | https://www.macrumors.com/2025/10/20/ios-26-1-liquid-glass-toggle/ (third-party, for the 26.1 setting location only) |
| 3P-9TO5 | https://9to5mac.com/2026/06/15/ios-27-adds-new-liquid-glass-slider-on-iphone-heres-what-it-lets-you-do/ (third-party, for the 27 slider location only) |

**Not reachable or not found**
- `curl` to developer.apple.com was blocked by the proxy (403). All fetches went through WebFetch.
- These JSON paths returned client errors: `uikit/uicornerconfiguration.json`, `uikit/uiview/cornerconfiguration.json`, `uikit/uiglasseffect/style-swift.enum.json` (and `/clear`), and `swiftui/view/sharedbackgroundvisibility(_:).json`. Alternative paths worked for all except `UIView.cornerConfiguration` itself.
- The `wwdc2026/269` page with no query string returned empty. `?time=60` worked.
- Not fetched: the Landmarks sample page, the `glassEffectUnion` and `glassEffectID` symbol pages (covered by the article instead), the HIG Color and Accessibility pages, and the `TabViewBottomAccessoryPlacement` cases.
- No Apple page gives the default `spacing`, blur radii, refraction amounts, or animation curves.
- No WWDC26 session dedicated to Liquid Glass was found. Glass updates are spread across 102, 269, 278, and 289.
- The iOS 27 "optional refraction" API name, the macOS 27 "show borders" environment value name, and any API that exposes the user's glass slider value remain **[unverified]**.
