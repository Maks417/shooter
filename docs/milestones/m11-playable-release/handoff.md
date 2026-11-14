# M11 — Playable build and acceptance — handoff and gate record

**Current disposition — 2026-11-14 review from 84befb9 / a54ed83:** M11 acceptance remains open. Clean `7cd7e81` Client/Server reports and verified local ZIP exist; T071 manual gameplay/recovery/rollback handoff remains partial. T067 native-navigation publication/query work, T062 visual disposition and T065 match/device/reference/soak/delivery exits remain open. REL01–REL06 are NotClaimed; P05 FAIL is retained. The `114815c` archive remains historical/superseded for measurement. Use the [playtest guide](../../playtest-guide.md), [audit](../../product-state-audit.md), [remaining tasks](../../next-playable-build.md) and [FIX acceptance](../../release-remediation-validation.md). No online multiplayer or hosted server is required for the first local-bot version.


## T072 — Owner-requested local sprint slide — implementation record

Owner Max requested **implementation now** of slide (подкат), default Left Shift + F, on 2026-11-14. ADR-039 and the T072 card bound this work to local motor/input/session/read-only views/settings/tests/build/docs; no agents, online rollout or waiver. The old 7cd7e81 ZIP is retained unchanged and has no slide.

Software: local command envelope carries a buffered SlidePressed edge with the C0 command; the authority accepts it only with a fresh command for the current human life. Motor owns crouch capsule/eye pose, 6.5 m/s entry, 4 m/s² decay, ≤45 ticks, 60-tick cooldown, jump/air/wall cancellation and safe stand recovery. Read-only movement view now drives camera/rig crouch; no animation-driven motion. Schema-2 Slide binding/defaults/rebinding/legacy-F preservation are implemented under the reviewed additive Game-v1 lock; C0/N1/content/toolchain formats stay unchanged.

Implementation-time evidence at `77bfa93` + working changes (sourceDirty=true): Contracts 72 PASS (`20261114T161803424Z-043e9d72`), Generation 319 PASS (`20261114T161814495Z-5eb83155`), Simulation 119 PASS (`20261114T161911919Z-5a6e4fce`), Game 55 PASS (`20261114T162353106Z-1d18c101`). Runs live under their usual Artifacts milestone/77bfa93de99ef98fc5232f73e9946b4628fad752 directories. Provenance passes, 662 entries / 331 GUIDs; specification fixtures pass 35 assertions. The initial nullable compile failure is retained (`20261114T161713482Z-46e6fbfe`), then repaired before reruns.

Unity EditMode initially failed **before project compilation**: editor Unity.dll could not load; dependency inspection found missing compress_bc7e.dll. Failure retained at `Artifacts/m0-foundation/77bfa93de99ef98fc5232f73e9946b4628fad752/tests-editmode-20261114T161720443Z-808d8f64/`. Exact 6000.3.28f1 installer redownload verified against original MD5 aa4e924970ec074fe7da2cb711b3fd49 and valid Unity Technologies signature; repair/testing is in progress, without upgrading editor/packages. Final source-matched checks/build/package are recorded in the M11 T072 delivery record when complete. Interactive both-map slide/UI/audio/performance review remains Pending; all existing release gates/P05 FAIL remain unchanged.


## Documentation review / candidate recheck — 2026-11-14

The archive below still exists: 40,645,312 bytes, matching SHA256 `fd54c97e2ad8d7dbf6505d1ccbdd7e6b2c67d64e4bffa13fcc55b8b0c6aebb50`. All 245 inventory hashes and ZIP CRC pass in a fresh read-only check; source remains `7cd7e81`, not later review HEAD. Retained clean EditMode 24/24, PlayMode 12/12 and Client/Server manifests were inspected. Fresh pure/provenance results and attempts are in the [audit](../../product-state-audit.md). This review performed no new Unity build or player run.

T067 is still technical work, not just missing sign-off: the normal pure MapContent wrapper publishes software-grid NVB1 (`nativeUnityNavBake=Pending`); native editor registration exists in another process. Runtime installer reconstructs NavMeshData and permits partial/fallback paths; actual native two-map publication/query/spawn/load evidence is outstanding. T068/T069/T070 software is already built. T071 has delivered the ZIP/menu-launch record, but manual gameplay and player corruption/recovery/rollback remain Pending. Prior tables below are implementation-time history where explicitly labeled.

The old `C:/Temp/shooter-t071-playtest-59bbcbb` extraction is absent at review time. Preserve the archive; use a fresh independent extraction and retain new raw player logs under Artifacts. Recorded menu launch in this handoff was not independently repeated. Package `playerLaunchEvidence=Pending` remains unchanged; no REL stamp is inferred.

## T071 — Owner playtest candidate (post-T070 rebuild) — 2026-11-14

| Field | Disposition |
|---|---|
| Task | T071 (owner playtest candidate; preliminary T065 / FIX13 player path; source-matched after T070) |
| Source | `7cd7e8191e118929af742bef382774edb22a57c2` (`sourceDirty=false` at Client request); includes T070 tip `59bbcbb5629571be4a2f9ffbd56f200d6db3f6a4` plus committed settings provenance refresh for `ShooterBuild.cs` |
| Toolchain | Unity `6000.3.28f1` / `8f082d2fe476`; toolchainHash `9ac31fe82117043e2bba3bfb843dd1ca32834b15425188e96d6f3369e2c627a8` |
| Content | `Compile-GameContent -Force` PASS — 8 packages (both maps × gameplay/low/balanced/high); contentClosureHash `715e762129848f4942f908e896ead3f2fa42a8d2b19cb2ab5612339647e41f07` |
| Client | `Artifacts/m0-foundation/7cd7e8191e118929af742bef382774edb22a57c2/build-client-20261114T153050748Z-b2a23740` — Succeeded, non-development, `sourceDirty=false` |
| Server | `Artifacts/m0-foundation/7cd7e8191e118929af742bef382774edb22a57c2/build-server-20261114T153304840Z-bd3cbeda` — Succeeded, `sourceDirty=false`, `graphicsSourceAssets` empty |
| Package | `Artifacts/m11-playable-release/7cd7e8191e118929af742bef382774edb22a57c2/package-20261114T153524429Z-8edfd0c3/` |
| Archive | `…/archive/Shooter-Offline.zip` SHA256 `fd54c97e2ad8d7dbf6505d1ccbdd7e6b2c67d64e4bffa13fcc55b8b0c6aebb50` |
| Extract probe | `C:\Temp\shooter-t071-playtest-59bbcbb` via archive; launcher from cwd `C:\Temp` |
| Automated launch | PASS to Main menu — Player.log `SHOOTER_SHELL screen=Main matchActive=0` (IL2CPP 6000.3.28f1, D3D11 AMD Radeon Graphics) |
| Manual playtest | **Pending** — both maps × both sizes, combat/UI/settings/complete matches/results/replay |
| REL01–REL06 | **NotClaimed** |
| P05 | **FAIL** retained |

### Commands run (T071 post-T070 rebuild)

```powershell
./Tools/Configure-Unity.ps1 -EditorPath 'C:\Job\shooter\.local\Unity\6000.3.28f1\6000.3.28f1\Editor\Unity.exe'
# WriteSettingsProvenance refresh committed as 7cd7e81 (ShooterBuild source hash only)
./Tools/Test-SourceProvenance.ps1          # PASS (662 entries)
./Tools/Test-Contracts.ps1 -Offline        # 72/72
./Tools/Test-Generation.ps1 -Offline       # 319/319
./Tools/Test-Simulation.ps1 -Offline       # 110/110
./Tools/Test-Game.ps1 -Offline             # 51/51
./Tools/Test-Unity.ps1 -Mode EditMode      # PASS at 7cd7e81
./Tools/Test-Unity.ps1 -Mode PlayMode      # PASS at 7cd7e81
./Tools/Compile-GameContent.ps1 -Force     # PASS, 8 packages, sourceDirty=false
./Tools/Build.ps1 -Target Client           # PASS, sourceDirty=false at 7cd7e81
./Tools/Build.ps1 -Target Server           # PASS, graphics isolated
./Tools/Package-Release.ps1 -ClientBuildRunDirectory <client-build-run>
```

### Exact Pending list (do not upgrade to PASS without evidence)

- Manual owner playtest matrix (both maps × 5v5/6v6; move/aim/fire/rifle-pistol/reload; bots; death/180-tick respawn; pause/focus; scoreboard; settings; complete match; results/replay)
- Player corrupt-content recovery + prior-package rollback rehearsal (tooling extract/corrupt PASS; player confirmation Pending; rollback SkippedNoPrevious)
- FIX02–FIX10 / SET01–02 named player/device/UI/audio reviews
- FIX05 visibility captures; FIX11/REL03 visual sign-off (P05 FAIL immutable)
- REL04 / FIX12 / T070: twelve native720p Low captures + ≥30 min soak/map — **PendingHardware**
- Offline NuGet advisory — **PendingNetwork**
- REL01–REL06 acceptance stamps — **NotClaimed**

### Product fixes landed under T071 to unblock the candidate

- Unity compile: Settings→Contracts asmdef, Bootstrap→Generation.Core, PlayMode→Generation.Core, `com.unity.modules.audio`, InputBinding/EntityId aliases, nullable `PublishedMapPackage.MapId`, lifecycle definite assignment
- EditMode: DestroyImmediate outside play mode for GeneratedReflectionProbe/GeneratedSky
- NVB1 install: voxelSize below agentRadius; +Y triangle winding so SamplePosition works
- `ShooterBuild` records `sourceDirty`/`sourceCommit` at request time (before BuildPlayer side effects)

## T071 — Historical superseded candidate at 114815c — 2026-11-14

Superseded for source-matched measurement after T070 (`59bbcbb`). **Do not retcon** these package/build hashes; use only as historical record.

| Field | Disposition |
|---|---|
| Task | T071 (pre-T070 owner playtest archive) |
| Source | `114815c66dd5b951f1c1124e228f9a8acb2472a1` (`sourceDirty=false` at Client request) |
| Toolchain | Unity `6000.3.28f1` / `8f082d2fe476`; toolchainHash `9ac31fe82117043e2bba3bfb843dd1ca32834b15425188e96d6f3369e2c627a8` |
| Content | contentClosureHash `715e762129848f4942f908e896ead3f2fa42a8d2b19cb2ab5612339647e41f07` |
| Client | `Artifacts/m0-foundation/114815c…/build-client-20261114T145025293Z-1d4df0da` |
| Server | `Artifacts/m0-foundation/114815c…/build-server-20261114T145215699Z-4f4663eb` |
| Package | `Artifacts/m11-playable-release/114815c66dd5b951f1c1124e228f9a8acb2472a1/package-20261114T145536425Z-de3b650c/` |
| Archive | `…/archive/Shooter-Offline.zip` SHA256 `ccadafb06ae0a4541fac974ff459d4936814c57e8d48bb4362836a66c1e9afa4` |
| Status | **Superseded** — missing T070 finish/results/replay measurement software |

## Latest-commit review — 59316d3 (historical)

59316d3 contains committed T052–T064 remediation. The [T066–T071 queue](../../next-playable-build.md) defined the path to the first owner-playable archive; the post-T070 T071 rebuild at `7cd7e81` is the current source-matched candidate. FIX12 hardware remains **PendingHardware**; T062 visual disposition and T065 full acceptance remain open; REL01–REL06 remain NotClaimed.

## Historical T065 / FIX14 readiness at 59316d3 — 2026-11-14

| Field | Disposition |
|---|---|
| Task | T065 (original T049–T051 / FIX14) |
| Software prerequisites T052–T064 | Committed in 59316d3; current gaps T066–T071 remain before a clean built candidate |
| REL01–REL06 | **Open / NotClaimed** — require one clean source-matched client + Package-Release archive + operator evidence |
| P05 | **FAIL** retained (ADR-024); failed artifacts must not be deleted or relabeled |
| Operator runbook | Below — Compile-GameContent → clean Client build → Package-Release → extract/launch → named reviews → matrix/soaks |
| This document | Acceptance checklist + runbook only; does not mark gates PASS |

### Historical software vs remaining at 59316d3 (T052–T065)

Implementation-time summary at 59316d3, retained as history. The current review and T071 record above supersede its baseline/content/build availability claims; current audit C01–C05 and remaining queue govern next work. “Software” means code/tooling/tests landed; it is **not** REL/player/hardware PASS.

| Task | Gate | Software disposition | Remaining for true playable release |
|---|---|---|---|
| T052 | FIX01 | Contracts lock graph repaired; 72 tests PASS at 59316d3 | T066 Generation/provenance failures; offline advisory **PendingNetwork** |
| T053 | FIX02 | **Software met** — harness lifecycle/respawn | Actual-player lifecycle evidence **Pending** |
| T054 | FIX03 | **Software met** — pause/focus/input buffer | Player pause/focus device review **Pending** |
| T055 | FIX04 | **Partial** — packages/load (ADR-035) | T067 data-only catalog, actual visual closure and native bake/install; player proof **Pending** |
| T056 | FIX05 | **Software wired** — map/actor/rifle+pistol/action views (T068) | Player captures / FIX05 full coverage **Pending** |
| T057 | FIX06 | **Pure AI met** — enemy-only sensing/corners | Mixed-team real-map player **Pending** |
| T058 | FIX07 | **Software spawn safety** — rank/defer/deadline | Native/player route+spawn review **Pending** |
| T059 | FIX08 | **Shell software** — menu/load/pause/results | 20-launch soak + 720p UI review **Pending** |
| T060 | FIX09 | **ADR-036 + T069 software** — bindings/accessibility applied | SET02 named Keep/Revert/device **Pending** |
| T061 | FIX10 | **Device callback software** | Named audible review **Pending** |
| T062 | FIX11 | **ToolingReady** inventory/overlay | P05 **FAIL** retained; REL03 **Open** |
| T063 | FIX12 | **ADR-037 tooling** + T070 finish/replay/counters (software) | Twelve captures + soaks **PendingHardware**; rebuild after T070 tip |
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

Execute from repository root on the **intended acceptance candidate** only. Require a clean committed tree (`sourceDirty=false`). The retained 7cd7e81 candidate has build/package reports; use it for immediate T071 playtest. Remaining T067/player fixes require a new matching candidate and affected acceptance reruns. This is the final acceptance runbook, not an instruction to rebuild for documentation changes. Keep Smoke diagnostic separate from product launch. Failed P05 artifacts stay immutable.

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

Extract to a clean directory (no Unity Library, no absolute repo Artifacts path, no network, no server process). Launch the packaged `launch-offline.cmd` (supplies content/lock overrides; executable-relative defaults are implemented, with independent direct-exe confirmation still Pending); reach main menu with no active match. Confirm both maps load from packaged content. Corrupt one content file and confirm recoverable failure. Record paths/hashes — still **NotClaimed** until full REL checklist completes.

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
