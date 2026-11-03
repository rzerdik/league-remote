# Web implementation — recipes for League Remote

Concrete CSS/JS for the `lr-` stylesheet. Code is ours; techniques and browser facts are sourced (`SOURCES.md`). Deeper notes, library comparison and refraction math: `../research/04-web-implementation.md`.

**Platform reality for this app:** the main audience is iPhone Safari (a PWA). Safari supports `backdrop-filter`, `color-mix()`, `linear()` and View Transitions, but **not** `backdrop-filter: url(#svg)` (no refraction) and **not** `prefers-reduced-transparency` [WEBKIT-245510] [BCD-MEDIA]. Design for Tier 1 as the real look; Tier 2 is a Chromium-only bonus.

## 1. Browser support (MDN browser-compat-data, Nov 2026)

| Feature | Chrome | Firefox | Safari |
|---|---|---|---|
| `backdrop-filter` | 76 | 103 | 18 (9 prefixed) |
| `backdrop-filter: url(#svg)` | yes | parses, renders nothing | no (WebKit bug 245510 open) |
| `prefers-reduced-transparency` | 118 | behind a pref | no |
| `prefers-contrast` | 96 | 101 | 14.1 |
| `forced-colors` | 89 | 89 | 16 |
| `linear()` easing | 113 | 112 | 17.2 |
| `document.startViewTransition` | 111 | 144 | 18 |
| `@starting-style` | 117 | 129 | 17.5 |
| `@property` | 85 | 128 | 16.4 |
| `corner-shape: squircle` | 139 | — | — |

Sources: [BCD-*], [MDN-BACKDROP], [CANIUSE-BACKDROP], [WEBKIT-245510].

## 2. Tiers

| Tier | Who | Look |
|---|---|---|
| T0 opaque | No `backdrop-filter`; Reduce Transparency; glass amount = tinted; `forced-colors` | `glass-tinted` / `bg-elevated` + border |
| T1 frosted | Everyone else (the real look on iPhone) | translucent fill + blur/saturate + specular rim + dark edge + lift |
| T2 refractive | Chromium only, JS-gated, small fixed-size chrome (tab bar, action-bar capsule) | T1 + `feDisplacementMap` lensing at the rim |

## 3. The glass recipe (T1), token-driven

```css
:root {
  --lr-glass-amount: var(--glass-amount);           /* 0 clear … 0.5 default … 1 tinted */
}
.lr-glass, .lr-glass-thick, .lr-btn--glass {
  /* clear half: mix ultraclear→glass; tinted half: glass→tinted */
  --lr-glass-fill: color-mix(in srgb,
      var(--glass-ultraclear),
      var(--glass) calc(min(var(--lr-glass-amount), 0.5) * 200%));
  --lr-glass-fill-tinted: color-mix(in srgb,
      var(--lr-glass-fill),
      var(--glass-tinted) calc(max(var(--lr-glass-amount) - 0.5, 0) * 200%));
  background: var(--lr-glass-fill-tinted);
  -webkit-backdrop-filter: blur(var(--glass-blur)) saturate(var(--glass-saturate));
          backdrop-filter: blur(var(--glass-blur)) saturate(var(--glass-saturate));
  box-shadow: var(--glass-rim), var(--glass-lift);
}
.lr-glass-thick {
  /* same mix with glass-thick as the middle stop */
  -webkit-backdrop-filter: blur(var(--glass-blur-thick)) saturate(var(--glass-saturate-thick));
          backdrop-filter: blur(var(--glass-blur-thick)) saturate(var(--glass-saturate-thick));
}
```

Notes:
- `color-mix()` with percentages computed by `calc()` works in Safari 16.2+ and Chrome 111+; verify on the oldest iOS you support. If percentages from `calc()` misbehave, precompute five steps (`data-glass-amount="0|25|50|75|100"`) instead.
- Keep `-webkit-backdrop-filter`. One source reports old Safari failing to resolve custom properties inside the prefixed property; Safari 18+ is unprefixed, so only matters below that [BUILDMVP].
- **Backdrop-root trap:** any ancestor with `opacity < 1`, `filter`, `mask`, `clip-path`, `backdrop-filter`, `mix-blend-mode` or `will-change` on those makes the child's blur see only that ancestor's content. Animate opacity on the glass element itself, never on its parent [MDN-BACKDROP].
- **Edge sampling:** blur only reads pixels directly behind the element, so content pops in at the edges of sticky chrome. Fix with an oversized masked child backdrop when it shows [COMEAU].

## 4. Accessibility overrides

```css
/* T0 when the OS asks for less transparency (Chromium only) */
@media (prefers-reduced-transparency: reduce) {
  :root { --lr-glass-amount: 1; }
}
/* Increase Contrast is handled by the dark-hc / light-hc themes; the slider is ignored there */
[data-theme$="-hc"] { --lr-glass-amount: 1; }

/* Windows/Android forced colors: shadows and gradients are dropped by the UA */
@media (forced-colors: active) {
  .lr-glass, .lr-glass-thick, .lr-btn--glass {
    -webkit-backdrop-filter: none; backdrop-filter: none;
    border: 1px solid CanvasText;
  }
}
```

Because iOS Safari never matches `prefers-reduced-transparency`, the in-app glass amount is the only way an iPhone user can get frostier glass on the web (Decision D1) [BCD-MEDIA].

## 5. Interactive glass press (glow from the touch point)

```css
.lr-glass-interactive { position: relative; isolation: isolate; overflow: clip; }
.lr-glass-interactive::after {
  content: ""; position: absolute; inset: 0; border-radius: inherit; pointer-events: none;
  background: radial-gradient(circle at var(--lr-px, 50%) var(--lr-py, 50%),
              var(--glass-glow), transparent var(--glass-glow-size));
  opacity: 0; transition: opacity var(--dur-fade) var(--ease-fade);
}
.lr-glass-interactive.is-pressed::after { opacity: 1; transition-duration: var(--dur-glow); }
.lr-glass-interactive.is-pressed {
  transform: scale(var(--glass-press-scale));          /* or 0.96 if D4 = keep */
  transition: transform var(--dur-press) var(--ease-fade);
}
.lr-glass-interactive:not(.is-pressed) {
  transition: transform var(--dur-bouncy) var(--spring-bouncy);  /* overshoot on release */
}
@media (prefers-reduced-motion: reduce) {
  .lr-glass-interactive.is-pressed { transform: none; }
  .lr-glass-interactive::after { display: none; }
}
```

```js
// pointerdown → write the touch point on THIS element only (never an inherited var on a parent)
el.addEventListener('pointerdown', (e) => {
  const r = el.getBoundingClientRect();
  el.style.setProperty('--lr-px', `${e.clientX - r.left}px`);
  el.style.setProperty('--lr-py', `${e.clientY - r.top}px`);
  el.classList.add('is-pressed');
});
```

The app already shows press states on pointer-down (`is-pressed`); reuse that path. Apple describes the press as scale + bounce + inner light from the finger [V-219] [V-323]; Emil's guidance for non-glass buttons is a 0.95–0.98 press-down [EMIL-DESIGN-ENG] — hence Decision D4.

## 6. Materialize (enter/exit for menus, banners, popovers)

```css
@keyframes lr-materialize-in {
  from { opacity: 0; transform: scale(0.94); filter: blur(4px); }
}
@keyframes lr-materialize-out {
  to   { opacity: 0; transform: scale(0.96); filter: blur(2px); }
}
.lr-menu[data-state='open']   { animation: lr-materialize-in var(--dur-menu) var(--spring-menu); }
.lr-menu[data-state='closed'] { animation: lr-materialize-out var(--dur-fade) var(--ease-fade); }
@media (prefers-reduced-motion: reduce) {
  .lr-menu[data-state] { animation-name: lr-fade; }
}
```

- `filter: blur()` here blurs the element's own pixels (cheap, ≤ 5px); never tween the 28px backdrop blur [EMIL-PERF] [BUILDMVP].
- Never start from `scale(0)`; start 0.9–0.97, from the trigger's origin [EMIL-DESIGN-ENG]. The current `scale(0.6)` is a stand-in for Apple's morph; keep it only if a View-Transition morph is not built.
- `filter` on the menu makes it a backdrop root for its children — fine, children are not glass.

## 7. Button → menu morph (optional)

Apple morphs a button into its menu [TO-ADOPT]. Web analogue: View Transitions with a shared `view-transition-name` on the trigger and the open menu (one holder at a time), `::view-transition-group` timed with `spring-menu`. Caveats: snapshots are flat, so the glass doesn't re-sample the backdrop mid-morph — keep it ≤ 400ms; WebKit captures transformed elements at layout size, so scaled glass can look soft [CSSWG-9139] [WEBKIT-317712]. Not interruptible mid-gesture.

## 8. Top scroll edge (iOS 27 uniform toolbar zone)

```css
.lr-edge-top {
  position: fixed; inset: 0 0 auto 0; z-index: var(--z-edge); pointer-events: none;
  height: calc(var(--lr-nav-top) + var(--lr-nav-h) + var(--space-5));
  -webkit-backdrop-filter: blur(var(--edge-blur)); backdrop-filter: blur(var(--edge-blur));
  -webkit-mask-image: linear-gradient(#000 60%, transparent);
          mask-image: linear-gradient(#000 60%, transparent);
  opacity: 0; transition: opacity var(--dur-fade) var(--ease-fade);
}
.lr-html[data-scrolled='true'] .lr-edge-top { opacity: 1; }
```

Set `data-scrolled` from an `IntersectionObserver` on a sentinel at the top of `.lr-content` (cheaper than a scroll listener; scroll-driven animations would be nicer but need Safari 26+, verify before relying on them). One soft edge per edge — don't add a hard one [V-356].

## 9. Shared glass group

```css
.lr-glass-group { display: inline-flex; border-radius: var(--radius-full); padding: var(--space-0-5); }
.lr-glass-group > .lr-btn { background: transparent; box-shadow: none;
  -webkit-backdrop-filter: none; backdrop-filter: none; }
```

One glass surface per group = one backdrop sample, matching "glass can't sample glass" and `GlassEffectContainer` [V-323] [SUI-CONTAINER].

## 10. Concentric radius helper

```css
/* parent publishes its radius and padding; child computes its own */
.lr-concentric { border-radius: max(var(--lr-r-min, var(--radius-md)),
                 calc(var(--lr-r-outer) - var(--lr-pad))); }
@supports (corner-shape: squircle) {        /* Chrome 139+, progressive */
  .lr-squircle { corner-shape: squircle; }
}
```

Source for the rule: [V-356]; `corner-shape` support: [BCD-CORNER]. A squircle reads smaller than a circular corner of the same radius — bump the radius slightly where it's used.

## 11. Tier 2 refraction (optional, Decision D2)

- Gate in JS, not `@supports`: Firefox parses `backdrop-filter: url()` and draws nothing, Safari ignores it [BUILDMVP] [WEBKIT-245510].
- Build one displacement map per element **size** (canvas → `toDataURL` → `feImage`), convex-squircle bezel profile, Snell's law n=1.5, displacement in R/G with 128 neutral; `feDisplacementMap scale = refraction-scale` [KUBE].
- Regenerate only on resize (debounced `ResizeObserver`); animate `scale` to materialize the lensing [KUBE] [AAVE].
- Use it only on the tab bar and the action-bar capsule (small, fixed size, always visible). Never on sheets or menus.
- Skip when Reduce Transparency, IC themes, glass amount ≥ 0.75 or Reduce Motion is on.
- Libraries exist (liquid-glass-react, MIT, inactive since mid-2025; liquidGL, WebGL snapshot) — prefer a small in-house map generator; WebGL snapshot libraries can't see live DOM changes [LGR] [LIQUIDGL].

```js
const canRefract =
  navigator.userAgentData?.brands?.some(b => /Chromium/.test(b.brand)) &&
  CSS.supports('backdrop-filter', 'url(#x)') &&
  !matchMedia('(prefers-reduced-transparency: reduce)').matches &&
  !matchMedia('(prefers-reduced-motion: reduce)').matches;
if (canRefract) document.documentElement.dataset.glass = 'refract';
```

## 12. Performance budget

- ≤ 3–4 glass surfaces visible at once (tab bar, action bar, 1–2 nav circles is already at the limit; a sheet hides chrome) [BUILDMVP].
- Never animate a large backdrop blur; fade opacity instead. Animated `filter: blur()` ≤ 10px (lint), 2–5px for cross-fades [EMIL-PERF] [REACT-DOCTOR].
- Animate `transform`/`opacity` only; no `transition: all`; `will-change` as a last resort, never on glass ancestors (backdrop-root trap) [EMIL-PERF] [MDN-BACKDROP].
- Firefox Android re-renders the page on scroll while a backdrop-filter is present (bug 1732817) — test sticky chrome there [BZ-1732817].
- Design for 8ms frames at 120Hz on ProMotion iPhones [EMIL-PERF].

## 13. Lint / review checklist

- No `.lr-glass*` inside another `.lr-glass*` (except knobs while pressed).
- No `opacity`/`filter`/`mask`/`will-change` on any ancestor of glass.
- Clear glass only inside the scanner, always over `dim-clear`.
- Text on glass checked at 4.5:1 over the worst-case backdrop at glass amount 0.
- Every new animation has a `prefers-reduced-motion` branch.
