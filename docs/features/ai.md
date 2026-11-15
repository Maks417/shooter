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
  - Cover points come from runtime probes: up to 8 candidates 4–7 m away from the threat. A candidate qualifies when a ray from the threat's eye to a crouched chest at the point is blocked. T090 prefers a clear left/right peek toward the threat before claiming; blind-but-hidden points are fallback. Probes (including peeks) use leftover rays from the shared budget, at most 4 per tick.
  - Claims use stable quantized ids in `CoverReservationBoard` and are released on leaving cover, on path failure, on death and on cancel. Legacy `CoverSite` lists still work where a map provides them.
- **Planner lifetime.** A respawn now rebinds only the replaced bot through `TryReplaceLife`. Previously `RebuildBotPlanner` wiped every bot's targets, paths and cover several times a minute; it is now only a fallback.

New counters on `AiCounters`: `CoverProbeRays`, `CoverProbesFailed`, `BurstsStarted`, `StimuliHeard`, `DamageReactions`, `Callouts`, `Retreats`, `TacticalReloads`, `TargetSwitches`. `VisionRaysThisTick` includes cover-probe rays.

Known limits (narrowed by [T090](#bot-tactics-cover-peek-wall-hearing-ledge-strafe-route-spread-t090-2026-11-15)): probe reachability is still only via the normal path request. Real-player feel is Pending; see the [M11 handoff](../milestones/m11-playable-release/handoff.md#t082--solid-fps-bot-combat-behaviour).

## Awareness, team spread and lethality (T085, 2026-11-15)

[T085](../task-supplement.md#t085--bot-rear-awareness-team-spread-and-readable-time-to-kill-2026-11-15) answers an owner playtest of T082: bots ran after each other, did not notice enemies behind them, and killed too fast to read. Budgets, contracts and the planner seam are unchanged ([ADR-054](../decisions.md#adr-054--accepted-bot-footstep-hearing-wider-vision-and-a-readable-lethality-envelope-t085)).

- **Following each other (root cause).** Product patrol destinations are the map's ordered route corners. Each bot took `destinations[(id + cursor) % n]`, so teammates with consecutive ids walked the same route one corner apart, in a line. `ChooseSpreadDestination` now scores every destination: about 35 m legs preferred, −2 for each ally already heading within 12 m of it, −0.5 for each ally standing near it, a seeded per-bot preference, and never the last two picks. The goal is chosen once per arrival, not every tick. Combat destinations use the same scorer.
- **Ally noise.** Teammate gunfire used to send listeners to the teammate's own position. It now points at that teammate's last-seen enemy, or is ignored when it has none (a human teammate's shots are ignored). Gunfire, callouts and footsteps send at most 2 allies to one noise (within 10 m); the rest keep their route and face it.
- **Behind the back.**
  - Footsteps: every tick the planner derives each enemy's horizontal speed from its position. An enemy is audible from any direction within 24 m when sprinting (≥5 m/s), 13 m when running (≥2.6 m/s) and 5 m when walking, and always within 2.5 m. The heard point has 5% distance noise. A bot without a visible target turns toward it; a patrolling or searching bot investigates it. T090 adds wall occlusion beyond presence (see below).
  - A fresh footstep or damage stimulus newer than the last sighting wins the look over the older last-seen point.
  - An engaged bot hit twice within 1.5 s by an enemy it is not fighting (or once when below 50 health) breaks off for up to 0.75 s and turns on the attacker. Attacker priority in sensing then acquires it.
  - The vision cone widens from 90° to 120°.
- **Time-to-kill.** The rifle stays at 30 damage and 10 rounds/s for everyone (`combat.md`), so the bots' aim carries the change.
  - Accuracy recovery now starts when the reaction delay ends (the first possible shot), not at sighting. Before, most of the acquisition error had decayed during the reaction delay and the first shots hit.
  - Acquisition miss 1.5 m, settled miss 0.28 m + 0.5 cm/m, recovery 1 s, own-movement penalty 0.18 m, cold reaction 300–600 ms.
  - Measured by `BotLethalityTests` on the real combat harness (still, standing target, median of 9 bots, from sighting): 8 m about 1.6 s (T082: 1.0 s), 20 m about 2.5 s (1.5 s), 40 m about 3.9 s (2.4 s). The fastest kill after a bot's first shot is now 0.8 s, against 0.4 s before.
- **Readable damage (HUD).** `ProductSessionView` draws red chevrons around the reticle pointing at the attacker's position at the time of each authoritative hit taken: up to 4 at once, held 1.4 s, rotating as the player turns. The angle and fade math are `CombatHudVisual.DamageIndicatorAngle` and `DamageIndicatorAlpha`. This is presentation only.

New counters: `FootstepsHeard`, `ThreatTurns`, `CrowdedStimuliHeld`, `AllyGunfireIgnored`.

Known limits (narrowed by T090): footstep speed still comes from planner positions, so a teleport or respawn counts as one fast step and is heard at most once. Real-player feel is Pending.

## Bot tactics: cover peek, wall hearing, ledge strafe, route spread (T090, 2026-11-15)

[T090](../task-supplement.md#t090--bot-tactics-cover-peek-wall-hearing-ledge-strafe-route-spread-2026-11-15) closes the documented T082/T085 tactics gaps inside unchanged budgets ([ADR-070](../decisions.md#adr-070--accepted-bot-tactics-tranche-cover-peek-wall-hearing-ledge-strafe-route-spread-t090)).

- **Cover peek.** After a candidate hides a crouched chest from the threat, a leftover probe ray samples a left/right peek eye toward the threat. Peekable sites are claimed immediately; the first blind-but-hidden candidate is kept as fallback when the list ends. Peek rays share the ≤4/tick probe cap and the global 16-ray budget.
- **Wall-aware hearing.** Gunfire occlusion is a direct ear→source collision ray (not charged to the 16 AI rays) so stimulus handling stays synchronous. Footsteps beyond 2.5 m use leftover budgeted AI rays at most once per bot every 12 ticks (fail-open when deferred; last clear/blocked result reused while rate-limited). Presence always passes.
- **Ledge-safe strafe.** Before applying the strafe step, a downward ground probe at ±0.75 m along the strafe axis flips or cancels when the drop exceeds ~0.55 m or the probe has no footing while the bot still does. Open worlds with no ground under either sample fail-open. These probes are not vision rays (`AiRaysUsedThisTick` stays the budget meter).
- **Route-aware spread.** Destinations within ~9 m of an ally's remaining path corners take an extra −1 score (corners already on the blackboard; no new path solves). Goal/position penalties from T085 remain.

New counters: `CoverPeekRays`, `CoverPeekAccepted`, `HearingOcclusionRays`, `HearingOccluded`, `StrafeLedgeFlips`, `RouteCorridorPenalties`.

Known residuals: cover still has no authored map proxies; peek is a single side sample without exposure scoring over time; hearing occlusion uses the vision layer mask; footstep teleport spike remains; AI CPU 1.5 ms mean and owner feel review remain Pending.

## Knife and grenade loadout (T087, 2026-11-15)

When rifle then pistol magazines and reserves are empty, bots switch to knife (slot 2) inside ~2.5 m or spend their single grenade (slot 3) once against a visible target at ~8–22 m with a short cook, then fall back to knife. Respawn refills one grenade with the life kit ([ADR-057](../decisions.md#adr-057--accepted-knife-and-grenade-loadout-slots-t087)).

## Acceptance

Seeded scripted scenarios: occluded enemy never acquired via vision; hearing triggers investigation at last-known position; target loss decays confidence; bots reload safely without infinite loop; two bots do not permanently reserve same cover; unreachable destination exits bounded retry. Eight bots run 30-minute B03 soak with no unbounded path queue, query cap breaches or orphan reservations. Accuracy/reaction distribution matches configuration, not constant instant headshots. Same command/collision recording across profiles produces equivalent AI decisions within documented adapter nondeterminism. AI counters expose sense age, path latency, state changes, shot rate and CPU budget.
