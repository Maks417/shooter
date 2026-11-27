# Feature: FPS movement and combat

Movement and combat live in:
- `Simulation/Motor` (`KinematicMotor`, `InputEdgeBuffer`);
- `Simulation/Combat` (weapons, damage, hitscan, recoil, dropped weapons);
- `Simulation/Pose/GameplayPoseEvaluator` (hit capsules, canonical muzzle).

Shipping composition runs `CombatHarnessSimulation` under `CompositionPolicy.RequireReal`.

The frozen default numbers are in the [gameplay baseline](../gameplay-baseline.md). The tick order is in the [simulation protocol](../simulation-protocol.md). Team score, respawn and results are in [Team Deathmatch](team-deathmatch.md). Gameplay never depends on UI, presentation or quality tier.

## Movement

**Input.** Mouse look is sampled every frame, and commands carry aim at the tick. Button edges are consumed once, even when several ticks share one display sample. Raw delta, sensitivity, invert Y, rebinding and pause are all supported.

**Motor.** The motor is a kinematic capsule sweep: up to four slide iterations, 45° slope limit, 0.25 m step. It checks the ceiling before uncrouching, and crouch keeps the feet in place. Jump fires only on a grounded edge. Visual head bob never changes eye height.

**Tightness** (stamina, [ADR-060](../decisions.md#adr-060--accepted-local-authoritative-tightness-stamina)) is a 0..1 pool, full on spawn.

| Event | Effect |
|---|---|
| Continuous sprint | Empties the pool in 8 s |
| Jump | Costs 0.25 |
| Slide entry | Costs 0.40 |
| Empty pool | Forces walk speed and refuses jump and slide |
| Walking with Sprint released | Refills the pool in 5 s |

`LocalMovementView.Tightness01` drives the STA bar.

**Slide** ([ADR-039](../decisions.md#adr-039--accepted-local-authoritative-sprint-slide-t072)). While sprinting forward, hold Sprint (Left Shift) and press Slide (F, rebindable).

These cannot start a slide:
- F on its own;
- being airborne;
- aiming, crouching, firing or reloading;
- too little sprint momentum.

Slide numbers:
- lasts up to 45 ticks;
- 6.5 m/s entry, slowing at 4 m/s²;
- entry direction is locked;
- uses the 1.2 m crouch capsule and 1.05 m eye height;
- 60-tick cooldown;
- holding F never chains slides.

How a slide ends:
- Jump cancels it.
- Leaving the ground or a wall ends it.
- It returns to standing only with headroom.
- Death or a new life clears it.

Input path:
1. `LocalPlayerInput`
2. `LocalPlayerCommand` (a local envelope; C0 bytes stay frozen)
3. `OfflineGameSession`
4. `KinematicMotor`

Prone is deferred.

## Weapons

The four slots, with Q toggling to the last weapon:

| Key | Slot |
|---|---|
| 1 | Primary |
| 2 | Sidearm |
| 3 | Knife |
| 4 | One grenade per life |

**Named guns.** Every named gun is a data file with its own fire mode, cadence, magazine, reserve, damage, falloff and spread ([weapons](geometry-weapons.md#adding-or-tuning-a-weapon-adr-077), [ADR-102](../decisions.md#adr-102--accepted-named-real-world-arsenal-random-spawn-loadouts-and-muzzle-flash-fix)). Bots draw a random weighted primary and sidearm each life. The human spawns with the [Armory](game-shell.md) loadout.

**Firing rules:**
- Durations round up to ticks.
- Automatic fire repeats while held. Semi-auto and knife need a fresh press.
- Burst guns fire `burstCount` rounds.
- There is no fire while reloading, equipping or dead.

**Reloads.**
- An empty magazine with reserve ammo starts a reload automatically.
- Ammo transfers when the reload completes. Switching away first transfers nothing.
- `reloadPerRound` guns (the M870) load one shell per phase, and a trigger press with a shell loaded fires at once.

**Knife.** 1.8 m range, 55 damage, a 24-tick interval, no ammo.

**Grenade** ([ADR-057](../decisions.md#adr-057--accepted-knife-and-grenade-loadout-slots-t087), [ADR-111](../decisions.md#adr-111--accepted-knife-and-grenade-overhaul-one-handed-hold-cut-and-throw-half-map-grenade-range-sound-explosion-models)):
1. Holding fire cooks it on a 120-tick fuse.
2. Releasing throws it (28 m/s with an 8° loft, about 34 m).
3. The blast reaches 5 m. Self damage is on, friendly damage is zero.
4. After the throw, authority equips the primary.

**Shots** ([ADR-112](../decisions.md#adr-112--accepted-crosshair-true-hits-rig-aligned-hit-volumes-and-bots-that-use-map-cover-flank-and-move-smoothly)) are authoritative hitscan to 300 m.

How a shot resolves:
- The eye (crosshair) ray resolves the hit, nearest first, against static cover and hostile hit capsules.
- Teammates are skipped and friendly damage is zero.
- If the eye→muzzle segment crosses a wall, the shot is blocked.
- Otherwise the muzzle is only the tracer origin.

Other shot rules:
- Spread is sampled from named per-shot seeded streams.
- Authoritative recoil offsets aim; view kick is separate.
- No client-supplied impact is trusted.

**Pellets** ([ADR-118](../decisions.md#adr-118--accepted-m4a1-mk-16-m24-sws-and-m870-pellets-shell-reloads-and-geometryweapon-150)). A `pelletCount > 1` shot is one `ShotAccepted` and one recoil impulse. Each pellet gets its own eye ray, seeded channels (16 + 2p, 17 + 2p), impact and falloff damage. For the M870 that is 8 × 100 in a 2.6° cone, full damage to 10 m and down to 10 per pellet at 30 m ([ADR-127](../decisions.md#adr-127--accepted-m870-lethal-up-close-and-per-pellet-tracers)): up close two torso pellets kill a fresh, armored soldier.

**Damage** ([ADR-063](../decisions.md#adr-063--accepted-hit-region-damage-coefficients-one-shot-head)):
- health is 100;
- hit-region multipliers are head ×4, torso ×1, limbs ×0.5;
- rounding is `floor(base×mult+0.5)`;
- there is exactly one `DamageApplied` per accepted hit.

[Body armor](../decisions.md#adr-110--accepted-body-armor-100-points-torso-and-limbs) gives 100 points on every spawn, covering the torso and limbs. Armor takes `floor(d/2)` of each covered hit and blast; health takes the rest.

**Lethal on hit** ([ADR-125](../decisions.md#adr-125--accepted-bolt-action-snipers-kill-on-any-hit)). A gun flagged `lethalOnHit` (the M24 SWS and M-TAC 338) takes the target's remaining health with any hit, on any region and at any range. Armor is neither consulted nor spent.

Penetration and ricochet are deferred. Damage never targets render meshes.

**Career perks** ([ADR-119](../decisions.md#adr-119--accepted-30-rank-career-ladder-and-four-new-perks)) can narrow hip and pellet cones and shorten bolt and pump cycles, for the human only.

**Dropped weapons** ([ADR-105](../decisions.md#adr-105--accepted-dropped-primary-weapons-and-g-pickup), `DroppedWeaponField`).
- **When a gun drops.** A dying soldier drops their primary, with its magazine, reserve and mounted sight. Out-of-world deaths drop nothing.
- **How long it lasts.** It stays for 15 s of active time and blinks for the last 3 s.
- **Picking up.** Within 1.75 m the human presses G:
  - A different gun swaps in, and the old one drops.
  - The same gun tops up reserve, up to capacity.
- Bots never pick up guns.
- Respawn issues the life loadout again.

## Feedback

Feedback is presentation only, driven by authority events:

| Event | Feedback |
|---|---|
| Firearm `ShotAccepted` | A report from the [audio](audio.md) bank, a mesh muzzle flash, casing, tracer and view kick |
| Further pellet `Impact`s of the same shot | A tracer per pellet from the visible muzzle to its impact (`PelletTracerTracker`, [ADR-127](../decisions.md#adr-127--accepted-m870-lethal-up-close-and-per-pellet-tracers)); pellets that hit nothing draw none |
| Knife `ShotAccepted` | A slash, or a stab when the same tick hits |
| Grenade | A cook raise from `WeaponAction.Cooking`, then a cosmetic flying grenade after `ProjectileSpawned` |
| Impacts | Bounded particles (generated sparks and chips, licensed CC0 dust flipbook per [ADR-126](../decisions.md#adr-126--accepted-licensed-fire-and-smoke-flipbooks-and-burning-wrecks)) and generated decals |

**Local-only effects.** The full-screen muzzle overlay is for the local shooter only ([ADR-056](../decisions.md#adr-056--accepted-last-weapon-quick-switch-q-and-local-only-muzzle-overlay)).

**Safety and accessibility.** First-person rendering can never reveal enemies through walls. Accessibility can reduce shake, sway, flash and blur without changing authoritative spread.

## Acceptance

**Movement:**
- the same command script at 30, 60 and 144 FPS;
- slope, step, corner, crouch-ceiling, spawn-overlap and pause cases;
- every slide eligibility, collision and cooldown case.

**Weapons:**
- an exact shot count over 60 s for automatic fire;
- semi-auto ignores held repeats;
- ammo is conserved through reload, switch and death edges;
- close-wall and corner-peek blocking;
- hit profiles are equal across profiles, LODs and rig rates;
- idempotent damage and death under replay.

**Performance.** Full-roster runs show zero steady managed allocation within the simulation budget ([performance](../performance.md)).
