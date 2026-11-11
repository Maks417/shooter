# M0 — Foundation — handoff and gate record

Status: **IMPLEMENTED / ACCEPTANCE PENDING**, 2026-11-11. The foundation imports from a clean worktree, compiles, runs native client/server players and records real benchmark data. M0 is not signed off: required performance counters/reference conditions and explicit C0/reviewer acceptance remain pending. M1 has not started. See [acceptance](validation-acceptance.md), [entry](index.md) and [bootstrap runbook](../../bootstrap-runbook.md).

## Source, ownership and versions

One integrator, Codex; no parallel workers. Root `coordination.json` retains `readyForParallelIntegration=false`. Implementation commit `e94ca642b05442252789ca45502de89e6adcb4e7` added the Unity foundation; `a983a7bbd776dcdcb5be6aab9359944350d660ec` corrected package immutability after real clean-import failure. Subsequent handoff-only changes do not replace these tested source identities. No milestone tag or C0 freeze is claimed.

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
| Server smoke | Ready, ≥60 fixed ticks, clean exit; `smoke-server-20261111T215933065Z-32f31d80` |
| EditMode after server build | 6/6 pass, including registered-pipeline restoration; `tests-editmode-20261111T215944439Z-572f2011` |
| PlayMode | 5/5 pass; real physics tie/order/quality, penetration/overflow, nav failure/ticket and generated mesh checks; `tests-playmode-20261111T220012007Z-d7b35d26` |
| IL2CPP Release client | PASS, no imported image/model/animation/audio, prefab or built-in sprite/logo entries; `build-client-20261111T220035822Z-49b4bf4f` |
| Client smoke | Ready, ≥60 fixed ticks, clean exit; `smoke-client-20261111T220532899Z-beb98a8b` |
| Generated/source provenance | PASS before and after builds; 128 asset entries, 64 unique GUIDs; retained `reproduction/source-provenance.json` |

Pure tests include independent fixture reproduction, serial/ordinal and validation boundaries, bounded lifetime/cancellation/backpressure, fixed-clock overload/failure, fake rejection and steady Step allocation. The full Draft 2020-12 validator passes six expected valid/invalid cases; the standard-library verifier passes 35 assertions over 17 JSON files. Archive decoding/path/timestamp tests pass 2/2; analyzer percentile/invalid-sample tests pass 2/2. No fixture payload or schema was edited. Final documentation verification passes 84 pages, 533 local links and 104 integrity files; it certifies documentation only. Evidence retention verifies 786 file sizes and 54 key report/raw/player hashes (`reproduction/retention.json`). Hosted CI remains pending.

## Art and package provenance

[ADR-013](../../decisions.md#adr-013--accepted-initial-m0-toolchain-and-explicit-engine-resource-curation) records the exact curation, archive migration and source hashes. Two unused Core Resources debug UI prefabs plus their metadata are omitted; no engine source API/shader is modified. The immutable archive has SHA256 `ac919cdfbb561e93be0fc4b0a3c65ee998fd382d9933d1046785dba70ae74c8e`. Independent decoding matches all 2993 curated files. Ordinary verification refuses changed inputs/output rather than blessing or overwriting them.

`ShooterBuild.InitializeFoundation` generates numeric presets/material/scene settings and ordered dither lookups; meshes are generated at runtime. The 64×64 ordered lookup is not a blue-noise quality claim; M0 uses no LOD crossfade/post. A persistent one-pixel zero lookup replaces the unused debug-font atlas. No font is used. `Assets/Shooter/Settings/provenance.json` hashes source, toolchain and real generated outputs, including native URP shader-prefilter fields. Both splash and Unity-logo flags are disabled; shared wrapper guards reject extensionless built-in sprite/logo entries. Server audits additionally reject shader/material payloads. No visual-art exception was added and no installed package/Library payload or golden was hand-edited.

## B01 hardware and acceptance

Actual machine: Ryzen 5 4600H, rendered AMD Radeon(TM) Graphics/D3D11, driver 31.0.21923.11000, 16 GiB RAM configured at 3200, WDC PC SN730 NVMe. Captures record actual power plan/AC state; the separate virtual display adapter is not the rendered adapter. **RAM channel mode stays unverified at the user's explicit request.** A/B bank labels do not prove operating interleaving. Thermal stabilization is unverified.

Full captures use the same cleaned binary/content/route, 60s warmup then 180s sampling and exactly 10800 consecutive ticks per complete run. Native rendering is 1280×720 Low. Route SHA256 is `786aaefd68873583e51b00b5b2f8b24441a54c3d2dbb9df0773ead9eae89dc1a`. Raw unavailable values become JSON null plus reasons. External process-memory sampling is 1Hz and may miss subsecond peaks. Timing-budget failures remain failures; missing counters/conditions keep acceptance pending.

| Completed capture directory | Frames / ticks | Frame p95 / p99 / max (ms) | Sampled private bytes peak | Gate |
|---|---|---|---:|---|
| `benchmark-1-20261111T220545329Z-3a73bbc1` | 216453 / 10800 | 1.0621 / 1.3314 / 3.8479 | 255021056 | PendingHardware |
| `benchmark-2-20261111T221001799Z-4dcab2ae` | 205042 / 10800 | 1.4273 / 2.3317 / 40.8757 | 248610816 | PendingHardware |
| `benchmark-3-20261111T222100609Z-3456981e` (bounded retry) | 213100 / 10800 | 1.3253 / 2.2616 / 28.3892 | 248020992 | PendingHardware |

All three complete reports conform to the unchanged full JSON Schema. Each measured zero managed bytes per empty-authority Step; observed budgets did not fail. GPU/per-frame allocation counters were unavailable in all three; render timing was unavailable in runs 1–2, while the retry measured render p95 0.2073ms. These empty-box data do not prove combat performance or complete reference-machine acceptance.

The original third attempt, `benchmark-3-20261111T221414839Z-f9309a8f`, is **FAIL**, with no `capture.json`, `frames.csv` or `ticks.csv`. The player logged offline tick overload with retained debt 0.0273900333333339 seconds after its capped service, then hung during shutdown until the wrapper killed it at 330s. The bounded retry used unchanged binary/configuration/limits and does not erase that failure. The hitch/shutdown cause is unresolved; no all-attempts-passing or milestone performance claim is made. `reproduction/b01-summary.json` retains three complete reports plus the invalid attempt and overall PENDING status.

Earlier short and full captures under `Artifacts/m0-foundation/b858cba5d51fc49b3cdc1b86c36f785cdf5a40f9/` are retained diagnostics only. Their player had unused built-in splash/logo entries and is rejected by the strengthened guard. They are superseded and cannot count toward final acceptance. No 30-minute soak, B01-materials, B02/B03/B04, combat FPS/realism or hosted CI acceptance is claimed.

## Resolved failures and pending handoff

Retained logs cover initial template Input System 1.12.0 incompatibility (resolved to verified 1.20.1 during F01), API/settings persistence errors, disabled penetration probe, template Low exclusion, stock debug UI/image lookups, transient black lookup reloading a font atlas, omitted manual report argument and zero-test discovery after a server build. Wrappers now select Player before test discovery and reject zero/skipped/inconclusive results. Post-server null render references were fixed by reloading configured asset paths; a regression assertion passes afterward.

The first fresh import of `e94ca64` passed compilation/pure tests but Unity rewrote 82 importer metadata files in the mutable Core folder, so the next inventory check correctly failed. `Artifacts/unity-setup/mutable-package-import-diff.json` records exact changes; failed-import diagnostics and all 82 changed metadata files are retained under `Artifacts/m0-foundation/e94ca642b05442252789ca45502de89e6adcb4e7/workspace-evidence/`. Curation schema 2 packages unchanged source into an immutable archive; the second fresh worktree succeeds. No requirement or hash check was relaxed.

Workspace cleanup: both detached verification worktrees were clean and their commits already belonged to main. After retaining evidence, `.local/fresh-m0` and `.local/fresh-m0-archive` were removed. Only `C:\Job\shooter` on main remains registered. `Artifacts/workspace-cleanup/evidence-retention.json` records 882 preserved evidence/diagnostic files. Root project, installed editor, local configuration and required package archive remain available.

Remaining: explicit C0/reviewer decision, unavailable required metrics/reference conditions, investigation/retest of the overload/shutdown failure and hosted CI. RAM channel mode remains intentionally unverified. Missing evidence remains pending; do not start dependent feature integration or mark M0 PASS. Publish exact C0/fixture/toolchain identities only after the readiness decision. No automatic downstream assignment is made here.
