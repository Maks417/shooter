# M0 — Foundation — handoff and gate record

Status: **IMPLEMENTED / ACCEPTANCE PENDING**, 2026-11-12. The remaining M0 measurement implementation is committed and validated: calibrated release allocation callbacks, delayed timing correlation, pinned process-specific GPU capture, per-frame presentation guards and immutable hardware-evidence review. Clean-source client/server builds and smoke, 60 pure tests, 11 Unity tests, 55 Python tooling tests, allocation controls and three controlled shutdown tests pass. Three final-source B01 captures have complete GPU/allocation coverage, but four render timings and unaccepted thermal stabilization keep M0 open. **Zero of eight milestones are closed**; hosted CI remains explicitly deferred by the user. The final-source review below supersedes historical interpretations while retaining original reports. M1 has not started. See [acceptance](validation-acceptance.md), [entry](index.md) and [bootstrap runbook](../../bootstrap-runbook.md).

## Source, ownership and versions

The original foundation implementation used one integrator, Codex. Subsequent tooling-validation waves and the current M0 continuation are recorded below; root `coordination.json` retains `readyForParallelIntegration=false`. Implementation commit `e94ca642b05442252789ca45502de89e6adcb4e7` added the Unity foundation; `a983a7bbd776dcdcb5be6aab9359944350d660ec` corrected package immutability after real clean-import failure. The table and clean-checkout reports in this section retain those historical player identities; current C0-v1 builds are identified in the continuation. No milestone tag or milestone acceptance is claimed.

| Identity | Verified value |
|---|---|
| Editor | Unity 6000.3.28f1 revision 8f082d2fe476; official CLI/browser sign-in, user-approved installer and active local Unity Personal entitlement |
| Packages | URP/Core 17.3.0; Input System 1.20.1; Test Framework 1.6.0; AI Navigation 2.0.0; full resolved graph committed |
| Backends | Windows x64 IL2CPP Release client, D3D11; Windows Dedicated Server subtarget, Mono; matching installed modules and actual builds |
| SDK / contracts | .NET SDK 10.0.301; C# 9 / .NET Standard 2.1 pure source; C0-candidate.1, prediction schema 1, specification fixtures v1.1 unchanged |
| Toolchain SHA256 | `9ac31fe82117043e2bba3bfb843dd1ca32834b15425188e96d6f3369e2c627a8` |
| Client/server source SHA256 | `4883647126a83ef43a62d0d86248e271a9c0330d0e01f9ec2366db66525afc4c` |
| Harness manifest SHA256 | `4e787fd11e4952311128bcca0271e042609ddf4830865cf8855ef2a65eac8a9d` |
| Workload | B01-minimal-v1, seed 460072030; zero actors; numeric box/floor, fixed camera, sun and constant-color materials |

## Implemented scope

- F01: exact editor/package/project locks and reproducible Core RP archive preparation. `Tools/Configure-Unity.ps1` rejects another editor/revision, mutable Core source or stock resource fallback. Local paths/licenses remain ignored.
- F02: BCL-only Contracts, independent golden/conformance tests and real Unity compilation. C0 remains candidate pending explicit freeze; production geometry/wire formats and T005 are future tasks.
- F03: `Bootstrap/Core/FixedTickClock.cs`, empty `Simulation/Foundation/FoundationSimulation.cs`, real `World/Foundation` query/navigation adapters, `Bootstrap/Runtime/FoundationEntry.cs` and separate client/presentation assemblies. Fixed 60Hz, bounded catch-up/debt handling, immutable publication leases, required fake rejection and compile-time server exclusion are implemented.
- F04: generated Low/Balanced/High presets, code-owned `Bootstrap/Scenes/Bootstrap.unity`, source/generated provenance, packed-player audits, native B01 capture/analyzer and PowerShell wrappers. Pinned `.github/workflows/foundation.yml` exists; hosted CI has not run.

The view in `Presentation/Rendering/FoundationView.cs` is read-only. Two code-created BoxColliders give identical collision across quality tiers. The authority contains no actors, motor, weapons, AI, objective or network transport. Navigation has no actor/navmesh content and explicitly fails MissingContent; successful pathfinding remains M3 work. Publication lease acquisition allocates; the zero-allocation claim covers measured empty-authority Step only. `CompileContent` deliberately returns an unsupported-M0 diagnostic; no offline compiler or slice cache is fabricated.

## Clean-checkout evidence

Tested detached worktree (subsequently removed): `C:\Job\shooter\.local\fresh-m0-archive`, commit `a983a7bbd776dcdcb5be6aab9359944350d660ec`. Entry guards required clean source and no project Library. The installed editor and global package caches were reused; this is not an M1 canonical-content cold-regeneration claim. Source was clean again after both builds. `Artifacts/m0-foundation/a983a7bbd776dcdcb5be6aab9359944350d660ec/reproduction/fresh-checkout.json` records the guards, end state and actual IL2CPP build-data Release configuration. Original worktree paths in retained reports are historical.

Run directories below originate in that worktree's `Artifacts/m0-foundation/a983a7bbd776dcdcb5be6aab9359944350d660ec/` and are retained in the same path under the main repository's Artifacts, including both players and the failed capture. Each contains fresh machine-readable results and actual commands. Build manifests' `sourceDirty=true` was captured while BuildPipeline held temporary graphics settings/Resources; it does not certify a clean manifest. Clean entry/end state, exact source hashes and generated provenance are separately recorded.

| Check | Actual result / directory |
|---|---|
| Fresh import / exact packages | PASS; `configure-unity-20261111T215550159Z-e51b876c` |
| Pure contracts/foundation | 54/54 pass, zero skipped; `20261111T215749293Z-25e65ccd` |
| Dedicated server | PASS, no graphics sources/client assemblies; `build-server-20261111T215803720Z-d20778d3` |
| Server smoke | Ready, â‰¥60 fixed ticks, clean exit; `smoke-server-20261111T215933065Z-32f31d80` |
| EditMode after server build | 6/6 pass, including registered-pipeline restoration; `tests-editmode-20261111T215944439Z-572f2011` |
| PlayMode | 5/5 pass; real physics tie/order/quality, penetration/overflow, nav failure/ticket and generated mesh checks; `tests-playmode-20261111T220012007Z-d7b35d26` |
| IL2CPP Release client | PASS, no imported image/model/animation/audio, prefab or built-in sprite/logo entries; `build-client-20261111T220035822Z-49b4bf4f` |
| Client smoke | Ready, â‰¥60 fixed ticks, clean exit; `smoke-client-20261111T220532899Z-beb98a8b` |
| Generated/source provenance | PASS before and after builds; 128 asset entries, 64 unique GUIDs; retained `reproduction/source-provenance.json` |

Pure tests include independent fixture reproduction, serial/ordinal and validation boundaries, bounded lifetime/cancellation/backpressure, fixed-clock overload/failure, fake rejection and steady Step allocation. The full Draft 2020-12 validator passes six expected valid/invalid cases; the standard-library verifier passes 35 assertions over 17 JSON files. Archive decoding/path/timestamp tests pass 2/2; analyzer percentile/invalid-sample tests pass 2/2. No fixture payload or schema was edited. Final documentation verification passes 84 pages, 533 local links and 104 integrity files; it certifies documentation only. Evidence retention verifies 786 file sizes and 54 key report/raw/player hashes (`reproduction/retention.json`). Hosted CI remains pending.

## Art and package provenance

[ADR-013](../../decisions.md#adr-013--accepted-initial-m0-toolchain-and-explicit-engine-resource-curation) records the exact curation, archive migration and source hashes. Two unused Core Resources debug UI prefabs plus their metadata are omitted; no engine source API/shader is modified. The immutable archive has SHA256 `ac919cdfbb561e93be0fc4b0a3c65ee998fd382d9933d1046785dba70ae74c8e`. Independent decoding matches all 2993 curated files. Ordinary verification refuses changed inputs/output rather than blessing or overwriting them.

`ShooterBuild.InitializeFoundation` generates numeric presets/material/scene settings and ordered dither lookups; meshes are generated at runtime. The 64Ã—64 ordered lookup is not a blue-noise quality claim; M0 uses no LOD crossfade/post. A persistent one-pixel zero lookup replaces the unused debug-font atlas. No font is used. `Assets/Shooter/Settings/provenance.json` hashes source, toolchain and real generated outputs, including native URP shader-prefilter fields. Both splash and Unity-logo flags are disabled; shared wrapper guards reject extensionless built-in sprite/logo entries. Server audits additionally reject shader/material payloads. No visual-art exception was added and no installed package/Library payload or golden was hand-edited.

## B01 hardware and acceptance

Actual machine: Ryzen 5 4600H, rendered AMD Radeon(TM) Graphics/D3D11, driver 31.0.21923.11000, 16 GiB RAM configured at 3200, WDC PC SN730 NVMe. Captures record actual power plan/AC state; the separate virtual display adapter is not the rendered adapter. **RAM channel mode stays unverified at the user's explicit request.** A/B bank labels do not prove operating interleaving. Thermal stabilization is unverified.

Full captures use the same cleaned binary/content/route, 60s warmup then 180s sampling and exactly 10800 consecutive ticks per complete run. Native rendering is 1280Ã—720 Low. Route SHA256 is `786aaefd68873583e51b00b5b2f8b24441a54c3d2dbb9df0773ead9eae89dc1a`. Raw unavailable values become JSON null plus reasons. External process-memory sampling is 1Hz and may miss subsecond peaks. Timing-budget failures remain failures; missing counters/conditions keep acceptance pending.

| Completed capture directory | Frames / ticks | Frame p95 / p99 / max (ms) | Sampled private bytes peak | Gate |
|---|---|---|---:|---|
| `benchmark-1-20261111T220545329Z-3a73bbc1` | 216453 / 10800 | 1.0621 / 1.3314 / 3.8479 | 255021056 | PendingHardware |
| `benchmark-2-20261111T221001799Z-4dcab2ae` | 205042 / 10800 | 1.4273 / 2.3317 / 40.8757 | 248610816 | PendingHardware |
| `benchmark-3-20261111T222100609Z-3456981e` (bounded retry) | 213100 / 10800 | 1.3253 / 2.2616 / 28.3892 | 248020992 | PendingHardware |

All three complete reports conform to the unchanged full JSON Schema. Each measured zero managed bytes per empty-authority Step; observed budgets did not fail. GPU/per-frame allocation counters were unavailable in all three; render timing was unavailable in runs 1â€“2, while the retry measured render p95 0.2073ms. These empty-box data do not prove combat performance or complete reference-machine acceptance.

The original third attempt, `benchmark-3-20261111T221414839Z-f9309a8f`, is **FAIL**, with no `capture.json`, `frames.csv` or `ticks.csv`. The player logged offline tick overload with retained debt 0.0273900333333339 seconds after its capped service, then hung during shutdown until the wrapper killed it at 330s. The bounded retry used unchanged binary/configuration/limits and does not erase that failure. The hitch/shutdown cause is unresolved; no all-attempts-passing or milestone performance claim is made. `reproduction/b01-summary.json` retains three complete reports plus the invalid attempt and overall PENDING status.

Earlier short and full captures under `Artifacts/m0-foundation/b858cba5d51fc49b3cdc1b86c36f785cdf5a40f9/` are retained diagnostics only. Their player had unused built-in splash/logo entries and is rejected by the strengthened guard. They are superseded and cannot count toward final acceptance. No 30-minute soak, B01-materials, B02/B03/B04, combat FPS/realism or hosted CI acceptance is claimed.

## Resolved failures and pending handoff

Retained logs cover initial template Input System 1.12.0 incompatibility (resolved to verified 1.20.1 during F01), API/settings persistence errors, disabled penetration probe, template Low exclusion, stock debug UI/image lookups, transient black lookup reloading a font atlas, omitted manual report argument and zero-test discovery after a server build. Wrappers now select Player before test discovery and reject zero/skipped/inconclusive results. Post-server null render references were fixed by reloading configured asset paths; a regression assertion passes afterward.

The first fresh import of `e94ca64` passed compilation/pure tests but Unity rewrote 82 importer metadata files in the mutable Core folder, so the next inventory check correctly failed. `Artifacts/unity-setup/mutable-package-import-diff.json` records exact changes; failed-import diagnostics and all 82 changed metadata files are retained under `Artifacts/m0-foundation/e94ca642b05442252789ca45502de89e6adcb4e7/workspace-evidence/`. Curation schema 2 packages unchanged source into an immutable archive; the second fresh worktree succeeds. No requirement or hash check was relaxed.

Workspace cleanup: both detached verification worktrees were clean and their commits already belonged to main. After retaining evidence, `.local/fresh-m0` and `.local/fresh-m0-archive` were removed. Only `C:\Job\shooter` on main remains registered. `Artifacts/workspace-cleanup/evidence-retention.json` records 882 preserved evidence/diagnostic files. Root project, installed editor, local configuration and required package archive remain available.

Remaining: explicit C0/reviewer decision, unavailable required metrics/reference conditions, investigation/retest of the overload/shutdown failure and hosted CI. RAM channel mode remains intentionally unverified. Missing evidence remains pending; do not start dependent feature integration or mark M0 PASS. Publish exact C0/fixture/toolchain identities only after the readiness decision. No automatic downstream assignment is made here.

## Independent M0 tooling-validation wave

At the user's request, three agents worked from base `1f2c9d8a10578949dd4ce73f1e65c6c2a64280d5` in separate worktrees/branches, with disjoint tool/test file ownership recorded in `coordination.json`. This T033/T034 support work had no dependency on frozen C0 and introduced no feature implementation, Unity import or project changes. Contracts remain C0-candidate.1; specification-v1.1 fixtures, schemas, seed 460072030, workload definitions, package locks and generated assets are unchanged. The integrator reviewed and copied only the assigned files, then wired the schema tests into the existing CI lane.

| Agent / owned implementation | Change | Integrated result |
|---|---|---|
| `benchmark_validation` / `Tools/analyze_benchmark.py` and its test file | Reject each negative tick-allocation/private-memory/RSS sample before peak aggregation; missing CPU/rendered GPU stays null and pending | 11/11 tests pass; optional recorder sentinels, mandatory timing validity, failure priority and diagnostic scope covered |
| `package_preflight` / `Tools/prepare_unity_packages.py` and its test file | Complete curation-proof/manifest validation before writes; initial-lock/migration flags mutually exclusive | 10/10 tests pass; rejected requests preserve inputs/outputs and approved archive identity is unchanged; write failures are not transactional |
| `schema_catalog` / `Tools/verify_schemas.py` and `Tools/test_verify_schemas.py` | Reject empty/malformed catalogs, unknown expectations, duplicate fixture/schema pairs and invalid/missing negative paths | 9/9 tests pass; original six schema cases pass with locked jsonschema 4.26.0 |

Integrated evidence: `Artifacts/m0-foundation/1f2c9d8a10578949dd4ce73f1e65c6c2a64280d5/tooling-validation-20261111T224045Z/validation.json`, plus individual command logs and `schemas.json`. The report records dirty working-source hashes, unchanged toolchain identity and raw-input hashes. Read-only reanalysis of all three complete retained B01 captures exactly reproduces their original reports, each still PendingHardware. Pinned installed-package preparation also passes and preserves archive SHA256 `ac919cdfbb561e93be0fc4b0a3c65ee998fd382d9933d1046785dba70ae74c8e`; installed source and archive were not modified.

`Tools/Test-Contracts.ps1 -Offline` passes 54/54 tests with zero skipped in `20261111T223859840Z-e974d63b` under that base directory. The preceding sandboxed attempt `20261111T223839564Z-f7356005` failed during restore and ran no tests; the successful run used approved execution outside the sandbox. Offline dependency advisory lookup remains pending. Source provenance passes with 128 entries/64 GUIDs; fixtures pass 35 assertions over 17 JSON files; final documentation verification passes 84 pages, 533 links and 104 integrity files. Worker sources are retained alongside integrated evidence and match the main working copy byte-for-byte; the subsequent user-requested cleanup consolidates them into `main` and removes the temporary worktrees/branches after preservation checks. `Artifacts/workspace-cleanup/tooling-wave-preservation.json` records all changed-file hashes and the absence of unique branch commits before removal. Synthetic regression samples are test-only, never hardware evidence. No new Unity build, benchmark capture, hosted CI execution, C0 freeze or M0 acceptance is claimed; the unresolved overload/shutdown failure and prior pending gates remain.


## Independent M0 wrapper-validation wave

The user requested another small parallel implementation wave. Three agents started from `a055cc2b08925110a341d329e8319533171c5f6d` in isolated worktrees with disjoint T033/T034 tooling ownership. Existing M0 wrapper inputs and retained evidence satisfied this independent tooling assignment; C0 remains candidate, `readyForParallelIntegration=false`, and dependent feature integration remains gated. Contracts, specification-v1.1 fixtures, package/toolchain locks, generated assets, seed 460072030 and benchmark workloads are unchanged.

| Agent / owned wrapper and regression suite | Result |
|---|---|
| `smoke_validation` / `Tools/Smoke.ps1`, `Tools/test_smoke.ps1` | 24 cases pass; requires exact readiness followed by one complete matching mode/tick marker with at least 60 ticks; rejects failure markers and records completed ticks |
| `unity_xml_validation` / `Tools/Test-Unity.ps1`, `Tools/test_unity_results.ps1` | 31 cases pass; verifies numeric summary counts, individual passing cases, unique IDs and passing suites; preserves Player target selection; a separate 33-case worker run also replays both retained Unity XML reports |
| `provenance_validation` / `Tools/Test-SourceProvenance.ps1`, `Tools/test_source_provenance.ps1` | 28 cases pass; rejects empty/malformed/duplicate/overlapping/self proofs and invalid hashes/paths, limits this generator to Settings assets/materials and the code-owned scene, and validates the entire manifest before whitelisting outputs |

The integrator reviewed and merged all task branches into `main`; merged code identity is `aec716585c861ec011d168e4dd7f1bbd54c0f812`. All three suites explicitly return success after expected-failure child cases, so the calling CI shell observes exit code zero. The fast CI lane now runs them. Integrated logs, source hashes, schema results and validation report are in `Artifacts/m0-foundation/aec716585c861ec011d168e4dd7f1bbd54c0f812/wrapper-validation-20261111T225312Z/`. All 83 wrapper cases and the existing 30 tooling regressions pass; six schema cases and 35 fixture assertions pass. Real unchanged source provenance passes with 128 entries/64 GUIDs. Synthetic test repositories/logs never count as generated-content or hardware evidence.

The required offline pure suite passes 54/54 with zero skipped in `Artifacts/m0-foundation/a055cc2b08925110a341d329e8319533171c5f6d/20261111T224850887Z-de964f75/`; the preceding sandboxed restore attempt `20261111T224830897Z-5922d4cd` failed before tests. Fresh client and dedicated-server smoke using the retained approved `a983a7b` builds pass with the new parser in `Artifacts/m0-foundation/bdcd2dfbd35bdadf08efeb095b25d345e32fcef5/smoke-client-20261111T225224482Z-f0fc6646/` and `Artifacts/m0-foundation/4c7c2d2c0c7f06487a380d5e629b8c6b4567b44a/smoke-server-20261111T225240055Z-a3ee1b9a/`. No new player build or benchmark capture is claimed.

Real Unity tests with the strengthened result parser pass 6/6 EditMode and 5/5 PlayMode at `Artifacts/m0-foundation/aec716585c861ec011d168e4dd7f1bbd54c0f812/tests-editmode-20261111T225546627Z-b569ae9c/` and `tests-playmode-20261111T225637383Z-a83cd1a2/`. The preceding sandboxed EditMode attempt `tests-editmode-20261111T225309486Z-b176feea` failed licensing IPC before producing test XML; the stalled wrapper was interrupted and its logs retained. The passing retry used approved execution outside the sandbox. No tests are counted from the failed environment attempt. Final documentation verification passes 84 pages, 533 local links and 104 integrity files; documentation integrity is separate from these test results.

Worker commits `759558e`, `c9728aa` and `2fe69db` are ancestors of main. Their clean sources match main after repository LF normalization. Before removing all three worktrees/branches, 1622 ignored worker evidence files were retained in hash-verified ZIP archives under `Artifacts/workspace-cleanup/wrapper-wave/`; ZIP retention avoids Windows path-length expansion. `Artifacts/workspace-cleanup/wrapper-wave-preservation.json` records every member hash, source identities, ancestry and completed cleanup. Only the main worktree remains. Hosted CI, advisory lookup, C0/reviewer acceptance, required hardware/counter conditions and the earlier overload/shutdown defect remain pending. M0 acceptance is unchanged; no M1 feature work started.

## M0 continuation — C0 freeze and failure lifecycle, 2026-11-12

Single integrator, no new agents or worktrees. Source commit `a4afce68fbe6dc4420d0d820cfba3b0abf197d1d` contains the C0-v1 freeze, completed test-only generation-port coverage and revised capture/failure lifecycle. Root `contract-lock.json` records the explicit integrator review and unchanged specification-v1.1 fixture/toolchain identities. `ContractVersion.Current` replaces the unused candidate label; no DTO semantics, units, seeds, enum values, prediction schema, package versions or workload definitions changed. Geometry codecs/compiler and networking remain outside M0. See [ADR-014](../../decisions.md#adr-014--accepted-initial-c0-source-freeze-and-failure-capture-lifecycle).

`FoundationEntry.Fail` transitions once, disables further authority updates and notifies cleanup observers before failure exit. `FoundationBenchmark` stops recording and releases native profiler recorders before shutdown on both failure and success. Failure produces separate `failure.json` with elapsed interval, serviced ticks, next tick and retained debt; it does not publish a successful partial capture. The clock still services at most four ticks per frame and freezes offline on overload. No timing/debt threshold was relaxed.

The following retained evidence is relative to `Artifacts/m0-foundation/a4afce68fbe6dc4420d0d820cfba3b0abf197d1d/` unless explicitly stated. Client/server source SHA256 is `5c4643be5e88c3c7faefad9d0460ab3f48c19a8b173b89db4114c8be3b21631d`; harness SHA256 is `c3385f9b70014c21162b5213793bf1adf78ddf99214257633dab7197de2faef0`; toolchain identity remains the historical locked hash above. Build entry source was clean; build manifests still record the temporary BuildPipeline settings/Resources state as `sourceDirty=true`, as described in the historical clean-checkout section. These continuation builds used the existing verified project/Library; they are not a new clean-checkout or M1 cold-generation claim. Documentation/coordination edits after the source commit do not change these player identities.

| Check | Actual result / retained run |
|---|---|
| Frozen C0 baseline | PASS; 24 source/assembly/metadata files, 18 JSON/binary fixtures; committed blob hashes independently compared to the lock; five baseline/drift tests pass |
| Pure contracts/foundation, online restore | 57/57 PASS, zero skipped, all-level dependency advisory lookup completed; clean-source run `20261112T072357183Z-f90656bc` |
| Release IL2CPP client | PASS, packed-source art audit included; `build-client-20261112T072207249Z-99c9e0ba` |
| Native injected overload/shutdown | 3/3 PASS; `failure-shutdown-20261112T072424896Z-310172f7`; every process exits naturally with code 1, 5.72–6.66 seconds from launch, no successful capture files |
| Client smoke | PASS, readiness and at least 60 ticks; `smoke-client-20261112T072522164Z-a08af5fd` |
| Short real diagnostic capture | PASS as diagnostic only, 5457 frames/300 ticks; `benchmark-diagnostic-20261112T072533530Z-b5f0eccc` |
| Dedicated server | PASS, no client/presentation assemblies or graphics payload; `build-server-20261112T072617722Z-0f9c21a7` |
| Server smoke | PASS; `smoke-server-20261112T072757073Z-e915aa0b` |
| Post-server EditMode / PlayMode | 6/6 and 5/5 PASS; `tests-editmode-20261112T072808401Z-4218825e`, `tests-playmode-20261112T072836351Z-10fc75ef` |
| Existing tooling/wrapper checks | 30 Python tooling and 83 PowerShell wrapper cases PASS; logs under `Artifacts/tooling-tests` and `Artifacts/wrapper-tests`, stamped 20261112T0721–0722 |
| Schema/fixture checks | Six schema cases and 35 fixture assertions PASS; `Artifacts/m0-current-schemas.json`; original fixture bytes unchanged |

Each injected failure uses a real 200 ms diagnostic-only stall. In all three, the next clock update serviced four ticks and stopped at tick 124 with approximately 0.135–0.137 seconds retained debt. The complete diagnostic process exited normally rather than requiring the wrapper's 30-second kill. This validates the revised shutdown path under a controlled overload; the historical uninstrumented hitch/driver-hang cause remains unproven, and its failed report is not erased.

The short capture reports `frameTimingEnabled=true`, valid main-thread recorder and unavailable render/allocation recorders. FrameTimingManager supplied render timing for all 5457 short-capture frames and GPU timing for 4138; GPU was unavailable for 1319. All frame-allocation samples were unavailable. Per-frame raw validity remains authoritative even when an API/recorder is enabled. This diagnostic is not a full B01 pass and does not replace required supported external measurement.

The first sandboxed online pure-suite attempt at base `9f73624` failed restore before tests (`20261112T071753667Z-a3549c7d`). The approved outside-sandbox runs completed online restore/advisory lookup and actual tests. The historical advisory-lookup pending item is therefore resolved for this locked dependency set at the recorded date; it is not a future vulnerability guarantee. All Unity/build/player runs here used approved outside-sandbox execution for licensing IPC and native machine access.

### Full B01 continuation results

All three prescribed attempts completed with the same release IL2CPP client/content/route above, 1280×720 Low/D3D11, seed 460072030, 60 seconds warmup and 180 seconds capture. Each contains exactly 10800 consecutive measured ticks, no overload/failure marker and a successful native process exit. There were no additional retries or discarded continuation attempts. All three actual result JSON files validate against the unchanged full Draft 2020-12 schema. The wrapper returns nonzero for PendingHardware; the orchestration's completion is not an acceptance pass.

| Capture directory | Frames / ticks | Frame p95 / p99 / max (ms) | Sampled private bytes peak | Gate |
|---|---|---|---:|---|
| `benchmark-1-20261112T072940756Z-77e87720` | 213135 / 10800 | 1.2669 / 2.1468 / 44.7044 | 248373248 | PendingHardware |
| `benchmark-2-20261112T073354699Z-b3825522` | 110199 / 10800 | 2.9644 / 3.5060 / 25.5277 | 248541184 | PendingHardware |
| `benchmark-3-20261112T073814774Z-95f09569` | 181836 / 10800 | 1.3768 / 2.4881 / 33.3064 | 248188928 | PendingHardware |

Worst frame p95 is 2.9644 ms; median across runs is 1.3768 ms. The available measured budgets do not fail. Each run measured zero managed bytes per empty-authority Step; this does not establish whole-frame allocation or combat performance. GPU sample coverage is 145730/213135, 85198/110199 and 105129/181836 respectively. No missing GPU value was filled or omitted to compute a passing percentile. Frame-allocation coverage is zero in every run. Run 1 has one missing render sample, so its render p95 remains null; runs 2–3 have measured render p95 of 0.3272/0.2078 ms. Reference NVMe and AC power are verified in these raw machine reports, while thermal stabilization and RAM channel mode remain unverified. The analyzer's combined thermal/NVMe reason does not mean NVMe failed discovery.

`b01-continuation-attempts.json` retains all wrapper outcomes. `continuation-validation.json` records each report's actual metrics/sample coverage, source/contract identity, schema validation outcome, final provenance result, hardware limitations and SHA256 of raw capture/report/log files. Historical failed attempts remain separately retained. These are empty-box results; there is no new 30-minute soak, combat, realism, M1 compiler or cold-regeneration claim.

Final source provenance passes with 128 asset entries and 64 unique GUIDs. The frozen contract/fixture identity check and standard-library fixture verifier pass unchanged. Final documentation verification passes 84 pages, 538 local links and 104 integrity files; these hashes are separate from runtime test results. The sole registered worktree remains `C:\Job\shooter` on main; no temporary branches/worktrees were created in this continuation. Required ignored evidence stays under Artifacts and no player/cache/evidence payload is added to Git.

### Remaining acceptance and ownership

- WP-09/integrator: acquire supported complete GPU/render/frame-allocation evidence in a release player. Enabled APIs or partial samples cannot close this item.
- Hardware reviewer: review the now-recorded concurrent thermal observations and stabilization across the identified monitored captures below. RAM channel mode is software-verified from the user's CPU-Z screenshot; historical reports retain their original null channel values. No channel mode is inferred from slot labels.
- Integrator/reviewer: review the current code, full reports and historical failure; the controlled shutdown mitigation passes, but the original uninstrumented hitch/native-hang cause remains unproven. Final M0 sign-off and a passing milestone tag remain pending.
- Hosted CI: explicitly deferred by the user on 2026-11-12. `git remote -v` returned no configured remote; no hosted Actions execution or prelicensed runner registration is claimed. CI entry points include C0 drift tests and the native failure-shutdown checks.

`readyForParallelIntegration=false` remains deliberate. No milestone is closed and no M1/dependent feature integration is authorized by this continuation.

### User-supplied hardware observation, 2026-11-12

The user supplied a CPU-Z Memory-tab screenshot and `C:\Users\Max\Downloads\1.CSV` after the continuation captures. Read-only review preserved both files byte-for-byte, with SHA256 proofs and a derived `hardware-observation.json`, under `Artifacts/m0-foundation/c6492343b21623f7be7ffbd030eada1c2e4248ca/user-hardware-20261112T080735Z/`. These are hardware evidence files outside shipping Assets, not imported game art. No raw benchmark, hardware.json, schema, fixture or golden result was edited.

CPU-Z 3.02.0 x64 reports 16 GBytes DDR4 and `Channel # 2 x 64-bit`, establishing software-reported dual-channel operation for this observation. Its DRAM clock is 1596.1 MHz, approximately 3192.2 MT/s after the DDR factor, consistent with the nominal DDR4-3200 configuration. This replaces the earlier intentionally unverified channel observation; it does not retroactively reissue or pass the existing B01 reports. The screenshot itself has no capture timestamp or workload identity.

The HWiNFO file decodes as Windows-1252 and contains 45 time samples across 88.023 seconds, from local unzoned `2026-11-12 09:01:10.245` to `09:02:38.268`. Intervals range from 1.989 to 2.024 seconds. A repeated header and the device/process footer are metadata, not additional samples; all 48 CSV rows are accounted for. The log does not identify a Shooter build, seed, workload or matching benchmark capture.

| Sensor | Observed range / flags | Scope |
|---|---|---|
| CPU Tctl/Tdie | 48.2–65.6 °C | AMD Ryzen 5 4600H Enhanced |
| iGPU temperature | 42–49 °C | AMD Radeon Vega |
| CPU thermal flags (HTC, PROCHOT CPU/EXT) | No in all 45 samples for every flag | CPU |
| iGPU thermal-throttle flag | No in all 45 samples | iGPU |
| iGPU power-throttle flag | Yes in 4/45 samples | iGPU; no causal diagnosis inferred |
| Average total CPU usage / GPU usage | 6.23% / 1.36% | Whole-device sampled usage; not workload attribution |
| Memory clock | 1597.1 MHz throughout | CPU memory controller observation |

The footer explicitly identifies presented-frame/FPS and GPU-busy fields as `PresentMon [dwm.exe]`, the Windows desktop compositor. These are process-specific sampled averages, not Shooter per-frame telemetry or evidence against its budgets. The GPU Clock column has an invalid-looking `263397376.0 MHz` value at CSV line 13 (`09:01:32.243`); the raw value is preserved and flagged, and no GPU-clock average/stability claim is made from that column. No driver/hardware fault cause is inferred from this isolated sensor anomaly.

This short low-load sample reports no thermal-throttling flags, but cannot establish stabilization under the B01 workload or cover a prescribed 60-second warmup plus 180-second capture. Thermal acceptance, complete release-player GPU/render/allocation measurements, review of the historical hitch limitation and final M0 sign-off remain pending. Hosted CI remains deferred by the user. The next useful sensor capture must run concurrently with an identified Shooter benchmark; check process targeting and timestamp coverage before interpreting its frame data.

Verification for this evidence-only documentation update: `Tools/Test-Contracts.ps1` passes 57/57, zero skipped, with online dependency advisory lookup completed in `Artifacts/m0-foundation/c6492343b21623f7be7ffbd030eada1c2e4248ca/20261112T080937145Z-306f2768/`. The contract lock and 35 fixture assertions pass unchanged; documentation verification passes 84 pages, 539 links and 104 integrity files. Source input copies match their original SHA256 values. No runtime code, package, settings, schema, fixture or workload changed, and no new Unity/player/benchmark performance result is claimed.

### Monitored release benchmark, 2026-11-12

At the user's explicit readiness instruction, three sequential B01-minimal-v1 attempts ran with live HWiNFO logging to `C:\Users\Max\Downloads\2.CSV`. The sole main worktree was clean at orchestration commit `6a5864163336628074095e6a989dc0c09d0f93af`; the Unity editor remained closed. The player is the retained approved IL2CPP Release build `a4afce6/build-client-20261112T072207249Z-99c9e0ba`, not a new build. All 34 source proofs in its harness manifest still match the current source byte-for-byte. Toolchain, content, route and seed identities are unchanged from the C0-v1 continuation. Each run uses native 1280×720 Low/D3D11, 60-second warmup, 180-second capture, exactly 10800 measured ticks and zero actors. There were no discarded or retried monitored attempts. All three native processes completed successfully without overload or failure markers; wrapper gate outcomes remain PendingHardware and return nonzero rather than falsely passing.

Evidence is under `Artifacts/m0-foundation/6a5864163336628074095e6a989dc0c09d0f93af/`. The `monitored-20261112T081650Z/` subfolder contains `series.json`, `attempts.json`, immutable `sensor-log-snapshot.csv`, reproducible `analyze_monitored.py` and `monitored-review.json` with raw file hashes, sensor ranges/flags, clock anomalies and timing coverage. The original live CSV was never changed. Snapshot time coverage is UTC 08:15:33.461–08:32:38.828, with 513 complete samples in 514 rows and no excluded/truncated rows. It was read while logging could still append and contains no device/process footer. CSV timestamps are unzoned; correlation uses the recorded same-host Windows `Central Europe Standard Time` offset of +01:00. Poll intervals range from 1.903 to 2.104 seconds. A process-version query could not discover HWiNFO's version; it remains unavailable in the evidence. That missing tool identity is not guessed.

| Capture directory | Frames / ticks | Frame p95 / p99 / max (ms) | Sampled private bytes peak | Gate |
|---|---|---|---:|---|
| `benchmark-1-20261112T081721875Z-ebb8d1b1` | 168505 / 10800 | 1.7015 / 2.4784 / 39.7842 | 247590912 | PendingHardware |
| `benchmark-2-20261112T082141646Z-15690d48` | 138915 / 10800 | 2.5304 / 4.3300 / 38.1163 | 248356864 | PendingHardware |
| `benchmark-3-20261112T082554219Z-d19da317` | 198580 / 10800 | 1.4550 / 2.2541 / 15.0465 | 247832576 | PendingHardware |

Worst frame p95 is 2.5304 ms; the three-run median is 1.7015 ms. The available measured timing/memory budgets do not fail, and each run measures zero managed bytes per empty-authority Step. These diagnostic-box numbers do not establish combat performance. All three actual result JSON files validate against the unchanged full Draft 2020-12 schema. No raw machine/benchmark JSON was backfilled or upgraded to PASS.

| Measured window | Sensor samples | CPU Tctl/Tdie range | iGPU temperature range | Sampled thermal flags |
|---|---:|---|---|---|
| Run 1 | 90 | 78.9–93.1 °C | 66–70 °C | HTC/PROCHOT CPU/EXT and iGPU thermal: all No |
| Run 2 | 90 | 74–83.4 °C | 63–65 °C | Same four flags: all No |
| Run 3 | 89 | 72.6–85.8 °C | 63–65 °C | Same four flags: all No |

The sensor log brackets every measured window. First/last sensor gaps within the capture boundaries are 0.283/1.511, 1.679/0.083 and 1.951/1.784 seconds respectively; the longest interval inside any capture is 2.104 seconds. The 89-sample third window is accounted for by actual poll placement, not a fabricated 90th sample. Across the whole orchestration including warmups, observed CPU temperature is 72–95 °C and iGPU temperature 55–70 °C. All four thermal flags are No in all 395 sampled points during that whole interval. Approximately two-second polling may miss shorter events, so these are sampled observations, not continuous-throttling proof.

The iGPU power-limit flag is Yes in 70/90, 90/90 and 89/89 measured samples; a power-limit flag is not silently relabeled as a thermal flag or diagnosed as a hardware fault. The first/last 30-second mean CPU temperatures are 83.11/84.01, 77.81/76.37 and 75.51/72.92 °C; corresponding iGPU means are 67.13/67.73, 64.20/64.07 and 64.27/64.13 °C. These trend observations and changing temperatures between runs are attached for stabilization review. No new numerical stabilization threshold or automatic thermal PASS was invented. One impossible GPU-clock value in the sensor snapshot remains flagged and preserved; no clock-stability claim uses that column.

Release timing coverage remains incomplete: valid GPU samples are 124545/168505, 100980/138915 and 149305/198580. Runs 1 and 2 miss three and one render samples respectively, so their render p95 values remain null; run 3 records render p95 0.2158 ms. Whole-frame allocation coverage is zero in every run. The live snapshot's frame-telemetry process scope is unavailable without its footer, and its sampled GPU-busy averages are not substituted for Shooter's missing per-frame GPU/allocation results. AC power and NVMe are verified by each machine report. The wrapper still writes `channelMode=null` and `thermalStabilized=false`; separate CPU-Z and concurrent sensor evidence are linked rather than altering those historical raw values.

Concurrent thermal data is now available for all three identified captures. Remaining work is complete supported release GPU/render/frame-allocation measurement, explicit handling of verified external hardware evidence for future reports, thermal/reference review including the monitor-version limitation, review of the historical hitch limitation and final milestone sign-off. Hosted CI remains deferred by the user. M0 and `readyForParallelIntegration` remain pending/false. No runtime, settings, package, schema, fixture or workload was changed for this monitored run.

Final handoff checks: pure contracts pass 57/57 with zero skipped and online advisory lookup completed in `20261112T083914625Z-74170673` under this orchestration commit's evidence directory. Source provenance passes 128 asset entries/64 GUIDs, the frozen C0/fixture identity check passes, and fixture verification passes 35 assertions. Documentation verification passes 84 pages, 540 links and 104 integrity files; it does not certify benchmark counters. Only the main worktree is registered, with no temporary branch/worktree created. Required raw evidence remains ignored under Artifacts, separate from the committed handoff/coordination changes.


### Calibrated measurement continuation, 2026-11-12

Source commits `66123bbe9bf17bc6b3f8f03389ff1be342bb8114` and `bdcc9dd1111a3064d43261b8dc9aa226118260a4` implement [ADR-015](../../decisions.md#adr-015--accepted-calibrated-release-measurement-and-immutable-evidence-review). C0-v1, specification fixtures, workload/seed and toolchain hashes are unchanged. No engine installation, Library package, curated package, game-art source or future feature was edited. This continuation uses the verified existing project/Library; the earlier clean-checkout reproduction remains historical, not a new cold-build claim.

**Historical allocation correction:** the locked IL2CPP BCL `GetAllocatedBytesForCurrentThread` is an unsupported zero-returning stub. Historical native 0 B/tick reports above do not establish zero allocation; their raw files remain unchanged. New analysis marks them unavailable. Pure .NET tests remain valid for their tested runtime. The final release positive control demonstrates the distinction directly: a known byte-array allocation reports 4128 bytes through the callback counter and zero through the legacy BCL API.

Collector version 2 observes all-thread managed object sizes plus thread-local tick allocations. Before warmup, rooted object/string/array controls report 4426 bytes on both main and worker threads. End-of-capture controls report 4128 bytes outside the measured window. The normal diagnostic measures zero B/frame; the injected diagnostic measures 4128 B/frame. Both map every main/render timing, with no ambiguous joins. The separate process-specific GPU diagnostic has 2847 valid GPU durations for exactly 2847 measured frames; native GPU gaps remain preserved. These diagnostics validate collection and are not full B01 acceptance.

Evidence below is relative to `Artifacts/m0-foundation/bdcc9dd1111a3064d43261b8dc9aa226118260a4/`:

| Check | Actual retained result |
|---|---|
| Pure contracts/foundation | 60/60 PASS, zero skipped, online restore/advisory lookup; `20261112T093320208Z-0e44522c` |
| Release IL2CPP client | PASS, packed-source audit; `build-client-20261112T093305882Z-1f0dd7d5` |
| Dedicated server | PASS, graphics/client assemblies excluded; `build-server-20261112T093730484Z-d5f753a5` |
| Client/server smoke | PASS; `smoke-client-20261112T093558499Z-53ec3791`, `smoke-server-20261112T093854991Z-790523ac` |
| Release allocation controls | PASS normal/injected; `measurement-controls-20261112T093610104Z-5466d6a0` |
| Native overload/shutdown | 3/3 PASS; `failure-shutdown-20261112T093658348Z-475ee487` |
| EditMode / PlayMode | 6/6 and 5/5 PASS; `tests-editmode-20261112T093907176Z-e71dc4be`, `tests-playmode-20261112T093935770Z-624b3862` |
| Process-targeted GPU diagnostic | PASS validation; `benchmark-diagnostic-20261112T093719279Z-2ef700e3/gpu-application.json` |
| Python validation suites | 54/54 actual tests PASS: analyzer 14, hardware evidence 8, external GPU 8, package preparation 10, schema tooling 9, frozen contract 5; synthetic tests are not player evidence |
| PowerShell wrapper regressions | 24 smoke, 31 Unity-result and 28 provenance cases PASS; retained under `Artifacts/tooling-tests` and `Artifacts/wrapper-tests`, stamped 20261112T093614–093704 |

Client/server source SHA256 is `3483e99821fcdf86847fad8beb75e18d266f0b926875eb8897f9d1aea9d04d5a`; client harness SHA256 is `349c10311d95bc1a5784be8955c36aa9e0bf9a8130190d6c34b2231f51506323`. Client wrapper entry is recorded clean. Build manifests still record temporary BuildPipeline state as dirty; later documentation edits do not alter source identity. Full protocol evidence and final acceptance will be recorded after the live monitored runs complete.


### Final-source validation and acceptance review, 2026-11-12

Reviewer/owner: Codex as the sole M0 integrator; no independent reviewer is implied. Final implementation commit `896e6a85c1f6cc02dc82ba2ab20f0f9c23ef561f` includes the preceding calibration/correlation commits plus corrected named recorder capture and independent application presentation guards. C0-v1, specification fixtures, seed 460072030, B01-minimal-v1, numeric geometry, quality profiles and the locked Unity toolchain are unchanged. No M1 code or installed/curated package patch was introduced. [ADR-015](../../decisions.md#adr-015--accepted-calibrated-release-measurement-and-immutable-evidence-review) records the measurement changes.

Evidence in this section is relative to `Artifacts/m0-foundation/896e6a85c1f6cc02dc82ba2ab20f0f9c23ef561f/`. All 36 harness source proofs match current committed source. Source SHA256 is `169a49dd6b43394d6e0060b960ea5d5dc3e5f279c31aea20a21cee50870f7ef8`; harness SHA256 is `c0312e09ac527cfd0feed9a0480cd09b53e177d07c6441bf190e2c3a93321d11`. The toolchain hash remains `9ac31fe82117043e2bba3bfb843dd1ca32834b15425188e96d6f3369e2c627a8`.

| Final-source check | Actual result and retained directory |
|---|---|
| Pure contracts/foundation | 60/60 PASS, zero skipped, online restore/advisory lookup; `20261112T102121341Z-aab4c1ed` |
| Unity integration | EditMode 6/6 and PlayMode 5/5 PASS; `tests-editmode-20261112T102245246Z-3e6c5d32`, `tests-playmode-20261112T102315085Z-9d545ebb` |
| Final release allocation controls | Normal/injected PASS; `measurement-controls-20261112T102341217Z-b1e4ec01`; object/string/array controls 4426 bytes on main and worker, end controls 4128 bytes; normal frame maximum zero, injected maximum at least 4096 bytes |
| Final native overload/shutdown | 3/3 PASS with retained debt, natural expected failure exit and no successful capture; `failure-shutdown-20261112T102425800Z-154387e1` |
| Python tooling | 55/55 PASS: benchmark 14, hardware evidence 8, GPU evidence 9, packages 10, schema tooling 9, contract lock 5; `fast-validation-console.log` |
| Clean Git-entry client/server builds | Both PASS, fresh output directories; `build-client-20261112T125111287Z-26b13af5`, `build-server-20261112T125232710Z-eac5897d`; `clean-entry-validation.json` records clean entry/end |
| Clean-build player smoke | Both PASS; `smoke-client-20261112T125221992Z-4929b859`, `smoke-server-20261112T125338396Z-783d4d24`; server has no graphics sources/client assemblies |
| Source provenance / frozen identities | PASS, 132 asset entries / 66 GUIDs; C0 source 24 and fixture files 18 unchanged |
| Specification schemas/fixtures | Six schema cases and 35 semantic fixture assertions PASS; `schemas-final.json`; these checks do not pass hardware gates |

The earlier final-source builds also pass: captured client `build-client-20261112T101852405Z-8f09aaa2` and server `build-server-20261112T102104567Z-54e37912`, with both smoke runs. The clean-entry repeat uses the same commit, source and harness hashes and the existing verified Library/package caches. It is not a new clean-Library import or M1 cold-regeneration claim. The benchmark and clean-build GameAssembly DLLs are byte-identical, SHA256 `cf597e36ce4241f6a783581f51f00cf4feffd95831af37a15a3ff68cfe48f819`. Build manifests still observe temporary BuildPipeline settings/Resources as dirty; wrapper clean entry/end and exact binary/source proofs are recorded separately.

### Final monitored B01 series

`release-acceptance-20261112T122259Z/` retains the plan, every attempt, conditioning review, raw sensor snapshot, source hashes, reproducible analysis/plot scripts, standalone figures and final gate review. After the usage-limit pause, the user restarted HWiNFO at the same CSV path; this fresh log begins at local 13:21:50.697. The earlier live file was reset on restart. Previously archived sensor snapshots remain retained; unsnapshotted preliminary observations from earlier source investigations are not used for final acceptance. HWiNFO's executable/version could not be read and remains explicitly unavailable.

One full conditioning attempt (`benchmark-1-20261112T122305226Z-25355811`, 192286 frames) preceded the three prescribed attempts below. Conditioning is separately labeled before launch and excluded from the three-run set. All four native processes completed successfully. Every prescribed capture is release IL2CPP, native 1280×720 Low/D3D11, zero actors, 60-second warmup, 180-second measurement and exactly 10800 consecutive ticks. No prescribed run was discarded or retried. Raw reports and wrapper nonzero PendingHardware outcomes remain unchanged.

| Prescribed capture | Frames | Frame p95 / p99 / max (ms) | GPU p95 (ms) | Missing render samples | Sampled private peak (bytes) |
|---|---:|---|---:|---:|---:|
| `benchmark-1-20261112T122850187Z-c3070425` | 221027 | 1.1020 / 1.8939 / 29.7714 | 0.6911 | 1 | 268902400 |
| `benchmark-2-20261112T123729524Z-add2add7` | 233913 | 0.9980 / 1.3810 / 40.3944 | 0.6566 | 0 | 269221888 |
| `benchmark-3-20261112T124326876Z-fe923c6c` | 234712 | 0.9844 / 1.4829 / 40.4717 | 0.6545 | 3 | 268963840 |

Worst frame p95 is 1.102 ms and the three-run median is 0.998 ms. All 689652 main timing, calibrated all-thread frame-allocation and external GPU samples are available; every measured frame has independent application VSync-off/uncapped checks. All 32400 measured ticks and all measured frames report zero managed allocation with passing positive controls. No frame exceeds 50 ms. Known measured budgets do not fail. These are empty-foundation results, not combat performance or a 30 FPS game claim.

The complete GPU results come from the pinned, hash-verified PresentMon per-process series with exactly matching present counts, no trace loss/overflow and all per-present durations. Incomplete native GPU values remain in raw files. Corrected source uses a 65536-entry present buffer. An earlier `bdcc9dd` conditioning trace overflowed its default buffer; it remains retained and cannot pass. An earlier source `28df022` prescribed attempt was rejected after two CSV SyncInterval values became 1. The pinned PresentMon source confirms kernel flip events can overwrite that field; source `896e6a8` independently checks the actual application configuration on every frame and records driver flip histograms. Earlier failed reports are not rewritten or counted toward the final set.

**M0-RT-001 — Pending, integrator:** four native render timings remain unverifiable, one in run 1 and three in run 3. All frame-start timestamps map unambiguously. Across 689640 comparable frames, native main timing agrees with the direct recorder at its documented four-frame delay, allowing the observed one-nanosecond integer conversion. At each missing render frame, that aligned direct recorder also emits zero with sample count 1. This does not establish a valid render-work duration. The analyzer retains null render percentiles for incomplete runs; no adjacent-frame fill, invented zero or incomplete percentile is accepted. Run 2's complete native render p95 is 0.1821 ms. `final-gate-review.json` retains every affected CSV line, timestamp, tick and direct recorder sample. The underlying zero-validity/root cause is unproven. Resolve with upstream clarification of this case or a validated supported independent release render measurement, then repeat the required set.

### Final thermal and memory review

The byte-exact sensor snapshot brackets all measured windows. Its 792 complete samples, explicit +01:00 host offset and source hash are in `monitored-review.json`; no device/process footer is present, and no HWiNFO sampled frame statistic is used as Shooter timing. No GPU-clock anomaly appears in this fresh snapshot. Each prescribed window contains 90 thermal samples. All three CPU thermal flags and the GPU thermal flag are No at every sampled point. GPU power-limit Yes counts are 16, 8 and 17 of 90 respectively; these are retained separately from thermal flags.

| Run | CPU range / first→last 30s mean (°C) | GPU range / first→last 30s mean (°C) |
|---|---|---|
| 1 | 75.1–92.6 / 79.37→85.78 | 63–70 / 64.73→68.47 |
| 2 | 75.6–91.4 / 80.32→84.81 | 63–70 / 64.47→68.67 |
| 3 | 75.8–92.0 / 80.29→85.32 | 64–71 / 64.80→69.20 |

**M0-THERM-001 — Pending, hardware/integrator:** thermal stabilization is not accepted. The initial conditioning's late plateau did not persist through the prescribed windows: measured CPU means rise about 4.49–6.41 °C and GPU means about 3.73–4.40 °C. Sequential launch/review gaps and the complete temperature curves are retained. Sampled No throttling flags are useful observations, not proof of stabilization or absence of shorter events. No new numerical acceptance threshold is invented. A repeat must maintain consistent conditioning through a reviewed stable-temperature capture set. `final-gate-review.json` explicitly records acceptedStabilization=false; no thermal-acceptance manifest is fabricated.

Private-memory sampling contains 178, 178 and 177 measured points at approximately 1 Hz, with peak 256.75 MiB. First/last 30-second private means decline slightly in all three windows; fitted short-window slopes are approximately −188, −515 and −659 bytes/s. This does not establish a 30-minute leak/soak pass. Actual RSS, private-byte curves, raw sample counts, plots and plotting tool identities are in `plot-and-memory-review.json`, `frame-times.png`, `frame-distributions.png`, `memory-trends.png` and `thermal-observations.png`. Visual inspection confirmed readable, complete plotted traces.

Fresh evidence reviews `benchmark-evidence-review-20261112T125319710Z-d19cdd2d`, `benchmark-evidence-review-20261112T125329815Z-b2629afd` and `benchmark-evidence-review-20261112T125339828Z-2d5eff28` apply only the reviewed dual-channel memory proof and validated GPU sources. All remain PendingHardware. All three raw and three derived result JSON files pass the unchanged full Draft 2020-12 schema; schema validity is separate from acceptance. `final-validation-v2.json` verifies all source/build/report hashes and the exact clean-build identity; the retained v1 report has the same substantive findings.

Final integrator decision: M0 implementation work is integrated, but acceptance and a passing milestone tag remain pending M0-RT-001 and M0-THERM-001. Zero of eight milestones are closed; readyForParallelIntegration remains false. Hosted CI is deferred by the user's explicit instruction, and no hosted execution is claimed. The historical overload/native-hang cause remains unproven; controlled shutdown checks pass and these final captures exit normally. No M1, combat, realism, cold-regeneration or 30-minute soak acceptance is claimed. Required ignored evidence remains in Artifacts; only main is used, with no temporary worktree/branch or retained task stash.
