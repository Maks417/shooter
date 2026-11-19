# VD0 visual art brief — Battlefield V / Bodycam / Warface

Direction sections T080–T084 below are newest first; the original VD0 brief starts at the **Status** paragraph. Later direction (ADR-084–095) is recorded in the visual roadmap, not here.

## T084 — Near-field finish and composition

Replace the flat weapon/glove/sleeve surfaces with original numeric coated-metal, stippled-polymer, woven-cloth and glove-grain PBR triplets at a 100 mm tile scale. Keep the coated metal restrained, cloth/gloves rough, and polymer distinct from both. Sleeve taper gains shallow folds and a slight bend. Hip-held equipment uses a modest cant toward the center of the view, fading fully out for accurate ADS. No new camera shake, weapon mechanics or third-person anatomy is introduced. [ADR-053](decisions.md#adr-053--accepted-procedural-near-field-finish-triplets-and-hip-composition-t084), [evidence](milestones/m11-playable-release/handoff.md). Named visual acceptance and reference-machine cost remain separate Pending checks.

## T083 — First-person presence

Implement original gloved trigger/support hands and long uniform sleeves as offline numeric meshes for both weapons. Palms/fingers follow existing grip coordinates; cuffs and knuckle pads separate fabric, glove and protective material. Support-hand reach follows the authority reload phase and returns continuously to the grip, with reduced-motion scaling. This closes the floating-weapon presentation gap in software; it does not solve third-person soldier anatomy or provide articulated fingers/full magazine exchange. [ADR-052](decisions.md#adr-052--accepted-packaged-first-person-glove-and-sleeve-props-t083), [delivery evidence](milestones/m11-playable-release/handoff.md).

## T081 — Place and construction language

Owner-requested continuation from `615b6a5`: make architecture read as constructed surfaces instead of decorated collision boxes. Compound uses pale concrete/plaster walls, dark footing bands, narrow 3.2 m panel joints and lighter coping/end protection. Depot contrasts those surfaces with muted blue/oxide cargo, steel corrugations, perimeter rails and door locking hardware. Human-scale loading-bay facades replace blank menu building faces. Weapon metal uses a dark, rougher coated finish; polymer remains matte. These are implemented numeric visual recipes, not imported art.

New geometry is baked into the verified map mesh; six shared material roles reuse existing PBR planes. The collision/nav/spawn identities remain unchanged. Do not infer reference-machine cost from the 180k vertex cap or six submeshes. See [ADR-050](decisions.md#adr-050--accepted-packaged-architectural-surface-construction-t081) and [M11 handoff](milestones/m11-playable-release/handoff.md). Soldier anatomy, first-person hands and full weapon-action continuity remain unresolved. A credible environment improvement is one part of the requested BFV quality target; no visual comparison or P05 acceptance follows from this source change.

## T080 review — quality priorities (2026-11-15)

The source baseline is T079 / installer 0.3.8 at `07c5db7`; older top-level delivery notes below are historical. This review found four concrete source defects: inconsistent physical material scale on scaled boxes; cover textures eight times denser than architectural solids; overview cameras still sized for old 256 m maps; and card renderers losing metal/trim submesh materials. T080 corrects those presentation paths; delivery and verification records actual results.

Design direction: make Compound read as pale weathered plaster/concrete with dark structural metal and earth; make Depot read as steel logistics architecture on asphalt. Hold surface texture scale at one repeat per two metres in the world and menu. Operation cards must fill their frame with the actual arena and retain material separation; ornamental plates must remain subordinate. Keep architectural mass and combat silhouettes readable before adding surface noise or more props.

The largest remaining visual deficits are soldier anatomy/uniform construction, hands and weapon-action continuity, repetitive rectangular architecture, and insufficient environmental storytelling. A material/framing fix cannot establish BFV parity. The representative-slice review must include near-field soldier/weapon footage, a doorway-to-yard transition, both sun and shade, and a normal combat view at native 720p Low. Evaluate proportion, material scale, action continuity and silhouette first, then compare the named reference frames. P05 work remains subject to its existing explicit bounded assignment; no clearance search or acceptance waiver is created here.

Status: **draft brief for owner approval**, 2026-11-14. Companion to [design milestones](design-milestones.md). This page records the working art direction, comparison register, current-build gaps and component ownership for parallel presentation work. It does **not** close VD0 (owner still selects exact Bodycam/Warface frames and approves the brief), does **not** accept any VD1–VD9 exit, and does **not** authorize a P05 clearance search (ADR-024).

Baseline software: the latest installer in [next playable build](next-playable-build.md); the visual roadmap (ADR-084–095) is the current execution plan built on this brief. No build claims reference parity.

## Quality emphases (project choices)

| Reference | Emphasize | Reject |
|---|---|---|
| Battlefield V | Scene-led menus; depth and composition; world-first readable HUD; coherent place + character presentation | Flat contour-only shell; HUD that dominates the frame; menu that feels like a debug panel |
| Bodycam | Physical material response; natural exposure; environmental depth; grounded weapon/camera presence | Obligatory heavy shake; unreadable darkness; distortion that breaks aim truth |
| Warface | Clear soldier/equipment silhouettes; readable combat feedback; polished weapon handling | Mannequin anatomy; floating kit; invisible reload/magazine discontinuities |

Secondary mood context only: Battlefield 6, Call of Duty. References inform original fictional factions, kit and places. Screenshots and footage are review material only — never shipping textures, models, animation or menu backgrounds.

## Binding constraints (unchanged)

- Procedural-only geometry/materials/rigs/motion; engine font allowlist exception; no silent external art fallback.
- Unity 6000.3.28f1 / URP 17.3.0; native720p Low30 on Ryzen 5 4600H iGPU; 60 FPS stretch separate.
- Quality tiers must not change collision, navigation, visibility rules, hit volumes or weapon mechanics.
- Reduced-motion / reduced-flash preferences remain authoritative for camera and FX cosmetics.
- P05 FAIL retained; further soldier-generator clearance needs an explicit owner assignment.

## Visual language

**World:** modern military training / logistics sites. Compound = concrete/plaster training structures with indoor/outdoor transitions. Depot = warehouses, steel, cargo, loading infrastructure. Surfaces should read at physical scale (wear at edges/contact, not white-noise splat). Daylight with readable silhouettes in sun and shade.

**Characters / kit:** clear team identity via silhouette + labels as well as cyan/coral accents. Equipment must sit on the body; hands stay connected to weapons through fire/reload/switch.

**First person:** tactile rifle/pistol, visible magazine exchange, analytic sights (no recursive scope RTT in baseline), near-wall composition that does not hide targets.

**Interface:** charcoal/graphite `#161C20`, ivory `#F1F3EF`, muted `#ADB7B7`, sand-amber focus `#E6C56A`, ally `#7BC5D1`, danger `#E47B70`. Large headings, compact left navigation, rendered operation cards, opaque HUD plates, circular heading-up friendly minimap ringed by the compass plus temporary gunfire reveal, bounded elimination feed. See [brandbook](brandbook.md).

**Motion / FX:** weighty locomotion; shot/impact/smoke as bounded analytic meshes; synchronized synthetic audio. Cosmetics never re-apply authoritative recoil.

## Comparison register

Exact high-resolution Bodycam/Warface frame IDs and timecodes remain **Pending owner supply**. Until then, review uses:

| ID | Source | Role | Status |
|---|---|---|---|
| BFV-MENU-01..06 | Owner-supplied Battlefield V screenshots (chat, 2026-11-14) | Menu composition, left rail, operation cards, depth | Direction set; higher-res copies preferred for fine surface review |
| BFV-HUD-* | Owner gameplay screenshots from same set where present | World-first HUD hierarchy | Partial; confirm FOV/capture unknowns |
| BODYCAM-* | [Steam media](https://store.steampowered.com/app/2406770/Bodycam/) | Material response, exposure, weapon presence | Pending exact frame/timecode selection |
| WARFACE-* | [Steam announcements/media](https://steamcommunity.com/app/291480/announcements/) | Silhouette, weapon handling, combat feedback | Pending exact frame/timecode selection |

Preserve originals under ignored local reference storage; never commit third-party screenshot binaries into `Assets/`. Evidence captures for this project go under `Artifacts/visual-design/<source>/<VD-id>/<run>/`.

## Current-build gap inventory (software → target)

Anchored scores use the VD rubric: 0 Missing / 1 Blockout / 2 Functional / 3 Polished below target / 4 Reference-comparable. Scores below are **integrator self-assessment for planning**, not owner acceptance.

| Category | Approx now | Gap vs target | Primary follow-on |
|---|---|---|---|
| Menu / UI craft | 2–3 (T074 scene-led baseline; interactive review Pending) | Staging/provisional soldiers; density and lighting below BFV; PROD-D03 PendingReview | VD7 craft; VD0 owner review |
| Soldiers / equipment | 1–2 (P05 FAIL) | Anatomy/clearance/eye-line residuals; live team readability PendingCapture | VD2 only with explicit P05 scope |
| Hands / weapons / actions | 2 | Flat first-person presence; magazine/hand polish incomplete; FP bind improved in T074 | VD3–VD4 |
| Environment / material / light | 1–2 | Product map view historically tinted solids; sky/fog provisional; maps still planner-box heavy | VD5–VD6 presentation then grammar |
| Motion / feedback | 2 | Analytic FX pool exists; product path largely IMGUI flash; motion polish open | VD4 + VD6 FX wiring |
| Whole-scene consistency | 2 | Menu and world do not yet share accepted production art | VD1 slice then VD8–VD9 |

Named defects continue in [visual-defect-inventory.md](visual-defect-inventory.md) (P05-D01–D03, PROD-D01–D03, plus VD presentation gaps).

## Component ownership for presentation waves

| Slice | Paths | Notes |
|---|---|---|
| Atmosphere / map surfaces | `Presentation/Rendering/` (`ProductMapView`, `GeneratedSky`, `GeneratedReflectionProbe`) | Prefer uploading packaged material planes already in map packages; no generator identity change |
| Combat FX | `Presentation/Fx/`, minimal `ProductSessionView` hooks | Mesh proxies from `AnalyticFxPool`; honor reduced-flash |
| Menu / HUD craft | `ProductMenuScene`, `TacticalUi`, `ProductShellHost` | Extend T074; no fake online lobby |
| Soldiers | `Generation/Core/Soldiers/`, `Presentation/Rig/` | **Blocked** on explicit P05 assignment |
| Weapons / FP | `Generation/Core/Weapons/`, `Presentation/View/` | Socket/hit semantics frozen without ADR |
| Map grammar | `Generation/Core/Environment/` | Cosmetic-only detail must preserve fair visibility; collision/nav changes need T067/fixtures |

Integrator owns Contracts, Bootstrap, locks, schemas, coordination and docs manifest.

## Exit criteria for VD0

1. Owner approves this brief (or a revised version) as the coherent direction.
2. Owner attaches or names exact Bodycam/Warface comparison frames/timecodes alongside the BFV set.
3. Technical reviewer confirms budgets/constraints above and the gap inventory.
4. Any P05-dependent VD1/VD2 scope is written as an explicit assignment before generator work.

Until those land, parallel presentation polish may proceed as **software toward the brief**, with all VD exits remaining Pending.
