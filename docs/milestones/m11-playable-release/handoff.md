# M11 — Playable build and acceptance — handoff and gate record

**Current disposition — 2026-11-14, reviewed at 59316d3:** M11 release acceptance open: 59316d3 commits T052–T064 remediation, not an accepted player. Provenance/regression failures and content/view/settings gaps precede a clean owner-playable archive (T066–T071); T062/T065 still own final visual/REL acceptance. P05 remains FAIL. See the [current audit](../../product-state-audit.md), [next playable-build tasks](../../next-playable-build.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence below remain; no new coding lease is created.

## Latest-commit review — 59316d3

59316d3 contains committed remediation. The new [T066–T071 queue](../../next-playable-build.md) records current baseline/content/view/settings/collector gaps and defines T071 as the first actual owner-playable archive handoff. No such current-source archive was proved here. T070, T062 visual disposition and T065 full acceptance follow on a matching candidate; REL01–REL06 remain NotClaimed. See the audit for exact fresh result paths and retained failures.

## T065 / FIX14 acceptance readiness — 2026-11-14

| Field | Disposition |
|---|---|
| Task | T065 (original T049–T051 / FIX14) |
| Software prerequisites T052–T064 | Committed in 59316d3; current gaps T066–T071 remain before a clean built candidate |
| REL01–REL06 | **Open / NotClaimed** — require one clean source-matched client + Package-Release archive + operator evidence |
| P05 | **FAIL** retained (ADR-024); failed artifacts must not be deleted or relabeled |
| Operator runbook | Below — Compile-GameContent → clean Client build → Package-Release → extract/launch → named reviews → matrix/soaks |
| This document | Acceptance checklist + runbook only; does not mark gates PASS |

### Master software vs remaining (T052–T065)

Implementation-time summary, now committed in 59316d3. Current audit B01–B10 and T066–T071 qualify these software labels. “Software” means code/tooling/tests landed; it is **not** REL/player/hardware PASS.

| Task | Gate | Software disposition | Remaining for true playable release |
|---|---|---|---|
| T052 | FIX01 | Contracts lock graph repaired; 72 tests PASS at 59316d3 | T066 Generation/provenance failures; offline advisory **PendingNetwork** |
| T053 | FIX02 | **Software met** — harness lifecycle/respawn | Actual-player lifecycle evidence **Pending** |
| T054 | FIX03 | **Software met** — pause/focus/input buffer | Player pause/focus device review **Pending** |
| T055 | FIX04 | **Partial** — packages/load (ADR-035) | T067 data-only catalog, actual visual closure and native bake/install; player proof **Pending** |
| T056 | FIX05 | **Software wired** — map/actor/rifle views | T067 packaged visuals / T068 selected weapon/action state; player captures **Pending** |
| T057 | FIX06 | **Pure AI met** — enemy-only sensing/corners | Mixed-team real-map player **Pending** |
| T058 | FIX07 | **Software spawn safety** — rank/defer/deadline | Native/player route+spawn review **Pending** |
| T059 | FIX08 | **Shell software** — menu/load/pause/results | 20-launch soak + 720p UI review **Pending** |
| T060 | FIX09 | **ADR-036 + T069 software** — bindings/accessibility applied | SET02 named Keep/Revert/device **Pending** |
| T061 | FIX10 | **Device callback software** | Named audible review **Pending** |
| T062 | FIX11 | **ToolingReady** inventory/overlay | P05 **FAIL** retained; REL03 **Open** |
| T063 | FIX12 | **ADR-037 tooling** collector/matrix/soak scripts | T070 multi-match/counter repair; twelve captures + soaks **PendingHardware** |
| T064 | FIX13 | **Package-Release software** rejects dirty/mismatched | Clean source-matched client + player launch **Pending** |
| T065 | FIX14 / REL01–REL06 | **Checklist + runbook ready** | Execute operator path; do not claim PASS until measured |

### T065 acceptance checklist (all Open until measured)

Record each item with source commit, `sourceDirty=false` build identity, content/toolchain hashes, artifact paths, named reviewer where required. Use Pending / PendingHardware / FAIL / NotRun — never invent PASS.

| ID | Check | Status now | Evidence required |
|---|---|---|---|
| REL01 | Clean independent artifact | Open | Clean Client build (`sourceDirty=false`) + `Compile-GameContent` closure + `Package-Release` archive; extract/launch without Unity/dev cache/network/server; corrupt content recovers |
| REL02 | Full play matrix | Open | Both maps × both sizes × score + unshortened 15-min timeout; draw fixture; ≥1 named manual match/map; replay/menu/settings |
| REL03 | Visual/usability sign-off | Open | Named review + captures; P05 FAIL immutable; live soldiers/UI at 720p 100%/150% |
| REL04 | Reference performance | Open / PendingHardware | Twelve TDM captures (both maps × both sizes × 3) + ≥30 min soak/map on reference machine |
| REL05 | Regression and provenance | Partial software | Locked offline wrappers + provenance; advisory **PendingNetwork**; Unity client/server isolation on candidate |
| REL06 | Delivery and rollback | Open | Archive path/hash, CONTROLS/KNOWN_ISSUES, rollback pair rehearsal; completed assigned work committed on main under the standing repository rule |
| FIX02 player | Lifecycle in player | Pending | First/repeated 6v6 lives in packaged player |
| FIX03 player | Pause/focus device | Pending | 10s pause + OS focus loss in player |
| FIX04 native/player | Nav bake + packaged load | Pending | Native Unity nav bake + release-player load both maps |
| FIX05 captures | Visibility both maps | Pending | Source-matched player captures |
| FIX06 player | Mixed-team AI | Pending | Real-map mixed-team combat |
| FIX07 player | Spawn/routes | Pending | Native/player review both layouts |
| FIX08 soak/UI | Shell durability | Pending | 20-launch soak + 720p UI |
| FIX09 SET02 | Display apply | Pending | Real display 15s Keep/Revert |
| FIX10 audible | Device review | Pending | Named shot/impact/footstep/UI hearing |
| FIX11/P05 | Visual defects | FAIL retained | No fourth clearance search unless owner authorizes |
| FIX12 matrix | Perf/soak | PendingHardware | Reference twelve-run + soaks |
| FIX13 candidate | Package from clean build | Pending | Reject dirty builds; one matching candidate for all REL gates |

## T065 operator runbook (do not stamp PASS)

Execute from repository root on the **intended acceptance candidate** only. Require a clean committed tree (`sourceDirty=false`). Source 59316d3 is committed, but is not a built/accepted player candidate; complete the remaining next-build repairs before this acceptance runbook. Keep Smoke diagnostic separate from product launch. Failed P05 artifacts stay immutable.

### 1. Content closure

```powershell
./Tools/Compile-GameContent.ps1
```

Confirm both Training Compound and Depot Yard packages publish under the verified map-package cache with matching toolchain hash. Incomplete/unknown Map selection must fail.

### 2. Clean Client (and Server isolation) build

```powershell
./Tools/Configure-Unity.ps1   # locked editor only; fresh checkouts
./Tools/Build.ps1 -Target Client
./Tools/Build.ps1 -Target Server
```

Require `build-manifest.json` with `result=Succeeded`, `target=Client`, `development=false`, **`sourceDirty=false`**, and matching `sourceCommit` / `sourceHash` / `toolchainHash` / `contentManifestHash`. Dirty or HEAD-stamped directories are rejected by T064.

### 3. Package-Release

```powershell
./Tools/Package-Release.ps1 -ClientBuildRunDirectory <clean-client-build-run> [-PreviousPackageDirectory <prior-package>]
```

Expect archive under `Artifacts/m11-playable-release/<build-sourceCommit>/package-*/`, SHA256 inventory, CONTROLS, LICENSES, KNOWN_ISSUES, `validation.json` with `releaseAcceptance=NotClaimed` and `playerLaunchEvidence=Pending` until operator launch is recorded. Tooling PASS ≠ REL01/REL06.

### 4. Extract and launch (REL01 rehearsal)

Extract to a clean directory (no Unity Library, no absolute repo Artifacts path, no network, no server process). Launch the packaged `launch-offline.cmd` (currently supplies content/lock overrides; direct-exe discovery remains T067); reach main menu with no active match. Confirm both maps load from packaged content. Corrupt one content file and confirm recoverable failure. Record paths/hashes — still **NotClaimed** until full REL checklist completes.

### 5. Named product reviews (Pending until done)

On the same packaged player:

- Pause 10s + OS focus loss/return (FIX03); no fatal Busy; no duplicate input.
- Lifecycle deaths/respawns including human camera rebind (FIX02).
- Visibility captures both maps (FIX05); spawn/route review (FIX07); mixed-team AI (FIX06).
- Shell: start/cancel load/pause/restart/leave/replay; 20 alternating launches (FIX08).
- Settings SET02 Keep/Revert on real display (FIX09); audible shot/impact/footstep/UI (FIX10).
- Visual/UI at native720p 100% and 150%; attach captures; keep P05 FAIL (FIX11/REL03).

### 6. Full match matrix (REL02)

Both maps × 5v5 and 6v6: complete 150-kill finish and unshortened 15-active-minute timeout; equal-score draw via boundary fixture; ≥1 named manual complete match per map; results immutable; replay/menu/settings after stop.

### 7. Reference matrix and soaks (REL04 / FIX12) — PendingHardware

After the same candidate content/build:

```powershell
./Tools/Compile-GameContent.ps1
./Tools/Benchmark-Product-Matrix.ps1 -BuildRunDirectory <same-clean-client-build-run> -SensorLog <live-sensor-csv> -HardwareEvidence <reference-proof-json>   # twelve captures: both maps × both sizes × three runs, 60s+180s, native1280×720 Low
./Tools/Benchmark-Soak.ps1 -BuildRunDirectory <same-clean-client-build-run> -Workload TDM-soak-6v6-compound-v1 -HardwareEvidence <reference-proof-json>
./Tools/Benchmark-Soak.ps1 -BuildRunDirectory <same-clean-client-build-run> -Workload TDM-soak-6v6-depot-v1 -HardwareEvidence <reference-proof-json>
```

Or equivalent `Benchmark.ps1` / `Benchmark-Series.ps1` with `TDM-5v5-v1`, `TDM-6v6-v1`, `TDM-5v5-depot-v1`, `TDM-6v6-depot-v1`. Missing reference machine/counters → **PendingHardware**, never PASS.

### 8. Regression / provenance (REL05)

```powershell
python docs/fixtures/verify_fixtures.py
python docs/verify_docs.py
./Tools/Test-SourceProvenance.ps1
./Tools/Test-Contracts.ps1 -Offline
./Tools/Test-Generation.ps1 -Offline
./Tools/Test-Simulation.ps1 -Offline
./Tools/Test-Game.ps1 -Offline
```

Report offline advisory lookup as **PendingNetwork** separately. Confirm server build excludes client presentation/content.

### 9. Handoff closeout (REL06) — after measured evidence only

Update this file with archive path/hash, evidence inventory, every FAIL/Pending/PendingHardware, rollback pair, and named reviewers. Commit completed assigned acceptance work to `main` under the standing repository rule; preserve ignored Artifacts. Do not upgrade packages/editor; do not delete failed P05 artifacts; do not edit gameplay solely to force greens.

## T062 / FIX11 visual inventory tooling — 2026-11-14

Initial product visual defect inventory and capture/overlay helpers are recorded under [visual-defect-inventory.md](../../visual-defect-inventory.md) and the M8 handoff T062 section. **P05 remains FAIL** under ADR-024. REL03 stays Open: tooling/inventory alone is not named visual sign-off, and no fourth clearance search was authorized or performed. Generator/recipe sources and failed historical artifacts are unchanged. This task did not edit Benchmark* or Package-Release* ownership.

| Gate | Status |
|---|---|
| FIX11 tooling/inventory | ToolingReady |
| REL03 named visual/usability sign-off | Open |
| P05 | FAIL (immutable) |

## T063 / FIX12 software — 2026-11-14

| Field | Disposition |
|---|---|
| Task | T063 (original T043/T050 measurement) |
| FIX12 software | **PASS** tooling/collector/negatives — ADR-037 depot workloads; product TDM collector v4; `analyze_tdm_benchmark.py` rejects wrong map/roster/profile/build/counters/windows |
| Twelve captures / soaks | **PendingHardware** — plan-only matrix/soak recorded; reference-machine runs not executed |
| Commands | `Compile-GameContent.ps1`; `Benchmark.ps1 -Workload TDM-…`; `Benchmark-Series.ps1`; `Benchmark-Product-Matrix.ps1`; `Benchmark-Soak.ps1` |
| Evidence | `python Tools/test_tdm_benchmark_analysis.py` (10 PASS); Game-v1 ProductTdmWorkload tests (6 PASS); Compile-GameContent PASS under `Artifacts/m8-runtime-composition/<commit>/compile-gamecontent-*`; plan PendingHardware under `Artifacts/m0-foundation/<commit>/benchmark-product-matrix-*` and `benchmark-soak-*` |
| Handoff to T064 | Same final candidate build/content for packaging; do not stamp HEAD onto arbitrary binaries; require matching source/content/toolchain identities |

## T064 / FIX13 software — 2026-11-14

| Field | Disposition |
|---|---|
| Task | T064 (original T048/T051 packaging requirements) |
| FIX13 software | **PASS** tooling — dirty/mismatched builds rejected; content closure required; exclude policy; archive/inventory/licenses/provenance/rollback |
| Player launch / REL01 | **Pending** — needs clean matching client build + operator extract launch |
| REL06 acceptance | **NotClaimed** by tooling alone (`releaseAcceptance=NotClaimed`) |
| Commands | `Tools/Package-Release.ps1 -ClientBuildRunDirectory <build-run> [-PreviousPackageDirectory <prior-package>]`; negatives: `Tools/test_package_release.ps1` |
| Policy | `Tools/release-package-policy.json`; Bootstrap `ReleasePackagePolicy` / `ReleaseRollbackLedger` |
| Evidence | `Artifacts/tooling-tests/package-release/` (7 wrapper cases); `Artifacts/tooling-tests/t064-release-dotnet/`; dirty `03fa6dd` reject under `Artifacts/m11-playable-release/03fa6dd.../package-*/validation.json` |

## Historical closeout record — 2026-11-13

Historical 2026-11-13 software closeout (retained; superseded for current product acceptance): **CLOSED (software package tooling)**, 2026-11-13. That PASS stamped a dirty `03fa6dd` build and copied Unity backup/debug folders; T064 now rejects that candidate. Named visual review (REL03/T049), native-720p matrix (REL04/T050), and soak remain **PendingHardware / Pending named review**. P05 historical FAIL retained (ADR-024). No invented goldens; no fake hardware PASS.

| Field | Historical recorded disposition |
|---|---|
| Tasks | T048–T051 |
| T048 Clean package | Historical **PASS** tooling — superseded by T064 reject of dirty/debug-shipping package |
| T049 Visual review | Pending (P05 FAIL retained) |
| T050 Perf/soak matrix | PendingHardware |
| T051 Handoff | This record + package inventory; rollback = prior diagnostic build pair |

### Package evidence

- Historical (dirty, superseded): `Artifacts/m11-playable-release/<commit>/package-20261113T230405706Z-7636baef/`
- T064 software: `Tools/test_package_release.ps1` results under `Artifacts/tooling-tests/package-release/`

[Release roadmap](../../playable-release-plan.md) · [Previous](../m10-maps-shell/index.md) · [Shared docs](../../index.md).
