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

`Generation/Core/Geometry/PrimitiveGeometry.cs` provides closed hard/beveled boxes and +Z cylinders/cones with metric UVs; `MeshAssembly` joins rigid components into stable material submeshes and `MeshValidation` checks frames/winding and seam-welded solid edges. This is component construction, not general boolean union or a full extrusion/sweep library. `Generation/Core/Weapons` builds four rifle/pistol LODs, bounded blind bores, independent collision proxies and immutable semantic sockets from the [numeric baseline](../weapon-recipe-baseline.md). All mechanical transforms and sockets are seed/profile/LOD invariant. Generator geometry.weapon1.0.1 fixes the initially disconnected magazine/trigger proxies under [ADR-020](../decisions.md#adr-020--accepted-versioned-first-visual-corrections-for-weapon-and-metal-generators); old diagnostic versions remain retained and explicitly reject regeneration.

Rifle triangle counts are3480/2760/312/240; pistol2508/1788/240/180. These pass ceilings and codec/topology tests, not a claim of final realistic detail. [Weapon input schema](../feature-schemas/weapon.schema.json), migration fixtures and root `feature-content-lock.json` record the registry. `ShooterFeatureBuild` compiles visual and gameplay keys separately; `GeneratedGallery` loads all LODs and shows them explicitly for captures. ScreenHeightLod and GeneratedWeaponLod now select visual children from viewport height with10% hysteresis, bounded camera cuts, explicit inspection override and first-person LOD0. Projection uses the verified weapon bounds center/diameter and does not alter colliders or sockets. Unit and Unity camera-cut checks cover selection; formal transition review, dropped-weapon physics and combat/viewmodel pose integration remain open. Review evidence and remaining visual/transition gates are in the [M1 handoff](../milestones/m1-procedural-feasibility/handoff.md).
