# M0 bootstrap runbook

Owner: integrator/WP-00. Follow [M0](milestones/m0-foundation/implementation.md). Repository root is `C:\Job\shooter`; project is `C:\Job\shooter\UnityProject`. The locked editor, project, contracts, generated-box composition, quality presets, test/build/smoke/capture wrappers now exist. Acceptance and limitations are in [handoff](milestones/m0-foundation/handoff.md). Preserve source, settings, metadata and package locks; do not recreate the project over them.

## F01 — establish the actual toolchain

1. Inspect repository status and any parent AGENTS.md. If not a git repository, initialize one only within shooter, add text/gitignore policy, and make a baseline docs commit. Do not accidentally use a parent repository.
2. Discover editors through Unity Hub and its configured editor locations. Inspect each candidate's exact version/revision. Select one installed, supported Unity 6 LTS with matching URP. Record the decision and path; if no suitable installation is available, report the precise missing installation/module and continue only independent pure-contract work.
3. Check Windows x64 IL2CPP build support, Dedicated Server support, Windows SDK/C++ toolchain and Unity license availability. Verify using real client/server builds rather than installation names alone. Record `dotnet --info` for pure tests; pin installed supported SDK in global.json during implementation.
4. Create the project from the selected editor's URP template into UnityProject. Remove template/demo art; retain only permitted engine/package assets named in provenance allowlist. Enable visible meta files and force-text serialization. Set +Y up/+Z forward and meter units in project conventions; enforce these in fixtures.
5. Commit ProjectVersion.txt, manifest.json, packages-lock.json and toolchain-lock.json. Lock fields: schemaVersion, editorVersion, editorRevision, urpVersion, inputSystemVersion, testFrameworkVersion, aiNavigationVersion, graphicsApi=`Direct3D11`, clientBackend=`IL2CPP`, clientTarget=`Windows64`, serverTarget=`Windows64Server`, dotnetSdk, requiredModules and provenanceAllowlistVersion. Record machine-local editor path in ignored local settings, not portable lock. Transport is null until M5.

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
| Compile content (explicit M0 diagnostic) | `Tools/Compile-Content.ps1 -RecipeSet slice-v1 -Profile Low` | Nonzero not-yet-implemented diagnostic; no fabricated cache manifest |
| Build (implemented) | `Tools/Build.ps1 -Target Client` / `-Target Server` | Release player, source/content/toolchain manifest and packed-source audit |
| Smoke (implemented) | `Tools/Smoke.ps1 -BuildRunDirectory <build-run>` | Ready marker, ≥60 ticks, clean exit; server client-assembly/graphics exclusion |
| Benchmark (implemented) | `Tools/Benchmark.ps1 -BuildRunDirectory <client-build-run> -Workload B01-minimal-v1 -Profile Low -RunIndex 1` | Raw frame/tick CSV, external memory samples, machine and versioned result JSON; no stale/synthetic pass |

These wrappers and `ShooterBuild` entry points now exist. Full offline content compilation remains M1–M3 work: M0 draws the fixed box/floor directly from numeric code and reports this limitation explicitly. `CompileContent` does not pretend to implement a compiler or a slice cache.

## Fresh checkout and locked editor

Select/install exactly the editor and Windows IL2CPP/Dedicated Server modules recorded in root `toolchain-lock.json`. An appropriate Unity license must be active on each build machine; the local development account activated Unity Personal through the official CLI/browser flow. CI uses a prelicensed Windows runner; no account credentials, serials or license files belong in this repository.

```powershell
./Tools/Configure-Unity.ps1 -EditorPath '<installed editor>/Editor/Unity.exe'
./Tools/Test-Unity.ps1 -Mode EditMode
./Tools/Test-Unity.ps1 -Mode PlayMode
./Tools/Build.ps1 -Target Client
```

Configure prepares the exact Core RP source curation from the installed editor before project resolution, verifies its upstream/curated inventory hashes, and imports through `ShooterBuild.VerifyToolchain`. Local editor path/executable hash live only in ignored `.local/unity.json`. The portable lock records editor revision, packages, backends, modules and curation. [ADR-013](decisions.md#adr-013--accepted-initial-m0-toolchain-and-explicit-engine-resource-curation) explains why the unused debug UI Resources and image-backed shader lookup references are excluded. It also records the initial bundled-template Input System incompatibility; no later feature may silently change the selected toolchain.

`ShooterBuild.InitializeFoundation` regenerates the numeric presets/material/lookup assets and code-owned scene; it is a deliberate generator operation, not a routine way to accept changed golden output. Source/toolchain/generated-file hashes are in `Assets/Shooter/Settings/provenance.json`. Native URP build preprocessors populate shader-prefilter cache fields; the build stage records that real generated output after restoring project state. A mismatched source/package/asset hash must be investigated.

The current camera is fixed and the authority contains zero actors. It validates composition/clock/publication, never movement, weapons, bots or an objective. Client view construction is in Bootstrap/Client; Presentation and Bootstrap/Client exclude UNITY_SERVER. Server construction uses no presentation types or graphics cache. All three presets share the same collision and navigation adapter/data. Unity tests explicitly select StandaloneWindows64/Player before loading, avoiding a previous server build's persisted UNITY_SERVER context; zero discovered tests is a failure. See the [editor command-line reference](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/editor).

## Player and benchmark evidence

Build prints a unique run directory containing `player/Shooter.exe`, `build-manifest.json`, `harness-manifest.json` and logs. Pass that directory to Smoke or Benchmark. The release client is IL2CPP; the Windows Dedicated Server is Mono. Packed-source audits reject imported art, extensionless built-in sprite/logo entries, and any graphics payload in the server. Both splash and Unity-logo flags are disabled; there is no logo art exception. Dedicated builds temporarily clear render dependencies, then reload the three configured pipeline assets/global settings by path to restore the client project; stale native object handles must not restore null references. No package/version upgrade or fallback is allowed by a wrapper.

```powershell
./Tools/Smoke.ps1 -BuildRunDirectory '<printed build-run directory>'
./Tools/Benchmark.ps1 -BuildRunDirectory '<client build-run>' -Diagnostic
./Tools/Benchmark.ps1 -BuildRunDirectory '<client build-run>' -RunIndex 1
```

Diagnostic capture is 2s warmup/5s sampling and writes a separate diagnostic report, never B01 acceptance. Full B01 uses 60s/180s, exactly 10800 captured ticks, and run indices 1–3. Keep the client/content/route/toolchain fixed across all three. CSV capacity overflow, missing tick/frame samples, wrong GPU/API/resolution/profile, clock overload or process timeout invalidate the run. Raw unavailable timings use a negative sentinel only in CSV; the analyzer emits JSON null plus reasons. Percentiles use nearest rank. Runtime CPU/GPU/GC timing availability is explicit; process memory is sampled externally at 1Hz and may miss subsecond peaks. Do not call those peaks exact.

The wrapper discovers CPU, actual rendered GPU, RAM topology/speed, driver, power plan, AC power and system-disk bus. RAM channel mode remains unverified at the user's request; populated A/B slots do not prove operating interleaving. Thermal stabilization is not inferred from a timer. Such missing conditions keep acceptance pending; timing-budget failures remain failures. No B01 result establishes combat performance or a 30-minute soak. The versioned benchmark schema is unchanged.

`.github/workflows/foundation.yml` provides hosted Windows fast checks and a manually requested sequential Unity lane on a prelicensed `unity-6000.3.28f1` runner using `SHOOTER_UNITY_EDITOR`. Action commits are pinned. Local wrapper execution is not evidence that hosted CI ran.

## Implemented independent checks

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

`jsonschema` 4.26.0 verifies all six schema cases and expected negative paths. The standard-library fixture verifier separately checks semantics/hashes/vectors. `Test-SourceProvenance.ps1` checks Assets sources, metadata/GUID uniqueness, generated settings provenance and the named nonshipping `abc` fixture; player builds additionally audit packed dependencies. It does not certify future M1 content caches. M0 uses no font, sprite UI or imported surface texture. `python Tools/test_benchmark_analysis.py` tests nearest-rank analysis and invalid-sample rejection.

## Foundation completion

Fresh checkout reproduces import; pure contracts compile; blank client/server boot and terminate; B01-minimal records real counters; lock/provenance/docs checks run; AGENTS and ownership ledger reflect actual work. Exact missing dependencies and hardware measurements remain pending until proven.
