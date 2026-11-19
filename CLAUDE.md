# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

`AGENTS.md` holds the binding project rules (scope/ownership, the no-imported-art requirement, verification and completion duties). Read it first; this file only adds orientation and commands.

## What this is

A Unity 6 URP procedural military FPS (offline team deathmatch vs bots; networking and the dedicated server are parked under ADR-076) where all geometry, materials, rigs and motion are generated from code/numeric recipes. The repo is spec-heavy: `docs/` is the authoritative specification and status record, and work is organized as milestones (M0–M11) and numbered tasks (T0xx). Before any edit, find the current state and assignment in `docs/next-playable-build.md` (the only status page); gates and scenario IDs are in `docs/acceptance.md`; `docs/index.md` links everything else. Superseded pages are not kept in the tree: use git history. Design changes go through ADRs in `docs/decisions.md` (index table at the top).

Toolchain is pinned in root `toolchain-lock.json` (Unity 6000.3.28f1 rev 8f082d2fe476, URP 17.3.0) and `global.json` (.NET SDK 10.0.301, no roll-forward). Never upgrade or substitute either.

## Architecture: one source tree, two build paths

All C# lives under `UnityProject/Assets/Shooter/`, split into asmdefs with strict dependency rules (table in `docs/architecture.md`):

- `Contracts` (C0-v1) and `Game/Contracts` (Game-v1): BCL-only IDs/DTOs/ports, `noEngineReferences`. Frozen, with hashes in `contract-lock.json` and `game-contract-lock.json`. Changes need an ADR plus fixture migration.
- `Generation/Core`: pure deterministic generators. `Generation/Editor` is the Unity-only asset compiler; runtime never references it. The content format is frozen in `content-format-lock.json` and the feature recipes in `feature-content-lock.json`.
- `Simulation`: fixed 60 Hz authority (motor, combat, pose, AI, match, session, TDM). It has no Unity rendering, Editor or transport references. Tick order is specified in `docs/simulation-protocol.md`.
- `World`: collision/nav adapters. `Content`: cache loading and map packages. `Networking`: codec, prediction and lag compensation, parked and dormant (ADR-076; no new code); it compiles only under `SHOOTER_NETWORKING` and is tested only by `Tools/Test-Networking.ps1` (ADR-120).
- `Presentation`: client-only, read-only consumer of snapshots. It can never apply damage or mutate authority, and it is excluded under `UNITY_SERVER`.
- `Bootstrap`: the only composition root (`Core`, `Client`, `Runtime`, `Release`, `Editor`). `ShooterBuild.*` Editor entry points (`BuildClient`, `BuildServer` (parked), `CompileContent`, `InitializeFoundation`, `WriteSettingsProvenance`, …) live here.

**Key non-obvious point:** every pure asmdef is also compiled outside Unity by a matching SDK project `Tools/<Assembly>/Shooter.<Assembly>.csproj` (e.g. `Tools/Contracts`, `Tools/Game.Contracts`, `Tools/Generation`, `Tools/Simulation`, `Tools/World.Runtime`, `Tools/Presentation.Model`, `Tools/Bootstrap.Core`), which globs the asmdef folder with `<Compile Include="../../UnityProject/Assets/Shooter/<folder>/**/*.cs">` and mirrors its references; `Tools/verify_asmdef_graph.py` fails when the two graphs drift. The xUnit projects `Tools/*.Tests` reference those projects instead of linking source (ADR-076). So code in those folders must stay C# 9 / .NET Standard 2.1-compatible and Unity-free unless the csproj explicitly excludes it. If you add a Unity-dependent file under a pure folder, the pure tests break. If you move code between folders, check the csproj globs and the asmdef graph.

## Commands (PowerShell 7, from repo root)

Pure tests run without Unity. Each wrapper checks the frozen lock identities first, pins the SDK, writes a report under `Artifacts/<milestone>/<commit>/<run-id>/` and exits nonzero on any failure, skip or timeout. Add `-Offline` to restore from the local NuGet cache only (advisory check then records PendingNetwork).

```powershell
./Tools/Test-All.ps1            # one restore + build of Tools/Shooter.Pure.slnx, then the four suites below in parallel
./Tools/Test-Contracts.ps1      # Tools/Contracts.Tests (C0)
./Tools/Test-Generation.ps1     # Tools/Generation.Tests (generators, compiler, content store, map packages)
./Tools/Test-Simulation.ps1     # Tools/Simulation.Tests (motor/combat/AI/network/view/hud/shell)
./Tools/Test-Game.ps1           # Tools/Game.Contracts.Tests (Game-v1, TDM, settings, workloads)
./Tools/Test-Maps.ps1           # map catalog/spawn/package tests
./Tools/Test-SourceProvenance.ps1  # Assets sources, .meta/GUID completeness, settings provenance
python docs/fixtures/verify_fixtures.py
python docs/verify_docs.py
python Tools/verify_contract_lock.py
```

Single test (iteration only; this bypasses the lock guard and produces no evidence report):

```powershell
dotnet test Tools/Simulation.Tests/Shooter.Simulation.Tests.csproj --filter "FullyQualifiedName~MotorTests"
```

Full JSON Schema checks need the hash-pinned venv:

```powershell
python -m venv .local/fixtures-venv
./.local/fixtures-venv/Scripts/python.exe -m pip install --require-hashes -r Tools/requirements-fixtures.lock
./.local/fixtures-venv/Scripts/python.exe Tools/verify_schemas.py --report Artifacts/toolchain-discovery/schema-results.json
```

Python tooling tests are standalone scripts (`python Tools/test_*.py`). PowerShell wrapper regressions are `./Tools/test_*.ps1` and run in synthetic repos with no Unity.

Unity lanes need the locked editor. On a fresh checkout run Configure first: it materializes the hash-pinned curated Core RP package (ADR-013) and writes the machine-local editor path to ignored `.local/unity.json`.

```powershell
./Tools/Configure-Unity.ps1 -EditorPath '<editor>/Editor/Unity.exe'
./Tools/Test-Unity.ps1 -Mode EditMode     # or -Mode PlayMode; zero tests = failure
./Tools/Build.ps1 -Target Client          # IL2CPP/D3D11; -Target Server (Mono) only on request, parked under ADR-076
./Tools/Smoke.ps1 -BuildRunDirectory <printed build-run dir>
./Tools/Compile-GameContent.ps1           # product map packages
./Tools/Package-Release.ps1 -ClientBuildRunDirectory <client build-run>
./Tools/Deliver.ps1 -ForceContent [-ReleaseVersion <n.n.n>]  # content + client build in one editor session, smoke, package, installer
./Tools/Benchmark.ps1 -BuildRunDirectory <client build-run> -Workload TDM-5v5-v1 -Profile Low -RunIndex 1
```

Other `Compile-*`, `Measure-*`, `Benchmark-*` and `Review-Benchmark` wrappers are documented with their exact scope in `docs/bootstrap-runbook.md`. Each covers only its named content set or workload. CI (`.github/workflows/foundation.yml`) runs the fast pure lane on hosted Windows and the Unity lane only on manual request.

## Repo-specific gotchas

- **Docs integrity manifest:** any change under `docs/` (`.md`/`.py`/`.json`/`.bin`) makes `docs/verify_docs.py` fail until you deliberately run `python docs/verify_docs.py --refresh-manifest` and commit the updated `docs/documentation-manifest.json`.
- **Hash-locked docs:** `docs/content-format.md`, `docs/weapon-recipe-baseline.md`, `docs/soldier-baseline.md` and everything in `docs/fixtures`, `game-fixtures`, `feature-fixtures`, `feature-schemas` and `content-schemas` are pinned by the root lock files; editing them needs a reviewed lock refreeze (`python Tools/verify_<lock>.py --refreeze ADR-NNN`, never a hand edit). The feature lock hashes generator C# by code tokens, so comment-only edits there need no refreeze (ADR-121). ADR headings in `docs/decisions.md` are link anchors cited by schemas, locks and generator output, so never rename them.
- **Unity `.meta` files:** every asset and folder under `UnityProject/Assets` needs its committed `.meta`. Missing or orphan metadata fails `Test-SourceProvenance.ps1`.
- **Generated outputs** (`Assets/Shooter/Settings/provenance.json`, generated presets/scenes, goldens, lock files) are regenerated through their named `ShooterBuild` editor method or tool, never hand-edited to make checks pass.
- **Evidence semantics:** results are PASS / FAIL / Pending (PendingHardware, PendingNetwork…). Missing editor, hardware or counters is Pending, never PASS. Synthetic or diagnostic runs (B01, gallery, smoke) never substitute for product acceptance. Record real outcomes in the relevant milestone `handoff.md`.
- `Artifacts/` and `.local/` are ignored local evidence and tooling. Preserve required evidence; don't delete it casually.
- Text is LF (`.gitattributes`); `.local/worktrees` holds per-worker worktrees, and completed work must be merged to `main` and the worktree removed.
