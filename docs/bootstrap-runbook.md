# Runbook: commands and wrapper scopes

Run everything from the repository root in PowerShell 7.

Every wrapper:
- checks the frozen lock identities first;
- pins the SDK and toolchain;
- writes a report under `Artifacts/<area>/<commit>/<run>/`;
- exits nonzero on any failure, skip or timeout.

Each wrapper covers only its own scope. Builder, smoke, package and installer PASS results are not gameplay evidence ([acceptance](acceptance.md#evidence-rules)).

Do not run two SDK wrappers that build at the same time in one checkout, because they share `bin/obj`. `Test-All.ps1` is the exception: it builds once, then runs its suites in parallel with `-NoBuild`, which only reads the build output.

## Pure lanes (no Unity)

```powershell
./Tools/Test-All.ps1              # the four suites below from one restore + build (Tools/Shooter.Pure.slnx), in parallel
./Tools/Test-Contracts.ps1        # C0 (Tools/Contracts.Tests)
./Tools/Test-Generation.ps1       # generators, compiler, content store, map packages
./Tools/Test-Simulation.ps1       # motor, combat, AI, view, HUD, shell
./Tools/Test-Game.ps1             # Game-v1, TDM, settings, workloads
./Tools/Test-Maps.ps1             # map catalog, spawn and package tests
./Tools/Test-SourceProvenance.ps1 # Assets sources, .meta/GUID completeness, settings provenance
./Tools/Test-Networking.ps1       # parked networking only (ADR-120); not part of the default lanes
python docs/fixtures/verify_fixtures.py
python docs/verify_docs.py
python Tools/verify_contract_lock.py
```

`Test-All` does the locked restore (with the NuGet advisory check) and the Release build once for the whole solution, then starts each suite with `-NoBuild`; each suite still checks its own locks and writes its own evidence, and `test-all-<run>/run.json` lists them with their durations (about 170 s in total on the dev laptop, bounded by Test-Generation). The single suites still restore and build on their own when run directly.

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

Every Unity wrapper re-runs the curation check. After one full hash verification, `prepare_unity_packages.py` writes `.local/unity-curation.stamp`: a key over the pinned curation, the installed source tree's file sizes and modification times, and the archive's size and modification time. While that key matches, later checks skip re-hashing (about 0.6 s instead of 11 s). Any change to those inputs forces the full verification again.

**Unity tests** select StandaloneWindows64. Zero discovered tests counts as a failure.

**Settings provenance.** `ShooterBuild.InitializeFoundation` regenerates the numeric presets, material/lookup assets and the code-owned scene. It is a deliberate generator step, never a way to accept changed output. Settings provenance is recorded in `Assets/Shooter/Settings/provenance.json`.

## Content, build, package and installer

```powershell
./Tools/Deliver.ps1 -ForceContent [-ReleaseVersion <n.n.n> -PreviousInstallerReport <installer-report.json>]
```

`Deliver.ps1` needs a clean tree and runs the steps below with no manual run-folder copying. It writes `deliver.json` with each step's arguments, exit code and duration under `Artifacts/m11-playable-release/<commit>/deliver-<run>/`. Without `-ReleaseVersion` it stops after Package-Release. The individual steps:

```powershell
./Tools/Compile-GameContent.ps1 -Native -Force [-AndBuildClient]
./Tools/Build.ps1 -Target Client
./Tools/Smoke.ps1 -BuildRunDirectory <printed build-run>
./Tools/Package-Release.ps1 -ClientBuildRunDirectory <client build-run> -MapCacheRoot Artifacts/map-packages-native -RequireNativeNav
./Tools/Build-Installer.ps1 -PackageDirectory <package run> -ReleaseVersion <n.n.n> -PreviousInstallerReport <installer-report.json>
```

**Content (`-Native`).** Pure .NET content is published first. Then `ShooterBuild.CompileNativeGameContent` bakes navigation from that collision, and .NET republishes with the native bakes ([ADR-043](decisions.md#adr-043--accepted-two-stage-native-navigation-publication)). The step writes `nav-provenance.json` and reports `nativeUnityNavBake=PASS` only after re-verifying every package. Without `-Native`, the bake is the labelled `software-walkable-grid` diagnostic. `-RequireNativeNav` refuses software navigation.
- The pure stage produces the reproduction, cancel and corrupt-recovery proofs. The native republication skips them (`selfProofs=false` in its proof) because the non-navigation byte-identity guard covers its output.
- After the guard passes, identical non-navigation files in `Artifacts/map-packages` become NTFS hard links to their `map-packages-native` twins (`nonNavByteIdentityGuard.sharedBytes`). Package files are only ever replaced through atomic staging, never rewritten in place, so sharing is safe.

**One editor session.** `-AndBuildClient` runs `ShooterBuild.CompileNativeGameContentAndBuildClient`: the navigation bake and the client build share one editor start-up. The build is an ordinary `build-client-<run>` folder with the same manifest and checks as `Build.ps1`, and `validation.json` records it as `buildRunDirectory`.

**Build.** A build prints a run directory containing `player/Shooter.exe`, `build-manifest.json` and logs. The Client is IL2CPP/D3D11. Packed-source audits reject imported art, and splash and logo are off. After a successful build, `Build.ps1` deletes the never-shipped `*_BackUpThisFolder_ButDontShipItWithYourGame` and `*_BurstDebugInformation_DoNotShip` folders, about 1.15 GB per run, and records them in `run.json` as `removedDebugFolders`. The incremental IL2CPP cache stays in `UnityProject/Library`.

**Smoke.** Smoke needs the ready marker, at least 60 ticks and a clean exit. It is a diagnostic only.

**Package and installer.**
- `Package-Release` rejects dirty or mismatched builds. It emits the staged `Shooter-Offline` folder, SHA-256 inventory, controls, licenses, known issues, and `validation.json` with `releaseAcceptance=NotClaimed`.
- Staging hard-links player, content and audio files instead of copying them (`stagedHardLinks` / `stagedCopies` in `validation.json`). It falls back to copying across volumes. The inventory hashes every staged file, and `Build-Installer` re-checks it, so a source rewritten in place cannot ship unnoticed.
- The ZIP is an internal artifact and is opt-in (`-Zip`). It is written with `System.IO.Compression.ZipFile` and verified by hashing every entry stream against the inventory, without extracting it.
- The installer is described in [installer](features/windows-installer.md) ([ADR-041](decisions.md#adr-041--accepted-inno-setup-per-user-installer-and-in-place-upgrade-t073)).
- `-Target Server` builds only on request and is parked (ADR-076).

**Real-client view recording** (review only, never acceptance; [ADR-129](decisions.md#adr-129--accepted-licensed-first-person-arms-wrapped-support-grips-strafe-clip-facing-and-grenade-start-contact)):
- `./Tools/Record-ViewModel.ps1 -BuildRunDirectory <build-client run> -OutDir <dir>` starts the built player windowed, enters a match from Main, and records it with ffmpeg (which must be on `PATH`).
- A fixed timeline of real keyboard and mouse input drives the player: rifle idle, ADS, fire, walk, sprint, tactical and empty reloads; then the pistol; then knife and grenade.
- It writes `viewmodel.mp4`, `timeline.json`, stills per step and a labelled `sheet.png`.
- It takes over the desktop for about 1.5 minutes. A bot can kill the player mid-run, so check the sheet.

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

Each wrapper keeps only its newest output (ADR-076). Small `*.json` reports move to `Artifacts/reports/<action>/<commit>/<run>/`. The pure test wrappers, `Test-Maps` and `Compile-GameContent` write prefixed run folders (`test-contracts-`, `test-generation-`, `test-simulation-`, `test-game-`, `test-maps-`, `compile-gamecontent-`) and prune their own older runs after a PASS. Older unprefixed runs are left to `Clean-Artifacts`.
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
