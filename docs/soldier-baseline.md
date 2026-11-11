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
