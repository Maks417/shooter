# M2 — Combat harness — handoff and gate record

Status: **CLOSED WITH USER WAIVER**, 2026-11-13. Max directed M2 implementation without further confirmations (“implement everything for M2”; “no confirmations”; “everything should just work”). [ADR-026](../../decisions.md#adr-026--accepted-m2-owner-waiver-of-remaining-hardwarereference-validation-and-milestone-close) records the administrative close. **Three of eight milestones are closed.** Software gates (C01–C04, V05/V06/V09 Unity scenarios, EditMode/PlayMode, client/server smoke) PASS. Reference-player budget remains **PendingHardware** and is Unverified/Waived for M2 only. Automated PendingHardware reports are unchanged. No AAA realism or reference-performance PASS is claimed. See [acceptance](validation-acceptance.md), closure evidence under `Artifacts/m2-combat-harness/owner-closure-20261113/`, and [entry](index.md).

| Field | Actual value |
|---|---|
| Owner / integrator / reviewer | Cursor M2 integrator (`bc-2637951e`); Unity smoke `bc-ff836dea`; V05/V09 scenarios `bc-6bff6f6f`; closeout `bc-457cec93`; workers `/root/m2_motor`, `/root/m2_combat`, `/root/m2_pose`, `/root/m2_view` |
| Source commit and upstream tag | Base `925d23f`; smoke HEAD `59bd8c6`; scenario evidence on docs HEAD `474427d`; software HEAD at close `97f10ef` |
| Editor/URP/toolchain hash | Unity 6000.3.28f1 / URP 17.3.0; toolchain SHA256 `9ac31fe82117043e2bba3bfb843dd1ca32834b15425188e96d6f3369e2c627a8` (unchanged); editor at `C:\Job\shooter\.local\Unity\6000.3.28f1\6000.3.28f1\Editor\Unity.exe` |
| Contract/schema/generator/protocol versions | C0-v1 frozen unchanged; specification-v1.1; predictionSchema 1; simulationHz 60; gameplay pose evaluator v1 |
| Seed/content/world/workload manifests | Fixture seed `460072030`; analytic static-world fakes for pure tests; Unity scenarios use `UnityWorldQueries` solids; shipping smoke empty-actor `CombatHarnessSimulation` |
| Client/server build artifacts | Rebuild PASS on `59bd8c6` — see evidence table |
| Test results and benchmark evidence | Pure contracts 71/71; M2 simulation 23/23; EditMode 13/13; PlayMode 11/11; client/server smoke 120 ticks each |
| Hardware/power/driver details | PresentMon diagnostic attempted — **PendingHardware** retained; no live HWiNFO/series; waived for M2 under ADR-026 |
| Accepted ADRs and known issues | Upstream ADR-017/024/025; **ADR-026** M2 hardware waiver; P05 soldier visual FAIL retained (not required for fake-world combat harness) |
| Gate status / reviewer / date | **CLOSED WITH USER WAIVER** — 2026-11-13 |

## Step status

- [x] C01 Motor and input: `Simulation/Motor` + `Tools/Simulation.Tests/Motor` (display FPS ≤1 cm; edge buffer).
- [x] C02 Weapon and damage state: `Simulation/Combat` + combat tests (cadence/semi/reload/damage rounding).
- [x] C03 Hit pose and obstruction: `Simulation/Pose` + hitscan muzzle/cover helpers; pose region tolerances.
- [x] C04 Presentation integration: `Presentation/View/ProceduralViewModel` event-deduped cosmetic springs/grip checks.
- [x] T027 defaults/layers: `GameplayDefaults` + TagManager layers 8–13 audited against fixture; Settings assets unchanged.
- [x] Unity EditMode/PlayMode + client/server build/smoke with `CombatHarnessSimulation` composition.
- [x] V05/V06/V09 Unity harness scenarios (cover/step overlays, two-weapon playable room, cross-profile hit volumes).
- [x] Reference-machine measurement attempted; stays PendingHardware; waived for M2 closure only under ADR-026.

## Shipping composition

`Bootstrap/Runtime/FoundationEntry` composes `CombatHarnessSimulation(world)` and `CompositionPolicy.RequireReal(world, navigation, simulation)`. Empty-actor mode preserves foundation smoke tick servicing. Test-only `StaticPlaneWorld` implements `IFakeAdapter` and is rejected by shipping policy.

## Evidence (repository-relative ignored Artifacts)

| Check | Result | Path |
|---|---|---|
| Pure contracts | 71/71 PASS | `Artifacts/m0-foundation/cb66e0026d0698994dc775d634392f99038d598a/20261113T181944159Z-c785efb2/` |
| M2 simulation (prior) | 21/21 PASS | `Artifacts/m2-combat-harness/cb66e0026d0698994dc775d634392f99038d598a/20261113T181934512Z-8bd4a7d1/` |
| M2 simulation (+cover) | 23/23 PASS | `Artifacts/m2-combat-harness/474427d77b6678ee651dfb344c5b0a26539a9b00/20261113T184547836Z-ef879cd3/` |
| Docs/fixtures | PASS | `docs/verify_docs.py`, `docs/fixtures/verify_fixtures.py` |
| Configure-Unity | PASS | `Artifacts/m0-foundation/59bd8c6b76ad6824d4369e8bca856cd431b226e4/configure-unity-20261113T182207522Z-492c9c04/` |
| Unity EditMode (smoke) | 10/10 PASS | `Artifacts/m0-foundation/59bd8c6b76ad6824d4369e8bca856cd431b226e4/tests-editmode-20261113T182308104Z-605117e3/` |
| Unity PlayMode (smoke) | 6/6 PASS | `Artifacts/m0-foundation/59bd8c6b76ad6824d4369e8bca856cd431b226e4/tests-playmode-20261113T182349783Z-c78d2dd5/` |
| Unity EditMode (+V05/V09) | 13/13 PASS | `Artifacts/m0-foundation/474427d77b6678ee651dfb344c5b0a26539a9b00/tests-editmode-20261113T184647877Z-5cb8df68/` |
| Unity PlayMode (+V06/V09) | 11/11 PASS | `Artifacts/m0-foundation/474427d77b6678ee651dfb344c5b0a26539a9b00/tests-playmode-20261113T185133931Z-8151ce05/` |
| Client build | PASS | `Artifacts/m0-foundation/59bd8c6b76ad6824d4369e8bca856cd431b226e4/build-client-20261113T182439530Z-cbdd9a99/` |
| Server build | PASS | `Artifacts/m0-foundation/59bd8c6b76ad6824d4369e8bca856cd431b226e4/build-server-20261113T182932012Z-22617664/` |
| Client smoke | PASS (120 ticks) | `Artifacts/m0-foundation/59bd8c6b76ad6824d4369e8bca856cd431b226e4/smoke-client-20261113T183109372Z-8e6e133d/` |
| Server smoke | PASS (120 ticks) | `Artifacts/m0-foundation/59bd8c6b76ad6824d4369e8bca856cd431b226e4/smoke-server-20261113T183123557Z-94800437/` |
| M2 Unity/smoke index | Recorded | `Artifacts/m2-combat-harness/59bd8c6b76ad6824d4369e8bca856cd431b226e4/unity-smoke-20261113T193220046Z/validation.json` |
| M2 V05/V06/V09 index | Recorded | `Artifacts/m2-combat-harness/474427d77b6678ee651dfb344c5b0a26539a9b00/v05-v09-scenarios-20261113T185222419Z/validation.json` |
| PresentMon configure | PASS (tool only) | `.local/perf-tools/presentmon-2.6.0/` via `Tools/Configure-PresentMon.ps1` |
| Reference diagnostic (+PresentMon) | PendingHardware | `Artifacts/m0-foundation/97f10ef88e17d25df62b8ddbb07f4ec18b8c59f8/benchmark-diagnostic-20261113T185709203Z-c04346e8/` |
| Hardware attempt record | PendingHardware retained | `Artifacts/m2-combat-harness/owner-closure-20261113/hardware-attempt.json` |
| Owner closure disposition | CLOSED_WITH_USER_WAIVER | `Artifacts/m2-combat-harness/owner-closure-20261113/closure-disposition.json` |

Wrapper runs write under `Artifacts/m0-foundation/<commit>/` by `Unity.Common.ps1`; M2 indexes link those paths. Scenario wave `sourceDirty=true` reflected uncommitted test sources on `474427d` during the run.

## Downstream handoff

Motor/combat/pose adapters and procedural view model are on main. Shipping bootstrap uses `CombatHarnessSimulation` and passes client/server empty-actor smoke. Dedicated Unity EditMode/PlayMode scenarios cover hit-volume overlays, cover obstruction, raised-platform step overlay, ceiling, two-weapon room and cross-profile hit fairness. AI/world milestones may consume command ports, capsule dimensions (`MotorConfig`/`CollisionLayers`) and pose evaluator version 1. Full eight-bot B03 performance remains M3/M4. M3 remains unassigned until explicit owner assignment.

## Owner closeout (ADR-026)

This section supersedes the preceding open-milestone disposition. Max’s implement-everything / no-confirmation direction authorizes administrative closure under [ADR-026](../../decisions.md#adr-026--accepted-m2-owner-waiver-of-remaining-hardwarereference-validation-and-milestone-close). **M2 is CLOSED WITH USER WAIVER; three of eight milestones are closed.** No automated PendingHardware report was rewritten to PASS; no golden/cache was edited; Unity/packages were not upgraded; M3 was not started.

| Gate | Disposition |
|---|---|
| C01–C04 / T027 software | **PASS** |
| Unity EditMode 13/13 + PlayMode 11/11 (V05/V06/V09) | **PASS** |
| Client/server smoke (`CombatHarnessSimulation`) | **PASS** |
| Reference-player / budget / thermal / series | Unverified / **Waived for M2** (ADR-026); diagnostic stays PendingHardware |
| B03 eight-bot combat performance | Deferred to M3/M4 (not an M2 exit gate) |

Closure package: `Artifacts/m2-combat-harness/owner-closure-20261113/closure-disposition.json`. Marker name: `m2-combat-harness-owner-accepted` (qualified closure, not a passing-performance tag). Hardware attempt: `Artifacts/m2-combat-harness/owner-closure-20261113/hardware-attempt.json`. Future M3 assignment must not treat this waiver as reference-performance PASS.

## Sign-off rule

Use PASS only when every required check has evidence. Use BLOCKED/PENDING with exact owner/dependency when required evidence is absent. Stretch/deferred work has separate status and cannot be represented as completed by the mandatory gate. Owner waivers (ADR-017/025/026) close milestones administratively without flipping automated PendingHardware reports to PASS.
