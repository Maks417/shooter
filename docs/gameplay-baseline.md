# Gameplay baseline, objective and collision matrix

These are the frozen motor, slot and layer defaults, mirrored in [`fixtures/gameplay-defaults.json`](fixtures/gameplay-defaults.json). Changing them needs tests and a version bump. The rifle and pistol rows are the baseline profile. Each named gun overrides them with its own per-weapon stats in `Content/Weapons/*.weapon.json` ([ADR-102](decisions.md#adr-102--accepted-named-real-world-arsenal-random-spawn-loadouts-and-muzzle-flash-fix), ADR-118; see [combat](features/combat.md)). Team rules are in [Team Deathmatch](features/team-deathmatch.md).

## Motor, camera and weapons

| Setting | Default |
|---|---|
| Tick / gravity / terminal fall | 60 Hz / −20m/s² / −35 m/s |
| Stand/crouch capsule | height1.8/1.2m, radius0.32m, position at foot center |
| Eye height stand/crouch | 1.65/1.05m above feet |
| Walk/sprint/crouch speed | 3.5/5.5/2.0 m/s; ADS speed multiplier0.65 |
| Ground accel/decel; air accel | 20/25m/s²; 5m/s² |
| Jump initial upward velocity | 5 m/s, grounded edge only; no coyote/buffer grace in slice |
| Tightness (stamina, ADR-060) | pool 0..1; sprint empty in 8 s; walk regen full in 5 s when Sprint not held; jump −0.25; slide entry −0.40; empty = walk only |
| Slope/step/skin/ground snap | 45 deg /0.25m /0.02m /0.08m; snap only falling/grounded, never jump ascent |
| Slide iterations / overlap recovery | max 4 /max 4; unresolved overlap stops and reports invalid spawn |
| Crouch physical transition | immediate validated resize at tick; eye/view blend0.12s cosmetic |
| Hip/ADS vertical FOV | 80/60 deg; user hip70–100; ADS keeps ratio60/80; blend0.15s |
| Rifle fire/reload/equip | auto interval6 ticks, reload144 ticks, equip24 ticks;30mag+90reserve |
| Pistol fire/reload/equip | semi interval10 ticks, reload108 ticks, equip18 ticks;15mag+45reserve |
| Knife (slot 2 / Digit3) | melee interval24 ticks, equip12; no ammo consume; range1.8 m; damage55; sprint fire allowed (ADR-057) |
| Grenade (slot 3 / Digit4) | one per life (1/0); cook on FirePressed, throw on release; fuse120 ticks; throw28 m/s with +8° loft, gravity 24 m/s² (about 34 m at 45°, ADR-111); blast5 m max100; self-damage yes, friendly0 (ADR-057) |
| Health / damage |100; rifle30/pistol25/knife55/grenade blast; head×4,torso×1,limbs×0.5 (ADR-063) |
| Damage rounding | floor(base×multiplier+0.5), clamp remaining health≥0 |
| Spread cone half-angle hip/ADS | rifle1.0/0.15 deg; pistol1.5/0.3 deg |
| Authoritative recoil impulse | rifle pitch+0.35 deg,yaw uniform±0.1 deg; pistol+0.55 deg,yaw±0.15 deg |
| Recoil model | per-tick offset decay exp(−8dt) before shot; impulses add, clamp pitch offset0..5 deg/yaw±2 deg; no angular velocity in v1 |
| Fire constraints | no sprint fire; ADS cancels sprint; fire/reload ends sprint; no ammo refill except restart |

Recoil angular velocities in full prediction state are zero in v1, reserved for a future versioned spring model. Visual kick may use springs, but does not alter these offsets. Raw input yaw/pitch plus decayed authoritative offset defines aim; apply spread once. Uniform solid-angle cone: u,v from independent RNG samples in [0,1), cosθ=1−u(1−cosα), φ=2πv, construct basis from aim/up with a deterministic alternate axis near vertical. Spread/recoil use named shot substreams, not shared global RNG. No movement spread/bloom in v1.

Gameplay muzzle default is eye-relative (x=+0.18,y=−0.15,z=+0.45)m in yaw/pitch aim frame. It is independent of optional visual handedness, FOV and body LOD. Canonical weapon definition refines muzzle/socket transforms only through a versioned test update. ADR-112: the eye ray resolves the hit to 300 m against static cover and enemy hit capsules; the eye→muzzle segment cannot pass through static cover, and the muzzle is otherwise only the tracer origin. Enemy bodies block bullets; teammates do not, and friendly damage is zero. Knife melee and one cook/throw grenade per life are in the four-slot loadout (ADR-057). Body armor is [ADR-110](decisions.md#adr-110--accepted-body-armor-100-points-torso-and-limbs). Penetration and extra throwable types are deferred.

## Diagnostic mode: Compound Clear v1

This is the original one-player-versus-eight-bots objective mode on the 256 m compound. It remains diagnostic and regression scope only, and the release selector exposes only TDM. The rules in brief:
- no respawn;
- the player wins when all bots are dead and the player holds the 3 m objective zone at (96, 0, 96) for 120 ticks;
- the player's death is an immediate loss.

Its fixtures stay frozen.

## Layer and query matrix

Layer names/indices are fixed project settings under integrator ownership: WorldSolid=8, ActorMotor=9, ActorHit=10, Cosmetic=11, ObjectiveTrigger=12, SensorTrigger=13. Other layers excluded from gameplay queries unless explicitly reviewed. Projectiles absent in hitscan slice.

| Query / collision | WorldSolid | ActorMotor | ActorHit | Cosmetic | Objective/Sensor |
|---|---|---|---|---|---|
| Motor sweep/ground/recovery | yes | no | no | no | no |
| Bullet obstruction | yes | no | yes (canonical math capsules) | no | no |
| AI vision | yes | no | no; test target points analytically | no | no |
| Foot IK/audio occlusion | yes | no | no | no | no |
| Objective occupancy | analytic zone/feet test | no | no | no | no physics trigger dependency |
| Cosmetic ragdoll rigidbodies | yes | no | no | capped cosmetic policy | no |

Actors do not physically push/block each other in slice; damage capsules are mathematical authoritative volumes, no extra live collider needed. ActorMotor engine layers exist for diagnostics, not duplicate simulation. All gameplay queries ignore triggers. Foliage/trim never occludes authoritative vision unless represented in WorldSolid and all visual profiles. Stable static IDs derive from logical world slot, never runtime instance ID. Surface tags Concrete=1,Metal=2,Soil=3,Wood=4,Polymer=5,Fabric=6,Flesh=7. No visual material code decides damage resistance.

## Validation

Test exact damage rounding (rifle limb 15, pistol limb 12.5→13), cadence at tick 0 and later allowed ticks, reload cancellation order, stance ceiling failure, same-tick death precedence and restart state. World checks enforce valid spawn/capture routes at all fixture seeds. These choices are accepted defaults and may be tuned only with synchronized definitions/fixtures.
