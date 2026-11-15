# Feature: offline Team Deathmatch

Status reviewed 2026-11-14 from 84befb9 / a54ed83: authoritative lifecycle/respawn, enemy filtering/path following, safe spawn ranking/deadlines and T070 finish/results/replay capture software are built into `7cd7e81`. Catalogs supply ≥12 slots/team. Real-map mixed-team/repeated-life/full-match evidence, remaining native-navigation publication and reference capacity are Pending per [audit](../product-state-audit.md). Use the [playtest guide](../playtest-guide.md); T065 retains full match acceptance and historical software closure does not accept it.

## Local release boundary

The owner confirmed on 2026-11-14 that this first version is local play with bots only. The human and 9/11 bots run in one offline client process. No online/LAN multiplayer, hosted service, separate server or server purchase is required. Existing network/server-compatible architecture does not add an online acceptance gate for this release.

## Player flow and roster

Select map → Team Deathmatch → 5v5 or 6v6 → load → play → final results → Play again or Main menu. Default 5v5: Team 1 is one human plus four bots, Team 2 five bots (10 actors / 9 bots). 6v6: one human plus five allied bots versus six bots (12 actors / 11 bots). Team membership stays fixed; no lobby, mid-match human joins, team switching, difficulty selector or remote multiplayer in this release. Bots have stable readable names and team markers independent of color.

Both human and bots use the same motor, four-slot loadout (rifle/pistol/knife/grenade), health, ammo, hitscan/melee/blast and friendly-body obstruction rules in [gameplay baseline](../gameplay-baseline.md). Friendly damage is zero. One grenade is refilled on respawn with the rest of the life kit (ADR-057). No kill streaks, pickups or progression systems. Personal scoreboard Score includes kill and damage-assist points ([ADR-059](../decisions.md#adr-059--accepted-personal-scoreboard-score-and-combat-score-badges)); that does not change team win totals. Mode-specific respawn refills are the explicit exception to Compound Clear's no-refill policy.

## Exact match rules

| Rule | Required behavior |
|---|---|
| Start | Loading completes only with real map queries/nav, verified visuals and complete roster; the match enters Playing with activeTicksElapsed=0 after Ready, then runs the 5 s pre-match countdown below before the clock starts |
| Pre-match | 300 ticks (5 s at 60 Hz) of frozen play in phase Playing: no human movement/fire/look, no bot planning, no respawns, no end-of-tick evaluation. Clock and timeout stay at 0 until it ends |
| Limits | 150 enemy kills or 54000 active simulation ticks at 60 Hz (15:00); HUD countdown uses ceiling of remainingTicks/60 |
| Kill credit | Each authoritative alive→dead transition increments victim deaths once; valid enemy killer gets one kill, its team one point, and personal Score += 100, even if killer also dies that tick; environmental/self death gives no team point |
| Personal Score | Scoreboard/results only (does not change 150 team-kill win). Killer = exactly 100. Non-killer damagers of that life get Score += HP they dealt. Env death: damagers get HP points, no 100. Killer does not also receive their damage sum |
| Score badges | Local human HUD: procedural kill ribbon + center feed (`ENEMY KILLED` / `ASSIST`) + ammo +N; authority awards only; no imported ribbon art |
| Duplicate/stale events | Session+EventId and victim life dedupe; old life or previous session events cannot change score, deaths or a respawned actor |
| End ordering | Resolve the entire tick's combat/death batch before evaluating limits. If either score ≥150, reason ScoreLimit; otherwise at 54000 reason TimeLimit. Higher final score wins; equal scores draw; no overtime. ScoreLimit takes precedence if both limits occur that tick |
| Simultaneous limit | Do not stop midway through shots or clamp points at 150. For example 149:149 with a trade ends 150:150 Draw; 149:149 with two valid kills by Team 1 ends 151:149 Team 1 |
| Finished | Freeze score, timer, actor commands, damage and respawns; publish one immutable results object and release gameplay cursor; late callbacks cannot change results |
| Pause/focus | Escape pauses the entire offline match including bots, respawn and timer; focus loss also pauses. Resume clears input edges and elapsed wall-clock debt. Scoreboard alone does not pause |
| Abort | Return to menu requires confirmation, stops session and releases resources; no fabricated win/results. Application Quit follows OS exit flow |

Active tick intervals are counted as [0,54000): after processing the 54000th active interval, elapsed=54000 and time expires. Shots from that final interval count; subsequent input does not. Existing same-tick shot trades and prior-tick dead-shooter rejection remain intact.

## Pre-match countdown

After a product-shell match enters Playing, the session holds a 5-second (300 ticks at 60 Hz) countdown, set by `SessionDefaults.PreMatchCountdownTicks` and read through `OfflineGameSession.PreMatchTicksRemaining`. The phase stays Playing; there is no new `GameMatchPhase` value and no Game-v1 change. During the countdown:

- the human cannot move, fire or look, because the local input gate is closed
- bots are not planned and issue no commands
- no respawns are scheduled
- `EvaluateEndOfTick` does not run, so `ActiveTicksElapsed`, the match clock and the timeout stay at 0

The harness still steps with no commands, so gravity and settling continue. The HUD shows "MATCH STARTS IN N", where N = ceil(remaining/60), and then "FIGHT!" during the first second of the live clock. The `OfflineGameSession` default is 0 ticks. Only `ProductShellHost` sets 300, so tests, benchmarks, `ProductTdmHost` and `ProductTdmBenchmark` are unchanged. The countdown adds 300 ticks to CompletedTick before the 54000-tick match clock starts. Decision record: [ADR-048](../decisions.md#adr-048--accepted-session-internal-pre-match-countdown). Tests: `PreMatchCountdownTests` (4). Real-player confirmation is Pending.

## Death and respawn

Death disables the life immediately. After 180 active ticks (3 seconds), attempt a safe spawn for that participant's team, never during Finished. Fresh life has 100 health, baseline ammo for both weapons, rifle selected, zero recoil/movement/action state, reset input edges and a fresh EntityId. ParticipantId and scoreboard row persist; LifeId increments. A new authority session is reserved for replay/new match, not each death. The human sees a respawn countdown with a fixed or last-death camera; no moving/firing dead body.

Team spawn candidates are validated map metadata. First reject non-navigable/overlapping/occupied slots. Rank remaining slots by no enemy direct line of fire within 40 m, then greatest nearest-enemy distance, then least-recently-used active tick, then stable slot ID. At least 2 m spacing from other spawned actors. Enemy LOS and spacing are authoritative and identical at all quality profiles. No temporary invulnerability or invisible cover.

If every candidate is unsafe, defer 30 active ticks and retry. **Relaxed fallback (ADR-047):** after `SpawnSafetyPolicy.SpawnRelaxAfterTicks` = 120 active ticks of failed safe search, the 40 m enemy line-of-fire rejection is dropped. The policy then picks a navigable, unoccupied slot with 2 m spacing, ranked by farthest nearest enemy, then least-recently-used, then lowest slot ID. This is a deliberate deviation from the strict ranking above, so that stationary bots cannot keep every slot exposed indefinitely. The 30-tick retry cadence is unchanged.

After 600 active ticks since the original due time, the match aborts only if no navigable, unoccupied slot exists. The abort records `GameErrorCode.SpawnFailure` in the session's `TerminalFault` property (not part of the frozen IGameSession or Game-v1 surface), moves the session to Stopped and makes `Step` a no-op returning Ok. `ProductShellHost` then returns to the main menu with "No safe spawn available. Returning to the menu." Before this change the match silently stopped while the session stayed Playing, which soft-locked the player on "Awaiting a safe spawn". Teleporting into solid geometry is still forbidden. All pending respawns are cancelled on finish/abort. Maps must pass the spawn-pressure scenario so this is an exceptional failure path. Decision record: [ADR-047](../decisions.md#adr-047--accepted-relaxed-spawn-fallback-and-recoverable-spawn-failure). Tests: `SpawnRelaxedFallbackTests` (3).

## Team bot behavior and capacity

T057 extended `CombatAiDirector` to 9/11 bots, enemy filtering and corner/reload handling behind the frozen planner seam; real-map acceptance remains pending. The required behavior is: Each bot evaluates living enemies, never allied actors, through bounded range/FOV/occlusion and reaction delay. Team membership is knowledge; enemy position is available to gameplay for queries, but cannot become an AI target without its sensing rules. Clear visible-target state on dead/out-of-range/occluded targets; deferred sensing ages knowledge and forbids shooting stale visibility.

**Bot navigation (2026-11-15):** `UnityNavMeshBakeInstaller` now winds each triangle to face up, using `UnityNavMeshBakeCodec.BuildUpFacingTriangleIndices`. The native bake had been clockwise from above, so it was installed face-down and produced an effectively empty NavMesh; bots failed every path request and idled at spawn. This is a code-reading hypothesis that has not been confirmed on a real player. The product composition enables `CompoundMapRuntime.AllowPackagedPathFallback`: on a sample miss or native failure, path requests fall back to the packaged nav graph, with fallback corners capped at 120. Diagnostics counters are `NavStartSampleMisses`, `NavGoalSampleMisses`, `NavCalculatePathFailures`, `NavPartialPaths`, `NavFallbackPathsUsed` and `NavFallbackPathsFailed`. The client logs a `SHOOTER_AI_SUMMARY` line to Player.log every 300 ticks. Tests: PlayMode `ProductBotNavigationTests` (not yet run in Unity) and xUnit `NavBakeInstallWindingTests`. Real-player bot movement is Pending.

Use round-robin scheduling across up to 11 bots; keep a **global** maximum 16 vision rays and 2 new path requests per tick initially. Each bot receives a sensing opportunity within 12 active ticks under the full roster; record actual deferrals/starvation and reject starvation. Do not run a separate full query budget per team. Budget changes need workload/ADR review, not hidden scaling. Preserve reaction delays; avoid perfect aim tracking through walls.

Bots patrol or move toward contested destinations when no enemy is known; follow actual path corners instead of walking directly at a goal through cover. Treat partial/failed/stale paths as explicit replans; cancel old tickets/cover claims on death, life reset, finish and abort. Read actual health, ammo, reload/action completion and position each tick. A blackboard refill is never an authority reload. Keep persistent command sequences per life; indexing commands by destination-array position cannot reset sequence numbers every tick. Test these current-code integration risks explicitly.

## HUD and results data

In play show Team 1/Team 2 scores, 150 target, time remaining, team marker, health/ammo/action and respawn state. Hold Tab for live team tables; use remapped binding if changed. At finish show Victory/Defeat/Draw from the human team's perspective, finish reason, map, team size, elapsed time and final team scores. Each team's table lists name, Human/Bot, Score, kills and deaths; highlight local player. Sort by Score descending, kills descending, deaths ascending, ParticipantId ascending. Results expose Play again (same map/mode/team size; fresh session) and Main menu. Results survive unloading the map; no continuing background match.

Acceptance IDs TDM01–TDM08 and real-player scenarios are in [M9 validation](../milestones/m9-team-deathmatch/validation-acceptance.md). Exact boundary tests use short test-only rule fixtures; release UI always uses 150/15 minutes. Test hooks cannot silently alter production rules or benchmark workloads.
