# Gameplay baseline, objective and collision matrix

These are fictional game tuning defaults, not claims about real weapons. Owner WP-05; objective WP-05 with integrator; world consumes this before M3 planning. Numeric values are mirrored in [fixture defaults](fixtures/gameplay-defaults.json); changing them requires tests and version bump. No user clarification is needed to begin with this baseline.

Scope update 2026-11-13: this page and its frozen fixtures retain the historical Compound Clear baseline. New [Team Deathmatch](features/team-deathmatch.md) reuses motor/weapons/layers but defines separate team roster, respawn/refill, scoring and deadline rules through [Game-v1](game-session-contracts.md). These additions do not edit the old fixture or silently change Compound Clear/co-op behavior. The public release selector exposes only TDM; the old mode remains diagnostic/regression scope.

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
| Grenade (slot 3 / Digit4) | one per life (1/0); cook on FirePressed, throw on release; fuse120 ticks; throw18 m/s; blast5 m max100; self-damage yes, friendly0 (ADR-057) |
| Health / damage |100; rifle30/pistol25/knife55/grenade blast; head×4,torso×1,limbs×0.5 (ADR-063) |
| Damage rounding | floor(base×multiplier+0.5), clamp remaining health≥0 |
| Spread cone half-angle hip/ADS | rifle1.0/0.15 deg; pistol1.5/0.3 deg |
| Authoritative recoil impulse | rifle pitch+0.35 deg,yaw uniform±0.1 deg; pistol+0.55 deg,yaw±0.15 deg |
| Recoil model | per-tick offset decay exp(−8dt) before shot; impulses add, clamp pitch offset0..5 deg/yaw±2 deg; no angular velocity in v1 |
| Fire constraints | no sprint fire; ADS cancels sprint; fire/reload ends sprint; no ammo refill except restart |

Recoil angular velocities in full prediction state are zero in v1, reserved for a future versioned spring model. Visual kick may use springs, but does not alter these offsets. Raw input yaw/pitch plus decayed authoritative offset defines aim; apply spread once. Uniform solid-angle cone: u,v from independent RNG samples in [0,1), cosθ=1−u(1−cosα), φ=2πv, construct basis from aim/up with a deterministic alternate axis near vertical. Spread/recoil use named shot substreams, not shared global RNG. No movement spread/bloom in v1.

Gameplay muzzle default is eye-relative (x=+0.18,y=−0.15,z=+0.45)m in yaw/pitch aim frame. It is independent of optional visual handedness, FOV and body LOD. Canonical weapon definition refines muzzle/socket transforms only through a versioned test update. Eye ray picks target to300m; muzzle→target ray cannot pass through static cover. Enemy/friendly bodies block bullets; friendly damage is zero. Knife melee and one cook/throw grenade per life are in the four-slot loadout (ADR-057). Armor, penetration and extra throwable types remain deferred.

## Mode: Compound Clear v1

One player Team1 versus eight bots Team2, no respawns or reinforcement. Win when all eight bots dead and living player occupies the objective zone continuously for 120 ticks. Any living bot entering zone or player leaving resets capture progress. Player death gives Loss immediately. No time limit, waves, pickups, progression or save checkpoint. Objective completion is frozen once terminal; restart is explicit UI action.

Map coordinates X/Z −128..128m; +Y up. Objective center (96,0,96), horizontal radius3m, vertical test player feet within 2m of zone ground. Player spawn at (−104,0,−104) facing +Z. Candidate bot positions: (−48,0,−32),(0,0,−48),(48,0,−16),(−32,0,32),(16,0,32),(64,0,48),(48,0,96),(96,0,64); generator samples ground and searches nearest valid nav point in0.5m rings up to8m, ties X then Z. If none, reject world seed. No initial bot has unobstructed shot ray to player spawn; planner must insert generated static cover or reject seed, not invent runtime invisibility.

Restart cancels nav tickets, releases cover/FX/content leases, clears event queues/history and allocates a new AuthoritySessionId. Reset all actors/ammo/health/objective/random streams from identical match seed. Entity IDs may restart only in a new authority session. World content reused by verified handles. Multiplayer N3 is private cooperative version of same mode: up to4 Team1 players, eight Team2 bots, no respawn; loss when all players dead, any living player can capture; player joining after Playing spectates until restart. This avoids undefined mid-match reinforcement. Disconnect removes actor and releases claims; if no active players remain, stop session. PvP and eight-player capacity remain later scope.

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

Test exact damage rounding for rifle limb15 and pistol limb12.5→13, cadence at tick0 and subsequent allowed ticks, reload cancellation order, stance ceiling failure, objective120-tick boundary, same-tick death precedence, restart state and cooperative join spectator rule. World checks enforce valid spawn/capture routes at all fixture seeds. These choices are accepted defaults and may be tuned only with synchronized definitions/fixtures.
