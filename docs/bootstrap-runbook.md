# Runbook: commands and wrapper scopes

Run everything from the repository root in PowerShell 7.

Every wrapper:
- checks the frozen lock identities first;
- pins the SDK and toolchain;
- writes a report under `Artifacts/<area>/<commit>/<run>/`;
- exits nonzero on any failure, skip or timeout.

Each wrapper covers only its own scope. Builder, smoke, package and installer PASS results are not gameplay evidence ([acceptance](acceptance.md#evidence-rules)).

Do not run two SDK wrappers at the same time in one checkout, because they share `bin/obj`.

## Pure lanes (no Unity)

```powershell
./Tools/Test-Contracts.ps1        # C0 (Tools/Contracts.Tests)
./Tools/Test-Generation.ps1       # generators, compiler, content store, map packages
./Tools/Test-Simulation.ps1       # motor, combat, AI, view, HUD, shell
./Tools/Test-Game.ps1             # Game-v1, TDM, settings, workloads
./Tools/Test-Maps.ps1             # map catalog, spawn and package tests
./Tools/Test-SourceProvenance.ps1 # Assets sources, .meta/GUID completeness, settings provenance
python docs/fixtures/verify_fixtures.py
python docs/verify_docs.py
python Tools/verify_contract_lock.py
```

`-Offline` restores from the local NuGet cache only and records the advisory lookup as PendingNetwork. For iteration you can run a single `dotnet test ... --filter`. That skips the lock guard and produces no evidence report.

**Lock verifiers.** These run inside the wrappers and never refresh a lock implicitly:
- `Tools/verify_contract_lock.py`
- `verify_game_contract_lock.py`
- `verify_content_format.py`
- `verify_feature_registry.py`

The frozen C0 baseline is recorded in [ADR-014](decisions.md#adr-014--accepted-initial-c0-source-freeze-and-failure-capture-lifecycle).

**Full JSON Schema checks** need the hash-pinned venv (Windows x64, CPython 3.12):

```powershell
python -m venv .local/fixtures-venv
./.local/fixtures-venv/Scripts/python.exe -m pip install --require-hashes -r Tools/requirements-fixtures.lock
./.local/fixtures-venv/Scripts/python.exe Tools/verify_schemas.py --report Artifacts/toolchain-discovery/schema-results.json
```

**Tooling regressions** run in synthetic repositories with no Unity:
- `python Tools/test_*.py`
- `./Tools/test_*.ps1` (smoke, Unity results, provenance, package, installer, build lock, artifact retention)

Synthetic logs never count as player evidence.

## Fresh checkout and the locked editor

Install exactly the editor and modules in `toolchain-lock.json`. Every build machine needs an active Unity license. No credentials go in the repository.

```powershell
./Tools/Configure-Unity.ps1 -EditorPath '<installed editor>/Editor/Unity.exe'
./Tools/Test-Unity.ps1 -Mode EditMode
./Tools/Test-Unity.ps1 -Mode PlayMode
```

**What Configure does:**
- It materializes the hash-pinned Core RP archive ([ADR-013](decisions.md#adr-013--accepted-initial-m0-toolchain-and-explicit-engine-resource-curation)).
- It verifies the curated inventories.
- It imports through `ShooterBuild.VerifyToolchain`.
- It writes the machine-local editor path to the ignored `.local/unity.json`.

**Unity tests** select StandaloneWindows64. Zero discovered tests counts as a failure.

**Settings provenance.** `ShooterBuild.InitializeFoundation` regenerates the numeric presets, material/lookup assets and the code-owned scene. It is a deliberate generator step, never a way to accept changed output. Settings provenance is recorded in `Assets/Shooter/Settings/provenance.json`.

## Content, build, package and installer

```powershell
./Tools/Compile-GameContent.ps1 -Native -Force
./Tools/Build.ps1 -Target Client
./Tools/Smoke.ps1 -BuildRunDirectory <printed build-run>
./Tools/Package-Release.ps1 -ClientBuildRunDirectory <client build-run> -MapCacheRoot Artifacts/map-packages-native -RequireNativeNav
./Tools/Build-Installer.ps1 -PackageDirectory <package run> -ReleaseVersion <n.n.n> -PreviousInstallerReport <installer-report.json>
```

**Content (`-Native`).** Pure .NET content is published first. Then `ShooterBuild.CompileNativeGameContent` bakes navigation from that collision, and .NET republishes with the native bakes ([ADR-043](decisions.md#adr-043--accepted-two-stage-native-navigation-publication)). The step writes `nav-provenance.json` and reports `nativeUnityNavBake=PASS` only after re-verifying every package. Without `-Native`, the bake is the labelled `software-walkable-grid` diagnostic. `-RequireNativeNav` refuses software navigation.

**Build.** A build prints a run directory containing `player/Shooter.exe`, `build-manifest.json` and logs. The Client is IL2CPP/D3D11. Packed-source audits reject imported art, and splash and logo are off.

**Smoke.** Smoke needs the ready marker, at least 60 ticks and a clean exit. It is a diagnostic only.

**Package and installer.**
- `Package-Release` rejects dirty or mismatched builds. It emits the archive, SHA-256 inventory, controls, licenses, known issues, and `validation.json` with `releaseAcceptance=NotClaimed`.
- The installer is described in [installer](features/windows-installer.md) ([ADR-041](decisions.md#adr-041--accepted-inno-setup-per-user-installer-and-in-place-upgrade-t073)).
- `-Target Server` builds only on request and is parked (ADR-076).

**Diagnostic compilers** never produce product content:
- `Compile-Content.ps1`, `Compile-Gallery.ps1` and `Smoke-Gallery.ps1`;
- `Compile-Soldier.ps1` and `Compile-M1Content.ps1`;
- the `Measure-*` scripts.

Product map registry and weapon catalog generators are `python Tools/generate_map_registry.py` and `python Tools/generate_weapon_catalog.py`.

## Benchmarks and hardware evidence

```powershell
./Tools/Configure-PresentMon.ps1
./Tools/Benchmark.ps1 -BuildRunDirectory <client build-run> -Workload TDM-5v5-v1 -Profile Low -RunIndex 1 [-PresentMon] [-HardwareEvidence <review.json>]
./Tools/Benchmark-Series.ps1 -BuildRunDirectory <client build-run> -SensorLog <live.csv> -HardwareEvidence <review.json> -PreconditionSeconds 1200
./Tools/Benchmark-Product-Matrix.ps1 -BuildRunDirectory <client build-run> -SensorLog <live.csv> -HardwareEvidence <review.json>
./Tools/Benchmark-Soak.ps1 -BuildRunDirectory <client build-run> -Workload TDM-soak-15v15-compound-v1 -HardwareEvidence <review.json>
./Tools/Review-Benchmark.ps1 -CaptureRunDirectory <raw-run> -BuildRunDirectory <client build-run> -HardwareEvidence <review.json> -RunIndex 1
```

Workloads and the capture protocol are in [performance](performance.md).

**Run integrity.**
- `-Diagnostic` capture (2 s + 5 s) never counts toward acceptance.
- Run with the machine idle.
- `BenchmarkProgressWatchdog` aborts a capture whose match tick stalls for 10 s and records the state.
- These conditions invalidate the run:
  - a missing tick or frame sample;
  - the wrong GPU, API, resolution or profile;
  - clock overload;
  - a timeout.

**GPU timing and the review file.**
- PresentMon gives process-specific external GPU timing for the reference driver's intermittent GPU gaps. Configure verifies the pinned executable and changes no machine settings.
- Hardware review JSON (`shooter.hardware-review.v1`) names the reviewer, time and rationale. It can carry memory channel-mode and thermal-stabilization proofs.

**Measurement rules.**
- Allocation measurement follows the calibrated collector ([ADR-015](decisions.md#adr-015--accepted-calibrated-release-measurement-and-immutable-evidence-review)). Render time uses the conservative interval ([ADR-016](decisions.md#adr-016--accepted-conservative-render-budget-proof-and-continuous-thermal-conditioning)).
- Thermal stability is never inferred from elapsed time.
- `Test-Measurement.ps1` checks the allocation controls. `Test-FailureShutdown.ps1` checks stall handling in the release player.

**Recording outcomes.** Without the reference machine, its counters or a thermal review, the outcome is PendingHardware. A failed timing budget stays a failure.

**Regressions:**
- `Tools/test_benchmark_analysis.py`
- `test_tdm_benchmark_analysis.py`
- `test_hardware_evidence.py`
- `test_external_gpu.py`

## Disk policy

Each wrapper keeps only its newest output (ADR-076). Small `*.json` reports move to `Artifacts/reports/<action>/<commit>/<run>/`.
- `SHOOTER_KEEP_RUNS=<n>` keeps more runs.
- Heavy wrappers refuse to start below `SHOOTER_MIN_FREE_GB` of free space (default 5).

`./Tools/Disk-Report.ps1` shows where the space goes.

```powershell
./Tools/Clean-Artifacts.ps1                 # dry run
./Tools/Clean-Artifacts.ps1 -Apply [-IncludeBuildCaches] [-KeepRecentHours 6] [-KeepNewestPerArea 1]
```

**What it touches.** Only per-commit folders `Artifacts/<area>/<40-hex commit>/`.

**A run is kept when any of these holds:**
- its full or short hash appears in a file tracked at HEAD (handoffs, `coordination.json`, locks, `evidence-retention.txt`);
- it is HEAD's run;
- it is the newest run of its area;
- it is within `-KeepRecentHours`;
- it holds the newest `installer-report.json`.

**What it never touches:** `.local`, `UnityProject/Library` and non-commit folders.

`-IncludeBuildCaches` also removes `Tools/*/bin`, `obj` and `__pycache__`. Do not use it while another wrapper is running.
