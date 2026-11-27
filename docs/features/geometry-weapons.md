# Feature: procedural weapons

Weapon geometry is built from code under `Generation/Core/Geometry` and `Generation/Core/Weapons`. Combat mechanics belong to [combat](combat.md), and geometry can never change damage, fire rate or magazine size.

The versioned recipe parameters are in the hash-locked [weapon recipe baseline](../weapon-recipe-baseline.md). The ADRs below explain each step.

## Construction

Parts come from the `ShapeKit` kernel (`Generation/Core/Geometry/ShapeKit.cs`, [ADR-106](../decisions.md#adr-106--accepted-kernel-built-weapons-gloves-soldiers-and-structured-map-surfaces)):
- rounded side-profile slabs for receivers, guards, slides and stocks;
- turned barrels, muzzle devices, scopes and rings;
- superellipse lofts for handguards and stocks;
- a sampled grip loft;
- curved extruded magazines;
- real rail teeth.

All dimensions are real metres. Mechanical parts are closed sections with explicit joins, never general CSG.

**Archetypes.** `ProfileArsenalGeometry` / `ArsenalGeometry` build every named gun from an archetype:
- `ak`, `ar`, `dmr`, `bolt`, `scar`, `pump`;
- `pistol-striker`, `pistol-eagle`.

Each archetype also takes parts: `barrelFrontMm`, `furniture`, `muzzle`, `optic`, `stock` (`fixed`, `collapsible`, `none` …).

**Material slots:**

| Slots | Material |
|---|---|
| 0–4 | Coated metal, polymer, machined steel, recess/rubber, olive enamel |
| 5 | Wood |
| 6 | Gold |
| 7–10 | Knife and grenade finishes |
| 11 | Sight glass |

**Generator versions** (`geometry.weapon`):
- **1.4.0** gives bare-rail primaries ([ADR-107](../decisions.md#adr-107--accepted-armory-loadouts-and-reusable-rail-mounted-sights)).
- **1.5.0** adds `scar`, `pump` and the collapsible stock ([ADR-118](../decisions.md#adr-118--accepted-m4a1-mk-16-m24-sws-and-m870-pellets-shell-reloads-and-geometryweapon-150)). It builds every 1.4.0 recipe byte-identically.
- **Older versions** stay buildable for their fixtures and reject regeneration under a new identity.

**Sockets.** Each socket has a stable semantic ID and a rigid local transform, with no non-uniform scale:
- muzzle (+Z);
- chamber/ejection;
- magazine;
- left and right hand grips;
- shoulder;
- sight eye line;
- rails.

Sockets and collision are declared per archetype. They match across every visual variant to within 1 mm and 0.1°. Invalid lengths or socket orientations return a structured compile error. Extreme seeds cannot detach parts or change gameplay dimensions.

**LODs.** Each gun has four LODs plus separate **magazine** and **cycle** (bolt, slide or pump forend) meshes. The initial triangle ceilings are rifle 18k/8k/3k/800 and pistol 8k/4k/1.5k/400. First-person view uses LOD0 independently of the world LOD. Gameplay collision uses simplified bounds.

**Props.** The knife and grenade are `LoadoutPropGeometry` (`geometry.loadout-prop` 1.1.0, [ADR-111](../decisions.md#adr-111--accepted-knife-and-grenade-overhaul-one-handed-hold-cut-and-throw-half-map-grenade-range-sound-explosion-models)):
- an M-style clip-point knife;
- an M67 grenade with spoon, pin and ring.

Both are held one-handed.

## Rail-mounted sights (ADR-107)

Sights are separate meshes (`ProfileArsenalGeometry.BuildSight`, `WeaponSights.cs`), built in the gun's weapon space for its archetype rail. Packages hold one mesh set per (sight, archetype), named `sight-<sight>-<kind>`. The fingerprint is `geometry.weapon-sight.1.1.0`.

| Sight | Look | ADS |
|---|---|---|
| `irons` | Archetype irons (AR rear block, flip-up pair on DMR/bolt/Mk 16, AK leaf, ghost ring on the M870) | 52/80 hip FOV, soft vignette |
| `collimator` | Open reflex sight with one clear glass pane ([ADR-115](../decisions.md#adr-115--accepted-open-reflex-collimator-with-a-clear-glass-pane)) | 1.2x, gun stays in view, one red dot ([ADR-117](../decisions.md#adr-117--accepted-single-point-collimator-dot-and-12x-collimator-zoom)) |
| `scope2` | M16 prism | PiP 2x |
| `scope4` | Turret scope | PiP 4x |
| `scope8` | Long scope | PiP 8x |

`WeaponSights.EyeFor(kind, sight)` gives the line of sight for first-person ADS and for the third-person head solve. Scoped ADS is a screen-space PiP: digital lens zoom with a blurred hip-FOV periphery ([ADR-065](../decisions.md#adr-065--accepted-screen-space-pip-rifle-scope-blurred-periphery)). Sights never change combat numbers.

## First-person handling

**Hands.** The rig is `fp.hand.v2`, with 17 camera-only joints per hand ([ADR-069](../decisions.md#adr-069--accepted-articulated-near-field-hands-and-global-analytic-reflections)). The packaged gloves are one piece with blended knuckles (ADR-106).

**Licensed arms** ([ADR-129](../decisions.md#adr-129--accepted-licensed-first-person-arms-wrapped-support-grips-strafe-clip-facing-and-grenade-start-contact)). With the authored bodies on (the product), the player sees the arms of their own licensed soldier, not the packaged gloves:
- `ProductActorPresentation` builds a second `AuthoredSoldierBody` of the local player's variant and team under the camera. `ConvertToFirstPersonArms` drops its clips and gear and cuts each skinned mesh to the forearm, hand and outer upper-arm triangles, one renderer per side, with no shadows. The cut is made once per variant.
- The `fp.hand.v2` bones stay posed but hidden. Their wrist (including reload and pin-pull reach) and elbow joints are the targets for the slots without a gun socket and the elbow poles for all slots.
- **Trigger hand.** The gun's pistol grip is fitted from its own vertices (a raked axis and a half width under the `right-hand-grip` socket). The knuckles stack up that axis with the index knuckle under the receiver, the fingers and thumb wrap the grip, and the index finger lies along the trigger guard, curling with the trigger. The knife sits in the fist at the rig wrist; the grenade is cradled palm-up. Hands are drawn at 0.8 of the body's.
- **Support hand.** The palm lies under the gun's own `left-hand-grip` socket and the fingers wrap the measured handguard radius (`AuthoredHandGrip`). Reload reach moves the wrist, opens the fingers and turns the hand from the handguard to a magazine grab. The support arm shows only when the packaged hand would.
- **Shoulders.** Fixed camera-space anchors 16–20 cm behind and 27 cm to each side of the eye, so the near-straight forearms reach the gun from the lower screen corners rather than pointing at the camera. They are pulled toward the wrist only when the arm cannot reach.
- **Reload framing.** The reload choreography lifts the gun toward the screen centre for its whole window: a long gun by (−3, +6, +3) cm and a pistol by (−2, +8, +1) cm, so the well and support hand stay in view. A reach up over the gun (slide rack, charging handle) turns the support hand palm down.
- **Sidearm hip pose.** The G17 and Golden Eagle sit higher and nearer the centre, turned 6° inward, so both hands show on the grip (their `nearFieldHipBias` and `hipRotationDegrees` in `Content/Weapons`).
- **Missing data.** No glove fallback: a body without measured hands or arm triangles throws.

**Choreography** comes from per-weapon numeric key tables (`MotionCurve`, [ADR-099](../decisions.md#adr-099--accepted-per-weapon-first-person-hand-and-weapon-choreography)):
- the reload sequence for each handling style (`Rifle`, `Ak`, `Bolt`, `Pistol`, pump shell-by-shell), with a visible magazine exchange and a fresh magazine carried in the support hand;
- per-weapon draw styles that fill the authoritative equip ticks;
- the knife slash and the grenade pin, cook and throw;
- sprint carry, jump and landing, crouch, slide, look inertia and idle breathing.

**Constraints.** Every layer is cosmetic and ADS-safe, and reduced motion scales it down. Presentation drives the magazine and cycle parts from the reload phase and from `ShotAccepted`. Camera-local placement never changes the ballistic origin, the hit pose or the sockets.

## Packages

Weapon meshes ship in one shared `shooter.weapon-package.v1` package beside the map packages ([ADR-078](../decisions.md#adr-078--accepted-shared-weapon-package-separate-from-map-packages)). `weapons.json` names its root hash.

- **Load.** `VerifiedMapPackageStore.TryLoadVisual` loads and hash-checks the package once and attaches it to every map's visual payloads. A missing or corrupt package fails the load, with no regeneration fallback.
- **Release.** `Package-Release.ps1` ships the package and adds `weapons:<root>` to the content closure.

## Adding or tuning a weapon (ADR-077)

Each weapon is one file, `Content/Weapons/<name>.weapon.json`, which holds:
- combat stats (fire mode, burst, falloff, move speed, `spawnWeight`; 0 keeps a gun out of random loadouts; optional `lethalOnHit` for single-projectile guns, [ADR-125](../decisions.md#adr-125--accepted-bolt-action-snipers-kill-on-any-hit));
- near-field handling;
- `visual.sights` (a default plus the allowed list);
- muzzle flash;
- the shot voice, and for a named gun without its own recording `audio.derive` (`from` RifleShot/PistolShot, `pitch`, `gain`): the interim varispeed of the shipped take;
- the kill-feed glyph, and `hud.silhouette` (Rifle, Carbine, Pistol, Knife or Grenade; defaults by family) for the analytic fallback shape.

After editing:

```powershell
python Tools/generate_weapon_catalog.py
./Tools/Test-Simulation.ps1
./Tools/Test-Generation.ps1
./Tools/Compile-GameContent.ps1 -Native
```

The generator rewrites seven files. Never edit a `.g.cs` file by hand; CI runs `--check`.
- `WeaponCatalog.g.cs`
- `WeaponVisualProfiles.g.cs`
- `WeaponMeshCatalog.g.cs`
- the sight catalogs
- `KillFeedGlyphs.g.cs`: icon name, fallback silhouette and review gallery per glyph (T195)
- `WeaponShotVoices.g.cs`: the gun-report voice set and named-report derivations used by the mixer and the bank baker (T195)

**What is still a hand edit for a new gun:**
- a new `KillFeedWeapon` member when it needs its own glyph;
- a new `SynthVoiceKind` member and its `CombatAudioBank` file name, appended so earlier bank indexes stay put, plus the audio bank rebuild (a named provenance exception);
- its feature recipe and career placement.

Kill-feed drawing, mixer classification, baker fallbacks and AI range behaviour all follow from the definition.

**Mesh keys.** `visual.meshKey` names the packaged mesh set. `visual.geometryRecipe` names its recipe. A key always maps to one recipe, but several guns may share a key.

**New shapes.** A new rifle shape needs no code ([ADR-082](../decisions.md#adr-082--accepted-geometryweapon-110-parametric-rifle-parts)). Give the definition `visual.geometryParts` (see the [recipe baseline](../weapon-recipe-baseline.md)) and a new `meshKey`. The C# recipe parser enforces every range when the catalog loads.

**New sounds.** A new sound needs a new `SynthVoiceKind` and bank samples ([audio](audio.md)).

## Acceptance

**Automated tests:** primitive bounds, winding, normals and UVs; stable hashes; every socket validated; LOD transitions keep the sights and silhouette.

**Diagnostics.** Offline source renders come from `ShooterFeatureBuild.CaptureArsenal` and `CaptureHandling`. They are not packaged-player evidence.

**Still open:** named in-game look and feel review of every gun and sight (VD3, [visual direction](../visual-direction.md)).
