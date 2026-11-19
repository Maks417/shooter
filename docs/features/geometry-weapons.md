# Feature: procedural geometry and modern weapons

Owner WP-02; implemented initial M1 source in `Generation/Core/Geometry`, `Generation/Core/Weapons`. Consumes [contracts](../contract-details.md) and outputs to [compiler](../generation.md). Combat mechanics are owned by [combat](combat.md); geometry cannot alter damage.

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

`ProductActorPresentation` binds packaged LOD0 rifle/pistol/knife/grenade meshes for the local near-field view independently of world LOD. Analytic `FirstPersonHandling` reads authority displacement/action timing for hip gait, equip/reload transforms and life resets; reduced motion suppresses cosmetic gait/rotation. Knife/grenade omit bolt/slide/magazine cycle parts and use `FirstPersonSpecialActions` slash/stab and cook/throw offsets instead of gun recoil. Weapon switches are two-phase (holster visible mesh → swap at tuck → draw) with an outward hip arc; ADS presentation blend gates on equip so a tucked gun cannot ride the sight ray. Rifle ADS uses a screen-space PiP scope (hip camera FOV, digital lens zoom, blurred periphery, housing + mil-dot; viewmodel hidden at full optic — [ADR-065](../decisions.md#adr-065--accepted-screen-space-pip-rifle-scope-blurred-periphery)); pistol ADS keeps irons with a soft vignette and real FOV blend. Exact stable recoil decay and sight-socket ADS alignment are covered by pure/Unity tests. Camera-local placement changes no ballistic origin, hit pose, generator or socket. T088 adds detachable magazine/cycle parts; articulated fingers and full VD3 named review remain open. [T075 evidence and limits](../milestones/m11-playable-release/handoff-archive.md#t075--procedural-presentation-preview-034).

## Arsenal visual profiles — geometry.weapon 1.0.3

Owner assignment 2026-11-15, [ADR-068](../decisions.md#adr-068--accepted-procedural-arsenal-visual-profiles-and-finishes). Rifle/pistol body and moving parts use deterministic numeric profiles, a slotted handguard cage, rail teeth, fasteners, recessed regions, a compact optic housing, barrel collar and faceted slide. Knife has a ground blade with a tapered point and conforming grip bands; grenade is an M67-style upright egg with a tall fuse well, thick safety spoon, pull ring, fill plug and pressed seam rings (not a featureless ball). All four slots preserve authority behavior. Sockets and collision dimensions remain fixed; rifle/pistol assembled near-field triangle totals include magazine/cycle and fit the existing LOD0 ceilings.

Five semantic material slots distinguish coated metal, polymer, machined steel, recess/rubber and olive enamel. `FirstPersonSurfaceBaker` publishes seven 128² triplets (the five equipment finishes plus sleeve/glove), all with full mip chains and spatial roughness/normal data, through the verified map closure. The three additional triplets add 786,420 decoded RGBA bytes including mips per visual package before compression; textures are shared across weapon instances. Product upload retains sparse semantic slot indices and caches exactly sized renderer palettes so Unity does not repeat the last submesh for surplus materials. Special slots hide absent magazine/cycle meshes. The cosmetic holster tuck moves 10 mm farther down before the slot swap, including reduced-motion playback.

`ShooterFeatureBuild.CaptureArsenal` renders eight source diagnostics (four weapons, side/three-quarter) using URP Lit and the same finish adapter. These are offline source renders, not packaged-player/first-person acceptance. Gallery and soldier diagnostic consumers accept the versioned rifle and show detached parts. Old 1.0.2 fixtures remain immutable and reject regeneration; 1.0.3 migration and schema fixtures are retained. Named gameplay/visual comparison, P05 and reference-machine performance acceptance remain open.

## Per-weapon hand and weapon choreography — ADR-099

[ADR-099](../decisions.md#adr-099--accepted-per-weapon-first-person-hand-and-weapon-choreography) replaces the shared reload arc and the fixed switch timing with numeric per-weapon key tables (`MotionCurve`) in pure `Presentation/Model/View`.

- **Reload.** The magazine-fed weapons each have their own sequence:
  - Rifle/carbine: tilt, release, strip/drop, fetch a fresh magazine from the belt, insert, slap; empty reloads add a bolt-catch press.
  - Pistol: cant, thumb drop, insert, palm seat; empty reloads add a slide rack.

  The packaged magazine falls away, and the `FP fresh magazine` child (same mesh) rides in the support hand until it seats.
- **Switching.** The draw style is per weapon and fills the incoming weapon's authoritative equip ticks after a short tuck.
- **Knife.** Swings last the knife's fire interval and alternate direction.
- **Grenade.**
  - Cook: a pin pull with the support hand, then a cocked raise with a fuse tremble.
  - Throw: survives the authority's same-tick switch back to the rifle and keeps the hands through the follow-through.
- **Movement and idle.** Sprint carry, jump lift/landing dip, crouch tuck, slide roll, look inertia, idle breathing and an eased crouch eye height are added from new `LocalMovementView` motor fields.
- **Constraints.** Every layer is cosmetic, ADS-safe and scaled by reduced motion. Hand rig `fp.hand.v2` and the packaged meshes are unchanged.

## Articulated handling and reflection pass — ADR-069

[ADR-069](../decisions.md#adr-069--accepted-articulated-near-field-hands-and-global-analytic-reflections) adds `fp.hand.v2`: 17 camera-only joints per hand, three finger segments plus thumb articulation, a wrist-driven sleeve with an anchored elbow, and independent rifle/pistol/knife/grenade grips. Eight skinned meshes and matching skeletons are published through `MapVisualClosure`; runtime validates required skin indices and binds verified payloads. Existing SGAF skin/skeleton encodings are reused. Soldier skeleton/P05 and authority hit data are unaffected.

Reload presentation reaches the magazine before removal, keeps support-hand travel tied to the detached magazine, seats it before returning and retains exact rest endpoints. The weapon/right-hand assembly has its own smaller reload cant rather than inheriting magazine translation. Fingers open during reach/release, close around the magazine during travel and respond to the accepted-shot cycle at the trigger. Reduced motion keeps bounded magazine travel and exact contact offsets. Skinning replaces the previous two rigid glove renderers with two Bone2 renderers; no new per-frame meshes or textures.

`geometry.weapon` **1.0.4** refines selected large-part bevels and near barrel roundness while retaining LOD ceilings, sockets and collision dimensions. Old 1.0.3 fixtures remain immutable. `GeneratedSky` supplies a 64² six-face RGBAHalf global analytic reflection with seven mips (about 256 KiB), covering arena positions outside the local probe and restoring previous ownership on disposal. Approximate mip filtering does not prove physical reflection accuracy or reference GPU performance. Named in-game visual acceptance remains Pending.

## Adding or tuning a weapon (ADR-077)

Each weapon is one file, `Content/Weapons/<name>.weapon.json`. It holds combat stats, near-field handling, optic, shot voice and kill-feed glyph. After editing, run:

```powershell
python Tools/generate_weapon_catalog.py
```

This rewrites `Simulation/Combat/WeaponCatalog.g.cs`, `Presentation/Model/View/WeaponVisualProfiles.g.cs` and `Generation/Core/Environment/WeaponMeshCatalog.g.cs`. Never edit the `.g.cs` files by hand; CI runs `--check`. Then run `./Tools/Test-Simulation.ps1` and `./Tools/Test-Generation.ps1`, and republish content with `./Tools/Compile-GameContent.ps1` (add `-Native` for a release).

`visual.meshKey` names the packaged mesh set and `visual.geometryRecipe` names the `geometry.weapon` recipe it is built from. Several weapons may share one mesh key (for example another rifle that reuses `rifle-carbine` geometry); a key always maps to one recipe. `knife` and `grenade` are built-in props without a recipe, magazine or cycle part.

A new rifle shape needs no code since ADR-082: give the definition `visual.geometryParts` (geometry.weapon 1.1.0 parts, see the [recipe baseline](../weapon-recipe-baseline.md)) instead of `visual.geometryRecipe`, with a new `meshKey`. For example, a stockless short rifle without an optic:

```json
"geometryParts": { "handguardFrontMm": 320, "barrelFrontMm": 430, "topRailTeeth": 8, "gasBlockZMm": 320,
                   "gasTubeZMm": 280, "gasTubeLengthMm": 60, "frontSightZMm": 330, "stock": "none", "optic": "none" }
```

`optic: none` cannot be combined with the `ScopedPip` profile optic. Run the generator, `./Tools/Test-Simulation.ps1` and `./Tools/Test-Generation.ps1` (the C# recipe parser enforces every range when the catalog loads), then republish content. Meshes are published in the shared weapon package and presentation binds every catalog mesh key. A new sound needs a new `SynthVoiceKind` and bank samples.

## Shared weapon package (ADR-078)

Weapon meshes are no longer inside map packages. `WeaponPackage` publishes one `shooter.weapon-package.v1` package beside the map packages, under its content-addressed directory, and `weapons.json` (`shooter.weapon-package-index.v1`) names its root hash. It holds four LODs per mesh key plus magazine and cycle parts for recipe-backed guns, at `mesh-<key>-lod<n>.bin`, `mesh-<key>-magazine.bin` and `mesh-<key>-cycle.bin`. Guns are encoded under the `geometry.weapon` fingerprint and props under `geometry.loadout-prop` (1.0.0; 1.1.0 since ADR-111).

`VerifiedMapPackageStore.TryLoadVisual` loads and hash-checks the weapon package once and attaches it to every map's `PackagedVisualPayloads` (`WithWeapons`); a missing or corrupt package fails the visual load with no regeneration fallback. Editor and test paths that use `PackagedVisualPayloads.FromBuild` attach an in-memory build. `Package-Release.ps1` verifies and ships `weapons.json` and the package, and adds `weapons:<root>` to the content closure.

## Weapon profiles — geometry.weapon 1.0.5 (ADR-072)

`geometry.weapon` advances **1.0.4 → 1.0.5**. Recipes require `parameters.silhouette` (`standard` | `carbine`; carbine only with `kind=rifle`). Combat remains owned by Simulation `WeaponCatalog` / `WeaponDefinition`; recipes still cannot override damage, fire rate or magazine size. Presentation binds packaged meshes by DefinitionId visual profile (`weapons/rifle`, `weapons/rifle-carbine`, …), not by loadout slot alone. Map visual closure publishes carbine LOD/mag/cycle paths beside the classic rifle. Sockets and collision mechanical dimensions stay on the numeric baseline. Old 1.0.4 fixtures remain immutable and reject regeneration. Named player/installer review remains Pending.

`ShooterFeatureBuild.CaptureHandling` renders ready/reach/extract/seat source diagnostics (ADR-099 adds empty-reload fresh/release, knife slash, grenade pin and throw frames) using verified map visual payloads and copied authority fixtures. The fixture adapter is Editor-only and cannot ship. Diagnostics do not substitute for actual gameplay or the comparative visual gate.

## Named arsenal — geometry.weapon 1.2.0 (ADR-102)

[ADR-102](../decisions.md#adr-102--accepted-named-real-world-arsenal-random-spawn-loadouts-and-muzzle-flash-fix) replaces the generic guns in play with seven named weapons: AK-47, AK-102, M16A4, Mk 14 EBR, M-TAC 338 (bolt-action sniper), G17 and Golden Eagle .50. Each has its own recipe under `Generation/FeatureRecipes/`, built by `ArsenalGeometry` from one of six archetype kinds (`ak`, `ar`, `dmr`, `bolt`, `pistol-striker`, `pistol-eagle`) and the parts `barrelFrontMm`, `furniture`, `muzzle`, `optic` and `stock`. Wood furniture uses material slot 5 and gold plating slot 6.

Definition files may also set:
- `combat.burstCount` / `combat.burstIntervalTicks` with `fireMode: Burst`;
- `combat.falloffStartMeters` / `falloffEndMeters` / `falloffMinScale` for range falloff;
- `combat.moveSpeedScale` and `combat.spawnWeight` (0 keeps a weapon out of random loadouts);
- `visual.handlingStyle` (`Rifle`, `Ak`, `Bolt`, `Pistol`), `visual.adsFovDegrees`, `visual.muzzleFlashScale` and `visual.muzzleFlashSideJets`.

Every bot life draws a random primary and sidearm in product sessions; since [ADR-107](../decisions.md#adr-107--accepted-armory-loadouts-and-reusable-rail-mounted-sights) the human uses the Armory loadout instead. The generic rifle, carbine and pistol remain catalogued with `spawnWeight 0` as the fixed test loadout.

## Kernel-built arsenal and gloves — ADR-106 (geometry.weapon 1.3.0)

[ADR-106](../decisions.md#adr-106--accepted-kernel-built-weapons-gloves-soldiers-and-structured-map-surfaces) moves the seven named recipes to geometry.weapon 1.3.0 (`Generation/Core/Weapons/ProfileArsenalGeometry.cs`). Parts come from the `Generation/Core/Geometry/ShapeKit.cs` kernel instead of bevelled boxes: rounded side-profile slabs for receivers, guards, slides and stocks; turned barrels, muzzle devices, scopes, turrets and rings; superellipse lofts for handguards and wooden/polymer stocks; a sampled grip loft with palm swell and finger ridges; curved extruded magazines with a raised spine; real rail teeth. Sockets, collision, envelopes, magazine/cycle parts and material slots are the 1.2.0 ones, so hands and gameplay are unchanged; 1.2.0 remains buildable for its fixtures.

The first-person gloves (`FirstPersonHandGeometry.Build`, rig `fp.hand.v2`) are a pillow-shaped palm, one swept tube per finger and the thumb through the rig joints with two-joint smoothstep skinning across each knuckle (`ChainWeights`), a knuckle guard, a back-of-hand pad and a wrist cuff. Offline renders: `ShooterFeatureBuild.CaptureArsenal` and `CaptureHandling` (`Artifacts/adr106-preview/`). In-game look review is Pending the next installer.

## Rail-mounted sights (ADR-107)

[ADR-107](../decisions.md#adr-107--accepted-armory-loadouts-and-reusable-rail-mounted-sights) takes the optic off the five primaries: their recipes move to geometry.weapon 1.4.0 with `optic: "mount"`, the 1.3.0 profile body without its scope (the AR keeps its A2 front tower, the AK its rear leaf and front post). Sights are separate meshes built by `ProfileArsenalGeometry.BuildSight` and exposed through `Generation/Core/Weapons/WeaponSights.cs`:

| Sight | Look | ADS |
|---|---|---|
| `irons` | AR rear block; flip-up rear aperture and front post on the DMR and bolt rails; part of the AK body | Irons, 52/80 hip FOV, soft vignette |
| `collimator` | Open reflex sight (ADR-115): low box base, rounded-rectangle frame leaning back 8°, one clear glass pane (material slot 11); dot 32 mm above the rail | Collimator, 1.2x (ADR-117: 80/1.2 ≈ 66.7°), gun stays in view, one small red dot in the clear pane |
| `scope2` | The M16 prism scope (unchanged 1.3.0 construction), now reusable | Scoped PiP, 40/80 (2x) |
| `scope4` | Mk 14 scope with turrets and rings | Scoped PiP, 20/80 (4x) |
| `scope8` | M-TAC 338 long scope | Scoped PiP, 10/80 (8x) |

Each sight mesh is built in the gun's own weapon space for its archetype rail (`ArsenalGeometry.MountRail`: AR, DMR and bolt top rails; an AK dust-cover rail adapter that ends ahead of the rear leaf) and parents to the gun at its origin. `WeaponSights.EyeFor(kind, sight)` is the line of sight used for first-person ADS alignment and the third-person head solve; the AR prism keeps the 1.3.0 M16 eye (0, 153, −10) mm. Packages hold one mesh set per (sight, archetype) any gun allows (`sight-<sight>-<kind>`, four LODs, fingerprint `geometry.weapon-sight.1.1.0` since [ADR-115](../decisions.md#adr-115--accepted-open-reflex-collimator-with-a-clear-glass-pane)). Sight meshes use weapon slots 0–3 plus slot 11, the transparent `Shooter/SightGlass` pane.

Which sights a gun takes is data: `visual.sights { "default": ..., "allowed": [...] }` in its definition file, in loadout order (irons, collimator, scope2, scope4, scope8). The generator requires it for every 1.4.0 rail recipe, checks that `visual.optic` matches the default sight and writes `Simulation/Combat/SightCatalog.g.cs` and `Presentation/Model/View/WeaponSightChoices.g.cs`. Sights never change combat numbers.

## Knife and grenade props (ADR-111)

[ADR-111](../decisions.md#adr-111--accepted-knife-and-grenade-overhaul-one-handed-hold-cut-and-throw-half-map-grenade-range-sound-explosion-models) rebuilds `LoadoutPropGeometry` on `ShapeKit` (`geometry.loadout-prop` 1.1.0). Knife: clip-point blade of filleted slabs around a fuller groove, spine jimping, a separate edge-grind solid, hooked crossguard, moulded grip with palm swell and finger grooves, lathe pommel with lanyard hole. Grenade: M67-style lathe body with a rolled seam and yellow band, lathe fuse, spoon swept along a spline, pull pin and a seamless ring (`ShapeKit.ClosedTube`). Finish slots 7 blade steel, 8 edge grind, 9 grenade paint and 10 yellow band come from new `FirstPersonSurfaceBaker` families. Both are held one-handed in first person.
