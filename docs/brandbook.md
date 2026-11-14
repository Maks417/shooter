# Visual direction and UI design system

T074 stage 1 implements the shell/HUD direction below in `Presentation/Menus/TacticalUi.cs`, `Presentation/Session/ProductShellHost.cs` and `ProductSessionView.cs`. The environment, equipment and motion direction remains staged work. See [vision](vision.md), [shell](features/game-shell.md) and [rendering](features/materials-rendering.md). Implementation is separate from named visual acceptance.

## Owner direction — 2026-11-14

The owner requested the complete visual design and selected implementation in Unity, in stages beginning with menus and HUD. Reference games: Battlefield, Call of Duty, Warface and Bodycam. These are mood and hierarchy references, never source assets or a promise of matching their rendering cost or fidelity. Consulted publisher/developer pages: [Battlefield media](https://www.ea.com/games/battlefield/battlefield-6/features/bf6-media-hub), [Call of Duty](https://www.callofduty.com/en/blackops7/multiplayer), [Warface](https://steamcommunity.com/app/291480/announcements/) and [Bodycam](https://store.steampowered.com/app/2406770/Bodycam/).

The original direction combines military restraint, large clear headings, tactile equipment and natural light. Use charcoal/graphite surfaces, ivory text, sand amber selection and labeled cyan/coral team identity. Bodycam is a reference for grounded light and physical presence; aggressive distortion, obligatory shake and unreadable darkness are not requirements. Keep accessibility preferences and the 720p Low target.

| Stage | Scope | Disposition |
|---|---|---|
| 1 — shell and HUD / T074 | Main, local setup, loading, pause, confirms, grouped settings, live/final team tables; score/clock, health/ammunition, reload, death/respawn and analytic reticle | Implemented software; actual test/build/player evidence in the M10/M11 handoffs |
| 2 — environments | Distinct compound concrete/plaster and depot steel/loading-yard palettes; believable dimensions, procedural surface breakup, directional light/sky, landmarks and readable cover | Pending implementation; regenerate verified packages and preserve gameplay equivalence |
| 3 — operators | Distinct fictional team uniform/gear silhouettes, anatomy, skin response, equipment attachment and LOD review | Pending; ADR-024/P05 FAIL retained. Obtain explicit bounded soldier-generator/clearance scope before further search |
| 4 — weapons and hands | Original rifle/pistol detailing, visible first-person arms/gloves/magazine, firing, empty/tactical reload, holster/equip, ADS/analytic optic and recoil | Pending; visual stages consume existing authority events/timing. New mechanical behavior needs its own contract/assignment |
| 5 — procedural motion and effects | Gait/stance/slide, hand IK, magazine exchange, firing recovery, death collapse, analytic muzzle/impact effects and camera response | Pending; solve from equations/dynamics rather than authored clips, then review live/player/performance evidence |

No store, progression, matchmaking or online lobby is introduced. The lobby is local match setup with one human plus bots. The full visual overhaul is not complete when stage 1 ships.

## Implemented shell and HUD

`TacticalUi` draws solid rectangles, lines and an analytic decorative contour field using the existing engine white texture/font exception; it imports no image assets and creates no surface-texture caches. Its coordinate space applies the persisted UI scale once; layouts use the actual logical display bounds and a 5% safe inset. The contour field is decoration, not a map preview. Main/setup/loading are full-screen; pause and confirmations cover the paused world with a near-opaque dark scrim. Loading activity is indeterminate, never a guessed completion percentage.

Buttons use 18 logical-pixel text and visible focus/selection bars. Tab/Shift-Tab and up/down traverse enabled controls; Enter/Space select and left/right adjust focused sliders. Escape retains its reserved recovery role. Settings groups are Audio, Video, Controls and Accessibility, with a scrolling content region and fixed Apply/Cancel/Defaults/Back actions; existing schema-2 storage, conflicts and 15-second Keep/Revert behavior remain authoritative. Binding capture ignores its initiating frame.

Team tables place six rows per side with aligned kills/deaths columns and a highlighted YOU row. The combat HUD uses opaque compact backing plates, a centered team score and minutes/seconds clock, lower-corner health/ammo, event/tick-driven reload progress, empty-ammo guidance, and a death/authority-respawn notice. ADS hides the analytic hip reticle so the weapon sight remains visible. HUD/preferences remain read-only consumers of gameplay.

## Shared visual principles

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
