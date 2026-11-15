# Feature: combat AI

Owner WP-07, proposed `Simulation/AI`; uses [world/navigation contracts](../contracts.md) and emits [combat commands](combat.md). No direct Animator/renderer access and no privileged damage calls.

Current pure implementation exists at `UnityProject/Assets/Shooter/Simulation/AI/CombatAiDirector.cs` and `PerceptionSystem.cs`, with the T082 aim/fire/cover helpers listed under [Combat behaviour](#combat-behaviour-t082-2026-11-15): capacity eight, one optional player target. M9 extends this under [Team Deathmatch](team-deathmatch.md#team-bot-behavior-and-capacity) to 9/11 bots and both teams; the initial global 16-ray/2-path limits remain shared. T042 explicitly tests real path corners, authoritative reload/state, persistent sequences, sensing fairness and stale-visibility cleanup. Historical eight-bot pure acceptance is not evidence for this new runtime capacity.

## Model

Per-agent blackboard: current goal, last seen/heard enemy positions and ticks, confidence, cover reservation, health/ammo, path ticket and stuck timer. Small explicit state machine: Idle/Patrol → Investigate → Engage → SeekCover → Reload/Reposition → Search → Patrol; Dead terminal. Score utilities inside states for cover and threat choice, rather than building a general behavior-tree editor.

Perception uses distance, FOV and authoritative occlusion rays to canonical target points. Query budget eight bots: ≤16 vision rays per tick globally, stagger candidate checks at 5 Hz, urgent damage stimulus next tick, hearing/decision update 10 Hz, motor command every tick. Budget exhaustion defers sensing with age tracking, never marks hidden targets visible. State stores last-known information; no access to unseen exact player position. Generated sound stimuli include source class, tick, position and radius; line-of-sight and distance attenuate hearing using gameplay data.

Cover candidates are generated from cover proxies/nav edges with valid capsule stance and peek positions. Score travel, threat occlusion, exposure and reservations; use two-phase claim/release keyed by entity ID. Aim uses reaction delay (initial 250–500 ms), bounded angular acceleration, seeded uncertainty and accuracy recovery; exact tuning is data and identical across quality profiles. AI commands use normal ammo, fire cadence, aim and movement rules.

## Scheduling and fail-safes

Queue path requests with priority, max two new path solves/tick initially; poll through INavigation. Retire stale paths on revision change, release reservations on death/despawn/cancel. Stuck if progress <0.2 m over 2 s while movement requested: attempt replan, change cover, then hold/search after three failures with diagnostic counter. Do not teleport bots to hide path bugs. Replan cooldown and deterministic tie-breaking avoid synchronized spikes. Pure decision code may jobify later; engine navigation queries stay in adapter constraints.

## Implementation (M3)

`UnityProject/Assets/Shooter/Simulation/AI` — `CombatAiDirector`, `PerceptionSystem`, `CoverReservationBoard`. Emits normal `InputCommand` values; shipping `CompositionPolicy.RequireReal` rejects test fakes. Milestone status: [M3 handoff](../milestones/m3-generated-compound/handoff.md).

**Navigation note (2026-11-15):** the Unity NavMesh installer winds triangles face-up, and the product client falls back to the packaged nav graph on sample misses or native path failures. Details, counters and tests are in [Team Deathmatch bot navigation](team-deathmatch.md#team-bot-behavior-and-capacity). Real-player bot movement is Pending.

## Combat behaviour (T082, 2026-11-15)

[T082](../task-supplement.md#t082--solid-fps-bot-combat-behaviour-2026-11-15) implements the aim, hearing and cover parts of the model above inside the unchanged budgets: 16 AI rays and 2 new paths per tick globally, the Game-v1 `ITeamBotPlanner` seam and plain C0 `InputCommand`s. All tuning is data in `Simulation/AI/AiTuning.cs`. Per-bot variation comes from the `AiNoise.cs` SplitMix hash of entity id, tick bucket and salt. No RNG is used, so identical recordings give identical commands.

- **Aim** (`AimController.cs`). Turning has a bounded rate and acceleration: about 420°/s in combat, with a time-optimal stop.
  - Bots aim at a seeded error point. The miss distance starts at about 1.1 m when a target is acquired and recovers over about 0.7 s, scaled by a per-bot skill factor between 0.8 and 1.25.
  - The settled error grows by 0.9 cm per metre of range. Own movement, target motion and a 0.5 s flinch after being hit add to it.
  - The muzzle-offset correction is kept.
- **Reaction.** The cold-acquisition delay is 250–500 ms. Range adds up to 200 ms for distant targets, and switching targets mid-fight takes 133–216 ms.
  - A target seen again within 0.5 s keeps its reaction and accuracy state. The old code re-armed the delay on any one-tick dropout.
- **Trigger** (`FireDiscipline.cs`). A burst starts only when aim is within about a torso half-width (at least 1.5°). A burst continues within 2.5× that tolerance.
  - Close range (<12 m): sustained fire.
  - Mid range: bursts of 3–6 rounds with 10–20 tick pauses.
  - Long range (≥30 m): crouched, stationary taps of 1–2 rounds with `AimHeld`.
- **Targeting.** The current target is sticky. A recent attacker is checked first. Every 20 ticks, staggered per bot, one other candidate is scanned, and it replaces the current target only if it is at least 30% closer.
  - Still at most one line-of-sight ray per bot per pass, and every bot gets a sensing opportunity within 12 ticks.
- **Engage movement.** Bots strafe across the line of fire within ±2 m of where the fight started. The strafe direction reverses on a seeded 0.4–1.2 s timer, at the 2 m leash, or when the bot has been blocked for 0.2 s.
  - Bots back off below 4 m and path toward the target beyond 55 m.
  - Entity ids divisible by 3 are "crouch shooters": they crouch and hold still during mid-range bursts.
- **Stimuli** (consumed at t+1). `OfflineGameSession` forwards `ShotAccepted` events to `ObserveGunfire` and `DamageApplied` events to `ObserveDamage`.
  - Gunfire within 45 m sets a noisy heard position (8% of the distance) and calm bots investigate it.
  - Damage from an attacker the bot isn't fighting sets the attacker's position (±1.5 m) and makes the bot turn to it and investigate. Damage while already engaging the attacker only adds flinch.
  - No rays are used for hearing.
  - Urgent goals (stimuli, last contact, cover, advancing) replace a stale route. They still respect the 30-tick replan cooldown and the path budget.
- **Search.** The last-seen point is projected along the target's observed velocity, capped at 1 s and 5 m. While searching or investigating a threat within 30 m, bots face the threat and walk instead of sprinting along the route.
- **Team.** On first sighting, calm allies within 30 m get a callout and investigate. When choosing a combat destination, bots skip destinations already targeted by two or more allies.
- **Health, ammo and cover** (`CoverFinder.cs`, [ADR-051](../decisions.md#adr-051--accepted-runtime-cover-probes-share-the-ai-ray-budget-t082)).
  - Below 35 health, if the bot is losing the trade, it retreats to cover. There is a 10 s cooldown before it can retreat again.
  - An empty magazine mid-fight starts a reload together with a cover search.
  - A tactical reload happens below 12 rounds once there has been no contact for 2 s.
  - In cover, the bot crouches facing the threat, reloads, holds for 2 s, then repositions.
  - Cover points come from runtime probes: up to 8 candidates 4–7 m away from the threat. A candidate qualifies when a ray from the threat's eye to a crouched chest at the point is blocked. Probes use leftover rays from the shared budget, at most 4 per tick.
  - Claims use stable quantized ids in `CoverReservationBoard` and are released on leaving cover, on path failure, on death and on cancel. Legacy `CoverSite` lists still work where a map provides them.
- **Planner lifetime.** A respawn now rebinds only the replaced bot through `TryReplaceLife`. Previously `RebuildBotPlanner` wiped every bot's targets, paths and cover several times a minute; it is now only a fallback.

New counters on `AiCounters`: `CoverProbeRays`, `CoverProbesFailed`, `BurstsStarted`, `StimuliHeard`, `DamageReactions`, `Callouts`, `Retreats`, `TacticalReloads`, `TargetSwitches`. `VisionRaysThisTick` includes cover-probe rays.

Known limits: strafing has no ledge query. The ±2 m leash around a navmesh point limits the risk of walking off edges but does not remove it. Probe points are checked for reachability only through the normal path request. Real-player feel is Pending; see the [M11 handoff](../milestones/m11-playable-release/handoff.md#t082--solid-fps-bot-combat-behaviour).

## Acceptance

Seeded scripted scenarios: occluded enemy never acquired via vision; hearing triggers investigation at last-known position; target loss decays confidence; bots reload safely without infinite loop; two bots do not permanently reserve same cover; unreachable destination exits bounded retry. Eight bots run 30-minute B03 soak with no unbounded path queue, query cap breaches or orphan reservations. Accuracy/reaction distribution matches configuration, not constant instant headshots. Same command/collision recording across profiles produces equivalent AI decisions within documented adapter nondeterminism. AI counters expose sense age, path latency, state changes, shot rate and CPU budget.
