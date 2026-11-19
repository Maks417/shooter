# Feature: offline Team Deathmatch

The match rules live here. Authority lives in `Simulation/TeamDeathmatch` and in `OfflineGameSession`. Bot behavior is in [combat AI](ai.md), and maps are in [maps](tdm-map-layouts.md). The human and every bot run in one offline client process ([ADR-076](../decisions.md#adr-076--accepted-offline-only-scope-and-bounded-local-evidence)).

## Player flow and roster

Select map → Team Deathmatch → 5v5, 8v8, 10v10 or 15v15 → load → play → final results → Play again or Main menu. Default 5v5: Team 1 is one human plus four bots, Team 2 five bots (10 actors / 9 bots). Larger sizes follow the same pattern (one human on Team 1; bots = `TeamSize×2 − 1`, up to 29 bots / 30 actors at 15v15). Team membership stays fixed; no lobby, mid-match human joins, team switching, difficulty selector or remote multiplayer in this release. The human display name stays `Player`. Bots get unique short first names from a fixed pool (`BotDisplayNames`, ≥29 names), assigned deterministically from the match seed for scoreboard/kill-feed; team markers stay independent of color.

Both human and bots use the same motor, four-slot loadout (rifle/pistol/knife/grenade; the human's primary, its sight and sidearm come from the Armory, bots draw random guns and sights — [ADR-107](../decisions.md#adr-107--accepted-armory-loadouts-and-reusable-rail-mounted-sights)), health, ammo, hitscan/melee/blast and friendly-body obstruction rules in [gameplay baseline](../gameplay-baseline.md). Friendly damage is zero. One grenade is refilled on respawn with the rest of the life kit (ADR-057). Dropped primaries can be picked up ([ADR-105](../decisions.md#adr-105--accepted-dropped-primary-weapons-and-g-pickup)); a finished match feeds the local career ladder ([ADR-108](../decisions.md#adr-108--accepted-career-ranks-unlocks-buffs-and-local-service-record)), whose rank unlocks guns, sights and small human-only gun buffs. Personal scoreboard Score includes kill and damage-assist points ([ADR-059](../decisions.md#adr-059--accepted-personal-scoreboard-score-and-combat-score-badges)); that does not change team win totals. Life-scoped multi-kill / headshot **HUD badges** are presentation feedback ([ADR-073](../decisions.md#adr-073--accepted-life-scoped-multi-kill-and-headshot-hud-badges)); a headshot enemy kill adds a +20 personal Score bonus ([ADR-097](../decisions.md#adr-097--accepted-headshot-kill-score-bonus)).

## Exact match rules

| Rule | Required behavior |
|---|---|
| Start | Loading completes only with real map queries/nav, verified visuals and complete roster; the match enters Playing with activeTicksElapsed=0 after Ready, then runs the 5 s pre-match countdown below before the clock starts |
| Pre-match | 300 ticks (5 s at 60 Hz) of frozen play in phase Playing: no human movement/fire/look, no bot planning, no respawns, no end-of-tick evaluation. Clock and timeout stay at 0 until it ends |
| Limits | 150 enemy kills or 54000 active simulation ticks at 60 Hz (15:00); HUD countdown uses ceiling of remainingTicks/60 |
| Kill credit | Each authoritative alive→dead transition increments victim deaths once; valid enemy killer gets one kill, its team one point, and personal Score += 100, even if killer also dies that tick; environmental/self death gives no team point |
| Personal Score | Scoreboard/results only (does not change 150 team-kill win). Killer = 100, plus 20 when the fatal hit was to the head (ADR-097). Non-killer damagers of that life get Score += HP they dealt. Env death: damagers get HP points, no 100. Killer does not also receive their damage sum |
| Score badges | Local human HUD: procedural kill ribbon + center feed (`ENEMY KILLED` / `DOUBLE KILL` / `TRIPLE KILL` / `MULTI KILL` / `ASSIST` / extra `HEADSHOT`) + ammo +N (a headshot adds a `+20 HEADSHOT` feed row and 20 to the ribbon/ammo total); life-scoped streak resets on local death; authority awards only; no imported ribbon art; no streak Score bonus |
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

Death disables the life immediately. After 180 active ticks (3 seconds), attempt a safe spawn for that participant's team, never during Finished. The fresh life has 100 health and 100 armor ([ADR-110](../decisions.md#adr-110--accepted-body-armor-100-points-torso-and-limbs)), and the life loadout with full ammo and the primary selected. It also gets zero recoil/movement/action state, reset input edges and a fresh EntityId. ParticipantId and scoreboard row persist; LifeId increments. A new authority session is reserved for replay/new match, not each death. The human sees a respawn countdown with a fixed or last-death camera; no moving/firing dead body.

Team spawn candidates are validated map metadata. First reject non-navigable/overlapping/occupied slots. Rank remaining slots by no enemy direct line of fire within 40 m, then greatest nearest-enemy distance, then least-recently-used active tick, then stable slot ID. At least 2 m spacing from other spawned actors. Enemy LOS and spacing are authoritative and identical at all quality profiles. No temporary invulnerability or invisible cover.

If every candidate is unsafe, defer 30 active ticks and retry. **Relaxed fallback (ADR-047):** after `SpawnSafetyPolicy.SpawnRelaxAfterTicks` = 120 active ticks of failed safe search, the 40 m enemy line-of-fire rejection is dropped. The policy then picks a navigable, unoccupied slot with 2 m spacing, ranked by farthest nearest enemy, then least-recently-used, then lowest slot ID. This is a deliberate deviation from the strict ranking above, so that stationary bots cannot keep every slot exposed indefinitely. The 30-tick retry cadence is unchanged.

After 600 active ticks since the original due time, the match aborts only if no navigable, unoccupied slot exists. The abort records `GameErrorCode.SpawnFailure` in the session's `TerminalFault` property (not part of the frozen IGameSession or Game-v1 surface), moves the session to Stopped and makes `Step` a no-op returning Ok. `ProductShellHost` then returns to the main menu with "No safe spawn available. Returning to the menu." Before this change the match silently stopped while the session stayed Playing, which soft-locked the player on "Awaiting a safe spawn". Teleporting into solid geometry is still forbidden. All pending respawns are cancelled on finish/abort. Maps must pass the spawn-pressure scenario so this is an exceptional failure path. Decision record: [ADR-047](../decisions.md#adr-047--accepted-relaxed-spawn-fallback-and-recoverable-spawn-failure). Tests: `SpawnRelaxedFallbackTests` (3).

## Team bot behavior and capacity

Bots plan behind the frozen `ITeamBotPlanner` seam ([combat AI](ai.md)). Their rules:
- **Targets.** A bot evaluates living enemies only, never allies, through bounded range, FOV, occlusion and reaction delay.
- **Knowledge.** Team membership is knowledge, but an enemy becomes a target only through sensing. Deferred sensing ages that knowledge and never shoots on stale visibility.
- **Query budget.** One global budget is shared by both teams and every size up to 29 bots ([ADR-101](../decisions.md#adr-101--accepted-selectable-team-sizes-5--8--10--15)): ≤16 vision rays and ≤2 new paths per tick, with round-robin scheduling. Every bot gets a sensing opportunity within `MaxBots` active ticks, and deferrals are recorded. Any budget change needs an ADR.
- **Movement.** Bots follow real path corners. Partial, failed or stale paths cause an explicit replan.
- **Cleanup.** Tickets and cover claims are cancelled on death, life reset, finish and abort.
- **Real state.** Bots read real health, ammo and reload state. A blackboard refill is never a reload.
- **Commands.** Command sequences persist for each life.

**Navigation.** Native NavMesh packages are installed face-up (`UnityNavMeshBakeCodec.BuildUpFacingTriangleIndices`). Product composition enables `CompoundMapRuntime.AllowPackagedPathFallback`, so a sample miss or native failure falls back to the packaged graph, capped at 120 corners. Counters (`Nav*`) and a `SHOOTER_AI_SUMMARY` line every 300 ticks go to Player.log.

## HUD and results data

In play show Team 1/Team 2 scores, 150 target, time remaining, team marker, health/ammo/action and respawn state. Hold Tab for live team tables; use remapped binding if changed. At finish show Victory/Defeat/Draw from the human team's perspective, finish reason, map, team size, elapsed time and final team scores. Each team's table lists name, Human/Bot, Score, kills and deaths; highlight local player. Sort by Score descending, kills descending, deaths ascending, ParticipantId ascending. Results expose Play again (same map/mode/team size; fresh session) and Main menu. Results survive unloading the map; no continuing background match.

Acceptance IDs TDM01–TDM08 and real-player scenarios are in [M9 validation](../acceptance.md#m9--team-deathmatch). Exact boundary tests use short test-only rule fixtures; release UI always uses 150/15 minutes. Test hooks cannot silently alter production rules or benchmark workloads.
