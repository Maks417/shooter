# Feature: FPS movement and combat

Owner WP-05, paths `Simulation/Motor`, `Simulation/Combat`, with presentation adapters owned separately. Consumes [contracts](../contracts.md); authority extension in [networking](networking.md).

## Player flow and fixed-tick rules

Spawn only after world/collision ready → look/move/stance → equip rifle or pistol → aim/fire/reload → impacts/damage/death → objective complete or restart. Mouse look samples frame input, commands carry aim at tick. Raw mouse delta, sensitivity, invert Y, rebindable controls and pause are baseline. Prevent double button edges when multiple simulation ticks consume one display sample.

Kinematic capsule sweeps resolve horizontal movement then gravity/grounding with bounded iterative sliding (max four iterations), slope limit 45 degrees, step 0.25 m initial tuning. Check ceiling before uncrouching; crouch preserves feet. Jump only on grounded accepted edge; no stamina or prone until base movement is stable. Do not use visual head bob as physical eye height. Capsule resize checks overlaps. Ground platform support is limited to static world in slice.

Default tuning is data, not real weapon ballistics: rifle 600 rounds/min, 30-round magazine, 90 reserve, 2.4 s reload; pistol 360 rounds/min semi-auto, 15-round magazine, 45 reserve, 1.8 s reload. 60 Hz tick maps rifle interval to 6 ticks, pistol to 10. Durations round up to ticks. Reload transfers ammunition at completion; switching/canceling before completion transfers none. No fire while reloading/equipping/dead. Trigger held repeats only for automatic mode; semi-auto requires distinct press edge.

Baseline uses authoritative hitscan to 300 m. Seeded spread samples a specified disk/cone distribution using shot-index stream, bounded recoil updates authoritative aim offset, while view kick is separate. Eye ray selects aim point; muzzle ray to that point determines obstruction and actual hit. If muzzle starts inside geometry, reject/block rather than firing through a wall. Third-person weapon sockets are visual; canonical muzzle is computed from gameplay pose and weapon dimensions. No client-supplied impact location accepted as truth.

Surface ray hits stop at nearest valid collider; hit-region capsule query checks entity volumes and occluders consistently. Damage defaults rifle 30, pistol 25, head multiplier 2, torso 1, limb .75, health 100; armor/penetration/ricochet are postponed. Round using explicit integer rule and emit exactly one DamageApplied event per accepted hit. Friendly fire disabled initially; layer/filter logic still includes friendly bodies as shot blockers if configuration says so. Damage queries never target decorative render mesh.

## Ballistics later phase

Projectile interface supports origin, velocity, gravity, drag ID, spawn tick and seed. After hitscan gate, add swept-segment projectiles with bounded substeps and lifetime; no tunneling via transform movement. Server resolves final hits. Penetration needs canonical thickness/material data and energy loss tests; do not approximate from texture roughness. Grenades, explosions and suppression are later features with capped overlap queries and damage falloff tests.

## Feedback

ShotAccepted event drives synthesized report, mesh muzzle flash/analytic glow, casing proxy and recoil. Impact creates bounded generated geometry/analytic decal or surface patch with pooled lifetime; no image sprite. Audio occlusion takes world queries under a budget. Hit confirmation follows server result; local predicted shots are provisional. First-person arms, weapon camera FOV and depth handling cannot reveal enemies through walls. Accessibility allows reducing camera shake, sway, flash and blur without changing authoritative spread.

## Acceptance

Movement tests cover 30/60/144 display FPS with the same 60 Hz command script, slope boundary, steps, corners, crouch ceiling, spawn overlaps and pause. Over 60 s, auto rifle has exactly the tick-specified shot count and no duplicate damage; semi-auto ignores held-only repeats. Ammo conserved through reload/switch/death edge sequences. Cover blocks eye/muzzle scenarios, including close wall and corner peeking. Hit profile tests pass across render profiles, LODs and rig update rates. All damage and kill events are idempotent under replay. B03 proves zero steady managed allocations and simulation budget at eight bots.


## Complete baseline and match rules

[Gameplay baseline](../gameplay-baseline.md) supplies motor/ADS/spread/recoil/damage defaults, canonical muzzle, collision matrix and Compound Clear objective. [Simulation protocol](../simulation-protocol.md) fixes boundary ordering, actor nonblocking policy, same-tick trades and complete restoration state. These replace unspecified rounding and filter choices in this overview.
