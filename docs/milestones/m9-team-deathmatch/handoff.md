# M9 — Team Deathmatch and bot teams — handoff and gate record

**Current disposition — 2026-11-14, reviewed at 59316d3:** M9 integration validation open: T053/T057/T058 lifecycle, enemy sensing/path/reload and spawn policy software landed in 59316d3; native-world mixed-team/full-match evidence and correct multi-match telemetry/soak remain (T067/T070/T065). See the [current audit](../../product-state-audit.md), [next playable-build tasks](../../next-playable-build.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence below remain; no new coding lease is created.

## Latest-commit review — 59316d3

T053/T057/T058/T063 software is committed in 59316d3; Game 38/38 and Simulation 101/101 pass in their scope. Real native-world mixed-team/spawn/full-match acceptance remains pending. T070 must implement finish/replay-aware multi-match soak and repair telemetry semantics; the current one-shot host cannot capture 30 active minutes across normal 15-minute matches. T066/T067 precede player evidence; no hardware PASS is inferred.

### T058 / FIX07 software note — 2026-11-14

- `SpawnSafetyPolicy` + `TeamDeathmatchMatch.ScheduleDueRespawns`: reject blocked/occupied; rank by no enemy LOS within 40 m, nearest-enemy distance, LRU, slot ID; defer 30 ticks; abort at first-failure +600 without moving the deadline.
- Evidence: `Tools/Test-Maps.ps1 -Offline` PASS (22); `Game.Contracts.Tests` spawn suite PASS (23 total Game-v1). Unity editor absent → native bake/player **Pending**.
- Notes for T056: consume existing catalog spawn/combat destinations; do not restyle Presentation for spawn policy. T059: spawn-failure Stopped/Aborted must surface as recoverable shell error. T063: depot workload must use seed `460072031`; re-run Compile-GameContent after depot topology/spawn metadata changes before packaging workloads.

### T063 / FIX12 software note — 2026-11-14

- ADR-037: schemaVersion 2 `tdm-workloads.json` adds `TDM-5v5-depot-v1` / `TDM-6v6-depot-v1` (seed `460072031`) and soak IDs; compound rows unchanged.
- Product collector (`ProductTdmHost`/`ProductTdmBenchmark`, collector v4) + `analyze_tdm_benchmark.py` negatives; wrappers `Benchmark.ps1` / `Benchmark-Series.ps1` / `Benchmark-Product-Matrix.ps1` / `Benchmark-Soak.ps1`.
- Evidence: `python Tools/test_tdm_benchmark_analysis.py`; Game-v1 product workload tests. **TDM08 / twelve captures / soaks = PendingHardware.**
- Handoff to T064: use the same final candidate build/content identities; do not stamp HEAD onto arbitrary binaries; Package-Release must reject dirty/mismatched candidates.

## Historical closeout record — 2026-11-13

Historical 2026-11-13 software closeout (retained; superseded for current product acceptance): **CLOSED (software)**, 2026-11-13. Authoritative TDM reducer, 11-bot planner, and OfflineGameSession tick binding are on main. Unshortened human 15-minute/score-limit player matches and reference hardware counters remain Pending — not claimed PASS.

| Field | Historical recorded disposition |
|---|---|
| Tasks | T040–T043 |
| Pure TDM | Score/trade/draw/deadline/duplicate/stale covered by Game-v1 tests |
| AI | `CombatAiDirector.MaxBots=11` + `TeamBotPlanner` multi-enemy sensing; global ray/path budgets retained |
| Session | `OfflineGameSession.Step` installs respawns, plans bots, steps harness, processes deaths, evaluates finish |
| TDM07 | Pending — named unshortened player matches not run this assignment |
| TDM08 | PendingHardware (FIX12 tooling ready; reference captures/soaks not run) |

## Ledger

| Task | Status |
|---|---|
| T040 Match authority | **PASS** (pure) |
| T041 Life cycles | **PASS** (software; spawn deferral in reducer) |
| T042 Team AI | **PASS** (software; 9/11 capacity) |
| T043 Prove whole match | Partial — software binding PASS; full player matrix Pending |

[Release roadmap](../../playable-release-plan.md) · [Previous](../m8-runtime-composition/index.md) · [Next](../m10-maps-shell/index.md).
