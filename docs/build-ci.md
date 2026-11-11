# Build, CI and release strategy

Depends on [toolchain layout](repo-layout.md), [compiler](generation.md), [validation](validation.md). M0 client/server, test, smoke and B01 capture entry points are implemented; the offline content compiler and later workload/network lanes remain future tasks. Actual evidence and missing acceptance conditions are in the [M0 handoff](milestones/m0-foundation/handoff.md).

## Pipelines

| Lane | Trigger / environment | Actions and blocking checks |
|---|---|---|
| Fast | Every PR; ordinary build worker | Docs/link/schema checks, pure contract/generator tests, ownership/provenance diff audit |
| Unity edit | Every merge/feature PR; licensed pinned Windows editor | Import/compile, EditMode tests, shader checks, small fixture content compile |
| Player smoke | Merge to main; pinned Windows with build tools | Build Windows x64 client + headless server, PlayMode/standalone smoke, stripped-server dependency audit |
| Cold regeneration | Nightly/release; no local cache | Regenerate canonical recipes, validate hashes/provenance, full client and server build |
| Hardware performance | Nightly/release and perf-sensitive PR; named reference laptop | Release B01–B03, periodically B04, three warm runs and soak; compare fixed baseline |
| Network | Network PR/nightly; separate server/client processes | Packet simulator matrix, codec fuzz budget, late-join/resync, four-peer soak |

Unity batch invocation uses exact pinned Editor executable, `-batchmode`, project path, `-runTests` with EditMode/PlayMode and result/log paths. Builds use `ShooterBuild.BuildClient` and `ShooterBuild.BuildServer`. `ShooterBuild.CompileContent` currently exits with an explicit unsupported-M0 diagnostic; it cannot satisfy a content-compile gate. Parse explicit failure markers and exit codes; timeout/hung license/import is a failed job, never success from an existing output file. Do not use `-nographics` for any lane that needs GPU texture baking or shader/render validation; the future canonical CPU compiler must support genuinely headless generation.

## Caching and reproducibility

Editor Library cache key includes editor revision, OS, package lock and import settings. Content cache key follows generation spec; restore only hash-verified outputs. Caches speed builds but are never a prerequisite. Record source commit, clean/dirty flag, toolchain hash, compiler/generator set, content manifest and build options in every executable artifact. Determinism means reproducible content semantics/hashes in the specified envelope; Unity executables themselves may contain timestamps and are not promised byte-identical.

Build machine requires installed Unity license/entitlement per applicable terms, Windows SDK and IL2CPP toolchain; do not place credentials in repository. `.github/workflows/foundation.yml` pins action revisions, runs hosted Windows fast checks and offers a manually selected sequential Unity lane on a prelicensed self-hosted runner. Run `Tools/Configure-Unity.ps1` first to verify the editor and materialize the hash-pinned Core RP curation; see [ADR-013](decisions.md#adr-013--accepted-initial-m0-toolchain-and-explicit-engine-resource-curation). Hosted CI execution and later scheduled lanes remain unverified. Store license/service credentials in protected secrets, redact logs and restrict release publishing permissions. Package sources are pinned/allowlisted. No cloud service billing assumption is part of offline architecture.

## Failure gates

Block merge for compile/contract/schema/provenance failures, broken links, invalid generation, unit/adapter tests or required smoke. Hardware lane blocks milestone/release and perf-sensitive merges once runner is available. Without runner, label hardware validation pending, never infer success from another GPU. Initial performance failures establish honest baseline, not acceptance. Steady budget breach or >10% p95 frame regression from passing baseline requires investigation; staying under absolute budget does not automatically excuse a regression.

Baseline changes need old/new metrics, workload equality, reference-machine conditions and integrator review. Golden image/hash changes require intended generator/schema rationale. Tests cannot update goldens automatically on main. Quarantine flaky tests with issue/owner/time limit; core provenance/combat/codec tests cannot simply be disabled to ship.

## Artifact retention and release

PR artifacts 14 days, nightly 30 days, milestone/release manifests/reports indefinitely with matching binary/content retention. Retain previous passing release for rollback. Disk budgets and cleanup are CI-owned, never part of game code. Release candidate must install into a clean directory, launch offline without editor/cache, load source-matched content, complete objective, run headless without graphics content and reject incompatible network client.

Release archive contains client, required generated content, licenses/provenance, build manifest, default profiles and user-facing controls. Server archive contains gameplay definitions/collision/nav and no renderer/material caches. Integrity hashes verified at load; executable signing/update delivery are separate deployment decisions before public distribution. Rollback restores both binary and matching content/protocol, not only executable.


## Foundation wrappers and fixture checks

[Bootstrap runbook](bootstrap-runbook.md) assigns exact Tools wrapper deliverables and failure semantics. Run docs/fixtures/verify_fixtures.py with standard Python plus a pinned JSON Schema2020-12 validator during fast lane, then independent C# fixture reproduction. Missing editor/hardware is pending, never a synthetic report. Full GPU baking remains optional; canonical CPU path required.
