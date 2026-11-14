# M9 — Team Deathmatch and bot teams — validation and acceptance

**Current disposition — 2026-11-14: M9 integration incomplete.** Reducer tests and initial roster capacity pass in their scope; real repeated respawn/team AI/spawn safety and TDM07/08 are not accepted. Repairs: T053, T057–T058, T063, T065 in the [shared implementation plan](../../release-remediation-plan.md), with [FIX acceptance](../../release-remediation-validation.md). The [audit](../../product-state-audit.md) supersedes earlier readiness interpretations; original requirements, dated ledgers and raw evidence below are retained. No runtime assignment or worker lease is created here.

Current coverage, 2026-11-14: partial historical pure/build/diagnostic evidence exists; complete product acceptance is open. Scenario IDs below remain required, not blanket NotRun or PASS claims. See [audit evidence](../../product-state-audit.md) and [FIX matrix](../../release-remediation-validation.md); repaired-code checks remain NotRun until implementation. Every run records exact source/build/toolchain/content/map/profile/roster/workload/duration and artifacts.

| ID | Scenario | Required result |
|---|---|---|
| TDM01 | Scoring and credit | Enemy kills score once; allies/self/environment do not add points; death increments once; invalid killer and duplicate/stale EventIds/life mappings cannot corrupt stats. |
| TDM02 | Deadline and trades | 53999/54000 active-interval boundary; zero-score timeout draw; 149:149 trade yields 150:150 Draw; multiple same-tick kills may exceed 150; score+time same-tick reason is ScoreLimit; exactly one finish publication. |
| TDM03 | Respawn lifecycle | 180 active ticks; fresh health/ammo/actions/EntityId and same participant stats; no dead command or previous-life fire; blocked/occupied/unsafe spawn deferral, 30-tick retry and 600-tick failure; all queues cancelled on finish. |
| TDM04 | Rosters and bots | Exactly 10 actors/9 bots and 12 actors/11 bots; both teams acquire only sensed enemies, allied body blocks without damage, wall occlusion clears stale fire; authoritative reload and monotonically advancing command sequences; real route corner following. |
| TDM05 | Query fairness and teardown | Global ≤16 vision rays/≤2 new paths per tick at 11 bots; sensing opportunity per bot within 12 active ticks; no leaked nav tickets/cover claims on death/replay; budget counters and starvation tests cover both teams. |
| TDM06 | Pause and finish freeze | Pause/focus loss stops clock/bots/respawn; resume clears fire edges and wall debt; final results remain identical after commands/callbacks/unload; abort never appears as victory. |
| TDM07 | Real complete matches | Real player 5v5 and 6v6 on compound reaches results, replay and return path; run at least one unshortened 15-minute timeout, plus score-limit scenario with real combat. Test-only accelerated fixtures do not count as complete player matches. |
| TDM08 | Quality and capacity | Same scripted commands/gameplay identities across Low/Balanced/High; full-roster release-player counters, allocations and duration recorded. Numeric/pure soak cannot certify reference FPS or human bot-quality review. |

## Commands and evidence

Existing commands from repository root: python docs/fixtures/verify_fixtures.py; python docs/verify_docs.py; ./Tools/Test-Contracts.ps1 -Offline; ./Tools/Test-Generation.ps1 -Offline; ./Tools/Test-Simulation.ps1 -Offline; ./Tools/Test-SourceProvenance.ps1. Offline advisory lookup is PendingNetwork, separate from execution results. Add the new product fixtures/projects to wrapper coverage through integrator and prove nonzero execution, rather than assuming wildcard inclusion.

On a fresh Unity checkout run ./Tools/Configure-Unity.ps1 with the locked installed EditorPath first. Then ./Tools/Test-Unity.ps1 -Mode EditMode and -Mode PlayMode; ./Tools/Build.ps1 -Target Client and -Target Server; ./Tools/Smoke.ps1 -BuildRunDirectory <actual-build-run>. Existing Smoke is the diagnostic ready/tick check and must remain distinct from new real-game acceptance. Compile-GameContent, Test-Maps, Test-Game and Package-Release exist with limited test/copy scopes recorded in the audit; T052–T065 complete genuine content/player/delivery behavior. Product TDM benchmark commands are not implemented. Record actual parameters/counts/failures before using a wrapper as a gate. Do not claim today's B01-only capture path supports full team workloads.

Retain raw XML/TRX/logs, versioned fixture/manifest/provenance reports, real player captures and machine/counter/thermal evidence where required. Use PASS only for measured checks, FAIL for actual failures, Pending/PendingHardware for unavailable execution, and NotRun for work not attempted. Screenshots/pure loops/docs hashes cannot replace actual Unity/player/performance or named human review. Detailed historical gates remain in their original handoffs.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m8-runtime-composition/index.md) · [Next pack](../m10-maps-shell/index.md).
