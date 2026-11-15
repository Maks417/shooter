# Feature: procedural geometry and modern weapons

Owner WP-02; implemented initial M1 source in `Generation/Core/Geometry`, `Generation/Core/Weapons`. Consumes [contracts](../contracts.md) and outputs to [compiler](../generation.md). Combat mechanics are owned by [combat](combat.md); geometry cannot alter damage.

## Construction

Core library: boxes with parameterized bevels, cylinders/cones, revolved profiles, extrusions, swept tubes, planar triangulation, rounded grip solids, boolean-like cutouts from constructed surfaces, mesh joining and analytic normals. Use real-world meter dimensions. Construct mechanical parts as closed sections with explicit joins; general arbitrary CSG is a later spike. Deterministic build order and stable part IDs preserve attachment sockets and material tags.

First unbranded rifle: receiver, barrel, handguard, stock, pistol grip, magazine, sights, trigger guard, bolt and charging-handle proxies. Pistol: frame, slide, barrel, trigger guard, magazine and sights. Recipes specify dimensions, transforms, material family and feature parameters, not manually supplied vertex lists. Numbers can follow measured mechanical proportions without using proprietary logos or copied mesh data.

Sockets: muzzle (+Z firing axis), chamber/ejection, magazine grip, left/right hand grip, shoulder, sight eye line and attachment rails. Each has stable semantic ID and local rigid transform; scales cannot be nonuniform. Camera/viewmodel transform is separate from world muzzle. Recoil moves the view; ballistic origin and obstruction follow combat rules. Mechanical animation is scalar spring/phase-driven transforms for bolt/slide/magazine, never authored clips.

## LOD and collision

Rifle target LOD triangles 18k/8k/3k/800; pistol 8k/4k/1.5k/400 as initial ceilings, not required detail. First-person and world meshes share source parameters but have separate visibility/LOD settings. Gameplay collision uses simplified bounds; dropped weapons are optional capped physics props, not full mesh colliders. Muzzle, sight and hand sockets must match across all visual variants within 1 mm and 0.1 degrees.

Weapon art recipe links WeaponDefinition ID but cannot override fire rate or ammunition. Stable material regions and construction masks support edge wear. Barrel cavities are represented with bounded geometry depth, not expensive dark transparent overlays. Numeric recipes include overall-size validation and minimum wall/feature thickness to avoid degeneracy.

## Edge cases

Invalid length/radius or socket orientation returns a structured compile error, not a malformed mesh. Zero bevel gives a valid hard-edge part. Extreme seeds cannot detach key components or alter gameplay dimensions. Optional cosmetic wear substream does not consume mechanical-generation RNG. Mirrors require tangent/normal fixups and do not silently flip handed grip.

## Acceptance

Primitive tests cover bounds, winding, normals, UVs, watertightness for solid primitives and stable hashes. Rifle/pistol regenerate from recipes with all sockets validated, no imported source meshes. Golden silhouette views at front/side/three-quarter and first-person ADS reviewed for plausible scale. LOD transition preserves sights and main silhouette; compiler reports all counts. B02 budgets remain within cap with first-person arms/weapon and eight world weapons. Geometry quality review may reject visually implausible output even if topology tests pass.

## Implemented M1 baseline (2026-11-13)

`Generation/Core/Geometry/PrimitiveGeometry.cs` provides closed hard/beveled boxes and +Z cylinders/cones with metric UVs; `MeshAssembly` joins rigid components into stable material submeshes and `MeshValidation` checks frames/winding and seam-welded solid edges. This is component construction, not general boolean union or a full extrusion/sweep library. `Generation/Core/Weapons` builds four rifle/pistol LODs, bounded blind bores, independent collision proxies and immutable semantic sockets from the [numeric baseline](../weapon-recipe-baseline.md). All mechanical transforms and sockets are seed/profile/LOD invariant. Generator geometry.weapon1.0.1 fixed the initially disconnected magazine/trigger proxies under [ADR-020](../decisions.md#adr-020--accepted-versioned-first-visual-corrections-for-weapon-and-metal-generators); old diagnostic versions remain retained and explicitly reject regeneration.

## T088 / geometry.weapon 1.0.2

[ADR-061](../decisions.md#adr-061--accepted-geometryweapon-102-mechanical-parts-and-denser-fire-fx-t088) versions the generator to **1.0.2**: richer body LOD silhouette detail, separate packaged **magazine** and **cycle** (bolt/slide) meshes, same sockets and collision mechanical dimensions. Presentation drives mag remove/seat from reload phase and cycle open/close from `ShotAccepted`. Analytic FX add shell casings and tracers with denser muzzle. Named visual acceptance remains Pending. [Weapon schema](../feature-schemas/weapon.schema.json), migration fixtures and root `feature-content-lock.json` record the registry.

## T075 first-person presentation

`ProductActorPresentation` binds packaged LOD0 rifle/pistol/knife/grenade meshes for the local near-field view independently of world LOD. Analytic `FirstPersonHandling` reads authority displacement/action timing for hip gait, equip/reload transforms and life resets; reduced motion suppresses cosmetic gait/rotation. Knife/grenade omit bolt/slide/magazine cycle parts and use `FirstPersonSpecialActions` slash/stab and cook/throw offsets instead of gun recoil. Weapon switches are two-phase (holster visible mesh → swap at tuck → draw) with an outward hip arc; ADS presentation blend gates on equip so a tucked gun cannot ride the sight ray. Rifle ADS uses a screen-space PiP scope (hip camera FOV, digital lens zoom, blurred periphery, housing + mil-dot; viewmodel hidden at full optic — [ADR-065](../decisions.md#adr-065--accepted-screen-space-pip-rifle-scope-blurred-periphery)); pistol ADS keeps irons with a soft vignette and real FOV blend. Exact stable recoil decay and sight-socket ADS alignment are covered by pure/Unity tests. Camera-local placement changes no ballistic origin, hit pose, generator or socket. T088 adds detachable magazine/cycle parts; articulated fingers and full VD3 named review remain open. [T075 evidence and limits](../milestones/m11-playable-release/handoff.md#t075--procedural-presentation-preview-034).

## Arsenal visual profiles — geometry.weapon 1.0.3

Owner assignment 2026-11-15, [ADR-068](../decisions.md#adr-068--accepted-procedural-arsenal-visual-profiles-and-finishes). Rifle/pistol body and moving parts use deterministic numeric profiles, a slotted handguard cage, rail teeth, fasteners, recessed regions, a compact optic housing, barrel collar and faceted slide. Knife has a ground blade with a tapered point and conforming grip bands; grenade is an M67-style upright egg with a tall fuse well, thick safety spoon, pull ring, fill plug and pressed seam rings (not a featureless ball). All four slots preserve authority behavior. Sockets and collision dimensions remain fixed; rifle/pistol assembled near-field triangle totals include magazine/cycle and fit the existing LOD0 ceilings.

Five semantic material slots distinguish coated metal, polymer, machined steel, recess/rubber and olive enamel. `FirstPersonSurfaceBaker` publishes seven 128² triplets (the five equipment finishes plus sleeve/glove), all with full mip chains and spatial roughness/normal data, through the verified map closure. The three additional triplets add 786,420 decoded RGBA bytes including mips per visual package before compression; textures are shared across weapon instances. Product upload retains sparse semantic slot indices and caches exactly sized renderer palettes so Unity does not repeat the last submesh for surplus materials. Special slots hide absent magazine/cycle meshes. The cosmetic holster tuck moves 10 mm farther down before the slot swap, including reduced-motion playback.

`ShooterFeatureBuild.CaptureArsenal` renders eight source diagnostics (four weapons, side/three-quarter) using URP Lit and the same finish adapter. These are offline source renders, not packaged-player/first-person acceptance. Gallery and soldier diagnostic consumers accept the versioned rifle and show detached parts. Old 1.0.2 fixtures remain immutable and reject regeneration; 1.0.3 migration and schema fixtures are retained. Named gameplay/visual comparison, P05 and reference-machine performance acceptance remain open.

## Articulated handling and reflection pass — ADR-069

[ADR-069](../decisions.md#adr-069--accepted-articulated-near-field-hands-and-global-analytic-reflections) adds `fp.hand.v2`: 17 camera-only joints per hand, three finger segments plus thumb articulation, a wrist-driven sleeve with an anchored elbow, and independent rifle/pistol/knife/grenade grips. Eight skinned meshes and matching skeletons are published through `MapVisualClosure`; runtime validates required skin indices and binds verified payloads. Existing SGAF skin/skeleton encodings are reused. Soldier skeleton/P05 and authority hit data are unaffected.

Reload presentation reaches the magazine before removal, keeps support-hand travel tied to the detached magazine, seats it before returning and retains exact rest endpoints. The weapon/right-hand assembly has its own smaller reload cant rather than inheriting magazine translation. Fingers open during reach/release, close around the magazine during travel and respond to the accepted-shot cycle at the trigger. Reduced motion keeps bounded magazine travel and exact contact offsets. Skinning replaces the previous two rigid glove renderers with two Bone2 renderers; no new per-frame meshes or textures.

`geometry.weapon` **1.0.4** refines selected large-part bevels and near barrel roundness while retaining LOD ceilings, sockets and collision dimensions. Old 1.0.3 fixtures remain immutable. `GeneratedSky` supplies a 64² six-face RGBAHalf global analytic reflection with seven mips (about 256 KiB), covering arena positions outside the local probe and restoring previous ownership on disposal. Approximate mip filtering does not prove physical reflection accuracy or reference GPU performance. Named in-game visual acceptance remains Pending.

`ShooterFeatureBuild.CaptureHandling` renders ready/reach/extract/seat source diagnostics using verified map visual payloads and copied authority fixtures. The fixture adapter is Editor-only and cannot ship. Diagnostics do not substitute for actual gameplay or the comparative visual gate.
