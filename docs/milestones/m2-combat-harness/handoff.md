# M2 — Combat harness — handoff and gate record

Status: **IN PROGRESS** (Unity EditMode/PlayMode + client/server smoke with `CombatHarnessSimulation` PASS; milestone not closed). M1 closed under ADR-025 on `925d23f`. Owner Max assigned full M2 multi-agent implementation on 2026-11-13. See [acceptance](validation-acceptance.md) and [entry](index.md).

| Field | Actual value |
|---|---|
| Owner / integrator / reviewer | Cursor M2 integrator (`bc-2637951e`); Unity smoke follow-up `bc-ff836dea`; workers `/root/m2_motor`, `/root/m2_combat`, `/root/m2_pose`, `/root/m2_view` (integrator-executed on disjoint worktrees; CLI subagents unauthenticated) |
| Source commit and upstream tag | Base `925d23f`; kickoff `5566cdc`; pure-integration HEAD `59bd8c6b76ad6824d4369e8bca856cd431b226e4` (Unity/smoke evidence below); docs/meta commit records this wave |
| Editor/URP/toolchain hash | Unity 6000.3.28f1 / URP 17.3.0; toolchain SHA256 `9ac31fe82117043e2bba3bfb843dd1ca32834b15425188e96d6f3369e2c627a8` (unchanged); editor at `C:\Job\shooter\.local\Unity\6000.3.28f1\6000.3.28f1\Editor\Unity.exe` |
| Contract/schema/generator/protocol versions | C0-v1 frozen unchanged; specification-v1.1; predictionSchema 1; simulationHz 60; gameplay pose evaluator v1 |
| Seed/content/world/workload manifests | Fixture seed `460072030`; analytic static-world fakes for pure tests; shipping smoke uses empty-actor `CombatHarnessSimulation` + generated floor/box |
| Client/server build artifacts | Rebuild PASS on `59bd8c6` — see evidence table |
| Test results and benchmark evidence | Pure contracts 71/71; M2 simulation 21/21; EditMode 10/10; PlayMode 6/6; client/server smoke 120 ticks each |
| Hardware/power/driver details | Not measured — **PendingHardware** for any reference-player gate |
| Accepted ADRs and known issues | Upstream ADR-017/024/025; no new M2 ADR; P05 soldier visual FAIL retained (not required for fake-world combat harness) |
| Gate status / reviewer / date | IN PROGRESS — 2026-11-13 |

## Step status

- [x] C01 Motor and input: `Simulation/Motor` + `Tools/Simulation.Tests/Motor` (display FPS ≤1 cm; edge buffer).
- [x] C02 Weapon and damage state: `Simulation/Combat` + combat tests (cadence/semi/reload/damage rounding).
- [x] C03 Hit pose and obstruction: `Simulation/Pose` + hitscan muzzle/cover helpers; pose region tolerances.
- [x] C04 Presentation integration: `Presentation/View/ProceduralViewModel` event-deduped cosmetic springs/grip checks.
- [x] T027 defaults/layers: `GameplayDefaults` + TagManager layers 8–13 audited against fixture; Settings assets unchanged.
- [x] Unity EditMode/PlayMode + client/server build/smoke with `CombatHarnessSimulation` composition.
- [ ] V05/V06/V09 broader Unity harness scenarios (cover/step overlays, two-weapon playable room).
- [ ] Reference-machine measurement (stays PendingHardware without waiver).

## Shipping composition

`Bootstrap/Runtime/FoundationEntry` composes `CombatHarnessSimulation(world)` and `CompositionPolicy.RequireReal(world, navigation, simulation)`. Empty-actor mode preserves foundation smoke tick servicing. Test-only `StaticPlaneWorld` implements `IFakeAdapter` and is rejected by shipping policy.

## Evidence (repository-relative ignored Artifacts)

| Check | Result | Path |
|---|---|---|
| Pure contracts | 71/71 PASS | `Artifacts/m0-foundation/cb66e0026d0698994dc775d634392f99038d598a/20261113T181944159Z-c785efb2/` |
| M2 simulation | 21/21 PASS | `Artifacts/m2-combat-harness/cb66e0026d0698994dc775d634392f99038d598a/20261113T181934512Z-8bd4a7d1/` |
| Docs/fixtures | PASS | `docs/verify_docs.py`, `docs/fixtures/verify_fixtures.py` |
| Configure-Unity | PASS | `Artifacts/m0-foundation/59bd8c6b76ad6824d4369e8bca856cd431b226e4/configure-unity-20261113T182207522Z-492c9c04/` |
| Unity EditMode | 10/10 PASS | `Artifacts/m0-foundation/59bd8c6b76ad6824d4369e8bca856cd431b226e4/tests-editmode-20261113T182308104Z-605117e3/` |
| Unity PlayMode | 6/6 PASS | `Artifacts/m0-foundation/59bd8c6b76ad6824d4369e8bca856cd431b226e4/tests-playmode-20261113T182349783Z-c78d2dd5/` |
| Client build | PASS | `Artifacts/m0-foundation/59bd8c6b76ad6824d4369e8bca856cd431b226e4/build-client-20261113T182439530Z-cbdd9a99/` |
| Server build | PASS | `Artifacts/m0-foundation/59bd8c6b76ad6824d4369e8bca856cd431b226e4/build-server-20261113T182932012Z-22617664/` |
| Client smoke | PASS (120 ticks) | `Artifacts/m0-foundation/59bd8c6b76ad6824d4369e8bca856cd431b226e4/smoke-client-20261113T183109372Z-8e6e133d/` |
| Server smoke | PASS (120 ticks) | `Artifacts/m0-foundation/59bd8c6b76ad6824d4369e8bca856cd431b226e4/smoke-server-20261113T183123557Z-94800437/` |
| M2 Unity/smoke index | Recorded | `Artifacts/m2-combat-harness/59bd8c6b76ad6824d4369e8bca856cd431b226e4/unity-smoke-20261113T193220046Z/validation.json` |
| Reference hardware | PendingHardware | No B03/player capture claimed |

Wrapper runs write under `Artifacts/m0-foundation/<commit>/` by `Unity.Common.ps1`; the M2 index above links those paths for this milestone. During the Unity wave, `sourceDirty=true` reflected only `ProceduralViewModelEditMode.md.meta` TextScriptImporter fields (GUID unchanged); committed with this handoff.

## Downstream handoff

Motor/combat/pose adapters and procedural view model are on main. Shipping bootstrap uses `CombatHarnessSimulation` and passes client/server empty-actor smoke. AI/world milestones may consume command ports, capsule dimensions (`MotorConfig`/`CollisionLayers`) and pose evaluator version 1. Full eight-bot B03 performance remains M3/M4.

## Open gates before CLOSED

1. ~~Unity EditMode/PlayMode + client/server smoke with `CombatHarnessSimulation`.~~ **PASS** on `59bd8c6`.
2. V05/V06/V09 Unity harness scenarios beyond pure tests (cover/step overlays, two-weapon playable room) — no dedicated Unity cases yet; pure V06/V05 coverage remains in `Tools/Simulation.Tests` only.
3. Any reference-machine measurement → **PendingHardware**, never fake PASS.
4. Owner sign-off / waiver if hardware remains unavailable; acceptance checklist in [validation-acceptance.md](validation-acceptance.md) still incomplete.

## Sign-off rule

Use PASS only when every required check has evidence. Use BLOCKED/PENDING with exact owner/dependency when required evidence is absent. Stretch/deferred work has separate status and cannot be represented as completed by the mandatory gate.
