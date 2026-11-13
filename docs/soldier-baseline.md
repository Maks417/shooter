# Soldier recipe and motion starting baseline

Owner WP-04; refines [soldiers/animation](features/soldiers-animation.md). This is a common engineering seed, not a promise of convincing anatomy. Values are procedural parameters, not manually modeled vertex data or authored joint-pose frames. [Soldier fixture](fixtures/soldier-recipe.json) is the baseline source.

## Anatomy and bind pose

Baseline adult helmeted soldier height1800 mm; pelvis center height960, chest1380, neck1540, cranium center1660, shoulder width420, hip width280. Feet at ground0. Body uses lofted rings derived from segment lengths/radii, not a stored vertex list. Bilateral positions mirror X. All bind rotations identity, +Y bone primary axis except child offsets determine segment direction. Arms in T bind, root at feet, +Z facing; local offsets below create world positions by accumulating parent transforms. Parent IDs stable; 65535 root parent sentinel.

| Joint ID/name | Parent | Local bind offset mm |
|---|---|---|
|0 root|65535|0,0,0|
|1 pelvis|0|0,960,0|
|2 spine0|1|0,140,0|
|3 spine1|2|0,140,0|
|4 chest|3|0,140,0|
|5 neck|4|0,160,0|
|6 head|5|0,120,0|
|7 leftClavicle|4|−80,70,0|
|8 leftUpperArm|7|−130,0,0|
|9 leftForearm|8|−290,0,0|
|10 leftHand|9|−260,0,0|
|11 rightClavicle|4|80,70,0|
|12 rightUpperArm|11|130,0,0|
|13 rightForearm|12|290,0,0|
|14 rightHand|13|260,0,0|
|15 leftThigh|1|−100,−40,0|
|16 leftCalf|15|0,−450,0|
|17 leftFoot|16|0,−390,20|
|18 leftToe|17|0,−50,170|
|19 rightThigh|1|100,−40,0|
|20 rightCalf|19|0,−450,0|
|21 rightFoot|20|0,−390,20|
|22 rightToe|21|0,−50,170|

Skeleton's segment frame = local +Y toward child, secondary +Z projected onto perpendicular plane, choose +X if degenerate; derive frame independently of identity bind rotation and store basis in generated manifest. Hinge IK bend about segment-frame X, with pole −Z for elbows and +Z for knees. Limits relative to straight segment: elbow0..145 deg flexion, knee0..150 deg; wrist±45 deg flex/±30 deg twist; ankle−35..25 deg pitch/±20 deg roll; shoulder/hip swing cone90/75 deg; torso distributed yaw±45 deg total, pitch±25 deg. Clamp twist±45 deg arms,±30 deg legs. M1 pose review may refine with version bump.

Loft near LOD radial segments torso16, limbs12; joints use four longitudinal rings with radius interpolation; medium torso12/limbs8; far torso8/limbs6. Preserve joint seam rings and cap solid ends. Start body radii upper arm55→40 mm, forearm42→30 mm, thigh95→60 mm, calf60→40 mm; torso elliptical half-width210 mm/half-depth120 mm at chest tapering to150/100 at waist. Head ellipsoid half-axes90/120/100 mm, helmet offset10 mm; uniform shell offset6 mm, armor/pack clearance 12 mm. Validate topology/weights before adding folds/equipment. Numeric anatomy variation initially disabled; then height1700..1900 mm in seed-keyed streams with segment scaling tested.

## Gait and task trajectories

Walk cycle distance1.4m, run 2.2m; phase=frac(totalHorizontalDistance/cycleDistance). Left/right phase offset0.5, duty factor0.62walk/0.45run, swing clearance 0.08/0.12m, maximum reach98% total leg length, pelvis correction clamp±0.08m. Plant world-space contact while stance interval holds; replan foot at lift only. Foot probe origin0.25m above target, ray 0.6m downward; near both feet update60 Hz capped16 queries/frame for eight rigs, distance tiers stagger. Cosmetic queries cannot alter motor state.

ADS hand targets derive from stable weapon grip sockets. Reload task normalized phase:0..0.2 support hand reaches magazine,0.2..0.45 withdraws it downward0.18m,0.45..0.7 reaches belt location pelvis-relative(−0.15,−0.05,0.1)m,0.7..0.9 inserts generated magazine,0.9..1 returns grip. Interpolate task targets using smoothstep cubic and solve IK; do not author joint keyframes. Trigger-hand remains on grip; if unreachable, fail pose test and adjust task/socket clearance. M1 uses scripted phase fixtures independent of M2 weapon mechanics.

At LOD reduction map removed joints to nearest retained parent and renormalize weights; gameplay23 joint evaluator remains unchanged. Far visual rig retains root,pelvis,chest,head,upper/lower arm/hand and thigh/calf/foot, no optional fingers. Same socket world transforms computed from full semantic rig, not low-tier mesh bone availability.

## Required review

Pose sweep includes stand/crouch, full supported aim cone, turn, walk/run on15 deg slope/0.2m steps, reload phases and correction resets. Validate grip≤2cm, plant slide≤5cm, capsule/body center tolerances, finite weights and eight-rig CPU budget. Numeric baseline is a starting point; passing math tests never substitutes for silhouette/motion review. Publish changed anatomy/IK parameters under new generator version.

## Versioned M1 second iteration

The original values above and frozen `fixtures/soldier-recipe.json` describe iteration1, soldier.loft1.0.0/motion.spike.v1. They remain historical inputs, not silently updated production parameters. Under [ADR-022](decisions.md#adr-022--accepted-versioned-soldier-spike-and-second-numeric-motion-baseline), production soldier.loft1.0.1/motion.spike.v2 uses walk/run duty0.50/0.35. Distance cycles and speeds remain1.4/2.2m and3.5/5.5m/s. Nominal moving pelvis heights0.78/0.76m interpolate to0.66m at full crouch, with separate dynamic correction limited to0.08m downward; idle standing stays0.96m. The reload belt target becomes pelvis-relative(−0.15,+0.075,0.1)m. Initial tests exposed unreachable original contacts/tasks; passing the revised numeric limits still requires visual review of this tactical posture.

The generation-local semantic23 registry is `Generation/Core/Soldiers/SoldierSkeleton.cs`; hierarchy, identity bind rotations, child-derived segment bases and full joint IDs remain invariant across LODs. `SoldierGeometry` emits three weighted meshes and a visual skeleton through the existing SGAF v1 format. Removed far influences map to retained parents; no graphics profile changes an authoritative hit evaluator. M1 has no gameplay hit evaluator. Layered components may intersect and are not a single boolean-union body. Real skinned geometry, conservative posed bounds, grip orientation, silhouette and motion need their own evidence.

## Third bounded clearance candidate

The owner's explicit authorization in [ADR-023](decisions.md#adr-023--accepted-owner-authorized-third-bounded-body-clearance-iteration) permits one further candidate, soldier.loft1.0.2/motion.spike.v3. Prior1.0.0/1.0.1 inputs/caches remain immutable; the new validator rejects those versions rather than silently applying new motion. The semantic23 bind/reference registry and all local weapon sockets remain unchanged.

The candidate uses35°cosmetic pelvis/body blade and5°leg heading offset while preserving authoritative input root position/rotation and requested weapon forward direction. Spine yaw/pitch limits, limb lengths and gait v2 remain unchanged. Bounded clavicle tasks and a clearance-aware elbow-circle solve place the rifle from its real stock socket. Neck/head tasks expose actual right-eye/sight-line error rather than declaring ADS alignment. Exact-key upper-body reuse applies only a rigid translation; yaw/aim/reload changes invalidate it.

The exterior reload contact is chest-local(−0.24,−0.28,+0.20)m with+Z grab-face normal. A real numeric pouch, centered(−0.24,−0.28,+0.175)m and sized(0.10,0.13,0.05)m, and its short chest-mounted strap appear on all LODs. Reload uses bounded analytic task arcs and a45°sin²(pi×phase) weapon cant around the unchanged forward axis. These are equipment tasks, not authored joint frames.

This candidate is **not accepted**: the unchanged conservative clearance acceptance probe fails near down-aim reload phase0.45, with a dense possible left forearm/strap overlap of approximately2.18mm. Its120mm shoulder-attachment exclusion and6mm torso margin are explicit source-derived proxy assumptions; they do not replace actual skinned inspection. Neutral eye/sight-line separation is about6.49cm and the worst reported reload value about12.70cm. Hand/contact and finite-pose passes do not override these failures. The third iteration ends at review; no fourth iteration is authorized.
