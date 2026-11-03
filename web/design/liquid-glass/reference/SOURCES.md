# Sources

Fetched 2026-11-02. Apple developer pages are JavaScript-rendered; they were read through their JSON form `https://developer.apple.com/tutorials/data/<path>.json` — the human URL is listed. Trust order: Apple developer docs / HIG / WWDC sessions → apple.com and support.apple.com → reputable press (corroboration only) → web-implementation write-ups (techniques, never claims about Apple).

## Apple — developer documentation

| Tag | URL |
|---|---|
| TO-LG | https://developer.apple.com/documentation/technologyoverviews/liquid-glass |
| TO-ADOPT | https://developer.apple.com/documentation/technologyoverviews/adopting-liquid-glass |
| SUI-CUSTOM | https://developer.apple.com/documentation/swiftui/applying-liquid-glass-to-custom-views |
| SUI-GLASS | https://developer.apple.com/documentation/swiftui/glass |
| SUI-GLASSEFFECT | https://developer.apple.com/documentation/swiftui/view/glasseffect(_:in:) |
| SUI-CONTAINER | https://developer.apple.com/documentation/swiftui/glasseffectcontainer |
| SUI-TRANSITION | https://developer.apple.com/documentation/swiftui/glasseffecttransition |
| SUI-SCROLLEDGE | https://developer.apple.com/documentation/swiftui/scrolledgeeffectstyle |
| SUI-TABMIN | https://developer.apple.com/documentation/swiftui/tabbarminimizebehavior |
| SUI-SPRING | https://developer.apple.com/documentation/swiftui/animation/spring(duration:bounce:blendduration:) |
| SUI-SPRING-LEGACY | https://developer.apple.com/documentation/swiftui/animation/spring(response:dampingfraction:blendduration:) |
| SUI-INTERACTIVE-SPRING | https://developer.apple.com/documentation/swiftui/animation/interactivespring(response:dampingfraction:blendduration:) |
| SUI-SMOOTH | https://developer.apple.com/documentation/swiftui/animation/smooth(duration:extrabounce:) |
| SUI-SNAPPY | https://developer.apple.com/documentation/swiftui/animation/snappy(duration:extrabounce:) |
| SUI-BOUNCY | https://developer.apple.com/documentation/swiftui/animation/bouncy(duration:extrabounce:) |
| UIK-BTN | https://developer.apple.com/documentation/uikit/uibutton/configuration-swift.struct/glass() |
| UPD-SUI | https://developer.apple.com/documentation/updates/swiftui |
| PLIST-COMPAT | https://developer.apple.com/documentation/bundleresources/information-property-list/uidesignrequirescompatibility |
| DEV-NEWS-SDK | https://developer.apple.com/news/?id=k1mtkt1k |
| FORUM-DTS | https://developer.apple.com/forums/thread/832543 (Apple DTS engineer) |

More API pages (UIKit, AppKit, ConcentricRectangle, ToolbarSpacer, background extension…) are listed in `../research/01-apple-apis.md` → Sources.

## Apple — Human Interface Guidelines

Base: `https://developer.apple.com/design/human-interface-guidelines/<page>`

| Tag | Page |
|---|---|
| HIG-MAT | materials |
| HIG-COLOR | color |
| HIG-LAYOUT | layout |
| HIG-MOTION | motion |
| HIG-BRAND | branding |
| HIG-ICON | app-icons |
| HIG-BTN | buttons |
| HIG-TAB | tab-bars |
| HIG-TB | toolbars (navigation bars were merged into this page) |
| HIG-SHEET | sheets |
| HIG-MENU | menus |
| HIG-CTX | context-menus |
| HIG-ALERT | alerts |
| HIG-SEARCH | search-fields |
| HIG-SEG | segmented-controls |
| HIG-TOGGLE | toggles |
| HIG-SLIDER | sliders |
| HIG-LIST | lists-and-tables |
| HIG-SCROLL | scroll-views |
| HIG-NEW | https://developer.apple.com/design/whats-new/ (dated entries end 2025-12-16) |
| RES | https://developer.apple.com/design/resources/ (iOS 27 / macOS 27 UI Kits for Figma and Sketch, Icon Composer, SF Symbols 27) |

## Apple — WWDC sessions

| Tag | URL |
|---|---|
| V-219 | https://developer.apple.com/videos/play/wwdc2025/219/ — Meet Liquid Glass |
| V-356 | https://developer.apple.com/videos/play/wwdc2025/356/ — Get to know the new design system |
| V-323 | https://developer.apple.com/videos/play/wwdc2025/323/ — Build a SwiftUI app with the new design |
| V-284 | https://developer.apple.com/videos/play/wwdc2025/284/ — Build a UIKit app with the new design |
| V-2026-102 | https://developer.apple.com/videos/play/wwdc2026/102/ — Platforms State of the Union 2026 |
| V-2026-251 | https://developer.apple.com/videos/play/wwdc2026/251/ — Communicate your brand identity on iOS |
| V-2026-269 | https://developer.apple.com/videos/play/wwdc2026/269/?time=60 — What's new in SwiftUI |
| V-2026-278 | https://developer.apple.com/videos/play/wwdc2026/278/ — Modernize your UIKit app |
| WWDC23-SPRINGS | https://developer.apple.com/videos/play/wwdc2023/10158/ — Animate with springs |

## Apple — product, newsroom, support

| Tag | URL |
|---|---|
| NEWS-WWDC26 | https://www.apple.com/newsroom/2026/06/apple-unveils-next-generation-of-apple-intelligence-siri-ai-and-more/ |
| OS-IOS | https://www.apple.com/os/ios/ |
| SUPPORT-26 | https://support.apple.com/en-us/123075 (iOS 26 release notes) |
| SUPPORT-27 | https://support.apple.com/en-us/149076 (iOS 27 release notes) |

## Press (corroboration only)

| Tag | URL |
|---|---|
| MR-261 | https://www.macrumors.com/2025/11/03/apple-releases-ios-26-1/ |
| MR-262 | https://www.macrumors.com/2025/11/04/ios-26-2-liquid-glass-slider/ |
| 9TO5-262 | https://9to5mac.com/2025/12/15/ios-26-2-expands-liquid-glass-in-three-ways-heres-whats-new/ |
| 9TO5-264 | https://9to5mac.com/2026/03/23/ios-26-4-offers-two-liquid-glass-customization-updates/ |
| MR-27-LG | https://www.macrumors.com/2026/06/10/how-liquid-glass-is-changing-in-ios-27/ |
| MR-27-RELEASE | https://www.macrumors.com/2026/09/14/apple-releases-ios-27/ |
| SIXCOLORS-27 | https://sixcolors.com/post/2026/09/ios-27-review-little-things-mean-a-lot/ |
| MR-272 | https://www.macrumors.com/2026/10/26/ios-27-2-features/ |

## Web platform

| Tag | URL |
|---|---|
| MDN-BACKDROP | https://developer.mozilla.org/en-US/docs/Web/CSS/backdrop-filter |
| BCD-MEDIA | https://raw.githubusercontent.com/mdn/browser-compat-data/main/css/at-rules/media.json |
| BCD-CORNER | https://raw.githubusercontent.com/mdn/browser-compat-data/main/css/properties/corner-shape.json |
| BCD-* | other browser-compat-data files listed in `../research/04-web-implementation.md` |
| CANIUSE-BACKDROP | https://caniuse.com/css-backdrop-filter |
| WEBKIT-245510 | https://bugs.webkit.org/show_bug.cgi?id=245510 (backdrop-filter with SVG url — open) |
| WEBKIT-317712 | https://bugs.webkit.org/show_bug.cgi?id=317712 (view-transition snapshot of transformed elements) |
| BZ-1732817 | https://bugzilla.mozilla.org/show_bug.cgi?id=1732817 (Firefox scroll cost with backdrop-filter) |
| CSSWG-9139 | https://github.com/w3c/csswg-drafts/issues/9139 |

## Implementation write-ups and libraries (techniques)

| Tag | URL |
|---|---|
| KUBE | https://kube.io/blog/liquid-glass-css-svg |
| COMEAU | https://www.joshwcomeau.com/css/backdrop-filter/ |
| AAVE | https://aave.com/design/building-glass-for-the-web |
| BUILDMVP | https://www.buildmvpfast.com/blog/liquid-glass-css-backdrop-filter-recipes-2026 |
| REACT-DOCTOR | https://www.react.doctor/docs/rules/react-doctor/no-large-animated-blur |
| LGR | https://github.com/rdev/liquid-glass-react (MIT) |
| LIQUIDGL | https://github.com/naughtyduk/liquidGL (MIT) |

## Emil Kowalski's skills (MIT, © 2026 Emil Kowalski)

| Tag | URL |
|---|---|
| EMIL-REPO | https://github.com/emilkowalski/skills |
| EMIL-APPLE | https://raw.githubusercontent.com/emilkowalski/skills/main/skills/apple-design/SKILL.md |
| EMIL-DESIGN-ENG | https://raw.githubusercontent.com/emilkowalski/skills/main/skills/emil-design-eng/SKILL.md |
| EMIL-PERF | https://raw.githubusercontent.com/emilkowalski/skills/main/skills/animation-performance/SKILL.md |

## Could not be read

HIG "What's new" JSON after 2025-12-16; WWDC26 sessions 292 (search) and the bare 269 URL; the HIG gray color values (images only) and SF tracking table. Apple publishes no blur, opacity, radius, bar-height or animation numbers — take any new metric from the iOS 27 UI Kit, never invent it.
