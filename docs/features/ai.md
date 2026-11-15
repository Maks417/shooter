# Feature: combat AI

Owner WP-07, proposed `Simulation/AI`; uses [world/navigation contracts](../contracts.md) and emits [combat commands](combat.md). No direct Animator/renderer access and no privileged damage calls.

Current pure implementation exists at `UnityProject/Assets/Shooter/Simulation/AI/CombatAiDirector.cs` and `PerceptionSystem.cs`: capacity eight, one optional player target. M9 extends this under [Team Deathmatch](team-deathmatch.md#team-bot-behavior-and-capacity) to 9/11 bots and both teams; the initial global 16-ray/2-path limits remain shared. T042 explicitly tests real path corners, authoritative reload/state, persistent sequences, sensing fairness and stale-visibility cleanup. Historical eight-bot pure acceptance is not evidence for this new runtime capacity.

## Model

Per-agent blackboard: current goal, last seen/heard enemy positions and ticks, confidence, cover reservation, health/ammo, path ticket and stuck timer. Small explicit state machine: Idle/Patrol → Investigate → Engage → SeekCover → Reload/Reposition → Search → Patrol; Dead terminal. Score utilities inside states for cover and threat choice, rather than building a general behavior-tree editor.

Perception uses distance, FOV and authoritative occlusion rays to canonical target points. Query budget eight bots: ≤16 vision rays per tick globally, stagger candidate checks at 5 Hz, urgent damage stimulus next tick, hearing/decision update 10 Hz, motor command every tick. Budget exhaustion defers sensing with age tracking, never marks hidden targets visible. State stores last-known information; no access to unseen exact player position. Generated sound stimuli include source class, tick, position and radius; line-of-sight and distance attenuate hearing using gameplay data.

Cover candidates are generated from cover proxies/nav edges with valid capsule stance and peek positions. Score travel, threat occlusion, exposure and reservations; use two-phase claim/release keyed by entity ID. Aim uses reaction delay (initial 250–500 ms), bounded angular acceleration, seeded uncertainty and accuracy recovery; exact tuning is data and identical across quality profiles. AI commands use normal ammo, fire cadence, aim and movement rules.

## Scheduling and fail-safes

Queue path requests with priority, max two new path solves/tick initially; poll through INavigation. Retire stale paths on revision change, release reservations on death/despawn/cancel. Stuck if progress <0.2 m over 2 s while movement requested: attempt replan, change cover, then hold/search after three failures with diagnostic counter. Do not teleport bots to hide path bugs. Replan cooldown and deterministic tie-breaking avoid synchronized spikes. Pure decision code may jobify later; engine navigation queries stay in adapter constraints.

## Implementation (M3)

`UnityProject/Assets/Shooter/Simulation/AI` — `CombatAiDirector`, `PerceptionSystem`, `CoverReservationBoard`. Emits normal `InputCommand` values; shipping `CompositionPolicy.RequireReal` rejects test fakes. Milestone status: [M3 handoff](../milestones/m3-generated-compound/handoff.md).

**Navigation note (2026-11-15):** the Unity NavMesh installer winds triangles face-up, and the product client falls back to the packaged nav graph on sample misses or native path failures. Details, counters and tests are in [Team Deathmatch bot navigation](team-deathmatch.md#team-bot-behavior-and-capacity). Real-player bot movement is Pending.

## Acceptance

Seeded scripted scenarios: occluded enemy never acquired via vision; hearing triggers investigation at last-known position; target loss decays confidence; bots reload safely without infinite loop; two bots do not permanently reserve same cover; unreachable destination exits bounded retry. Eight bots run 30-minute B03 soak with no unbounded path queue, query cap breaches or orphan reservations. Accuracy/reaction distribution matches configuration, not constant instant headshots. Same command/collision recording across profiles produces equivalent AI decisions within documented adapter nondeterminism. AI counters expose sense age, path latency, state changes, shot rate and CPU budget.
