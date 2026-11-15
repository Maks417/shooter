# Feature: FPS movement and combat

Product extension: [Team Deathmatch](team-deathmatch.md) adds team score/time/respawn/results around the existing combat rules, through the planned [session/lifecycle seam](../game-session-contracts.md). It preserves same-tick trades, friendly-body obstruction with zero friendly damage, canonical hit volumes and quality independence. A fresh respawn restores baseline ammo/health under a new life/EntityId; the historical Compound Clear no-refill rule stays confined to that mode. Gameplay authority remains independent of UI and presentation.

Owner WP-05, paths `Simulation/Motor`, `Simulation/Combat`, with presentation adapters owned separately. Consumes [contracts](../contracts.md); authority extension in [networking](networking.md).

## Player flow and fixed-tick rules

Spawn only after world/collision ready → look/move/stance → equip rifle, pistol, knife or grenade → aim/fire/reload (or cook/throw) → impacts/damage/death → objective complete or restart. Mouse look samples frame input, commands carry aim at tick. Raw mouse delta, sensitivity, invert Y, rebindable controls and pause are baseline. Prevent double button edges when multiple simulation ticks consume one display sample.

Kinematic capsule sweeps resolve horizontal movement then gravity/grounding with bounded iterative sliding (max four iterations), slope limit 45 degrees, step 0.25 m initial tuning. Check ceiling before uncrouching; crouch preserves feet. Jump only on grounded accepted edge; no stamina or prone until base movement is stable. Do not use visual head bob as physical eye height. Capsule resize checks overlaps. Ground platform support is limited to static world in slice.

Default tuning is data, not real weapon ballistics: rifle 600 rounds/min, 30-round magazine, 90 reserve, 2.4 s reload; pistol 360 rounds/min semi-auto, 15-round magazine, 45 reserve, 1.8 s reload; knife melee 1.8 m / 55 damage / 24-tick interval (no ammo); one grenade per life with 120-tick fuse cook/throw and 5 m blast falloff (ADR-057). 60 Hz tick maps rifle interval to 6 ticks, pistol to 10. Durations round up to ticks. Reload transfers ammunition at completion; switching/canceling before completion transfers none (cook cancel restores the unthrown grenade). No fire while reloading/equipping/dead. Trigger held repeats only for automatic mode; semi-auto and knife require distinct press edges; grenade cooks while held and throws on release.

Baseline uses authoritative hitscan to 300 m. Seeded spread samples a specified disk/cone distribution using shot-index stream, bounded recoil updates authoritative aim offset, while view kick is separate. Eye ray selects aim point; muzzle ray to that point determines obstruction and actual hit. If muzzle starts inside geometry, reject/block rather than firing through a wall. Third-person weapon sockets are visual; canonical muzzle is computed from gameplay pose and weapon dimensions. No client-supplied impact location accepted as truth.

Surface ray hits stop at nearest valid collider; hit-region capsule query checks entity volumes and occluders consistently. Damage defaults rifle 30, pistol 25, head multiplier 2, torso 1, limb .75, health 100; armor/penetration/ricochet are postponed. Round using explicit integer rule and emit exactly one DamageApplied event per accepted hit. Friendly fire disabled initially; layer/filter logic still includes friendly bodies as shot blockers if configuration says so. Damage queries never target decorative render mesh.

## Ballistics later phase

Projectile interface supports origin, velocity, gravity, drag ID, spawn tick and seed. After hitscan gate, add swept-segment projectiles with bounded substeps and lifetime; no tunneling via transform movement. Server resolves final hits. Penetration needs canonical thickness/material data and energy loss tests; do not approximate from texture roughness. One cook/throw frag grenade per life is authoritative (ProjectileSpawned + fuse blast, friendly damage still zero). Extra throwable types, penetration and suppression remain later features.

## Feedback

ShotAccepted event drives a weapon-specific sample-bank report (rifle/pistol under ADR-055), mesh muzzle flash/analytic glow, casing proxy and recoil. The first-person full-screen muzzle overlay and “Shot” subtitle are local-instigator only (ADR-056); remote bot shots must not flash the whole screen. Impact creates bounded generated geometry/analytic decal or surface patch with pooled lifetime; no image sprite. Reload start/complete, damage-taken and death also emit presentation audio from the same bank. Audio occlusion takes world queries under a budget (distance attenuation and HF rolloff ship first). Hit confirmation follows server result; local predicted shots are provisional. First-person arms, weapon camera FOV and depth handling cannot reveal enemies through walls. Accessibility allows reducing camera shake, sway, flash and blur without changing authoritative spread. Rifle/pistol select remains 1/2; Q toggles the last used weapon slot (ADR-056).

## Acceptance

Movement tests cover 30/60/144 display FPS with the same 60 Hz command script, slope boundary, steps, corners, crouch ceiling, spawn overlaps and pause. Over 60 s, auto rifle has exactly the tick-specified shot count and no duplicate damage; semi-auto ignores held-only repeats. Ammo conserved through reload/switch/death edge sequences. Cover blocks eye/muzzle scenarios, including close wall and corner peeking. Hit profile tests pass across render profiles, LODs and rig update rates. All damage and kill events are idempotent under replay. B03 proves zero steady managed allocations and simulation budget at eight bots.


## Complete baseline and match rules

[Gameplay baseline](../gameplay-baseline.md) supplies motor/ADS/spread/recoil/damage defaults, canonical muzzle, collision matrix and Compound Clear objective. [Simulation protocol](../simulation-protocol.md) fixes boundary ordering, actor nonblocking policy, same-tick trades and complete restoration state. These replace unspecified rounding and filter choices in this overview.

## M2 implementation references

Pure-source adapters (C0-v1 unchanged): `Simulation/Motor` (kinematic capsule + `InputEdgeBuffer`), `Simulation/Combat` (weapon/damage/hitscan/recoil), `Simulation/Pose/GameplayPoseEvaluator` (hit capsules + canonical muzzle), `Presentation/View/ProceduralViewModel` (event-deduped cosmetic springs). Shipping composition uses `Simulation/Foundation/CombatHarnessSimulation` from `Bootstrap/Runtime/FoundationEntry` with `CompositionPolicy.RequireReal`. Pure evidence via `Tools/Test-Simulation.ps1`; milestone status in [M2 handoff](../milestones/m2-combat-harness/handoff.md).

## Local sprint slide — T072 / ADR-039

The owner requested a local slide (подкат) on 2026-11-14. Default: while moving forward at sprint speed, hold **Left Shift** and press **F**. The applied Sprint binding is the modifier and Slide is a separately rebindable action (default Key.F). F alone, an airborne actor, aiming/crouching/firing/reloading on entry, or insufficient sprint momentum cannot start it.

Authority runs a maximum 45-tick (0.75 s) slide at 60 Hz: 6.5 m/s entry, 4 m/s² deceleration, locked entry momentum direction, existing 1.2 m crouch capsule/hit pose and 1.05 m eye height. Normal swept collision remains active. Jump cancels; leaving ground or blocking horizontal motion ends it. It returns to standing only with headroom; otherwise crouch remains. A 60-tick cooldown starts on entry. Holding F does not retrigger; pause/focus clears pending input and death/reset/new life clears motion state. Camera and procedural rig read copied authority movement; no camera shake/roll or authored animation is added.

Paths: `Presentation/Input/LocalPlayerInput.cs` → `Game/Session/LocalPlayerCommand.cs` → Bootstrap local sampler → `OfflineGameSession.Step` → `CombatHarnessSimulation.Step` → `Simulation/Motor/KinematicMotor.cs`. The local envelope keeps C0/N1 command bytes frozen and can trigger only for a fresh accepted command for the current human life. Historical online prediction/transport cannot initiate slide; online feature/protocol expansion is deferred. `LocalMovementView` carries copied stance/eye/slide state to `ProductSessionView` and `ProductActorPresentation`.

Legacy schema-2 settings gain Slide=Key.F when free. If F is already customized, Slide=Unbound preserves that control; choose a key in Settings. New defaults/rebinding/persistence are covered alongside motor eligibility, momentum/duration/cooldown, collision/ceiling/jump, life reset, real InputSystem and PhysX/quality checks. See [ADR-039](../decisions.md#adr-039--accepted-local-authoritative-sprint-slide-t072), [M8 handoff](../milestones/m8-runtime-composition/handoff.md) and [playtest guide](../playtest-guide.md) for actual build/test disposition. The historical 7cd7e81 archive has no slide.
