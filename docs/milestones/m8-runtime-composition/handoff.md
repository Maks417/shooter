# M8 — Runtime composition and Game-v1 foundation — handoff and gate record

**Current disposition — 2026-11-14: M8 integration incomplete.** RUN02–RUN05 remain unaccepted as complete product gates: verified full content, visible views/audio, pause and real lifecycle evidence are missing. Repairs: T052–T056, T061–T063 in the [shared implementation plan](../../release-remediation-plan.md), with [FIX acceptance](../../release-remediation-validation.md). The [audit](../../product-state-audit.md) supersedes earlier readiness interpretations; original requirements, dated ledgers and raw evidence below are retained. No runtime assignment or worker lease is created here.

## Historical closeout record — 2026-11-13

Historical 2026-11-13 software closeout (retained; superseded for current product acceptance): **CLOSED (software)**, 2026-11-13. Product OfflineGameSession is the default Bootstrap composition. Diagnostic floor/box retained for `-shooterSmokeSeconds` / gallery / benchmark. Historical M0–M7 waivers and P05 FAIL (ADR-024) unchanged. No inherited hardware waiver; named visual/P05 acceptance remains open until T049.

| Field | Historical recorded disposition |
|---|---|
| Owner / reviewer / base / final main commit | Cursor M8–M11 integrator (bc-c6e6a4b4); base `5fb3e30`; see coordination `m8Close` |
| Tasks | T035–T039 |
| Game-v1 / C0 / content / toolchain / workload identities | **Game-v1 frozen** (ADR-034); C0-v1 unchanged; TDM workloads defined |
| Build / packaged content / executable paths and SHA256 | IL2CPP client + server Build/Smoke PASS; product composition READY evidenced |
| Pure / Unity / player / visual checks | RUN00–RUN01 PASS; RUN02 software PASS (CompoundMapRuntime); RUN03 product READY PASS / named human review Pending; RUN04 diagnostic Smoke PASS + product pause API; RUN05 captures Pending / P05 retained |
| Reference hardware / counters / thermal review | PendingHardware / NotRun |
| Scope limitations / rollback pair | P05 FAIL retained (ADR-024); named human vertical review not claimed PASS |
| Known entry blocker | Cleared |

## Deliver before downstream assignment

| Artifact | Status |
|---|---|
| Game-v1 source/fixture/lock | Done |
| Authority lifecycle adapter | Done |
| Team deathmatch pure reducer | Done |
| Packaged compound + real collision/nav | Done (`CompoundMapRuntime` + planner catalog spawns) |
| Client input/camera/HUD/audio product path | Done (`LocalPlayerInput`, `ProductSessionView`, synth mixer) |
| Vertical product player review | Product READY evidenced; named human review Pending |
| Frozen workloads | Done |

## Task and acceptance ledger

| Task | Status |
|---|---|
| T035 — Freeze product boundary first | **PASS** |
| T036 — Extract and compose real authority | **PASS** |
| T037 — Compile and install the compound | **PASS** (Compile-GameContent / Test-Maps wrappers + CompoundMapRuntime) |
| T038 — Connect the client path | **PASS** (software) |
| T039 — Run and review the vertical player | **PASS** software / **Pending** named review |

| ID | Result | Evidence |
|---|---|---|
| RUN00 | PASS | Provenance repair retained |
| RUN01 | PASS | `Tools/Test-Game.ps1 -Offline` → 15/15 |
| RUN02 | PASS | CompoundMapRuntime PhysX install + RevisionedPathAdapter corners; fake rejected |
| RUN03 | Partial | Product READY + composition log; named keyboard/mouse review Pending |
| RUN04 | PASS | Diagnostic client/server Smoke PASS; pause/resume API covered in pure tests |
| RUN05 | Pending | P05 live triage / hardware counters PendingHardware |

### Player evidence

- Configure-Unity PASS: `Artifacts/m0-foundation/03fa6dd.../configure-unity-20261113T225812175Z-205c4638`
- Build Client PASS: `.../build-client-20261113T225853642Z-b4041c8e`
- Build Server PASS: `.../build-server-20261113T230217807Z-6414f348`
- Smoke Client PASS: `.../smoke-client-20261113T230344429Z-fd01d64a`
- Smoke Server PASS: `.../smoke-server-20261113T230356298Z-a8fb5fb8`
- Product READY: `Artifacts/m8-runtime-composition/product-ready-final.log` (`SHOOTER_COMPOSITION mode=product`, `SHOOTER_READY_CLIENT`)

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m7-hardening-stretch/index.md) · [Next pack](../m9-team-deathmatch/index.md).
