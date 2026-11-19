# M0 bootstrap runbook

## Current product/tool boundary

This runbook retains historical M0/M1 commands/evidence. Product launch prepares services and opens the shell with no active match; Smoke/gallery/B01 arguments select diagnostics. Installers are the delivery ([launch/playtest guide](playtest-guide.md), current build in [next playable build](next-playable-build.md)). `Compile-GameContent.ps1 -Native` publishes persisted catalog, visual and native-navigation packages for every registered map; `Build-Installer.ps1` wraps a validated package; Test-Maps/Test-Game are pure suites; Package-Release creates a guarded archive; Benchmark supports ADR-037 TDM workloads with T070 replay/counters. See [audit](next-playable-build.md), [remaining queue](next-playable-build.md) and the M11 operator runbook. Server build/smoke steps below are parked under ADR-076 (offline-only scope); Artifacts keep only the newest run per action. Run SDK test wrappers serially in one checkout because they share bin/obj outputs. Tooling PASS/docs hashes do not establish REL acceptance.

Owner: integrator/WP-00. Follow the M0 pack (original plan archived). Repository root is `C:\Job\shooter`; project is `C:\Job\shooter\UnityProject`. The locked editor, project, contracts, generated-box composition, quality presets, test/build/smoke/capture wrappers now exist. Acceptance and limitations are in handoff. Preserve source, settings, metadata and package locks; do not recreate the project over them.

## F01 — establish the actual toolchain

1. Inspect repository status and any parent AGENTS.md. If not a git repository, initialize one only within shooter, add text/gitignore policy, and make a baseline docs commit. Do not accidentally use a parent repository.
2. Discover editors through Unity Hub and its configured editor locations. Inspect each candidate's exact version/revision. Select one installed, supported Unity 6 LTS with matching URP. Record the decision and path; if no suitable installation is available, report the precise missing installation/module and continue only independent pure-contract work.
3. Check Windows x64 IL2CPP build support, Dedicated Server support, Windows SDK/C++ toolchain and Unity license availability. Verify using real client/server builds rather than installation names alone. Record `dotnet --info` for pure tests; pin installed supported SDK in global.json during implementation.
4. Create the project from the selected editor's URP template into UnityProject. Remove template/demo art; retain only permitted engine/package assets named in provenance allowlist. Enable visible meta files and force-text serialization. Set +Y up/+Z forward and meter units in project conventions; enforce these in fixtures.
5. Commit ProjectVersion.txt, manifest.json, packages-lock.json, toolchain-lock.json and build-lock.json. Since ADR-079 `toolchain-lock.json` (schemaVersion 2) holds only what shapes generated content: editorVersion, editorRevision, urpVersion, aiNavigationVersion, graphicsApi=`Direct3D11`, dotnetSdk and the Core RP curation; its SHA-256 is the `toolchainHash` in content and the C0/format/Game-v1 locks. `build-lock.json` (`shooter.build-lock.v1`) holds every other direct package (`packages`, name + version), clientBackend=`IL2CPP`, serverBackend, clientTarget=`Windows64`, serverTarget=`Windows64Server`, requiredModules, provenanceAllowlistVersion and transportVersion; builds record its hash as `buildLockHash`. Adding a runtime or editor package means adding it to `manifest.json`, `packages-lock.json` and `build-lock.json` only; the Unity wrappers reject an unlocked direct dependency. Record machine-local editor path in ignored local settings, not portable lock.

F01 selected and verified Unity 6000.3.28f1 revision 8f082d2fe476, URP/Core 17.3.0, Input System 1.20.1, Test Framework 1.6.0 and AI Navigation 2.0.0. Root `toolchain-lock.json` and the resolved package lock are mandatory. The machine-local installation path is deliberately separate.

## F02 — compile C0

Create Shooter.Contracts.asmdef (`noEngineReferences=true`) in Assets/Shooter/Contracts. Use C# 9-compatible syntax and .NET Standard 2.1 API surface for Unity-facing pure code; avoid required members, record structs, source-generator-only serialization or newer BCL APIs. Create `Tools/Contracts.Tests/` with a standalone SDK test project that compiles the same source files by explicit include and pinned test dependencies. Do not place test framework dependencies in Contracts.

Implement [contract details](contract-details.md), schema fixtures and validation with no Unity references. Add explicit numeric enum values. Validate golden vectors using [fixture verifier](fixtures/verify_fixtures.py), then run actual C# serializer/PRNG tests against those independently generated fixtures. A Python fixture check alone cannot certify the C# implementation.

## F03 — composition and smoke scenes

Create assemblies from the architecture table. Add a single minimal `Bootstrap.unity` scene with a code-owned entry object; no template geometry or art. Offline mode installs real tick scheduler and test world adapter. M0 diagnostic harness may use explicit fake world/nav ports; shipping mode must reject fake adapters. Generate a floor/box harness from code before any feature content is available.

Create separate client and server composition source paths inside Bootstrap: client view factory code guarded by `!UNITY_SERVER`, Presentation asmdef excluded on UNITY_SERVER, and server entry using no presentation types. Split assemblies further if exclusion leaves unresolved references; prove this in F03. A runtime `if (server)` alone is insufficient compile-time separation.

Implement `ShooterBuild.BuildClient`, `ShooterBuild.BuildServer`, `ShooterBuild.CompileContent` Editor-only entry points. Server uses Dedicated Server subtarget and logs ready without graphics payloads. [Official Unity guidance](https://docs.unity.com/en-us/engine/6000.5/manual/platform-specific/dedicated-server/build) documents subtarget/UNITY_SERVER; verify APIs on selected patch. Contract tests must not depend on that editor API.

## F04 — profiles, provenance and benchmark

Create profile assets exactly from performance table. Pin graphics API to D3D11 for baseline. Create B01-minimal (generated box only) at M0; B01-materials is introduced at M1 and has a different workload ID. Never compare these as identical workloads. Log metric availability explicitly.

Add provenance allowlist, schema/docs checks and client/server smoke jobs. After build entry points exist, provide PowerShell wrappers in Tools to locate locked editor, fail on wrong revision, set absolute project/report paths, enforce timeouts, and propagate nonzero exit codes. Build wrappers must print the resolved editor/version and artifact path without secrets.

## Commands to make operational during M0

| Action | Required implemented wrapper | Success evidence |
|---|---|---|
| Pure tests (implemented) | `Tools/Test-Contracts.ps1` | Fresh TRX + JUnit `test-results.xml`, run.json with source/fixture hashes and pinned SDK; nonzero on failure/skip/timeout |
| EditMode/PlayMode (implemented) | `Tools/Test-Unity.ps1 -Mode EditMode` / `-Mode PlayMode` | Editor log + fresh XML; failure/skip/zero tests never pass |
| Compile P02 collision diagnostic | `Tools/Compile-Content.ps1 -RecipeSet box-collision-v1 -Profile Low` | Verified immutable collision cache and byte lease; full slice-v1 remains unsupported |
| Build (implemented) | `Tools/Build.ps1 -Target Client` / `-Target Server` | Release player, source/content/toolchain manifest and packed-source audit |
| Smoke (implemented) | `Tools/Smoke.ps1 -BuildRunDirectory <build-run>` | Ready marker, ≥60 ticks, clean exit; server client-assembly/graphics exclusion |
| Benchmark B01 (implemented) | `Tools/Benchmark.ps1 -BuildRunDirectory <client-build-run> -Workload B01-minimal-v1 -Profile Low -RunIndex 1` | Raw frame/tick CSV, external memory samples, machine and versioned result JSON; no stale/synthetic pass |
| Benchmark product TDM (T063 / ADR-101 software) | `Tools/Compile-GameContent.ps1` then `Tools/Benchmark.ps1 -BuildRunDirectory <client-build-run> -Workload TDM-5v5-v1|TDM-15v15-v1|TDM-5v5-depot-v1|TDM-15v15-depot-v1|… -Profile Low -RunIndex 1` | Rendered product match capture; `analyze_tdm_benchmark.py`; PendingHardware until reference capture/soak proofs. `BenchmarkProgressWatchdog` aborts a validated capture whose match tick stops advancing for 10 s and records a state snapshot (focus, phase, pause, content recovery). The wrapper reports that reason, or the player's last `SHOOTER_` marker after a timeout. Run captures with the machine idle. |
| Product TDM matrix / soak | `Tools/Benchmark-Product-Matrix.ps1` / `Tools/Benchmark-Soak.ps1` (`-PlanOnly` records PendingHardware plan) | Six workloads × three runs = eighteen captures (min/max size per map); ≥30 min 15v15 soak/map |

These wrappers and `ShooterBuild` entry points now exist. Full offline content compilation remains M1–M3 work: M0 draws the fixed box/floor directly from numeric code and reports this limitation explicitly. P02 now supplies a real diagnostic compiler/cache, while the full slice cache remains pending.

## Native navigation content, release package and installer — 2026-11-14

Release content uses Unity's own NavMesh bake (T067 C01), published by the locked editor rather than the pure software grid:

```powershell
./Tools/Compile-GameContent.ps1 -Native -Force          # all maps/profiles into Artifacts/map-packages-native
./Tools/Package-Release.ps1 -ClientBuildRunDirectory <client build-run> -MapCacheRoot Artifacts/map-packages-native -RequireNativeNav
./Tools/Build-Installer.ps1 -PackageDirectory <package run> -ReleaseVersion <n.n.n> [-PreviousInstallerReport <installer-report.json>]
```

`-Native` first publishes pure .NET content, then `ShooterBuild.CompileNativeGameContent` bakes only navigation from that .NET collision and .NET republishes with the staged native bakes ([ADR-043](decisions.md#adr-043--accepted-two-stage-native-navigation-publication)); it writes `nav-provenance.json` (schema `shooter.nav-bake-provenance.v1`) beside `index.json`/`catalog.json`, and reports `nativeUnityNavBake=PASS` only after re-verifying every package's native provenance and nav hash. Without `-Native`, pure tooling keeps the labelled `software-walkable-grid` diagnostic bake. `-RequireNativeNav` makes packaging refuse any software navigation. Installer technology and identity are in [ADR-041](decisions.md#adr-041--accepted-inno-setup-per-user-installer-and-in-place-upgrade-t073); `Tools/test_build_installer.ps1` and `Tools/test_package_release.ps1` are the synthetic regressions. Do not run two SDK wrappers concurrently in one checkout: `Compile-GameContent` builds `Generation.Tests` too and collides with `Test-Maps`/`Test-Generation`.

### Disk policy (ADR-076)

Each wrapper keeps only its newest output. When a build, smoke, benchmark, package or installer run finishes without FAIL, older runs of the same action are deleted. Their small `*.json` reports move to `Artifacts/reports/<action>/<commit>/<run>/`. Set `SHOOTER_KEEP_RUNS=<n>` to keep more. Heavy wrappers refuse to start below `SHOOTER_MIN_FREE_GB` (default 5) of free space. To see where space goes, including the Unity, NuGet and temp caches outside the repository, run:

```powershell
./Tools/Disk-Report.ps1
```

### Reclaiming disk after an installer/milestone build

```powershell
./Tools/Clean-Artifacts.ps1                 # dry run: lists what would be deleted
./Tools/Clean-Artifacts.ps1 -Apply [-IncludeBuildCaches] [-KeepRecentHours 6] [-KeepNewestPerArea 1]
```

Scope is only per-commit run folders `Artifacts/<area>/<40-hex commit>/`. A run is kept when its full **or 7-character short** hash appears in any file tracked at HEAD (handoffs, audit, `coordination.json`, locks), when it is HEAD's run, the newest run of its area, written within `-KeepRecentHours`, or holds the newest `installer-report.json` (needed for `Build-Installer -PreviousInstallerReport`). `.local` (pinned editor, venvs, worktrees), `UnityProject/Library` and non-commit Artifacts folders are never touched. `-IncludeBuildCaches` also removes regenerable `Tools/*/bin`, `obj` and `__pycache__`; do not use it while another SDK wrapper runs in the checkout. Every run writes `Artifacts/workspace-cleanup/<utc-run-id>/cleanup-report.json`. Cite evidence by commit hash in a tracked doc before cleaning, or it is treated as unreferenced.

## Fresh checkout and locked editor

Select/install exactly the editor and Windows IL2CPP/Dedicated Server modules recorded in root `toolchain-lock.json`. An appropriate Unity license must be active on each build machine; the local development account activated Unity Personal through the official CLI/browser flow. CI uses a prelicensed Windows runner; no account credentials, serials or license files belong in this repository.

```powershell
./Tools/Configure-Unity.ps1 -EditorPath '<installed editor>/Editor/Unity.exe'
./Tools/Test-Unity.ps1 -Mode EditMode
./Tools/Test-Unity.ps1 -Mode PlayMode
./Tools/Build.ps1 -Target Client
```

Configure prepares the exact immutable Core RP archive from the installed editor before project resolution, verifies upstream/curated inventories and archive SHA256, and imports through `ShooterBuild.VerifyToolchain`. A mutable folder dependency is rejected: clean import proved that Unity rewrites its texture importer metadata. Local editor path/executable hash live only in ignored `.local/unity.json`. The portable lock records editor revision, packages, backends, modules and curation schema 2. [ADR-013](decisions.md#adr-013--accepted-initial-m0-toolchain-and-explicit-engine-resource-curation) explains the archive migration and why unused debug UI Resources and image-backed shader lookup references are excluded. It also records the initial bundled-template Input System incompatibility; no later feature may silently change the selected toolchain.

`ShooterBuild.InitializeFoundation` regenerates the numeric presets/material/lookup assets and code-owned scene; it is a deliberate generator operation, not a routine way to accept changed golden output. Source/toolchain/generated-file hashes are in `Assets/Shooter/Settings/provenance.json`. Native URP build preprocessors populate shader-prefilter cache fields; the build stage records that real generated output after restoring project state. A mismatched source/package/asset hash must be investigated.

In the historical M0 diagnostic described in this section, the camera is fixed and authority contains zero actors; this does not describe current default product mode. It validates composition/clock/publication, never movement, weapons, bots or an objective. Client view construction is in Bootstrap/Client; Presentation and Bootstrap/Client exclude UNITY_SERVER. Server construction uses no presentation types or graphics cache. All three presets share the same collision and navigation adapter/data. Unity tests explicitly select StandaloneWindows64/Player before loading, avoiding a previous server build's persisted UNITY_SERVER context; zero discovered tests is a failure. See the [editor command-line reference](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/editor).

## Player and benchmark evidence

Build prints a unique run directory containing `player/Shooter.exe`, `build-manifest.json`, `harness-manifest.json` and logs. Pass that directory to Smoke or Benchmark. The release client is IL2CPP; the Windows Dedicated Server is Mono. Packed-source audits reject imported art, extensionless built-in sprite/logo entries, and any graphics payload in the server. Both splash and Unity-logo flags are disabled; there is no logo art exception. Dedicated builds temporarily clear render dependencies, then reload the three configured pipeline assets/global settings by path to restore the client project; stale native object handles must not restore null references. No package/version upgrade or fallback is allowed by a wrapper.

```powershell
./Tools/Smoke.ps1 -BuildRunDirectory '<printed build-run directory>'
./Tools/Benchmark.ps1 -BuildRunDirectory '<client build-run>' -Diagnostic
./Tools/Benchmark.ps1 -BuildRunDirectory '<client build-run>' -RunIndex 1
```

Diagnostic capture is 2s warmup/5s sampling and writes a separate diagnostic report, never B01 acceptance. Full B01 uses 60s/180s, exactly 10800 captured ticks, and run indices 1–3. Keep the client/content/route/toolchain fixed across all three. CSV capacity overflow, missing tick/frame samples, wrong GPU/API/resolution/profile, clock overload or process timeout invalidate the run. Raw unavailable timings use a negative sentinel only in CSV; the analyzer emits JSON null plus reasons. Percentiles use nearest rank. Runtime CPU/GPU/GC timing availability is explicit; process memory is sampled externally at 1Hz and may miss subsecond peaks. Do not call those peaks exact.

The wrapper discovers CPU, actual rendered GPU, RAM topology/speed, driver, power plan, AC power and system-disk bus. The user supplied CPU-Z evidence on 2026-11-12 showing DDR4 `2 x 64-bit`, resolving the software-reported dual-channel observation; populated A/B slots alone would not prove operating interleaving. Existing raw machine/benchmark reports retain their original null channel value, and reviewed proofs are applied only to fresh derived reports as described below. Preserve the separate hardware observation and explicitly associate it with future captures rather than silently backfilling prior reports. Thermal stabilization is not inferred from a timer or a short desktop sensor log. Missing conditions keep acceptance pending; timing-budget failures remain failures. No B01 result establishes combat performance or a 30-minute soak. The versioned benchmark schema is unchanged.

`.github/workflows/foundation.yml` provides hosted Windows fast checks and a manually requested sequential Unity lane on a prelicensed `unity-6000.3.28f1` runner using `SHOOTER_UNITY_EDITOR`. Action commits are pinned. Local wrapper execution is not evidence that hosted CI ran.

## Implemented independent checks

C0-v1 is frozen by the M0 integrator in root `contract-lock.json`. `python Tools/verify_contract_lock.py` checks the exact contract/metadata inventory, JSON/binary specification fixtures and toolchain hash. `python Tools/test_contract_lock.py` exercises rejected source/fixture drift and unfrozen status. The pure and Unity wrappers run the identity check before tests/builds; no command implicitly refreshes the frozen baseline. This is contract identity enforcement, not hardware or milestone acceptance. See [ADR-014](decisions.md#adr-014--accepted-initial-c0-source-freeze-and-failure-capture-lifecycle).

Run from the repository root in PowerShell 7:

```powershell
./Tools/Inspect-Toolchain.ps1
./Tools/Test-Contracts.ps1
./Tools/Test-SourceProvenance.ps1
python docs/fixtures/verify_fixtures.py
python docs/verify_docs.py
```

Discovery writes `Artifacts/toolchain-discovery/discovery.json` plus `dotnet-info.txt`; no editor candidates gives a nonzero pending-installation result, not acceptance. It distinguishes the standalone `unity.exe` CLI alias from an editor. The pure test wrapper pins SDK 10.0.301 via `global.json`, restores exact `packages.lock.json` dependencies, audits all dependency levels and fails on warnings. `-Offline` uses only the local NuGet cache and records advisory lookup as pending. Tests need local VSTest/testhost connectivity; the sandboxed run aborted, while the same tests passed outside the sandbox. Reports use a fresh `Artifacts/m0-foundation/<source-commit>/<run-id>/` folder, capture dirty state/source/fixture fingerprints and the real toolchain hash, and cannot pass from stale XML.

Full JSON Schema checks use a repository-local environment. The hash-pinned wheel lock currently targets Windows x64 / CPython 3.12:

```powershell
python -m venv .local/fixtures-venv
./.local/fixtures-venv/Scripts/python.exe -m pip install --require-hashes -r Tools/requirements-fixtures.lock
./.local/fixtures-venv/Scripts/python.exe Tools/verify_schemas.py --report Artifacts/toolchain-discovery/schema-results.json
```

`jsonschema` 4.26.0 verifies all six schema cases and expected negative paths. Run `./.local/fixtures-venv/Scripts/python.exe Tools/test_verify_schemas.py` for catalog and loader regressions: empty/malformed catalogs, unknown expectations, duplicate case pairs and missing negative paths are rejected before cases run. The standard-library fixture verifier separately checks semantics/hashes/vectors. `Test-SourceProvenance.ps1` checks Assets sources, metadata/GUID uniqueness, generated settings provenance and the named nonshipping `abc` fixture; player builds additionally audit packed dependencies. It does not certify future M1 content caches. M0 uses no font, sprite UI or imported surface texture.

`python Tools/test_benchmark_analysis.py` tests nearest-rank analysis, individual tick-allocation/memory sample validation, unavailable hardware/counters, failure priority and diagnostic scope. `python Tools/test_prepare_unity_packages.py` independently decodes archives, checks identity across host paths/timestamps and verifies that rejected curation approval/input validation leaves outputs and locks untouched. Package preparation validates proof and manifest inputs before writing, and rejects simultaneous initial-lock/migration flags. These checks do not make filesystem write failures transactional. CI runs fixture/schema, benchmark/hardware/GPU, package and contract tooling regressions and stops immediately on environment creation or dependency-install failure; hosted execution remains pending until actually run.

Wrapper regressions run without Unity or Pester using `./Tools/test_smoke.ps1`, `./Tools/test_unity_results.ps1` and `./Tools/test_source_provenance.ps1`. They invoke the real wrappers in isolated synthetic repositories with test-only process adapters. Smoke acceptance requires the matching ready marker and a complete mode/tick marker proving at least 60 ticks. Unity result acceptance checks individual test cases against the summary. Provenance verification rejects incomplete or duplicate proofs and limits the named settings generator to its existing output boundary. These checks run in the fast CI lane; synthetic logs and miniature assets do not count as player, generated-content or hardware evidence.

## Foundation completion

`Tools/Test-FailureShutdown.ps1 -BuildRunDirectory <client-build-run>` runs three explicitly diagnostic 200 ms stall injections in the release IL2CPP player. Each must retain clock debt after the unchanged four-tick cap, emit `failure.json`, exit naturally with code 1 within 30 seconds and leave no successful capture files. `FoundationEntry` notifies failure observers once; `FoundationBenchmark` disposes recorders before requesting native shutdown, including the success path. Failed captures remain failures. The wrapper preserves per-attempt telemetry/logs and never treats a killed process as a successful exit. These controlled tests do not explain the historical hitch/driver cause or count toward B01 acceptance.

Successful `capture.json` now also records whether frame timing is enabled and whether each profiler recorder is valid. These diagnostic flags do not replace per-frame sample validity: intermittent missing GPU samples and unavailable frame-allocation samples still keep acceptance pending. Unity documents delayed/asynchronous timing and possible missing GPU results in its [Frame Timing Manager guidance](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager/manager). Required external timing, thermal conditions and final review cannot be fabricated from these flags. The supplied HWiNFO log's device/process footer identifies frame statistics as `PresentMon [dwm.exe]`; those desktop-compositor averages cannot substitute for Shooter's per-frame GPU timings. Check the process scope and overlap with an identified benchmark before accepting external data. RAM channel mode now has separate CPU-Z evidence in the handoff.

The monitored release captures additionally retain a live sensor-log byte snapshot with timing correlations for all three measured windows. Preserve the source CSV unchanged, record the host timezone/offset because its timestamps are unzoned, distinguish measured windows from warmup/idle periods, and account for incomplete final live rows or metadata footers explicitly. A snapshot without a footer cannot establish the process scope of its frame statistics. Record actual samples, gaps, thermal/power flags and anomalies; sampled No flags and elapsed warmup alone do not establish thermal stability. Keep this external observation separate from immutable raw benchmark reports. The original wrapper recorded unknown channel/stabilization values; the reviewed-evidence path below applies explicit proofs to fresh derived reports. A sidecar observation alone does not change a gate outcome.

Fresh checkout reproduces import; pure contracts compile; blank client/server boot and terminate; B01-minimal records real counters; lock/provenance/docs checks run; AGENTS and ownership ledger reflect actual work. Exact missing dependencies and hardware measurements remain pending until proven.

## Calibrated release capture and hardware review

[ADR-015](decisions.md#adr-015--accepted-calibrated-release-measurement-and-immutable-evidence-review) supersedes historical native zero-allocation interpretations. The locked IL2CPP BCL thread API returns a stub zero. Collector version 2 uses allocation callbacks with object/string/array and worker controls before warmup, plus end-of-capture controls outside sampling. `Tools/Test-Measurement.ps1 -BuildRunDirectory <release-client-run>` runs normal and known-allocation diagnostic captures; known allocations must be detected. Unknown or failed controls keep metrics unavailable. These tests are separate from full benchmark acceptance.

Native timing history is joined by exact QPC brackets; missing/ambiguous values remain unavailable. For the reference driver's intermittent GPU gaps, use pinned process-specific external measurement:

```powershell
./Tools/Configure-PresentMon.ps1
./Tools/Benchmark.ps1 -BuildRunDirectory '<release-client-run>' -RunIndex 1 -PresentMon -HardwareEvidence '<review.json>'
```

Repeat sequentially for indices 2 and 3 after reviewing thermal stabilization. Configure verifies the pinned executable and license hashes and changes no machine settings. The wrapper starts/stops only its uniquely named ETW session. It records the Shooter PID/executable hash and preserves `presentmon.csv`, log, `player-launch.json` and `external-gpu.json`. Analysis requires the exact per-present count in the measured QPC interval, all GPU durations, verified clock/process identity and no event loss. `gpu-application.json` states that this is a separate per-present aggregate; it does not fill individual native rows. Missing counts/durations stay pending. The 65536-entry present buffer prevents the observed high-FPS overflow; any reported overflow or ETW event loss rejects validation. PresentMon overwrites CSV SyncInterval with driver/kernel flip intervals, so the player independently checks application VSync off and targetFrameRate=-1 for every measured frame. Raw driver flip histograms remain in GPU application proofs. Nonzero driver intervals without that independent guard fail validation. PresentMon's driver/HAGS accuracy caveats remain applicable; no HWiNFO desktop-compositor statistics are substituted.

Hardware review JSON has `schemaVersion=1`, `kind="shooter.hardware-review.v1"`, `subject` matching CPU/RAM bytes/speed, and a `review` object with named `reviewer`, zoned `reviewedUtc` and concrete `rationale`. Optional `memory` contains its own review, `channelMode` and `source` with repository-relative Artifacts PNG `path` and SHA256. Optional `thermal` contains its own review, `acceptedStabilization=true`, the exact `captureSha256`, source CSV proof and explicit `utcOffsetMinutes`. Thermal source must bracket this capture with valid temperatures and no sampled thermal flags; reviewer rationale must assess stabilization rather than infer it from elapsed time. Source files and raw hardware reports stay unchanged.

Use `Tools/Review-Benchmark.ps1 -CaptureRunDirectory <raw-run> -BuildRunDirectory <release-client-run> -HardwareEvidence <review.json> -RunIndex 1` after sensor review. It writes a new evidence-review directory with result and application proofs, verifies any external GPU trace, and returns nonzero for failure or pending status. Analysis refuses to overwrite result/application reports. Python regressions are `Tools/test_benchmark_analysis.py`, `Tools/test_hardware_evidence.py` and `Tools/test_external_gpu.py`; synthetic validation tests never count as measured player evidence. The optional hosted Unity lane runs release allocation controls and failure shutdown checks; hosted execution remains deferred by the user.


The final 2026-11-12 source and monitored series are recorded in the handoff. GPU and allocation collection are verified, while intermittent zero native render values and continuing temperature rise remain explicit pending gates. Keep raw zero render validity unresolved even when a direct recorder emits a count; retain the frame/timestamp evidence and seek supported independent measurement or upstream clarification. Preserve every prescribed attempt and thermal trend rather than retrying until a passing subset appears. M0 remains open.

## Collector version 3 closure validation

[ADR-016](decisions.md#adr-016--accepted-conservative-render-budget-proof-and-continuous-thermal-conditioning) defines the measured CPU-frame-start-to-Present interval as a conservative render-work upper bound. Every native endpoint and all known positive work durations must pass containment checks. `render-budget-proof.json` records the method and source hashes; raw native zeros remain unknown. The render budget stays8 ms, applied to the larger complete enclosing-interval p95. Missing endpoints or failed containment cannot pass.

`Tools/Benchmark-Series.ps1 -BuildRunDirectory <release-client-run> -SensorLog <active.csv> -HardwareEvidence <memory-review.json> -PreconditionSeconds 1200` keeps the first player under the unchanged workload for 20 minutes before its standard 60-second warmup. It then launches the next two prescribed captures immediately after their predecessor, retaining all attempts and sensor snapshots. Preconditioning duration and actual capture-start ticks are explicit; the duration is configurable and must be reviewed against actual thermal observations. The plan is saved before collection/launch, and an exception records FAIL. Live sensor snapshots use bounded, writer-compatible reads. A complete series ends PendingHardware until a separate real thermal review is accepted. `Tools/Benchmark.ps1` also accepts this bounded preconditioning option for a single full capture; short diagnostics reject it. PresentMon runs through the actual player lifetime and is stopped only through its uniquely named session after the player exits.

M0 was subsequently closed with the owner's explicit waiver under [ADR-017](decisions.md#adr-017--accepted-m0-owner-waiver-of-remaining-hardware-validation). This disposition does not change the wrapper's FAIL/PendingHardware/ComputedPass logic or establish a full collector-version-3 reference pass. No further monitored runs are required for that qualified M0 closure; future technical acceptance still requires real evidence.

## M1 P01 pure generation checks

`Tools/Test-Generation.ps1 -Offline` builds the BCL-only production source under `Generation/Core` through `Tools/Generation/Shooter.Generation.Core.csproj` and runs the independent fixture/negative tests in `Tools/Generation.Tests`. It uses the existing pinned SDK/test dependencies and frozen C0 guard, writing fresh TRX/XML and a source/toolchain/seed report under `Artifacts/m1-procedural-feasibility/<source-commit>/<run-id>/`. Offline advisory lookup stays PendingNetwork. The default command requests online advisory checks. This is a P01 primitive check, not a compiler, content, release-player or hardware pass.

The locked-editor `Tools/Test-Unity.ps1 -Mode EditMode` also compiles the generation assembly and executes two generation smoke cases. Its shared wrapper still writes into the historical `Artifacts/m0-foundation` root; retain that exact path in evidence. P02 schema validation, format freeze, compiler/cache and byte-lease tests now exist. Full feature content and Unity resource installation remain pending. See the M1 handoff.

## M1 P02 compiler and byte-cache checks

`Tools/Test-Generation.ps1 -Offline` now runs 150 P01/P02 cases across the pure generation, runtime byte-store and Editor-only compiler projects. It checks both C0 and the new frozen `content-format-lock.json` before restore/build. Offline advisory checks remain PendingNetwork. `Tools/Compile-Content.ps1 -CacheRoot <repository Artifacts path>` runs the real numeric collision diagnostic. Reuse that exact cache root for a warm run; add `-Force` to regenerate and compare every immutable byte, or `-DryRun` to validate the recipe and key without creating content. A High request must reuse the same gameplay identity as Low. Unknown recipe sets reject; no default fake generator is composed.

`Tools/verify_content_format.py` checks the initial source/schema registry. Use the pinned `.local/fixtures-venv/Scripts/python.exe Tools/verify_content_schemas.py --manifest <published manifest.json> --report <Artifacts report.json>` to validate the new production schemas with full Draft 2020-12 validation. These are separate from frozen C0 fixtures. The existing wrapper report root remains `Artifacts/m0-foundation` for Unity entry/test/build calls; retain actual paths rather than rename evidence. `ShooterBuild.WriteSettingsProvenance` regenerated the source proof after the editor entry change; generated payloads were not hand-edited. See [ADR-018](decisions.md#adr-018--accepted-initial-p02-compiler-format-and-byte-cache-boundary) and handoff.

## M1 P03/P04 generator and gallery checks

`Tools/Test-Generation.ps1 -Offline` now runs229 cases and verifies `feature-content-lock.json` in addition to C0/P02 locks. A clean detached checkout reproduced229/229 without a Unity Library or editor import; use the pinned editor/package preparation before any later Unity opening. Initial P02 source-lock CRLF mistakes were corrected against exact committed LF bytes under ADR-019, not by relaxing verification. Existing C0 fixtures and generated payloads were unchanged.

`Tools/Compile-Gallery.ps1 -CacheRoot <repository Artifacts path> -Capture` compiles eleven registered content requests, checks their hashes and writes22 diagnostic PNGs. Reuse the same cache root for warm builds, add `-Force` for byte reproduction, or `-Profile High` for the same explicit-resolution visual payloads and invariant gameplay keys. The wrapper selects Player before import, so it also works after a Dedicated Server build. `Tools/Smoke-Gallery.ps1 -BuildRunDirectory <release client run> -CacheRoot <verified gallery cache>` validates actual IL2CPP cache/mesh/texture/shader loading and at least60 empty-authority ticks. Default client/server smoke remains separate. All gallery work is M1-content-gallery-v1 and must reject substitution for B01-materials-v1.

`Tools/verify_feature_registry.py` pins the27 reviewed generator/recipe/schema files. The pinned `.local/fixtures-venv/Scripts/python.exe Tools/verify_feature_schemas.py --report <Artifacts JSON>` validates nine current source inputs plus four version migration cases. Source files live in `Generation/FeatureRecipes`; no image/model/animation input is present. C#/HLSL is an allowed source type in the provenance audit. The separate client-only `Shooter.FeatureBuild.Editor` keeps presentation dependencies out of server tools; player shader inclusion is explicit in the existing build composition. Both normal variants compile through bounded local multi_compile keywords. The installed editor exposes no used variant-count API in this implementation; its report flag stays unavailable instead of treating zero as a count.

The locked runtime does not enable screenshot/image-conversion modules, so optional player PNG capture was removed rather than changing packages. Editor captures remain available; release smoke tests verify loading/lifecycle. Uncompressed RGBA32 texture inventory is7340004bytes; estimated mesh payload inventory is702096bytes, including the two analytic box buffers. The11604triangle inventory includes all weapon LODs plus eight box instances, not per-frame visible/shadow work. The continuation adds compression measurements, an exact separate B01-materials workload and production weapon LOD hysteresis; three-run hardware/resource and formal transition acceptance remain open. See the handoff for all real reports and retained failures.


## M1 soldier and material-workload continuation

Use Tools/Compile-Soldier.ps1 -CacheRoot <repository Artifacts path> -Capture for the real visual skeleton, three generated skinned LODs and baseline rifle. Reuse that root for warm compilation; add -Force to require exact immutable-byte reproduction. The tool records source, toolchain, seed, per-LOD counts and semantic joint frames. EditMode captures explicitly BakeMesh from the generated weights/bindposes/current bones before rendering; a stale bind mesh is not motion evidence. Gameplay-profile skeleton publication is rejected by the frozen SGAF v1 partition, and M1 does not install a gameplay hit evaluator.

Tools/Measure-Rig.ps1 -BuildRunDirectory <release client run> -CacheRoot <soldier cache> -Scenario walk loads eight real skinned rigs and records raw per-rendered-frame CPU duration for eight solver/bone/weapon updates. Other supported scripted scenarios include run, crouch, aim, reload, slope, steps and turn. This short release diagnostic excludes engine skinning/render cost and is not the full reference V05 performance gate. Raw errors and tolerance booleans remain explicit even when diagnostic execution succeeds.

Tools/Compile-M1Content.ps1 -CacheRoot <repository Artifacts path> compiles the complete current M1 feasibility set of twelve requests in the standalone pinned .NET tool. A new cache measures cold compilation; the same root measures verified warm reuse; -Force regenerates and compares immutable bytes. Reports retain all-thread managed allocation deltas around CompileAsync and process-lifetime peak working set with separate scope labels. This is neither Unity import timing nor full-map/shipping-load acceptance.

Tools/Measure-MaterialCompression.ps1 measures all seven families as BC7 albedo/mask and BC5 normal, including every mip. It calibrates uncompressed GPU readback, preserves compressed bytes/hashes and reports channel errors plus reconstructed-normal angles. Normal-error applicability is explicit. These are Editor platform experiments; runtime SGAF planes remain RGBA32 and its shader does not interpret BC5 as XYZ normals.

Tools/Benchmark.ps1 -BuildRunDirectory <release client run> -Workload B01-materials-v1 -MaterialCacheRoot <verified Low gallery cache> selects the separate seven-cube/floor workload. Add -Diagnostic for the short collector check and -PresentMon for the pinned external GPU trace. Full acceptance still requires prescribed 60-second warmup,180-second capture,three runs and actual hardware/thermal evidence. The original B01-minimal-v1 remains the default and rejects a material-cache argument. Tools/Benchmark-Series.ps1 accepts the same explicit workload/cache arguments plus its mandatory live sensor log. Never reuse a historical sensor CSV as current evidence.
