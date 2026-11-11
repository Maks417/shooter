# M0 — Foundation — handoff and gate record

Status: **IN PROGRESS / ACCEPTANCE PENDING**, 2026-11-11. The locked Unity project, foundation authority/adapters, generated box/floor client, dedicated server and verification wrappers exist. Real pure/Unity tests and release player smoke pass. Benchmark counters/conditions, clean-checkout reproduction and explicit C0/reviewer sign-off remain tracked below. M1 has not started. See [acceptance](validation-acceptance.md), [entry](index.md) and [bootstrap runbook](../../bootstrap-runbook.md).

| Field | Actual record |
|---|---|
| Owner / integrator / reviewer | Codex, one integrator; no parallel workers; reviewer unassigned; root `coordination.json` |
| Source identity | Baseline `55cfe5644a383e2c2bbcbdedacb1e600b7cf3b3e`; independent contracts `e7838a9fde3dd0202b173980a533a0e0b0ed163f`; current runs record HEAD `b858cba5d51fc49b3cdc1b86c36f785cdf5a40f9` plus dirty implementation. Build source/harness hashes identify tested files; no clean-source acceptance claim or milestone tag |
| Editor / packages | Unity 6000.3.28f1 revision 8f082d2fe476; URP/Core 17.3.0; Input System 1.20.1; Test Framework 1.6.0; AI Navigation 2.0.0; SDK 10.0.301. Verified installed modules and actual builds; portable `toolchain-lock.json`, project/package locks; local path/license ignored |
| Toolchain / content identity | Lock SHA256 `3d5d2b28c22a9be3425712bc8507402ab3c14b841f95193032c069aac5369223`; final local client/server source SHA256 `7969c2e42b210cfa836068bab02061e456b2199b225617d5583cfcc7c7aa74a3`; harness SHA256 `469850f06e16b1bc887d180976f9a75c9699565403793ff56142fd30936c268b`. Subsequent build manifests are authoritative for their source |
| Contract / schema / protocol | C0-candidate.1; prediction schema 1; specification/fixtures v1.1 unchanged. No C0 freeze, production geometry format or transport format claimed |
| Generated workload | `B01-minimal-v1`, seed 460072030, zero actors; numeric floor/box, fixed camera and constant-color URP Lit materials. No gameplay content compiler, combat, motor, bots, objective or networking |
| Gate | M0 PENDING; `readyForParallelIntegration=false`; missing counters/conditions and reviewer decision cannot be replaced by fast box rendering |

## Implemented scope

- F01: official Unity CLI/browser sign-in and user-approved installer; active local Unity Personal entitlement; exact editor, Windows IL2CPP and Dedicated Server modules installed. Actual release client uses IL2CPP/D3D11; server uses Dedicated Server subtarget/Mono. Fresh-checkout reproduction remains a separate check.
- F02: BCL-only contracts and independent fixtures compile in .NET Standard 2.1/C# 9 and Unity. Explicit C0 freeze remains pending; no units, seeds, hit poses, schema or golden payload changes.
- F03: `Bootstrap/Core/FixedTickClock.cs` services fixed 60Hz with bounded catch-up/debt handling. `Simulation/Foundation/FoundationSimulation.cs` has an empty authority, bounded snapshot/event publication slots and zero measured steady Step allocation. `World/Foundation` implements real main-thread physics/navigation ports. `Bootstrap/Runtime/FoundationEntry.cs` composes them and rejects required fake adapters. Client composition and `Presentation/Rendering/FoundationView.cs` are excluded from server compilation.
- F04: generated Low/Balanced/High presets, code-owned `Bootstrap/Scenes/Bootstrap.unity`, source/generated provenance, packed-player dependency audits and real B01 capture/analyzer. `Tools` implements Configure, Unity tests, builds, smoke and benchmark wrappers. `.github/workflows/foundation.yml` pins actions and configures fast/self-hosted Unity lanes; no hosted execution is claimed.

The view is read-only. Two code-created BoxColliders provide quality-independent world collision. Navigation has no actors/navmesh at M0: requests explicitly fail MissingContent; tests cover bounded/stale/cancel behavior, not successful M3 navigation. Acquiring publication leases allocates; the measured allocation claim covers the empty authority's Step, not future gameplay or presentation consumers. No full-game performance or realism claim follows from this harness.

## Actual test and player evidence

Evidence root is `Artifacts/m0-foundation/b858cba5d51fc49b3cdc1b86c36f785cdf5a40f9/` unless stated otherwise. Artifacts are local ignored outputs, not committed binaries. Each wrapper creates fresh logs/reports with actual commands and identity.

| Check | Result / run directory |
|---|---|
| Pure contracts/foundation | 54/54 pass, zero skipped; `20261111T201158108Z-94de4040`; independent golden/conformance tests plus fixed-clock/debt, fake rejection, publication backpressure and steady Step allocation |
| EditMode | 6/6 pass; `tests-editmode-20261111T214210975Z-a0057dbb`; real locked toolchain/settings and physics adapter |
| PlayMode | 5/5 pass; `tests-playmode-20261111T214237635Z-07a5fcf2`; actual physics penetration/overflow/order and navigation failure/ticket cases |
| Release client | `build-client-20261111T214301083Z-fce44265`; successful Windows x64 IL2CPP build; `build-manifest.json` lists all packed source assets |
| Client smoke | `smoke-client-20261111T214357060Z-20a57114`; readiness, ≥60 fixed ticks and clean player exit |
| Dedicated server | `build-server-20261111T214133819Z-fbe9cfd5`; successful Windows Dedicated Server Mono build, 92,171,067 bytes; `graphicsSourceAssets=[]` |
| Server smoke | `smoke-server-20261111T214201325Z-0f7e3690`; readiness, ≥60 ticks, clean exit, no client/presentation assemblies or graphics payload |
| Short rendered diagnostic | `benchmark-diagnostic-20261111T211324138Z-c551855c`; actual Radeon/D3D11 native 1280×720 Low, 5636 frames / 300 ticks; 2s/5s diagnostic only, never acceptance |

Pure tests include test-only reference codecs/PCG encoding; production T005/P02/N01 implementations remain future work. Full Draft 2020-12 schema verification has six expected valid/invalid cases; the standard-library verifier has 35 assertions over 17 JSON files. Both reran successfully after documentation changes. All three actual B01 reports also conform to the unchanged full schema (`Artifacts/toolchain-discovery/b01-schema-results.json`). Analyzer percentile/invalid-sample tests pass (2/2), and PowerShell wrappers parse without syntax errors. Source provenance checks generated hashes and metadata/GUIDs. Documentation hashes certify documentation only. Source provenance passes (128 entries, 64 unique GUIDs); docs verification passes (84 pages, 531 local links, 104 integrity files). No fixture payload or schema changed.

## Art and generated provenance

`Tools/prepare_unity_packages.py` reconstructs and verifies the exact installed Core RP source with two unused Resources debug UI prefabs and their metadata omitted. The immutable local UPM archive is hash-pinned in curation schema 2 of the portable lock. [ADR-013](../../decisions.md#adr-013--accepted-initial-m0-toolchain-and-explicit-engine-resource-curation) records the real inclusion failure and migration. No installed source or Library payload was hand-edited; no golden fixture changed. Fresh setup must run Configure before resolving this local package.

`ShooterBuild.InitializeFoundation` generates presets, native material/scene settings and numeric ordered dither lookups; view meshes are generated at runtime. The 64×64 lookup is not a blue-noise quality claim; M0 does not exercise LOD/post. A persistent generated one-pixel lookup replaces the unused debug font atlas reference. M0 uses no font. `Assets/Shooter/Settings/provenance.json` hashes generator source, toolchain and actual generated outputs, including locked URP build-generated shader-prefilter fields. Packed-player checks reject imported image/model/animation/audio sources; server checks additionally reject shader/material sources. Disabling both splash and Unity-logo flags removed all built-in sprite/logo entries from the rebuilt client report. The shared Tools guard rejects the earlier report and accepts the cleaned report; no visual exception was added.

## Hardware and benchmark gate

Actual machine: AMD Ryzen 5 4600H, rendered AMD Radeon(TM) Graphics (D3D11), driver 31.0.21923.11000, 16 GiB populated RAM configured at 3200, system disk WDC PC SN730 NVMe. The virtual display adapter is listed in hardware evidence but is not the rendered adapter. Captures record actual power plan and AC state.

**RAM channel mode remains unverified at the user's explicit request.** Two populated A/B bank labels do not certify operating interleaving. Thermal stabilization is unverified. Process memory uses external 1Hz samples and can miss subsecond peaks. Unavailable render/GPU/per-frame allocation counters are JSON null with reasons, never invented zero values. Full B01 uses three runs of 60s warmup/180s capture, 10800 consecutive simulation ticks per run, with unchanged binary/content/route. A result with missing required evidence is PendingHardware even when observed timing budgets pass. A measured budget breach remains ComputedFail.

All three historical full runs completed against `build-client-20261111T210759401Z-78b35d1a`, superseded by the cleaned build in the evidence table. Each captured exactly 10800 consecutive ticks and measured zero managed bytes per empty-authority Step. Route SHA256 is `786aaefd68873583e51b00b5b2f8b24441a54c3d2dbb9df0773ead9eae89dc1a`. Raw frame/tick CSV, external memory samples, hardware JSON and player logs are retained in each directory.

| Full B01 run directory | Frames | Frame p95 / p99 / max (ms) | Sampled private bytes peak | Result |
|---|---:|---|---:|---|
| `benchmark-1-20261111T211733845Z-07c671bb` | 220393 | 1.0895 / 1.4101 / 27.8278 | 254681088 | PendingHardware |
| `benchmark-2-20261111T212221845Z-42ad6d59` | 225003 | 1.1008 / 1.5174 / 62.5075 | 254562304 | PendingHardware |
| `benchmark-3-20261111T212632312Z-2a45e4ae` | 218755 | 1.1914 / 1.6377 / 31.1034 | 254590976 | PendingHardware |

Measured budgets did not fail. GPU and per-frame allocation metrics were unavailable in all three; render timing was unavailable for part/all of runs 1–2, while run 3 has measured render p95 0.1924ms. These are historical empty-box diagnostics with incomplete required counters. Their initial player still had unused built-in splash/logo entries and is rejected by the strengthened provenance guard. They cannot pass final acceptance. Later splash/logging/restoration cleanup builds are separate source identities and are not silently substituted into these captures. Replacement captures against a cleaned source-matched build remain pending.

No 30-minute soak, B01-materials, B02/B03/B04, combat performance or hosted CI acceptance is claimed. C0/reviewer sign-off remains pending. Do not start dependent feature integration or mark M0 PASS from a diagnostic capture.

## Failures, limitations and next actions

Retained setup logs show resolved failures: bundled Input System 1.12.0 did not compile against this editor (initial F01 resolution selected verified 1.20.1); initial API/settings persistence errors; a disabled penetration probe returned no valid penetration; template quality exclusions removed Low from player; stock debug UI Resources and legacy URP lookup references imported images; a transient built-in black lookup reloaded the stock atlas. A final manual Initialize invocation omitted its required report argument and correctly failed; the corrected invocation succeeded (`initialize-foundation-20261111T213304700Z-792e02d0`). Tests after a previous server build discovered zero cases because of persisted UNITY_SERVER context; the wrapper rejected that run and now explicitly selects Player before project load. Each issue was corrected at its source/composition/generation/tool boundary and relevant checks rerun. Earlier sandboxed testhost/NuGet issues are recorded in the prior handoff commit; no test or requirement was disabled.

`CompileContent` deliberately fails with an explicit unsupported-M0 message. There is no offline content compiler or slice cache to certify. Source/build manifests record dirty state while implementation is being integrated. Post-server tests exposed null graphics references restored from stale native object handles: build cleanup now reloads the configured asset paths. The new registered-pipeline assertion and both suites pass after the server build (`tests-editmode-20261111T214210975Z-a0057dbb`, `tests-playmode-20261111T214237635Z-07a5fcf2`). An initial attempt referenced URP's internal global-settings class; using its public Core base resolved compilation. These failing diagnostics are retained.

Foundation implementation was committed as `e94ca642b05442252789ca45502de89e6adcb4e7`. An isolated clean worktree with no project Library imported and passed source provenance and 54/54 pure tests. The subsequent wrapper correctly rejected the folder-based Core package after Unity rewrote its importer metadata. This is a failed reproduction, not a passing fresh-checkout gate. The immutable-archive fix preserves the same upstream/curated inventories and editor/package versions; its new toolchain hash is `9ac31fe82117043e2bba3bfb843dd1ca32834b15425188e96d6f3369e2c627a8`. Root initialization and 6/6 EditMode tests pass with that archive (`Artifacts/m0-foundation/e94ca642b05442252789ca45502de89e6adcb4e7/initialize-immutable-package-20261111T215139097Z-22c974ab` and `tests-editmode-20261111T215229498Z-69d0b1e1`); source provenance and 2/2 archive tests pass. The failed worktree/logs are retained under `.local/fresh-m0`.

Clean-checkout reproduction of the immutable archive, final documentation/provenance checks, replacement full B01 reports and source commit remain execution items; absent evidence stays pending. Hosted CI requires a configured remote and prelicensed runner. Hardware counter availability/thermal conditions and C0/reviewer decision require separate resolution; RAM channel mode is to remain unknown.

Preserve source-matched players, raw captures and failing diagnostics for review. Freeze C0 explicitly before assigning downstream workers, then publish exact contract/fixture/toolchain identities. M1 starts only after the readiness gate; no automatic downstream assignment is made here.
