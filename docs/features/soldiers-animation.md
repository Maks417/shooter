# Feature: soldiers, rigging and motion

Soldiers are the largest visual and engineering risk. The code lives in:
- `Generation/Core/Soldiers` (geometry);
- `Presentation/Rig` (motion and presentation);
- `Simulation/Pose` (gameplay hit poses).

**Product body ([ADR-124](../decisions.md#adr-124--accepted-licensed-authored-art-track-characters-first)).** In the product, players and bots wear a licensed authored body: CC0 humanoid models and animation clips under `Assets/Shooter/Art/Soldiers`, hash-pinned in `Tools/art-sources.json`. The generated soldier below remains the semantic motion rig and the diagnostics body (`ProductActorPresentation.UseAuthoredBodies = false`, the soldier gallery). See [authored body](#authored-body).

The fixed anatomy, joint IDs, bind transforms, loft counts and the iteration history are in the hash-locked [soldier baseline](../soldier-baseline.md). P05 status is in the [defect inventory](../visual-defect-inventory.md). Animation never drives character movement; the capsule simulation owns root translation.

## Body, kit and skeleton

**Geometry.** Product soldiers use `soldier.loft` 1.1.0 (`SoldierGeometry.BuildProfile`, [ADR-106](../decisions.md#adr-106--accepted-kernel-built-weapons-gloves-soldiers-and-structured-map-surfaces)). The body is a constrained anatomical parameter model with lofted cross-sections joined at designed rings.

The kit:
- a balaclava head (no skin slot);
- a turned helmet with rails and an NVG mount;
- goggles;
- gloves;
- shaped boots;
- a plate carrier with webbing, pouches and straps;
- a backpack and a belt;
- cargo pockets and knee pads;
- a team armband and a numeric emblem patch (1.0.3, [ADR-094](../decisions.md#adr-094--accepted-bounded-soldier-presentation-revision-and-p05-reopening)).

The generated body uses no scanned body, imported rig or runtime cloth; the product's authored body is described below.

**LODs.** LOD0 is about 14.7k triangles. `ProductActorPresentation.SelectBodyLod` switches each avatar's packaged LOD at 15 m and 40 m with 1.5 m hysteresis. It never goes finer than the quality tier's own LOD.

**Skeleton:**
- root → pelvis → spine0 → spine1 → chest → neck → head;
- chest → clavicle → upperArm → forearm → hand (both sides);
- pelvis → thigh → calf → foot → toe (both sides).

Fingers are visual-only children. The bind pose is generated from the anatomy. Every anatomy variant and LOD uses the same gameplay joint set.

**Skin weights.** Weights come from segment-distance fields: at most four normalized influences per vertex, with explicit elbow and knee falloff and twist distribution. Validation checks:
- weights sum to 1 within 1e-4;
- no negative weights;
- every index is valid;
- posed bounds are conservative.

## Procedural motion

Each frame runs this pipeline:
1. Simulation state (speed, stance, grounded, aim, weapon phase) sets root motion and foot targets.
2. A gait oscillator runs on distance travelled.
3. Planted feet are constrained.
4. The pelvis compensates.
5. Two-bone leg and arm IK is solved, with an explicit pole, law-of-cosines clamping and a degenerate fallback.
6. Joint limits are applied.
7. Bounded additive springs are added.

**Locomotion** (`soldier.motion.v7.locomotion`, [ADR-100](../decisions.md#adr-100--accepted-third-person-directional-gait-airborne-pose-and-bot-hopsslides)):
- **Gait direction.** Strides follow the travel heading under the aimed torso. Strafe and backpedal use shorter cycles, and a reversal mirrors the gait phase.
- **Airborne.** Actors tuck their legs while rising and reach down while falling, with no ground samples. Landing dips the pelvis by up to 7 cm.
- **Slide.** The slide dive faces its direction of travel.
- **Death.** Death is an analytic collapse over 0.75 s, then a held side-lying pose until the four-corpse / five-second limit ends it.
- **Source data.** Presentation derives all of this from the copied motor view (`ThirdPersonLocomotionBlend`). Physics ragdoll is not implemented.

**Shooting posture** (`ShootingPosture`, [ADR-095](../decisions.md#adr-095--accepted-structural-weapon-from-eye-shooting-posture-p05-reopening)), the product default:
- The right clavicle sets the stock socket and drops during the reload cant.
- The neck is solved so that the right eye lies on the sight line (`WeaponSights.EyeFor`).
- Clearance-aware elbows are solved after the head.
- The support hand bows outward during the reload.

**Reload and handling** are equipment-relative analytic trajectories timed to authority ticks, never hand-keyed joint tracks. First-person layers are described in [weapons](geometry-weapons.md#first-person-handling).

**Recoil** is a seeded damped spring that shares shot indices with the view. It never applies authoritative recoil a second time.

**Visual update rates** may be 60, 30 or 15 Hz. Hit volumes stay at 60 Hz.

**Grounding.** `CollisionSoldierGround` gives cosmetic foot support from the verified gameplay boxes. `WeaponVisualProfiles` provides per-profile support sockets.

## Authored body

`Presentation/Rig/AuthoredSoldierBody` drives one licensed authored body per actor ([ADR-124](../decisions.md#adr-124--accepted-licensed-authored-art-track-characters-first)):
- **Catalog.** `SoldierSkinCatalog` reads `Resources/SoldierArt/soldier-skins.json`: the body height, the clip roles and death clips (each may name its own clip file as `resource#clip`), and the variants. A variant names its model, eligible teams, generated gear, uniform and gear material names, hidden renderers and material slots (hair under a generated helmet), borrowed parts from other models of the pack, and an optional gear fit. A missing or broken catalog throws; there is no generated-body fallback in the product.
- **Current art.** The Quaternius Ultimate Modular Men Swat body (CC0) in six variants: two with the Swat helmet, four with Adventurer, Casual2, Suit or Beach heads under a generated helmet, some with a generated backpack. Clips: Modular Men idle, walk and four-way run, plus Universal Animation Library crouch, jump and death.
- **Variants.** `SoldierVariantPicker` picks a variant per actor deterministically from its entity id within the team, so a squad shows several bodies and kits.
- **Clips.** A code-built `PlayableGraph` (manual evaluation, no controller asset) mixes the clip roles with `AuthoredSoldierBlend`: idle, four-way walk and run, crouch idle and four-way crouch walk, airborne, all on one shared gait phase. A missing role falls down a chain (same direction in the other gait, then forward, then idle); when no clip covers the travel direction the legs turn toward it and backpedal plays the forward cycle in reverse. A one-shot death clip, picked per actor, replaces the death collapse. Clips import in place; the root follows the authoritative feet as before.
- **Upper body.** `SoldierMotion` still solves the shooting posture every frame on the unrendered semantic rig. Its chest, neck, head, clavicles and arms are retargeted onto the humanoid bones through T-pose offsets (A-pose packs are straightened into a T-pose first). The hips then carry the drawn head onto the solver's head, which ADR-112 keeps on the head hit sphere, and the legs re-solve onto the clip's foot plants. The weapon and hand targets shift with the authored chest, a two-bone solve puts the wrists on the grips (the support hand slides back along the handguard when the arms are short), the hands take frames from the weapon axes and the fingers curl round the grip. Reload, knife and grenade overlays keep their validated motion.
- **Gear.** `SoldierGearKit` (generated, `Generation/Core/Environment`) builds the helmet, goggles, plate carrier or chest rig, pouches, backpack, belt and knee pads as rigid per-joint pieces in two detail levels; they ride the matching humanoid bones with a per-variant fit.
- **Materials.** Uniform materials wear the team kit, skin the team skin tone, and other authored materials are rebuilt on `Shooter/MapSurface` from their own texture and colour, so actor ambient and tiers keep one lighting model.
- **LOD.** The ADR-106 distances (15 m and 40 m, 1.5 m hysteresis) lower skin influences and switch gear to the coarse pieces.
- **Import settings** are code: `Bootstrap/Editor/SoldierArtPostprocessor` sets the humanoid import, in-place clip flags and texture settings.

**Tests.** `AuthoredSoldierTests` (EditMode) checks the catalog, humanoid validity and clip roles, that a broken catalog throws, and that the drawn authored head and chest stay inside the analytic hit volumes for stand, run, sprint and crouch. `AuthoredSoldierBlendTests` and `SoldierGearKitTests` cover the pure blend, the variant pick and the gear kit.

## Authoritative body

The gameplay pose evaluator (v2, [ADR-112](../decisions.md#adr-112--accepted-crosshair-true-hits-rig-aligned-hit-volumes-and-bots-that-use-map-cover-flank-and-move-smoothly)) maps stance, velocity, aim and action phase to fixed head, torso and limb capsules.
- **A mathematical approximation.** It is not a readback of skinned vertices. Its version is recorded in the match hash.
- **Alignment.** `HitVolumeRigAlignmentTests` keep the head, pelvis and chest within 5 cm of the shooting posture when still, moving or crouched.
- **Mismatches.** When visual and gameplay disagree, they are fixed together through contract review. Hit volumes never vary by visual profile.
- **Death.** Death disables input and damage once. A corpse does not block, and corpses never replicate bones.

## Acceptance

**Tests:**
- no invalid topology or weights;
- no gross joint inversion;
- stance transitions preserve the root and capsule;
- planted-foot slide ≤5 cm;
- hand-to-grip separation ≤2 cm, except during an intentional release;
- 1000 seeded poses are all finite;
- the P05 clearance and eye-line gate (passing numerically since ADR-095).

**Performance.** Animation CPU for the full roster stays within budget at Low on the reference machine.

**Visual review.** Silhouette and motion need a named review with images (VD2/VD4, [visual direction](../visual-direction.md)). That review is Pending, and thresholds alone never prove realism.

**Fallbacks.** If the soldiers fail review, change the authored variants or the generated kit through an ADR. Authored art enters only through the licensed art track ([ADR-124](../decisions.md#adr-124--accepted-licensed-authored-art-track-characters-first)).
