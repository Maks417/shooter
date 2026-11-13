# Visual direction and UI design system

Proposed design, not extracted from existing code. See [vision](vision.md) and [rendering](features/materials-rendering.md).

The new [game shell](features/game-shell.md) specifies actual menu/setup/loading/pause/settings/scoreboard/results behavior and working persistence/device controls for M10. Existing HudLayout is a pure helper; its test is not interactive UI evidence. Keep the tokens below as proposed until validated in the real 720p player, including the 12-row tables and 150% scale. No image preview requirement is introduced for map selection.

Grounded, functional, readable modern military training environment. Favor believable proportions, physical material scale, restrained wear and clear player silhouettes over noisy detail. Fictional insignia generated from lines/polygons, no imported logos. Realism review uses silhouette, material response, spatial scale and motion separately; a complex shader cannot compensate for implausible proportions.

| UI token | Proposed value | Use |
|---|---|---|
| surface | #161C20 | Menus and diagnostics |
| text | #F1F3EF | Primary text |
| muted | #ADB7B7 | Secondary text |
| focus | #E6C56A | Keyboard/controller focus |
| danger | #E47B70 | Damage/state, always paired with shape/text |
| ally | #7BC5D1 | Friendly marker with icon/label |

Implement UI with layout rectangles, lines and generated vector/mesh reticles; no image sprites. Engine-provided font is an explicit provenance exception; record font/package license. At 720p output, body 18 px, key ammo 28 px, minimum interactive target 32×32 px, 4/8/12/16/24 spacing. UI scale 100–150%, safe area 5%; ensure important HUD does not collide at maximum scale. Minimum text/background contrast 4.5:1; validate actual colors rather than assuming table passes all combinations.

Components: reticle with optional center dot and spread ring, ammo/action indicator, health, objective text, pause/settings, quality selector and benchmark report. Reload feedback includes remaining action time; network reconnect/loading has plain text and no hidden technical stack traces. Performance overlay is opt-in developer mode, shows frame time rather than just FPS.

Accessibility: independent mouse sensitivity, field-of-view range 70–100 vertical degrees with explicit label, reduced camera motion/flash, remappable controls, subtitles for objective cues, color-independent team indicators and readable fonts. Choose and test FOV conversion once; ADS scopes use analytic sights initially, no recursive render-to-texture optics. All settings persist versioned user configuration outside generated content.

Acceptance: 720p screenshots of HUD/menu/loading and maximum scale readable with no clipping; color contrast and keyboard navigation verified; reticle ADS and world collision visibility checks pass; UI uses no prohibited visual assets. Visual snapshots are reviewed with a named baseline build and seed, not copied external imagery as source assets.
