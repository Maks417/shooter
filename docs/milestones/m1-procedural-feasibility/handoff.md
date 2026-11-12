# M1 - Procedural feasibility - handoff and gate record

Status: **IN PROGRESS**, 2026-11-12. The owner requested M1 implementation with multiple agents. P01 deterministic foundations are implemented and tested; M1 is not closed. P02-P05, generated topology reproduction, cache compilation and visual/performance gates remain pending. M0's explicit owner waiver under ADR-017 is retained and does not waive M1 acceptance. See [acceptance](validation-acceptance.md) and [entry](index.md).

## Source, ownership and versions

| Field | Actual value |
|---|---|
| Integrator | Codex; serialized ownership of shared contracts, assembly metadata, tests, docs and coordination |
| Workers | `/root/random`, `/root/canonical`; disjoint pure-source/test paths, isolated worktrees, no worker Unity editor |
| Upstream | M0 closure `ba44c9f1f5cdd4fa8be044a04b4af528306d06fa`; assignment/harness `649975adc6635dc3f7561092f836904c242586aa` |
| Task commits | Random `5887869`; canonical `6b7f4eb`; identity/dependency harness `a116b4e`; integrated source `dad9785e273d02c6fd623f1abb334d4d59aca085` plus folder metadata committed with this handoff |
| Toolchain | Unity 6000.3.28f1 revision 8f082d2fe476, URP 17.3.0, .NET SDK 10.0.301; no upgrades |
| Toolchain SHA256 | `9ac31fe82117043e2bba3bfb843dd1ca32834b15425188e96d6f3369e2c627a8` |
| Contracts / fixtures / format | C0-v1 frozen and unchanged; specification-v1.1 unchanged; deterministic-format v1 implemented without semantic revisions |
| Seed | Fixture root `460072030`, plus uint64 boundary tests |
| Source constraints | Only code/numeric source and metadata; no art, cache payload or golden changes |

## Implemented P01 foundation

`UnityProject/Assets/Shooter/Generation/Core/Shooter.Generation.Core.asmdef` is BCL-only, with no UnityEngine references. `Tools/Generation/Shooter.Generation.Core.csproj` independently compiles the same production source as .NET Standard 2.1/C# 9.

- `Random/Pcg32.cs`: exact PCG-XSH-RR set-sequence, explicit wraparound, rejection-sampled bounded integers and single-draw unit floats.
- `Random/NamedRandomStream.cs`: immutable SHA256 stream identity, explicit little-endian seeds, independent feature-key generators and retained full stream digest.
- `Canonical/CanonicalJson.cs`: strict UTF-8/ASCII v1 subset, ordinal decoded keys, duplicate rejection, int32-only numbers, canonical escapes and bounded input/output (1 MiB), depth (64) and nodes (65536).
- `Identity/ContentIdentity.cs`: canonical uint64 seeds, source/content/gameplay keys, stable Unity GUIDs and payload SHA256. Dependencies sort by hash; duplicates reject. Gameplay key fixes profile to `gameplay`.
- `Dependencies/DependencyOrder.cs`: deterministic dependency-first preflight, rejecting missing/duplicate/cyclic edges and enforcing 4096 nodes / 256 edges per node.

Canonical encoding is not schema validation. `SourceHash` requires schema-valid input; no shipping compiler calls it yet. Recipe-specific schema/range validation, actual generator execution and geometry codecs belong to P02/P03. The dependency graph preflight does not execute or publish assets. Exact fixture identity and stream independence pass; same-input generated topology acceptance remains pending until P03 exists. No breaking contract/format changes occurred, so no migration or replacement goldens were introduced.

## Actual validation and evidence

Report paths below are repository-relative ignored evidence retained locally. Wrappers record source commit/dirty state, toolchain, commands and XML results. Integrated runs have `sourceDirty=true` because the two new folder metadata files were awaiting the handoff commit; this is not a clean-checkout or release-player claim.

| Check | Actual result / evidence |
|---|---|
| Pure generation | 69/69 pass, zero skipped; `Artifacts/m1-procedural-feasibility/dad9785e273d02c6fd623f1abb334d4d59aca085/20261112T143149477Z-787613bf/` (`Tools/Test-Generation.ps1 -Offline`) |
| Existing pure contracts/foundation | 62/62 pass; `Artifacts/m0-foundation/649975adc6635dc3f7561092f836904c242586aa/20261112T143037839Z-fabdb093/` (`Tools/Test-Contracts.ps1 -Offline`) |
| Locked-editor EditMode | 8/8 pass, including two generation tests; `Artifacts/m0-foundation/dad9785e273d02c6fd623f1abb334d4d59aca085/tests-editmode-20261112T143209493Z-23c9e11f/` (`Tools/Test-Unity.ps1 -Mode EditMode`; wrapper retains its M0 report-root name) |
| Source provenance | PASS, 158 entries / 79 unique GUIDs; `Artifacts/source-provenance/result.json` |
| Frozen C0 identity | PASS before pure and Unity wrappers; 24 source/metadata files and 18 fixture files unchanged |
| Specification fixtures | 35 assertions / 17 JSON files pass (`python docs/fixtures/verify_fixtures.py`); specification checks only |
| Review | Random worker performed a read-only review of integrated canonical/identity/dependency source, tests and wrapper; no actionable findings. Integrator reviewed both worker diffs and integrated tests. |
| Worker preservation | `Artifacts/m1-procedural-feasibility/dad9785e273d02c6fd623f1abb334d4d59aca085/worker-preservation/preservation.json`; both original TRX files copied and SHA256-verified, task commits verified as ancestors of main; both temporary worktrees/branches removed |

Offline restores use the existing pinned dependency cache; NuGet advisory lookup is PendingNetwork, not passed. Initial sandboxed .NET child processes exited before producing diagnostics; those failed attempts were retained, and the same required wrappers passed with normal build access. Documentation link/hash verification is recorded separately and is not game evidence. No new client/server player build, PlayMode rerun, topology output, cold/warm cache measurement, visual review or hardware benchmark is claimed by P01. Prior M0 player evidence remains historical. Shipping composition and CompileContent remain unchanged; CompileContent still reports unsupported pending P02 integration.

## Remaining step status and next assignment

- P01/T005: production seed/encoding/identity foundations implemented; exact vectors and failure tests pass. Full topology reproduction criterion remains pending P03.
- P02/T006: next content-owner task. Add recipe-specific validation, freeze typed binary geometry formats and tiny independent golden, bounded deterministic manifests, DAG execution, per-key locking, atomic immutable publication, corruption rejection and reference-counted lease loading. Test cold/warm, cancellation, concurrency, traversal/size attacks and same-key/different-payload failure before dependent adapters merge.
- P03/T007,T009: pending P02 API/format freeze; then primitive and rifle/pistol LODs, collision proxies and sockets with topology/count/silhouette evidence.
- P04/T008,T031: pending P02; can run in parallel with P03 after ownership split. Seven CPU material families, cheap URP path, basic sun and distinct B01-materials-v1 measurement.
- P05/T010,T028: pending primitive/socket inputs; early test-only rig stubs allowed, then 1000-pose sweep, visual/grip/slide and eight-rig timing gates. Stop after two failed visual iterations and record scope decision as specified.

## Downstream and sign-off

No compiler manifest/codec, socket registry or skeleton C1 freeze is claimed. M2/M3 remain unassigned. Keep compiler/schema ownership serialized before another feature wave; preserve C0-v1 and pinned editor/packages. All required full-M1 cache, visual and performance suites remain pending. No M1 hardware exception was requested or accepted, and no milestone completion or new performance claim is made. Completed P01 work is committed into main; rollback is the pre-M1 M0 source above, with historical M0 artifacts retained.
