# 04 — Liquid Glass on the Web: Implementation Reference

Researched 2026-11-02. Audience: an LLM coding agent updating a web design system modeled on Apple Liquid Glass.
Every claim carries a `[src: URL]` tag. Code is original (rewritten from the techniques the sources describe), not copied.
Where a value is my own derivation or recommendation, it is tagged `[derived]` or `[recommendation]`.

---

## 0. TL;DR for the agent

1. Glass is the **chrome/controls layer only** (nav bars, tab bars, toolbars, sheets, popovers, floating buttons). Never content surfaces, never glass on glass. [src: https://developer.apple.com/design/human-interface-guidelines/materials] [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/apple-design/SKILL.md]
2. Build three tiers: **T0 opaque** (default for everyone) → **T1 frosted** (`backdrop-filter: blur() saturate()`, all modern engines) → **T2 refractive** (`backdrop-filter: url(#svg-filter)`, Chromium only, JS-gated). [src: https://kube.io/blog/liquid-glass-css-svg] [src: https://bugs.webkit.org/show_bug.cgi?id=245510]
3. Rim highlight + depth come from inset `box-shadow`, a gradient border via mask, and an outer shadow. These work everywhere and are dropped by `forced-colors`. [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026] [src: https://developer.mozilla.org/en-US/docs/Web/CSS/@media/forced-colors]
4. Honor `prefers-reduced-transparency` (Chromium only; Safari has none), `prefers-contrast: more`, `prefers-reduced-motion`, and `forced-colors: active`. [src: https://raw.githubusercontent.com/mdn/browser-compat-data/main/css/at-rules/media.json]
5. Motion: springs with Apple's `duration` + `bounce` model. Default bounce 0, snappy 0.15, bouncy 0.3, duration 0.5s. Use JS springs (Motion) for gestures and CSS `linear()` for predetermined transitions. [src: https://developer.apple.com/documentation/swiftui/animation/snappy(duration:extrabounce:)] [src: https://developer.chrome.com/docs/css-ui/css-linear-easing-function]
6. Perf budget: at most 3–4 glass layers per viewport. Never animate `backdrop-filter` blur radius on large surfaces; fade opacity instead. Keep the filtered area small. [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026] [src: https://www.react.doctor/docs/rules/react-doctor/no-large-animated-blur]

---

## 1. What Apple's material actually does (the target to imitate)

From Apple's public design material (WWDC25 "Meet Liquid Glass" and the HIG):

- **Lensing, not scattering.** Liquid Glass bends and concentrates light in real time, where older materials scattered it (plain blur). On the web this means refraction near the edges plus a mild blur, not heavy blur. [src: https://developer.apple.com/videos/play/wwdc2025/219/]
- **Specular highlights follow the geometry** and move with device motion and lock/unlock. → a rim highlight with a light angle; optional pointer/tilt-driven angle. [src: https://developer.apple.com/videos/play/wwdc2025/219/]
- **Adaptive shadows.** Shadow opacity goes up over text and down over plain light backgrounds. [src: https://developer.apple.com/videos/play/wwdc2025/219/]
- **Size encodes thickness.** Larger glass behaves like thicker material: deeper shadows, stronger lensing, softer scattering. Small elements (tab bars) flip light/dark based on what is behind them. Large ones (menus, sidebars) adapt but do not flip. [src: https://developer.apple.com/videos/play/wwdc2025/219/]
- **Two variants, never mixed.** *Regular* is adaptive, works at any size, and is the default. *Clear* is more transparent and only for use over media, with a dimming layer. [src: https://developer.apple.com/videos/play/wwdc2025/219/] The HIG says to add a dark dimming layer at **35% opacity** over bright content behind Clear glass. [src: https://developer.apple.com/design/human-interface-guidelines/materials]
- **Tint sparingly.** Tint only primary elements and actions. Tint maps to the brightness of the content underneath. [src: https://developer.apple.com/videos/play/wwdc2025/219/]
- **Interaction:** the material lights up from within on touch, and the glow spreads to nearby glass. Elements *materialize* by modulating lensing rather than fading. Menus pop out of their trigger, and controls morph between contexts. [src: https://developer.apple.com/videos/play/wwdc2025/219/]
- **Accessibility:** Reduce Transparency makes glass frostier. Increase Contrast makes it mostly black or white with a contrasting border. Reduce Motion disables the elastic properties. [src: https://developer.apple.com/videos/play/wwdc2025/219/]
- **Use sparingly.** The HIG phrase is "Use Liquid Glass effects sparingly." Glass is for controls and navigation, not the content layer. [src: https://developer.apple.com/design/human-interface-guidelines/materials]
- **Grouping/morphing (SwiftUI model to mirror):** `GlassEffectContainer(spacing:)` makes nearby glass shapes blend and morph. `glassEffectID` handles matched-geometry morphs, and `.materialize` handles shapes that are far apart. Too many glass effects outside containers hurts performance. [src: https://developer.apple.com/documentation/swiftui/applying-liquid-glass-to-custom-views]

Standard (non-glass) materials for the content layer come in ultraThin, thin, regular, and thick. Thicker means more opaque and better for fine text. [src: https://developer.apple.com/design/human-interface-guidelines/materials]

---

## 2. Browser support (verified from MDN browser-compat-data, 2026-11)

| Feature | Chrome | Firefox | Safari | Notes |
|---|---|---|---|---|
| `backdrop-filter` (filter functions) | 76 | 103 | 18 unprefixed, 9 with `-webkit-` | Baseline 2024. Firefox before 123 was off on unknown GPU vendors. [src: https://raw.githubusercontent.com/mdn/browser-compat-data/main/css/properties/backdrop-filter.json] [src: https://developer.mozilla.org/en-US/docs/Web/CSS/backdrop-filter] caniuse global ≈97%. [src: https://caniuse.com/css-backdrop-filter] |
| `backdrop-filter: url(#svg)` (refraction) | Yes | Parses but renders nothing | **No.** WebKit bug 245510 is still NEW; PRs posted Jul–Sep 2026, not merged | Simon Fraser (WebKit): accelerated filters can't use SVG filters. [src: https://bugs.webkit.org/show_bug.cgi?id=245510] [src: https://bugs.webkit.org/show_bug.cgi?id=297770] [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026] |
| `prefers-reduced-transparency` | 118 | 113 behind pref `layout.css.prefers-reduced-transparency.enabled` | **None** | Experimental, not Baseline. caniuse ≈74%. [src: https://raw.githubusercontent.com/mdn/browser-compat-data/main/css/at-rules/media.json] [src: https://caniuse.com/mdn-css_at-rules_media_prefers-reduced-transparency] [src: https://developer.mozilla.org/en-US/docs/Web/CSS/@media/prefers-reduced-transparency] |
| `prefers-contrast` | 96 | 101 | 14.1 | [src: https://raw.githubusercontent.com/mdn/browser-compat-data/main/css/at-rules/media.json] |
| `prefers-reduced-motion` | 74 | 63 | 10.1 | same |
| `forced-colors` | 89 | 89 | 16 | same |
| `linear()` easing | 113 | 112 | 17.2 | [src: https://raw.githubusercontent.com/mdn/browser-compat-data/main/css/types/easing-function.json] [src: https://developer.chrome.com/docs/css-ui/css-linear-easing-function] |
| `document.startViewTransition` | 111 | 144 | 18 | Same-document. [src: https://raw.githubusercontent.com/mdn/browser-compat-data/main/api/Document.json] |
| `@view-transition` (cross-document) | 126 | No | 18.2 | [src: https://raw.githubusercontent.com/mdn/browser-compat-data/main/css/at-rules/view-transition.json] |
| `@starting-style` | 117 | 129 | 17.5 | [src: https://raw.githubusercontent.com/mdn/browser-compat-data/main/css/at-rules/starting-style.json] |
| `@property` | 85 | 128 | 16.4 | Lets you transition typed custom properties (e.g. highlight angle). [src: https://raw.githubusercontent.com/mdn/browser-compat-data/main/css/at-rules/property.json] |
| `corner-shape: squircle` | 139 | preview | preview | Progressive enhancement for Apple-like continuous corners. [src: https://raw.githubusercontent.com/mdn/browser-compat-data/main/css/properties/corner-shape.json] |

A CSSWG discussion (csswg-drafts #12316, "Apple macOS26 Liquid Glass Effect") floats possible native primitives such as a `backdrop-filter` offset and falloff. Nothing is specced, so do not depend on it. [src: https://lists.w3.org/Archives/Public/public-css-archive/2025Jun/0348.html]

---

## 3. Glass tokens (CSS custom properties)

The values combine the published recipes (BuildMVPFast, Josh Comeau, LogRocket, kube.io, Apple HIG) and my own tuning; `[derived]` marks my own choices.

```css
:root {
  /* ---- material ---- */
  --glass-blur-sm: 8px;          /* chips, small buttons */
  --glass-blur-md: 16px;         /* bars, toolbars (published range 12–30px) */
  --glass-blur-lg: 24px;         /* sheets, sidebars; keep large surfaces <= ~20–24px */
  --glass-saturate: 180%;        /* published range 140–200% */
  --glass-brightness: 1.05;      /* [derived] slight lift so dark backdrops don't muddy */

  /* tint: Regular (light) */
  --glass-tint: rgb(255 255 255 / 0.18);
  --glass-tint-strong: rgb(255 255 255 / 0.55);   /* when legibility is at risk */
  --glass-tint-clear: rgb(255 255 255 / 0.06);    /* Clear variant, over media only */
  --glass-dim-clear: rgb(0 0 0 / 0.35);           /* HIG: 35% dimming under Clear over bright media */

  /* rim / specular */
  --glass-rim-top: rgb(255 255 255 / 0.55);
  --glass-rim: rgb(255 255 255 / 0.28);
  --glass-inner-glow: rgb(255 255 255 / 0.35);
  --glass-light-angle: -60deg;   /* kube.io default specular angle */

  /* depth (bigger surface = deeper shadow) */
  --glass-shadow-sm: 0 2px 8px rgb(0 0 0 / 0.12);
  --glass-shadow-md: 0 8px 24px rgb(0 0 0 / 0.18);
  --glass-shadow-lg: 0 12px 40px rgb(0 0 0 / 0.28);

  /* refraction (Tier 2 only) */
  --glass-refraction-scale: 60;  /* feDisplacementMap scale; published 20–70 */
  --glass-bezel: 16px;           /* width of the refracting edge band [derived] */

  /* opaque fallback (Tier 0 / reduced transparency) */
  --glass-solid: rgb(246 246 248 / 0.96);
  --glass-on: #111;              /* text on glass */

  --glass-radius: 24px;
}

@media (prefers-color-scheme: dark) {
  :root {
    --glass-tint: rgb(28 28 32 / 0.38);
    --glass-tint-strong: rgb(22 25 40 / 0.85);
    --glass-tint-clear: rgb(0 0 0 / 0.08);
    --glass-rim-top: rgb(255 255 255 / 0.28);
    --glass-rim: rgb(255 255 255 / 0.12);
    --glass-inner-glow: rgb(255 255 255 / 0.12);
    --glass-shadow-md: 0 8px 24px rgb(0 0 0 / 0.45);
    --glass-shadow-lg: 0 12px 40px rgb(0 0 0 / 0.55);
    --glass-solid: rgb(22 25 40 / 0.94);
    --glass-on: #f5f5f7;
  }
}
```

Sources for the ranges and values: base recipe of white 0.12 background, `blur(16px) saturate(180%)`, 1px white 0.28 border, 0.55 top border, `inset 0 1px 0` white 0.35, `0 12px 40px` black 0.35, 24px radius, blur 12–30px, saturate 140–200%, and the navy `rgba(22,25,40,…)` fallbacks [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026]; −60° specular default [src: https://kube.io/blog/liquid-glass-css-svg]; 35% dim [src: https://developer.apple.com/design/human-interface-guidelines/materials]; displacement scales of 20/30, 55, 60 and 70 [src: https://medium.com/ekino-france/liquid-glass-in-css-and-svg-839985fcb88d] [src: https://blog.logrocket.com/how-create-liquid-glass-effects-css-and-svg/] [src: https://raw.githubusercontent.com/rdev/liquid-glass-react/master/README.md]; Apple's emil skill toolbar example of white 0.6 background with `blur(20px) saturate(180%)` and a bright top border [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/apple-design/SKILL.md].

**Safari caveat:** one source reports that custom properties fail to resolve inside `-webkit-backdrop-filter`, so it hardcodes the prefixed declaration. Safari 18+ is unprefixed, so this only matters for Safari 9–17. Verify before relying on it. [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026]

---

## 4. Progressive enhancement tiers

### Tier 0 — opaque (baseline for everyone, plus reduced-transparency and forced-colors)

```css
.glass {
  position: relative;
  isolation: isolate;
  border-radius: var(--glass-radius);
  color: var(--glass-on);
  background: var(--glass-solid);
  border: 1px solid var(--glass-rim);
  box-shadow: var(--glass-shadow-md);
}
```

### Tier 1 — frosted (any engine with backdrop-filter)

```css
@supports (backdrop-filter: blur(1px)) or (-webkit-backdrop-filter: blur(1px)) {
  /* glass is the default here; §5 opts OUT under reduced-transparency / contrast / forced-colors */
  .glass {
    background: var(--glass-tint);
    -webkit-backdrop-filter: blur(16px) saturate(180%);          /* hardcoded for old Safari */
    backdrop-filter: blur(var(--glass-blur-md)) saturate(var(--glass-saturate)) brightness(var(--glass-brightness));
    border-color: transparent;
    box-shadow:
      inset 0 1px 0 var(--glass-inner-glow),     /* top specular line */
      inset 0 0 0 1px var(--glass-rim),          /* hairline rim */
      var(--glass-shadow-md);
  }
  /* gradient rim: brighter toward the light, fading away from it */
  .glass::before {
    content: "";
    position: absolute; inset: 0;
    border-radius: inherit;
    padding: 1px;                                  /* rim thickness */
    background: linear-gradient(calc(180deg + var(--glass-light-angle)),
                var(--glass-rim-top), transparent 40%, transparent 60%, var(--glass-rim));
    -webkit-mask: linear-gradient(#000 0 0) content-box exclude, linear-gradient(#000 0 0);
            mask: linear-gradient(#000 0 0) content-box exclude, linear-gradient(#000 0 0);
    pointer-events: none;
  }
}
```

Important notes:
- Make **glass the default inside `@supports`** and opt *out* in `@media (prefers-reduced-transparency: reduce)` (§5), because Safari never matches that feature, and gating glass on `no-preference` would turn glass off for every Safari user. BuildMVPFast instead defaults to solid and gates glass on `no-preference`, which in practice disables glass on Safari and Firefox. Choose deliberately. [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026] [src: https://developer.mozilla.org/en-US/docs/Web/CSS/@media/prefers-reduced-transparency]
- **Backdrop root pitfall:** any ancestor with `opacity < 1`, `filter`, `mask`, `clip-path`, `backdrop-filter`, `mix-blend-mode`, or `will-change` on those properties becomes a backdrop root. The child's blur then only sees content inside that ancestor and appears to do nothing. Never put `opacity` on a parent of glass; animate opacity on the glass element itself. [src: https://developer.mozilla.org/en-US/docs/Web/CSS/backdrop-filter]
- **Edge sampling:** the blur only reads pixels directly behind the element, so content approaching a sticky header pops in abruptly. The fix is a child `.backdrop` at `height: 200%`, masked to the top half with `mask-image: linear-gradient(to bottom, #000 0 50%, transparent 50% 100%)` and `pointer-events: none`. `overflow: hidden` on the parent also works in Chrome 142+. [src: https://www.joshwcomeau.com/css/backdrop-filter/]
- **Thickness edge (Comeau):** a second thin layer (4–6px) under the bar with `blur(12px) brightness(0.96)` or `blur(8px) brightness(120%)` and a white 0.1 background, masked to the thickness, reads as a glass slab edge. [src: https://www.joshwcomeau.com/css/backdrop-filter/]
- **Firefox bugs:** backdrop-filter breaks on a `position: sticky` element whose ancestor has `overflow` plus `border-radius`; the fix was in Nightly as of Sept 2026. Blur flicker has been reported, fixed with `overscroll-behavior: none`. [src: https://www.joshwcomeau.com/css/backdrop-filter/]
- **Scroll edge effect** instead of a 1px divider under sticky chrome: a small gradient/blur mask only where floating UI overlaps content. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/apple-design/SKILL.md]
- **Continuous corners:** `@supports (corner-shape: squircle) { .glass { corner-shape: squircle; } }` in Chrome 139+ [derived from BCD; src: https://raw.githubusercontent.com/mdn/browser-compat-data/main/css/properties/corner-shape.json]. Pair it with a slightly larger `border-radius`, because a squircle looks smaller than a round corner of the same radius [recommendation].

### Tier 2 — refraction (Chromium only, JS-gated)

**Gate in JS, not `@supports`.** Firefox accepts `backdrop-filter: url()` as valid but renders nothing, and Safari ignores it. [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026] [src: https://bugs.webkit.org/show_bug.cgi?id=245510]

```js
// Feature gate: Chromium-family engines only (user-agent client hints exist only in Chromium)
const supportsBackdropSvg =
  typeof navigator !== 'undefined' &&
  navigator.userAgentData?.brands?.some(b => /Chromium/.test(b.brand)) &&
  CSS.supports('backdrop-filter', 'url(#x)');
const reduceTransparency = matchMedia('(prefers-reduced-transparency: reduce)').matches;
const moreContrast = matchMedia('(prefers-contrast: more)').matches;
if (supportsBackdropSvg && !reduceTransparency && !moreContrast) {
  document.documentElement.dataset.glass = 'refract';
}
```

```css
:root[data-glass="refract"] .glass--refract {
  backdrop-filter: url(#lg-refract) blur(2px) saturate(160%);  /* small blur: lensing does the work */
}
```

**How the displacement map is built (kube.io approach)** [src: https://kube.io/blog/liquid-glass-css-svg]:
- Model a glass surface height `f(x)` across a bezel band (x = 0 at the outer edge, 1 at the inner flat). A *convex squircle* profile `y = (1 − (1 − x)^4)^(1/4)` gives smooth gradients and an optically thin bezel. Concave profiles push samples outside the shape, so avoid them.
- Take the surface normal by finite difference, apply Snell's law with n = 1 (air) and 1.5 (glass), and get a per-pixel displacement magnitude. Normalize by the max displacement and keep the max for the filter `scale`.
- Encode as RGBA: `R = 128 + dx·127`, `G = 128 + dy·127`, `B = 128`, `A = 255`. 128 is neutral.
- Filter chain: `feImage` (map) → `feDisplacementMap in=SourceGraphic in2=map xChannelSelector=R yChannelSelector=G scale=maxDisplacement`. A second `feImage` carries a specular rim lit from a fixed angle (default −60°) and is blended on top. Set `color-interpolation-filters="sRGB"`.
- Animating `scale` is cheap and fades the refraction in and out. That is the web version of Apple's "materialize by modulating lensing". Changing size or shape forces a map rebuild, and the filter does not resize with the element.
- Demo parameters: refraction 0.7–1.0, specular opacity 0.2–0.5, blur 0–1.

Original sketch (my code) of a filter definition the component injects once per size:

```html
<svg width="0" height="0" style="position:absolute" aria-hidden="true">
  <filter id="lg-refract" x="0" y="0" width="100%" height="100%"
          color-interpolation-filters="sRGB" filterUnits="userSpaceOnUse">
    <!-- map is generated on a canvas at the element's exact px size, then toDataURL() -->
    <feImage result="map" href="data:image/png;base64,..." x="0" y="0" width="320" height="56"/>
    <feDisplacementMap in="SourceGraphic" in2="map" scale="60"
                       xChannelSelector="R" yChannelSelector="G" result="bent"/>
    <feColorMatrix in="bent" type="saturate" values="1.6" result="vivid"/>
    <feImage result="rim" href="data:image/png;base64,..." x="0" y="0" width="320" height="56"/>
    <feBlend in="rim" in2="vivid" mode="screen"/>
  </filter>
</svg>
```

Practical constraints from the sources:
- Generate **one map per unique size**; reusing a map across sizes causes artifacts. Filters assume fixed dimensions, so be careful with elements that resize. [src: https://blog.logrocket.com/how-create-liquid-glass-effects-css-and-svg/]
- A cheaper procedural alternative is `feTurbulence` (e.g. `baseFrequency 0.05`, `numOctaves 2`) into `feDisplacementMap scale≈20`. It gives a wobbly glass look, not true edge lensing. SVG displacement has no supersampling, so the output can look pixelated. [src: https://medium.com/ekino-france/liquid-glass-in-css-and-svg-839985fcb88d]
- Generate a quarter of the map and mirror it, since a rounded rect has four-fold symmetry. When dragging, move the filter region instead of regenerating. Safari caches SVG filter output by `id`, so assign a fresh id when the map changes. [src: https://aave.com/design/building-glass-for-the-web]
- **Cross-engine alternative:** apply the displacement as a regular `filter` to *the element's own content* (not the backdrop). Aave says this works in Chromium, Safari and Firefox, because nothing is sampled from behind. It is useful for a magnifier/lens over known content. WebGL handles canvas and video. [src: https://aave.com/design/building-glass-for-the-web]

### Libraries (evaluate, don't blindly adopt)

| Library | Approach | Stars / License | Notes |
|---|---|---|---|
| `rdev/liquid-glass-react` | SVG displacement + backdrop | 6,584★ / MIT, last push 2025-06-13 | Props default to `displacementScale 70`, `blurAmount 0.0625`, `saturation 140`, `aberrationIntensity 2`, `elasticity 0.15`, `cornerRadius 999`, and modes standard, polar, prominent, shader. Displacement is not visible in Safari or Firefox. [src: https://api.github.com/repos/rdev/liquid-glass-react] [src: https://raw.githubusercontent.com/rdev/liquid-glass-react/master/README.md] |
| `naughtyduk/liquidGL` | WebGPU → WebGL2 → WebGL1 → backdrop-filter, page snapshot | 939★ / MIT | Safari can be unstable above 50% of the viewport. CSS animations under the lens are not refracted, and dynamic elements must be registered. [src: https://github.com/naughtyduk/liquidGL] |
| `ybouane/liquidglass` | WebGL shaders | 148★ / MIT, last push 2026-04-10 | [src: https://awesome.ecosyste.ms/projects/github.com%2Fybouane%2Fliquidglass] |

[recommendation] For a design system, own a small in-house Tier 2 implementation (map generator + filter registry keyed by size), because liquid-glass-react has been inactive since mid-2025 and WebGL snapshot libraries cannot see live DOM changes.

---

## 5. Accessibility fallbacks

```css
/* Reduce Transparency → frostier/solid (Apple: "frostier") */
@media (prefers-reduced-transparency: reduce) {
  .glass { background: var(--glass-solid); backdrop-filter: none; -webkit-backdrop-filter: none; }
  :root[data-glass="refract"] .glass--refract { backdrop-filter: none; }
}

/* Increase Contrast → near-solid with a defined border (Apple: mostly black/white + contrasting border) */
@media (prefers-contrast: more) {
  .glass {
    background: var(--glass-solid);
    backdrop-filter: none; -webkit-backdrop-filter: none;
    border: 2px solid currentColor;
    box-shadow: none;
  }
  .glass::before { display: none; }
}

/* Forced colors (Windows High Contrast): box-shadow and gradient backgrounds are forced to none;
   restore the edge with a system-color border */
@media (forced-colors: active) {
  .glass { border: 1px solid CanvasText; backdrop-filter: none; }
  .glass::before { display: none; }
}

/* Reduced motion → cross-fades, no springs/overshoot/parallax; no animated blur or lensing */
@media (prefers-reduced-motion: reduce) {
  .glass, .glass * { transition-property: opacity, color, background-color; transition-duration: 150ms; }
  .glass[data-state] { transform: none !important; }
}
```

Sources: Apple's three settings and their effects [src: https://developer.apple.com/videos/play/wwdc2025/219/]; the emil apple-design skill's mapping of reduced-transparency → raise opacity and drop blur, contrast-more → near-solid with a defined border, and reduced-motion → cross-fade with no overshoot [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/apple-design/SKILL.md]; forced-colors sets `box-shadow` and non-URL `background-image` to `none` and recommends `ButtonText`/`CanvasText` borders [src: https://developer.mozilla.org/en-US/docs/Web/CSS/@media/forced-colors]; contrast-more → `border: 2px solid currentColor` [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026].

Other rules:
- **Safari has no `prefers-reduced-transparency`.** Even with Reduce Transparency on in iOS, web glass stays translucent there. Compensate with a higher-alpha tint whenever text sits on glass (e.g. `--glass-tint-strong`). [src: https://raw.githubusercontent.com/mdn/browser-compat-data/main/css/at-rules/media.json] [recommendation]
- **Contrast has no fixed ratio on translucent surfaces.** Test WCAG contrast against the worst-case backdrop, such as a white photo under light glass. [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026]
- **No glass for data-dense views or content.** If users read values off the screen, flatten it. [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026] Text contrast is the main criticism of the look. [src: https://blog.master.dev/wp-json/wp/v2/posts/6644]
- **Text on glass (vibrancy analogue):** don't use flat gray. Use higher contrast, slightly heavier weight, and a small letter-spacing bump, with color on a solid layer. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/apple-design/SKILL.md] Never put a light translucent surface on another one. [same]
- **Clear variant only over media**, always with the 35% dim layer when the media is bright. [src: https://developer.apple.com/design/human-interface-guidelines/materials]
- Keep a semantic `<button>` or `<nav>` underneath. Decorative SVG gets `aria-hidden="true"`. [src: https://blog.logrocket.com/how-create-liquid-glass-effects-css-and-svg/]

---

## 6. Performance budget

| Budget item | Limit | Source |
|---|---|---|
| Glass layers per viewport | ≤ 3–4 | [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026] |
| Static blur radius on large surfaces | ≤ ~20px (sheets ≤ 24px) | [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026] [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/animation-performance/SKILL.md] |
| Animated blur | ≤ 20px (Emil); lint warns at > 10px; prefer 2–5px for crossfades | [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/animation-performance/SKILL.md] [src: https://www.react.doctor/docs/rules/react-doctor/no-large-animated-blur] |
| Animate blur radius of a large backdrop-filter? | **No.** Fade `opacity`, or animate `scale` (Tier 2), instead | [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026] [src: https://kube.io/blog/liquid-glass-css-svg] |
| Frame budget | 16.7ms at 60Hz; design for **8ms** at 120Hz | [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/animation-performance/SKILL.md] |
| Refraction element size | Keep small (≤ 50% of the viewport in either axis for WebGL libs in Safari) | [src: https://github.com/naughtyduk/liquidGL] |

Mitigations:
- **Shrink the filtered area.** Limit the backdrop overlay to the smallest region needed. [src: https://www.react.doctor/docs/rules/react-doctor/no-large-animated-blur] Use a small layer, since compositing it costs less than a full-screen one. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/animation-performance/SKILL.md]
- **Firefox scroll cost:** a backdrop-filter defeats WebRender picture caching, so the page can re-render on every scroll (bug 1732817 still open). Test sticky glass headers on Firefox Android and consider Tier 0 for Firefox mobile. [src: https://bugzilla.mozilla.org/show_bug.cgi?id=1732817]
- Avoid combining glass with scroll-linked effects on the same surface, and test on a mid-tier Android over the worst backdrop. [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026]
- Add `will-change` only after you have seen a 1px shift or measured dropped frames, and only on the animating element, never on `*` or large containers. Note that `will-change: opacity|filter` on a *parent* also creates a backdrop root. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/animation-performance/SKILL.md] [src: https://developer.mozilla.org/en-US/docs/Web/CSS/backdrop-filter]
- Don't drive per-frame motion through an inherited CSS variable on a parent, because it restyles every descendant. Write `transform` on the element. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/emil-design-eng/SKILL.md]
- Group glass like Apple's `GlassEffectContainer`: a single shared bar with several buttons beats N separate glass buttons. [src: https://developer.apple.com/documentation/swiftui/applying-liquid-glass-to-custom-views] [recommendation for web analogue]
- Regenerate refraction maps only on size or shape change (ResizeObserver, debounced). On drag, move the filter region. [src: https://aave.com/design/building-glass-for-the-web] [src: https://kube.io/blog/liquid-glass-css-svg]

---

## 7. Motion tokens

### 7.1 Apple's spring model (verified)

- SwiftUI `spring(duration: 0.5, bounce: 0.0, blendDuration: 0)` defaults. `bounce` 0 is critically damped, >0 is bouncier up to 1 (undamped), and <0 is overdamped down to −1. `duration` is *perceptual*: roughly the settle time, or the oscillation period for very bouncy springs. [src: https://developer.apple.com/documentation/swiftui/animation/spring(duration:bounce:blendduration:)]
- Legacy `spring(response: 0.5, dampingFraction: 0.825, blendDuration: 0)`, i.e. bounce ≈ 0.175. [src: https://developer.apple.com/documentation/swiftui/animation/spring(response:dampingfraction:blendduration:)]
- `interactiveSpring(response: 0.15, dampingFraction: 0.86, blendDuration: 0.25)` for finger-tracking. [src: https://developer.apple.com/documentation/swiftui/animation/interactivespring(response:dampingfraction:blendduration:)]
- Presets (all `duration` default 0.5): `.smooth` base bounce **0** [src: https://developer.apple.com/documentation/swiftui/animation/smooth(duration:extrabounce:)]; `.snappy` base bounce **0.15** [src: https://developer.apple.com/documentation/swiftui/animation/snappy(duration:extrabounce:)]; `.bouncy` base bounce **0.3** [src: https://developer.apple.com/documentation/swiftui/animation/bouncy(duration:extrabounce:)].
- Conversion: mass = 1, stiffness = (2π / duration)², damping = 4π(1 − bounce) / duration for bounce ≥ 0. I checked this against Apple's example, where `Spring(duration: 0.5, bounce: 0.3)` gives (1.0, 157.9, 17.6). [src: https://developer.apple.com/videos/play/wwdc2023/10158/] [src: https://developer.apple.com/documentation/swiftui/spring]
- Guidance: start at bounce 0. About 0.15 adds a brisk small bounce and 0.3 a noticeable one. Be cautious above about 0.4. Don't wait for the settling duration for user-facing changes. [src: https://developer.apple.com/videos/play/wwdc2023/10158/]
- Values Apple ships (from *Designing Fluid Interfaces*, via Emil's skill): reposition uses damping 1.0 / response 0.4, rotation 0.8 / 0.4, drawer/sheet 0.8 / 0.3. Add bounce only when the gesture carried momentum. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/apple-design/SKILL.md]

### 7.2 Web mapping

**JS (gestures, interruptible, velocity handoff): Motion (`motion.dev`).** Its `{ type: 'spring', duration, bounce }` maps onto Apple's model. Motion's own defaults are `duration 800ms` and `bounce 0.25`, so **always set them explicitly**. `visualDuration` (seconds) aligns a spring with time-based animations, and `velocity` defaults to the value's current velocity. [src: https://motion.dev/docs/spring]

```js
// Original token module
export const spring = {
  smooth:      { type: 'spring', duration: 0.5,  bounce: 0 },     // SwiftUI .smooth
  snappy:      { type: 'spring', duration: 0.5,  bounce: 0.15 },  // .snappy
  bouncy:      { type: 'spring', duration: 0.5,  bounce: 0.3 },   // .bouncy (momentum only)
  interactive: { type: 'spring', duration: 0.15, bounce: 0.14 },  // ≈ interactiveSpring (dampingFraction .86)
  sheet:       { type: 'spring', duration: 0.3,  bounce: 0.2 },   // drawer: damping .8, response .3
  move:        { type: 'spring', duration: 0.4,  bounce: 0 },     // reposition: damping 1, response .4
};
```

**CSS (predetermined transitions): `linear()` springs.** `linear()` approximates springs by sampling many points, and more stops give smoother motion. Generate the stops with Jake Archibald and Adam Argyle's Linear Easing Generator; Chrome DevTools has had a `linear()` editor since 114. [src: https://developer.chrome.com/docs/css-ui/css-linear-easing-function] [src: https://linear-easing-generator.netlify.app/]

The following stops were **computed by me** from Apple's formula above (closed-form damped oscillator, 21 samples, truncated at |error| < 0.4%). The CSS duration is that truncated settle time, not Apple's perceptual duration:

```css
:root {
  /* spring(duration .5, bounce 0) — .smooth */
  --spring-smooth: linear(0, 0.057, 0.18, 0.32, 0.454, 0.572, 0.67, 0.749, 0.811, 0.859, 0.896,
                          0.923, 0.944, 0.959, 0.97, 0.979, 0.985, 0.989, 0.992, 0.994, 1);
  --spring-smooth-dur: 610ms;

  /* spring(duration .5, bounce .15) — .snappy (peak ≈1.006) */
  --spring-snappy: linear(0, 0.052, 0.171, 0.314, 0.457, 0.586, 0.696, 0.785, 0.853, 0.904, 0.941,
                          0.967, 0.984, 0.995, 1.001, 1.005, 1.006, 1.006, 1.006, 1.005, 1);
  --spring-snappy-dur: 570ms;

  /* spring(duration .5, bounce .3) — .bouncy (peak ≈1.045) */
  --spring-bouncy: linear(0, 0.053, 0.178, 0.335, 0.495, 0.643, 0.767, 0.866, 0.939, 0.99, 1.022,
                          1.039, 1.045, 1.045, 1.04, 1.034, 1.026, 1.019, 1.013, 1.008, 1);
  --spring-bouncy-dur: 560ms;

  /* spring(duration .3, bounce .2) — sheet/drawer (peak ≈1.015) */
  --spring-sheet: linear(0, 0.057, 0.185, 0.34, 0.493, 0.63, 0.743, 0.833, 0.899, 0.946, 0.977,
                         0.997, 1.008, 1.014, 1.015, 1.014, 1.013, 1.01, 1.008, 1.006, 1);
  --spring-sheet-dur: 360ms;

  /* cubic fallbacks (Emil's set) for engines without linear() */
  --ease-out: cubic-bezier(0.23, 1, 0.32, 1);
  --ease-in-out: cubic-bezier(0.77, 0, 0.175, 1);
  --ease-drawer: cubic-bezier(0.32, 0.72, 0, 1);   /* iOS-like drawer (Ionic) */

  --dur-press: 120ms;   /* 100–160ms */
  --dur-tooltip: 150ms; /* 125–200ms */
  --dur-menu: 200ms;    /* 150–250ms */
  --dur-modal: 300ms;   /* 200–500ms */
}
@supports not (transition-timing-function: linear(0, 1)) {
  :root { --spring-smooth: var(--ease-out); --spring-snappy: var(--ease-out);
          --spring-bouncy: var(--ease-out); --spring-sheet: var(--ease-drawer); }
}
```

Emil's curves and duration tables: [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/emil-design-eng/SKILL.md] [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/animate/SKILL.md]. A `linear()` spring is **not** velocity-aware or interruptible the way a JS spring is. When retargeted mid-flight, CSS transitions restart from the current value with zero velocity. Use JS springs for anything a finger touches. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/apple-design/SKILL.md]

### 7.3 Glass-specific motion recipes

- **Press (light up from within):** on `pointerdown`, not click, apply `scale(0.97)` over 100–160ms ease-out and raise `--glass-inner-glow`. Add a radial highlight at the pointer, done with a `::after` whose `background: radial-gradient(circle at var(--px) var(--py), …)` is written only on that element. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/apple-design/SKILL.md] [src: https://developer.apple.com/videos/play/wwdc2025/219/] [recipe derived]
- **Interactive stretch ("elasticity"):** while dragging, scale the glass slightly along the drag axis using a JS spring, then settle with `spring.snappy` on release, handing off the release velocity. liquid-glass-react's `elasticity` defaults to 0.15. [src: https://raw.githubusercontent.com/rdev/liquid-glass-react/master/README.md] [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/apple-design/SKILL.md]
- **Materialize, don't just fade:** enter by animating opacity 0→1 together with `scale(0.96→1)` and, in Tier 2, the displacement `scale` 0→N. In Tier 1, animate a *small* blur (≤ 5px) on the content or fade the glass's opacity; do not tween a 24px backdrop blur. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/apple-design/SKILL.md] [src: https://kube.io/blog/liquid-glass-css-svg] [src: https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026]
- **Origin-aware menus:** popovers grow from their trigger via `transform-origin` on the trigger side; modals stay centered. Never start from `scale(0)`; start from 0.9–0.97 plus opacity 0. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/emil-design-eng/SKILL.md]
- **Enter with `@starting-style`** (Chrome 117, Firefox 129, Safari 17.5) instead of mount-flag effects. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/emil-design-eng/SKILL.md] [src: https://raw.githubusercontent.com/mdn/browser-compat-data/main/css/at-rules/starting-style.json]
- **Don't animate high-frequency or keyboard-initiated actions** (e.g. command palette toggles). Keep UI animations under 300ms. Make exits faster than entrances. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/emil-design-eng/SKILL.md]
- **Interruptibility is mandatory.** Let people cancel motion and never make them wait for it to finish. [src: https://developer.apple.com/design/human-interface-guidelines/motion] Start new animations from the on-screen (presentation) value. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/apple-design/SKILL.md]
- **Symmetric paths.** Something revealed by sliding down is dismissed by sliding up. [src: https://developer.apple.com/design/human-interface-guidelines/motion]

### 7.4 Morphing glass shapes with the View Transitions API

Use this for tab-bar → expanded-panel or button → sheet morphs (the web analogue of `glassEffectID` + matchedGeometry).

```css
.fab           { view-transition-name: glass-morph; }
.sheet[open]   { view-transition-name: glass-morph; }   /* only one element holds the name at a time */

::view-transition-group(glass-morph) {
  animation-duration: var(--spring-snappy-dur);
  animation-timing-function: var(--spring-snappy);
}
::view-transition-old(glass-morph),
::view-transition-new(glass-morph) { height: 100%; object-fit: none; overflow: clip; } /* avoid squash */

@media (prefers-reduced-motion: reduce) {
  ::view-transition-group(*) { animation-duration: 0s; }
  ::view-transition-old(*), ::view-transition-new(*) { animation-duration: 150ms; } /* crossfade only */
}
```

```js
function morph(update) {
  if (!document.startViewTransition) return update();
  document.startViewTransition(update);
}
```

Facts: the group animates size and position, and old/new snapshots cross-fade by default. Names must be unique at any moment, otherwise `ready` rejects and the transition is skipped. `match-element` auto-names. Cross-document transitions need `@view-transition { navigation: auto; }` on both pages and are same-origin only. [src: https://developer.mozilla.org/en-US/docs/Web/API/View_Transition_API/Using]

Caveats for glass:
- The snapshot is a **flat bitmap**. During the transition, `::view-transition-old` is static and `::view-transition-new` is live but flattened into a replaced element, so the glass **does not re-sample the moving backdrop mid-morph**. Effects on the captured element (including `backdrop-filter`) are baked into its snapshot, while effects inherited from ancestors are not; the spec wording on this is still being settled (csswg #9139). Keep morphs short (≤ 400ms) so the frozen frosting isn't noticeable. [src: https://github.com/w3c/csswg-drafts/issues/9139] [recommendation]
- WebKit captures transformed elements at layout size rather than visual size, so scaled glass can look soft during a morph (bug 317712, open). [src: https://bugs.webkit.org/show_bug.cgi?id=317712]
- For finger-driven morphs (sheet drag), use a JS spring with FLIP or Motion `layoutId` instead. View transitions are not interruptible mid-gesture. Motion layout animations run on the main thread. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/skills/emil-design-eng/SKILL.md]

---

## 8. emilkowalski/skills — what applies here

Repo is MIT licensed, © 2026 Emil Kowalski. [src: https://raw.githubusercontent.com/emilkowalski/skills/main/LICENSE] Folders come from the GitHub API listing [src: https://api.github.com/repos/emilkowalski/skills/contents/skills]. Three more skills (remove-animations, animation-performance, break-ui) are referenced in the README and exist at raw URLs but did not appear in the API listing (probably added recently). [src: https://raw.githubusercontent.com/emilkowalski/skills/main/README.md]

Rules extracted (all from the SKILL.md files under `https://raw.githubusercontent.com/emilkowalski/skills/main/skills/<name>/SKILL.md`):

**Easing / duration**
- Enter/exit: ease-out. On-screen movement: ease-in-out. Hover/color: `ease`. Constant motion: linear. **Never ease-in for UI.** Built-in keywords are too weak, so use custom curves: `--ease-out: cubic-bezier(0.23,1,0.32,1)`, `--ease-in-out: cubic-bezier(0.77,0,0.175,1)`, `--ease-drawer: cubic-bezier(0.32,0.72,0,1)`. (emil-design-eng, animate)
- Durations: press 100–160ms, tooltip 125–200ms, dropdown 150–250ms, modal/drawer 200–500ms. Keep UI under 300ms. Exits faster than entrances. Asymmetric timing: slow where the user decides, snappy where the system responds. (emil-design-eng, animate)
- Frequency gate: actions seen 100+ times a day, or triggered by keyboard, get no animation. (emil-design-eng, review-animations)
- No approximated values: pull curves from the tables and never invent one. (animate)

**Springs**
- Prefer Apple-style `{type:'spring', duration, bounce}`. Bounce 0.1–0.3 only for momentum or playful cases; default to damping 1.0. Reanimated configs: `{duration:400, dampingRatio:1}` default, `{duration:300, dampingRatio:0.8, velocity}` for sheets. (emil-design-eng, apple-design, animate-expo)
- Hand off velocity at release. Project momentum with `v/1000 · d/(1−d)`, d ≈ 0.998. Rubber-band with `(o·D·c)/(D + c·|o|)`, c = 0.55. Split 2D motion into X and Y springs. Animate from the presentation value. (apple-design)
- CSS transitions (retargetable) over keyframes (restart) for rapidly triggered UI. Springs or JS for gestures. CSS/WAAPI for predetermined motion, because they stay off the main thread under load. (emil-design-eng)

**Interaction feedback**
- `:active { transform: scale(0.97) }` (0.95–0.98). Respond on pointer-down. Never `scale(0)`; use 0.9–0.97 plus opacity. Popovers scale from the trigger origin, modals stay centered. Gate hover with `(hover: hover) and (pointer: fine)`. 30–80ms stagger. `@starting-style` for enters. Tap/drag hysteresis around 10px. Pointer capture. Ignore extra touches. Velocity-based dismiss above about 0.11 px/ms. (emil-design-eng, apple-design, mobile-native)

**Glass / blur / material** (apple-design §12, animation-performance, emil-design-eng)
- Translucent nav, toolbars, and sheets via `backdrop-filter: blur() + semi-transparent bg`, with content scrolling underneath.
- Material weight encodes hierarchy. **Never stack light translucent on light translucent.** Bigger surfaces get stronger blur and a deeper shadow. Shadow strength depends on context.
- Dim to focus (modal with scrim) versus translucency without a scrim (parallel panels).
- Text on glass: higher contrast, a bit heavier, slight tracking bump.
- Use a scroll-edge fade mask instead of 1px dividers.
- **Materialize:** animate blur and scale together on enter/exit, not just opacity.
- Keep animated blur under 20px, especially in Safari; 2–5px softens crossfades. `filter: blur(2px)` masks imperfect crossfades.
- Expo: never animate `BlurView` intensity; crossfade a static blur instead (the web analogue is not to tween a large backdrop blur).
- A11y trio: reduced-motion → crossfade; reduced-transparency → raise opacity, drop blur; contrast-more → near-solid plus border.

**Perf**
- Animate only transform/opacity (plus clip-path). Never `transition: all`. Avoid inherited CSS vars per frame. `will-change` is a last resort and should stay small. 8ms frame budget at 120Hz. Motion ≥ 14.2 hardware-accelerates `x`/`y`/`scale`. (animation-performance, emil-design-eng)

---

## 9. Implementation checklist for the agent

1. Add the §3 tokens and the light/dark overrides. Hardcode `-webkit-backdrop-filter` values.
2. Ship `.glass` with Tier 0 as the default and Tier 1 inside `@supports`. Add a `.glass--refract` modifier enabled only when `data-glass="refract"` is set by the JS gate.
3. Add the §5 a11y blocks. Also raise tint alpha wherever text sits on glass, because Safari ignores reduced-transparency.
4. Lint rules: no glass inside glass, no `opacity`/`filter`/`will-change` on glass ancestors, no `transition: all`, no animated backdrop blur above 10px, at most 4 glass elements per screen.
5. Motion: export `spring.*` (JS) and `--spring-*` (CSS) tokens. Press uses `--dur-press` ease-out on pointer-down. Morphs use View Transitions with `--spring-snappy`, and gestures use Motion springs with velocity handoff.
6. Test the worst-case backdrop (white photo, busy text) in both light and dark, on Chrome, Safari, and Firefox Android scrolling, with every OS a11y toggle on.

---

## Appendix: URLs fetched

- https://api.github.com/repos/emilkowalski/skills/contents/skills
- https://raw.githubusercontent.com/emilkowalski/skills/main/README.md
- https://raw.githubusercontent.com/emilkowalski/skills/main/LICENSE
- https://raw.githubusercontent.com/emilkowalski/skills/main/skills/{animate-expo,animate,animation-vocabulary,apple-design,ask-sonner,emil-design-eng,find-animation-opportunities,improve-animations,mobile-native,pick-ui-library,prototype,review-animations,write-swift,remove-animations,animation-performance,break-ui}/SKILL.md
- https://raw.githubusercontent.com/emilkowalski/skills/main/skills/review-animations/STANDARDS.md
- https://kube.io/blog/liquid-glass-css-svg
- https://www.joshwcomeau.com/css/backdrop-filter/
- https://aave.com/design/building-glass-for-the-web
- https://blog.logrocket.com/how-create-liquid-glass-effects-css-and-svg/
- https://medium.com/ekino-france/liquid-glass-in-css-and-svg-839985fcb88d
- https://blog.master.dev/wp-json/wp/v2/posts/6644 (Frontend Masters, "Liquid Glass on the Web")
- https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026
- https://developer.mozilla.org/en-US/docs/Web/CSS/backdrop-filter
- https://developer.mozilla.org/en-US/docs/Web/CSS/@media/prefers-reduced-transparency
- https://developer.mozilla.org/en-US/docs/Web/CSS/@media/forced-colors
- https://developer.mozilla.org/en-US/docs/Web/API/View_Transition_API/Using
- https://raw.githubusercontent.com/mdn/browser-compat-data/main/ (backdrop-filter, media, easing-function, Document, view-transition, view-transition-name, corner-shape, property, starting-style JSON)
- https://caniuse.com/css-backdrop-filter
- https://caniuse.com/mdn-css_at-rules_media_prefers-reduced-transparency
- https://developer.chrome.com/docs/css-ui/css-linear-easing-function
- https://linear-easing-generator.netlify.app/
- https://motion.dev/docs/spring
- https://bugs.webkit.org/show_bug.cgi?id=245510
- https://bugs.webkit.org/show_bug.cgi?id=297770
- https://bugs.webkit.org/show_bug.cgi?id=317712
- https://bugzilla.mozilla.org/show_bug.cgi?id=1732817
- https://github.com/w3c/csswg-drafts/issues/9139
- https://lists.w3.org/Archives/Public/public-css-archive/2025Jun/0348.html
- https://www.react.doctor/docs/rules/react-doctor/no-large-animated-blur
- https://api.github.com/repos/rdev/liquid-glass-react
- https://raw.githubusercontent.com/rdev/liquid-glass-react/master/README.md
- https://github.com/naughtyduk/liquidGL
- https://awesome.ecosyste.ms/projects/github.com%2Fybouane%2Fliquidglass
- Apple (via developer.apple.com tutorials/data JSON endpoints): HIG materials, HIG motion, SwiftUI Spring, Animation spring(duration:bounce:), spring(response:dampingFraction:), interactiveSpring, smooth/snappy/bouncy(duration:extraBounce:), Applying Liquid Glass to custom views; WWDC25 session 219 "Meet Liquid Glass"; WWDC23 session 10158 "Animate with springs"
