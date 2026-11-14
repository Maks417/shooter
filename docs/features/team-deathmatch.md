# Feature: offline Team Deathmatch

Status reviewed 2026-11-14: Game-v1 reducer/session/11-bot code exists, but real lifecycle, safe spawns and team AI remain incomplete per [audit](../product-state-audit.md). Requirements below remain normative, not claims of passing coverage. [T053/T057/T058/T065](../release-remediation-plan.md) repair and prove the match; historical M9 software closure does not accept it.

## Player flow and roster

Select map → Team Deathmatch → 5v5 or 6v6 → load → play → final results → Play again or Main menu. Default 5v5: Team 1 is one human plus four bots, Team 2 five bots (10 actors / 9 bots). 6v6: one human plus five allied bots versus six bots (12 actors / 11 bots). Team membership stays fixed; no lobby, mid-match human joins, team switching, difficulty selector or remote multiplayer in this release. Bots have stable readable names and team markers independent of color.

Both human and bots use the same motor, rifle/pistol, health, ammo, hitscan and friendly-body obstruction rules in [gameplay baseline](../gameplay-baseline.md). Friendly damage is zero. No kill streaks, assists, pickups, grenades or progression. Mode-specific respawn refills are the explicit exception to Compound Clear's no-refill policy.

## Exact match rules

| Rule | Required behavior |
|---|---|
| Start | Loading completes only with real map queries/nav, verified visuals and complete roster; start at activeTicksElapsed=0 after Ready |
| Limits | 150 enemy kills or 54000 active simulation ticks at 60 Hz (15:00); HUD countdown uses ceiling of remainingTicks/60 |
| Kill credit | Each authoritative alive→dead transition increments victim deaths once; valid enemy killer gets one kill and its team one point, even if killer also dies that tick; environmental/self death gives no team point |
| Duplicate/stale events | Session+EventId and victim life dedupe; old life or previous session events cannot change score, deaths or a respawned actor |
| End ordering | Resolve the entire tick's combat/death batch before evaluating limits. If either score ≥150, reason ScoreLimit; otherwise at 54000 reason TimeLimit. Higher final score wins; equal scores draw; no overtime. ScoreLimit takes precedence if both limits occur that tick |
| Simultaneous limit | Do not stop midway through shots or clamp points at 150. For example 149:149 with a trade ends 150:150 Draw; 149:149 with two valid kills by Team 1 ends 151:149 Team 1 |
| Finished | Freeze score, timer, actor commands, damage and respawns; publish one immutable results object and release gameplay cursor; late callbacks cannot change results |
| Pause/focus | Escape pauses the entire offline match including bots, respawn and timer; focus loss also pauses. Resume clears input edges and elapsed wall-clock debt. Scoreboard alone does not pause |
| Abort | Return to menu requires confirmation, stops session and releases resources; no fabricated win/results. Application Quit follows OS exit flow |

Active tick intervals are counted as [0,54000): after processing the 54000th active interval, elapsed=54000 and time expires. Shots from that final interval count; subsequent input does not. Existing same-tick shot trades and prior-tick dead-shooter rejection remain intact.

## Death and respawn

Death disables the life immediately. After 180 active ticks (3 seconds), attempt a safe spawn for that participant's team, never during Finished. Fresh life has 100 health, baseline ammo for both weapons, rifle selected, zero recoil/movement/action state, reset input edges and a fresh EntityId. ParticipantId and scoreboard row persist; LifeId increments. A new authority session is reserved for replay/new match, not each death. The human sees a respawn countdown with a fixed or last-death camera; no moving/firing dead body.

Team spawn candidates are validated map metadata. First reject non-navigable/overlapping/occupied slots. Rank remaining slots by no enemy direct line of fire within 40 m, then greatest nearest-enemy distance, then least-recently-used active tick, then stable slot ID. At least 2 m spacing from other spawned actors. Enemy LOS and spacing are authoritative and identical at all quality profiles. No temporary invulnerability or invisible cover.

If every candidate is unsafe, defer 30 active ticks and retry. After 600 active ticks since the original due time, terminate with a spawn-failure diagnostic and return to a recoverable menu error; do not soft-lock or teleport into solid geometry. All pending respawns are cancelled on finish/abort. Maps must pass the spawn-pressure scenario so this is an exceptional failure path.

## Team bot behavior and capacity

Extend the current eight-bot, single-target `CombatAiDirector` behind the frozen planner seam. Each bot evaluates living enemies, never allied actors, through bounded range/FOV/occlusion and reaction delay. Team membership is knowledge; enemy position is available to gameplay for queries, but cannot become an AI target without its sensing rules. Clear visible-target state on dead/out-of-range/occluded targets; deferred sensing ages knowledge and forbids shooting stale visibility.

Use round-robin scheduling across up to 11 bots; keep a **global** maximum 16 vision rays and 2 new path requests per tick initially. Each bot receives a sensing opportunity within 12 active ticks under the full roster; record actual deferrals/starvation and reject starvation. Do not run a separate full query budget per team. Budget changes need workload/ADR review, not hidden scaling. Preserve reaction delays; avoid perfect aim tracking through walls.

Bots patrol or move toward contested destinations when no enemy is known; follow actual path corners instead of walking directly at a goal through cover. Treat partial/failed/stale paths as explicit replans; cancel old tickets/cover claims on death, life reset, finish and abort. Read actual health, ammo, reload/action completion and position each tick. A blackboard refill is never an authority reload. Keep persistent command sequences per life; indexing commands by destination-array position cannot reset sequence numbers every tick. Test these current-code integration risks explicitly.

## HUD and results data

In play show Team 1/Team 2 scores, 150 target, time remaining, team marker, health/ammo/action and respawn state. Hold Tab for live team tables; use remapped binding if changed. At finish show Victory/Defeat/Draw from the human team's perspective, finish reason, map, team size, elapsed time and final team scores. Each team's table lists name, Human/Bot, kills and deaths; highlight local player. Sort by kills descending, deaths ascending, ParticipantId ascending. Results expose Play again (same map/mode/team size; fresh session) and Main menu. Results survive unloading the map; no continuing background match.

Acceptance IDs TDM01–TDM08 and real-player scenarios are in [M9 validation](../milestones/m9-team-deathmatch/validation-acceptance.md). Exact boundary tests use short test-only rule fixtures; release UI always uses 150/15 minutes. Test hooks cannot silently alter production rules or benchmark workloads.
