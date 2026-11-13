# M2 — Combat harness — handoff and gate record

Status: **IN PROGRESS**. M1 closed under ADR-025 on `925d23f`. Owner Max assigned full M2 multi-agent implementation on 2026-11-13. See [acceptance](validation-acceptance.md) and [entry](index.md).

| Field | Value to fill at execution |
|---|---|
| Owner / integrator / reviewer | Cursor M2 integrator (`bc-2637951e`); workers `/root/m2_motor`, `/root/m2_combat`, `/root/m2_pose`, `/root/m2_view` |
| Source commit and upstream tag | Base `925d23f74301fd08a6aeccdf977822438b061c4b` (M1 closed); integration commits TBD |
| Editor/URP/toolchain hash | Unity 6000.3.28f1 / URP 17.3.0; toolchain SHA256 `9ac31fe82117043e2bba3bfb843dd1ca32834b15425188e96d6f3369e2c627a8` (unchanged) |
| Contract/schema/generator/protocol versions | C0-v1 frozen; specification-v1.1; predictionSchema 1; simulationHz 60 |
| Seed/content/world/workload manifests | Fixture seed `460072030`; M2 harness content TBD |
| Client/server build artifacts | Not yet rebuilt for M2 composition |
| Test results and benchmark evidence | Pending worker merges; store under `Artifacts/m2-combat-harness/<source-commit>/<run-id>/` |
| Hardware/power/driver details | Not measured this wave; Missing hardware => PendingHardware, never fake PASS |
| Accepted ADRs and known issues | Upstream ADR-017/024/025; no new M2 ADR yet; P05 soldier visual FAIL retained and not required for M2 combat against fakes |
| Gate status / reviewer / date | IN PROGRESS — wave launched 2026-11-13 |

## Step status

- [ ] C01 Motor and input: implementation, tests and review attached.
- [ ] C02 Weapon and damage state: implementation, tests and review attached.
- [ ] C03 Hit pose and obstruction: implementation, tests and review attached.
- [ ] C04 Presentation integration: implementation, tests and review attached.

## Parallel wave ownership

Recorded in root `coordination.json` (`m2Start`):

| Package | Branch | Owned paths |
|---|---|---|
| WP-05/motor | `cursor/m2-motor-d311` | `Simulation/Motor`, `Tools/Simulation.Tests/Motor` |
| WP-05/combat | `cursor/m2-combat-d311` | `Simulation/Combat`, `Tools/Simulation.Tests/Combat` |
| WP-05/pose | `cursor/m2-pose-d311` | `Simulation/Pose`, `Tools/Simulation.Tests/Pose` |
| WP-04/view-arms | `cursor/m2-view-d311` | `Presentation/View`, `Tests/EditMode/View` |
| WP-05/integration | `main` (integrator) | Foundation/Bootstrap/Settings/docs/coordination/composition |

Workers are pure-source preferred; no Unity editor on worker worktrees. Contracts/Bootstrap/Settings/ProjectSettings remain integrator-serialized. Shipping composition must `RequireReal` and reject required fakes.

## Downstream handoff

Publish tested motor/query adapters, combat definitions/events, gameplay pose version and recorded command scripts. AI uses same command ports; environment uses capsule dimensions and query filters.

## Sign-off rule

Use PASS only when every required check has evidence. Use BLOCKED/PENDING with exact owner/dependency when required evidence is absent. Stretch/deferred work has separate status and cannot be represented as completed by the mandatory gate. Record measured differences from upstream baseline, data migrations, rollback tag and artifacts to retain. A reviewer signs the gate; an implementation agent cannot certify hardware it has not tested. Full eight-bot B03 performance belongs to M3/M4.
