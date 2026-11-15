# VD0 visual art brief — Battlefield V / Bodycam / Warface

**T075 implementation update — 2026-11-15:** source `7407bc4` / installer 0.3.4 extends the earlier T074 baseline with real procedural surface triplets, scene material reuse, working map overviews and near-field weapon handling. Player captures still show blockout environments/soldiers. All VD exits, exact comparison approval and P05 disposition remain unchanged. [Implementation/evidence](milestones/m11-playable-release/handoff.md#t075--procedural-presentation-preview-034).

Status: **draft brief for owner approval**, 2026-11-14. Companion to [design milestones](design-milestones.md). This page records the working art direction, comparison register, current-build gaps and component ownership for parallel presentation work. It does **not** close VD0 (owner still selects exact Bodycam/Warface frames and approves the brief), does **not** accept any VD1–VD9 exit, and does **not** authorize a P05 clearance search (ADR-024).

Baseline software under review: T074 preview source `b531da9` / installer 0.3.1; later HEAD may add presentation polish without claiming reference parity.

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

**Interface:** charcoal/graphite `#161C20`, ivory `#F1F3EF`, muted `#ADB7B7`, sand-amber focus `#E6C56A`, ally `#7BC5D1`, danger `#E47B70`. Large headings, compact left navigation, rendered operation cards, opaque HUD plates, friendly-only minimap, bounded elimination feed. See [brandbook](brandbook.md).

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
