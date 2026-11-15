# Visual direction and UI design system

## T080 review — quality priorities (2026-11-15)

The source baseline is T079 / installer 0.3.8 at `07c5db7`; older top-level delivery notes below are historical. This review found four concrete source defects: inconsistent physical material scale on scaled boxes; cover textures eight times denser than architectural solids; overview cameras still sized for old 256 m maps; and card renderers losing metal/trim submesh materials. T080 corrects those presentation paths; [delivery and verification](milestones/m11-playable-release/handoff.md#t080--visual-review-and-material-scale-correction) records actual results.

Design direction: make Compound read as pale weathered plaster/concrete with dark structural metal and earth; make Depot read as steel logistics architecture on asphalt. Hold surface texture scale at one repeat per two metres in the world and menu. Operation cards must fill their frame with the actual arena and retain material separation; ornamental plates must remain subordinate. Keep architectural mass and combat silhouettes readable before adding surface noise or more props.

The largest remaining visual deficits are soldier anatomy/uniform construction, hands and weapon-action continuity, repetitive rectangular architecture, and insufficient environmental storytelling. A material/framing fix cannot establish BFV parity. The representative-slice review must include near-field soldier/weapon footage, a doorway-to-yard transition, both sun and shade, and a normal combat view at native 720p Low. Evaluate proportion, material scale, action continuity and silhouette first, then compare the named reference frames. P05 work remains subject to its existing explicit bounded assignment; no clearance search or acceptance waiver is created here.

**T075 implementation update — 2026-11-15:** source `7407bc4` / installer 0.3.4 extends the earlier T074 baseline with real procedural surface triplets, scene material reuse, working map overviews and near-field weapon handling. Player captures still show blockout environments/soldiers. All VD exits, exact comparison approval and P05 disposition remain unchanged. [Implementation/evidence](milestones/m11-playable-release/handoff.md#t075--procedural-presentation-preview-034).

T074 stage 1 implements the shell/HUD direction below in `Presentation/Session/TacticalUi.cs`, `Presentation/Session/ProductShellHost.cs` and `ProductSessionView.cs`. The environment, equipment and motion direction remains staged work. See [vision](vision.md), [shell](features/game-shell.md) and [rendering](features/materials-rendering.md). Implementation is separate from named visual acceptance.

## Owner direction — 2026-11-14

**Latest quality goal:** the finished design should be **as good as Battlefield V, Bodycam and Warface**. [Visual milestones VD0–VD9](design-milestones.md) are the canonical execution and review plan, superseding the earlier five-stage grouping. The current UI/staging implementation is a partial VD7 foundation; no reference-quality milestone is accepted. VD1 must prove a representative art slice within the existing procedural and reference-performance constraints before broad production.

The owner subsequently supplied six Battlefield screenshots and clarified that the menu and combat interface must feel like a finished military FPS. The flat contour-only menu is superseded by a scene-led layout: a large original procedural staging scene, compact left navigation and two rendered operation cards. Combat uses a compact compass/score strip, a north-up friendly-only map, squad status, small ammo/reload readouts and a bounded elimination feed. The supplied screenshots are composition references, not imported backgrounds or shipping art.

The owner requested the complete visual design and selected implementation in Unity, in stages beginning with menus and HUD. Reference games: Battlefield, Call of Duty, Warface and Bodycam. These are mood and hierarchy references, never source assets or a promise of matching their rendering cost or fidelity. Consulted publisher/developer pages: [Battlefield media](https://www.ea.com/games/battlefield/battlefield-6/features/bf6-media-hub), [Call of Duty](https://www.callofduty.com/en/blackops7/multiplayer), [Warface](https://steamcommunity.com/app/291480/announcements/) and [Bodycam](https://store.steampowered.com/app/2406770/Bodycam/).

The original direction combines military restraint, large clear headings, tactile equipment and natural light. Use charcoal/graphite surfaces, ivory text, sand amber selection and labeled cyan/coral team identity. Bodycam is a reference for grounded light and physical presence; aggressive distortion, obligatory shake and unreadable darkness are not requirements. Keep accessibility preferences and the 720p Low target.

The milestone plan covers art direction/feasibility, soldiers, hands/weapons, procedural motion, both environments, lighting/materials/effects, final UI, measured scalability and comparative installer acceptance. Its per-stage deliverables and exits live once in [design-milestones.md](design-milestones.md). The working [VD0 art brief](visual-art-brief.md) holds the comparison register, gap inventory and presentation ownership; it stays draft until the owner approves exact Bodycam/Warface frames. Earlier T074 software and retained captures remain valid evidence of implementation; P05 and final visual/device review remain open.

No store, progression, matchmaking or online lobby is introduced. The lobby is local match setup with one human plus bots. The full visual overhaul is not complete when stage 1 ships.

## Implemented shell and HUD

`TacticalUi` draws solid rectangles, lines and an analytic decorative contour field using the existing engine white texture/font exception; it imports no image assets and creates no surface-texture caches. Its coordinate space applies the persisted UI scale once; layouts use the actual logical display bounds and a 5% safe inset. The contour field is decoration, not a map preview. Main/setup/loading are full-screen; pause and confirmations cover the paused world with a near-opaque dark scrim. Loading activity is indeterminate, never a guessed completion percentage.

For the scene-led revision, `ProductMenuScene` reads both approved Low visual packages through the optional pure-byte `IProductMenuVisualSource` seam on the real Bootstrap backend. It instantiates a bounded numerical staging layout from the verified unit-box mesh and three cosmetic copies of the existing packaged soldier/rifle. All LODs already belong to each visual package; the menu uses LOD0 independently of gameplay's applied LOD. There is no soldier-generator or clearance iteration. The scene has no colliders, navigation, live participants or fake gameplay adapters. Render-only layer 12 is excluded from the gameplay camera and sun. A 1280×720 render target updates at most 30 Hz only while the front end owns it; two 384×216 cards render the actual packaged map-solid meshes once. RenderSettings changes are scoped and restored around offscreen draws, and all menu objects/resources are owned by one shell child. Reduced motion stops cosmetic dust movement; entering loading/play/pause disables the staging scene. Missing verified bytes expose a repair message, without generated-content fallback.

The tactical map projects verified collision-box footprints and current allied snapshots within 42 metres. It never plots hidden enemies, changes sensing, or makes physics queries. Recent elimination rows consume observed authority Death events with a fixed 32-event dedupe ring and four expiring display rows; they are not a match-history database. First-person meshes are now assigned on the initial packaged bind, before slot switching. The first active clock sample rebases across loading, so visual binding is not charged as combat time; actual active-frame overload still fails. IMGUI Escape and applied-input Escape share a once-per-frame guard. Text tints compensate the locked Linear-color IMGUI font path so the intended sRGB palette survives rendering.

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
