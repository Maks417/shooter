# M0 bootstrap runbook

Owner: integrator/WP-00. Follow [M0](milestones/m0-foundation/implementation.md). These are steps for the implementation task, not actions already executed. Repository root is `C:\Job\shooter`; Unity project is `C:\Job\shooter\UnityProject`. Preserve docs and AGENTS.md when creating the project.

## F01 — establish the actual toolchain

1. Inspect repository status and any parent AGENTS.md. If not a git repository, initialize one only within shooter, add text/gitignore policy, and make a baseline docs commit. Do not accidentally use a parent repository.
2. Discover editors through Unity Hub and its configured editor locations. Inspect each candidate's exact version/revision. Select one installed, supported Unity 6 LTS with matching URP. Record the decision and path; if no suitable installation is available, report the precise missing installation/module and continue only independent pure-contract work.
3. Check Windows x64 IL2CPP build support, Dedicated Server support, Windows SDK/C++ toolchain and Unity license availability. Verify using real client/server builds rather than installation names alone. Record `dotnet --info` for pure tests; pin installed supported SDK in global.json during implementation.
4. Create the project from the selected editor's URP template into UnityProject. Remove template/demo art; retain only permitted engine/package assets named in provenance allowlist. Enable visible meta files and force-text serialization. Set +Y up/+Z forward and meter units in project conventions; enforce these in fixtures.
5. Commit ProjectVersion.txt, manifest.json, packages-lock.json and toolchain-lock.json. Lock fields: schemaVersion, editorVersion, editorRevision, urpVersion, inputSystemVersion, testFrameworkVersion, aiNavigationVersion, graphicsApi=`Direct3D11`, clientBackend=`IL2CPP`, clientTarget=`Windows64`, serverTarget=`Windows64Server`, dotnetSdk, requiredModules and provenanceAllowlistVersion. Record machine-local editor path in ignored local settings, not portable lock. Transport is null until M5.

No hard-coded editor path or package version is provided because neither has been verified on this machine. Once F01 runs, the lock becomes mandatory rather than an optional placeholder.

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
| Pure tests | `Tools/Test-Contracts.ps1` | Nonzero on failure, test-results.xml and fixture version |
| EditMode/PlayMode | `Tools/Test-Unity.ps1 -Mode EditMode` / `-Mode PlayMode` | Editor log + fresh XML, no stale-output pass |
| Compile content | `Tools/Compile-Content.ps1 -RecipeSet slice-v1 -Profile Low` | Verified manifest or explicit not-yet-implemented diagnostic at M0 |
| Build | `Tools/Build.ps1 -Target Client` / `-Target Server` | Clean build/report and matching lock hash |
| Benchmark | `Tools/Benchmark.ps1 -Workload B01-minimal-v1 -Profile Low` | Versioned result JSON; real reference-machine report |

These wrappers do not exist yet. F03/F04 must implement them and update this table with actual usage. Until then, do not give a user a success command that merely creates an empty report. M0 content compilation may only support the box recipe; full slice compilation is M1–M3 work.

## Foundation completion

Fresh checkout reproduces import; pure contracts compile; blank client/server boot and terminate; B01-minimal records real counters; lock/provenance/docs checks run; AGENTS and ownership ledger reflect actual work. Exact missing dependencies and hardware measurements remain pending until proven.
