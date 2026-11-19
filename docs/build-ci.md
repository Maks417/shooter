# Build, CI and release strategy

Commands and wrapper scopes are in the [runbook](bootstrap-runbook.md). Release gates are in [acceptance](acceptance.md).

Under [ADR-076](decisions.md#adr-076--accepted-offline-only-scope-and-bounded-local-evidence), CI and the release flow build and smoke the Client only. `Build.ps1 -Target Server` still works if you explicitly request it, but nothing depends on it.

## Lanes

| Lane | Trigger and environment | Checks |
|---|---|---|
| Fast | Every push and PR; hosted Windows (`.github/workflows/foundation.yml`) | Docs, fixtures, schemas, contract lock, asmdef graph, generated registries, `Test-Contracts`, source provenance and the Python/PowerShell tool regressions. Run Generation, Simulation and Game locally before merging. |
| Unity | Manual dispatch; self-hosted runner with a license and the locked editor | EditMode/PlayMode, Client build, smoke, measurement and failure-shutdown checks |
| Content | Before each delivery | `Compile-GameContent.ps1 -Native` from an empty cache; hash and provenance validation |
| Hardware | Before release; idle reference laptop | Product TDM matrix and soaks ([performance](performance.md)) |

How the Unity lane runs:
- **Invocation.** It runs the exact pinned editor with `-batchmode`, the project path, and `-runTests` with result and log paths. Builds use `ShooterBuild.BuildClient`.
- **What counts as failure.** A timeout, a hung license or a hung import fails the job. An output file left over from an earlier run never counts as success.
- **No `-nographics`.** Never pass it to a lane that bakes textures or validates shaders.
- **Fresh checkouts.** Run `Tools/Configure-Unity.ps1` first. It verifies the editor and materializes the hash-pinned Core RP curation ([ADR-013](decisions.md#adr-013--accepted-initial-m0-toolchain-and-explicit-engine-resource-curation)).

## Reproducibility

The editor Library cache key includes the editor revision, OS, package lock and import settings. The content cache restores only hash-verified outputs. Caches speed builds up, but nothing requires them.

**Caches in `.github/workflows/foundation.yml`:**
- The self-hosted Unity job checks out with `clean: false` and then runs `git clean -ffd`. This removes untracked source but keeps the ignored `UnityProject/Library`, `.local` and `Artifacts`, so imports and IL2CPP builds stay incremental between runs.
- It uploads only the current commit's runs (`Artifacts/*/<sha>`).
- The hosted fast job caches NuGet packages (keyed by `Tools/**/packages.lock.json`) and pip downloads (keyed by `Tools/requirements-fixtures.lock`).

Every executable records:
- the source commit and dirty flag;
- the toolchain hash;
- the generator set;
- the content manifest;
- the build options.

Unity executables are not byte-identical. Content semantics and hashes are reproducible.

Credentials never go in the repository. Packages are pinned. Workflow actions are pinned by revision.

## Failure gates

**Merge is blocked by:**
- compile, contract, schema or provenance failures;
- broken links;
- invalid generation;
- failing tests or a failing required smoke.

**Hardware.** Without the reference machine, hardware validation is Pending. It is never inferred from another GPU. A steady budget breach or a p95 regression above 10% needs investigation, even when the result is still under the absolute budget.

**Baselines.** A baseline change needs old and new metrics, workload equality and review. Tests never update goldens automatically. A flaky test is quarantined with an owner and a time limit. Core provenance, combat and codec tests are never disabled to ship.

## Release

Every release candidate goes through these steps:
1. `Package-Release.ps1 -RequireNativeNav` validates matching build and content identities, filters excluded files and emits the archive and inventory.
2. `Build-Installer.ps1` wraps that package in an installer.
3. Install the candidate into a clean directory, upgrade a previous installation in place, and launch offline with no editor or cache.
4. Load source-matched content and complete a match.

The release contains:
- the client;
- the generated content;
- licenses and provenance;
- the build manifest;
- default profiles;
- the controls reference.

Integrity hashes are verified at load. Rollback restores the binary and its matching content together. Signing, public upload and auto-update are out of scope.

Wrapper runs keep only the newest output per action (`Save-ShooterRun`), and small JSON reports are copied to `Artifacts/reports/`. Keep milestone and release manifests and reports indefinitely, together with their matching binaries and content.
