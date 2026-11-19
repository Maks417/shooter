# Feature: generated soldiers, rigging and motion

Soldiers are the largest visual and engineering risk. The code lives in:
- `Generation/Core/Soldiers` (geometry);
- `Presentation/Rig` (motion and presentation);
- `Simulation/Pose` (gameplay hit poses).

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

No scanned body, imported rig or runtime cloth is used.

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

**Fallbacks.** If the soldiers fail review, reduce exposed anatomy or kit complexity through an ADR. Never import a character.
