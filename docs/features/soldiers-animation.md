# Feature: generated soldiers, rigging and motion

Largest visual/engineering risk. Owner WP-04 with a dedicated early spike at M1; [risk register](../risks.md) and [performance](../performance.md) govern fallback. Proposed paths `Generation/Core/Soldiers`, `Presentation/Rig`. Gameplay pose/hit profiles live under Simulation with WP-05 ownership.

## Model and bind pipeline

Use a constrained anatomical parameter model: height, shoulder/hip width, torso lengths, segment radii and limb lengths within validated adult ranges. Build torso/limbs from lofted cross-sections joined at designed ring topology; use analytic joint patches with shared vertex rings. Hands begin as palm and grouped finger forms, head as parametric cranium/jaw/nose approximation. Helmets, goggles, gloves and uniforms reduce exposed facial/detail demands while remaining plausible military equipment. A helmeted soldier with simple face is an accepted first slice; convincing cinematic faces are excluded.

Clothing is a layered offset shell with construction seams, analytic folds around joints and controlled directional wrinkle fields. Equipment is generated hard-surface packs, armor plates and straps with conservative clearances. No runtime full cloth required; bounded spring accessories may appear on higher tiers. No scanned body or hidden imported rig.

Skeleton hierarchy semantic IDs: root → pelvis → spine0 → spine1 → chest → neck → head; chest → left/right clavicle → upperArm → forearm → hand; pelvis → left/right thigh → calf → foot → toe. Optional fingers are visual-only children. Bind pose is procedurally generated from anatomy. Joint local frames and rotation limits are documented in output manifest; quaternion normalization and parent acyclicity validated. Same semantic gameplay joint set across anatomy variants and visual LODs.

Generate skin weights from segment-distance fields and geodesic/ring constraints, max four normalized influences/vertex. Explicit elbow/knee falloff and twist distribution avoid collapsing joints. Bind matrices generated from hierarchy, not copied from engine examples. Validate sum weights within 1e-4, no negative weight, all indices valid, posed bounds conservative. Use engine skinning initially; no custom GPU skinning until measured need.

## Procedural motion graph

Simulation locomotion state + speed + stance + grounded + aim + weapon phase → desired root motion/foot targets → gait oscillator → planted-foot constraints → pelvis compensation → two-bone leg/arm IK → joint limits → additive bounded springs → final visual pose. Capsule simulation owns root translation; animation never drives character movement. Render gait interpolates but does not choose hit capsule position.

Walk/run gait uses distance traveled, not frame-dependent accumulated phase. Foot contact phases define planted intervals; raycast ground under planned contacts under bounded query budget. Keep planted foot in world space until lift; unreachable targets clamp stride and pelvis adjustment. Two-bone IK uses explicit pole direction, law-of-cosines clamping, joint limits and fallback for degenerate/collinear targets. Legs adapt to slopes/stairs; airborne uses stance interpolation. Arms use hand sockets, ADS alignment and aim cone; residual torso twist distributes across spine. Reload is a mechanical state-driven hand trajectory generated from grip/magazine waypoints and analytic curves, not hand-keyed joint tracks. Waypoints describe equipment-relative tasks; no authored bone pose list.

Procedural recoil is a seeded, damped spring response and shares shot indices with view events; visual spring does not change authoritative recoil twice. Breathing/sway are bounded and accessibility-scaled. Weapon collision compresses viewmodel toward shoulder without moving authoritative muzzle through walls. Near/far rig update rates may be 60/30/15 Hz visually, with interpolation; simulation hit volumes stay at 60 Hz independent of quality.

## Authoritative body and death

Shared gameplay pose evaluator maps stance, velocity, aim and action phase to a fixed set of head/torso/upper-limb/lower-limb capsules. It is a cheaper deterministic mathematical approximation, not a readback of skinned vertices. Record evaluator version in match hash. Test visible-body-to-hit-volume tolerance at defined sample poses: torso/head centers ≤5 cm, limb centers ≤10 cm for first slice. If violated, adjust animation or gameplay evaluator together through contract review; never change hit volumes per visual profile.

Death first disables input/damage once and uses analytic joint-constrained collapse; optional bounded procedural ragdoll is a separate measured T032 spike, not mandatory for M4; server corpse collision uses fixed policy (nonblocking after death for slice). Cosmetic ragdolls cap at four active near corpses, then freeze simplified pose. No ragdoll bones replicate. Low can use the same algorithm with fewer render updates, not a different living hit profile.

**Implemented (cosmetic):** `soldier.motion.spike.v4` adds `SoldierMotionInput.DeathPhase` (`-1` living, `0..1` collapse). Presentation drives phase over `DeathCollapseDurationSeconds` (0.75 s) when an avatar becomes a corpse or `!Alive`, then holds the side-lying pose until the existing four-corpse / five-second TTL. Root stays at authoritative feet; weapons stay hidden on corpses. Physics ragdoll remains optional and unimplemented.

## M1 spike and acceptance

Generate one helmeted soldier and one weapon, implement bind pose, walk/run, crouch, aim and reload with hard-surface equipment. Capture front/side/three-quarter and motion on level, 15-degree slope, 0.2 m steps and turn-in-place. Near body LOD target ≤24k triangles / ≤40 active bones, middle ≤10k, far ≤3k / ≤20 bones. View arms ≤12k. Counts adapt to performance evidence.

Pass: no invalid topology/weights, gross joint inversion or persistent self-intersection in approved test set; stance transitions preserve root and capsule; planted-foot slide ≤5 cm over a contact interval at supported speeds; hand/grip separation ≤2 cm for held weapon except intentional reload release; 1000 fixed-seed pose samples finite; eight-soldier animation CPU p95 ≤2 ms/frame at Low on reference machine. Record subjective silhouette/motion review with images; thresholds alone do not prove realism.

If spike fails after bounded two implementation iterations, keep generator constraint and reduce exposed anatomy, clothing complexity, weapon-hand interactions and visible soldier count only through a revised workload/scope ADR. Do not import a character or promise AAA results. Freeze the skeleton contract before parallel equipment/motion work proceeds.


## Concrete starting recipe and ownership

Use [soldier baseline](../soldier-baseline.md) for fixed anatomy, joint IDs/bind transforms, frame/limit rules, loft counts, gait and reload task trajectories. M1 drives phases with test fixtures, independent of M2 weapon mechanics. [T028/T032](../task-supplement.md) own hit-overlay validation and collapse/ragdoll behavior; no unassigned full physics feature is implied.
