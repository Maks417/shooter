# M1 - Procedural feasibility - handoff and gate record

Status: **IN PROGRESS**, 2026-11-12. The owner requested M1 implementation with multiple agents. P01/P02 deterministic and compiler/cache/byte-lease foundations are implemented and tested; M1 is not closed. P03-P05, generated visual topology and full-slice/visual/performance gates remain pending. M0's explicit owner waiver under ADR-017 is retained and does not waive M1 acceptance. See [acceptance](validation-acceptance.md) and [entry](index.md).

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

## Implemented P01 foundation (historical wave)

`UnityProject/Assets/Shooter/Generation/Core/Shooter.Generation.Core.asmdef` is BCL-only, with no UnityEngine references. `Tools/Generation/Shooter.Generation.Core.csproj` independently compiles the same production source as .NET Standard 2.1/C# 9.

- `Random/Pcg32.cs`: exact PCG-XSH-RR set-sequence, explicit wraparound, rejection-sampled bounded integers and single-draw unit floats.
- `Random/NamedRandomStream.cs`: immutable SHA256 stream identity, explicit little-endian seeds, independent feature-key generators and retained full stream digest.
- `Canonical/CanonicalJson.cs`: strict UTF-8/ASCII v1 subset, ordinal decoded keys, duplicate rejection, int32-only numbers, canonical escapes and bounded input/output (1 MiB), depth (64) and nodes (65536).
- `Identity/ContentIdentity.cs`: canonical uint64 seeds, source/content/gameplay keys, stable Unity GUIDs and payload SHA256. Dependencies sort by hash; duplicates reject. Gameplay key fixes profile to `gameplay`.
- `Dependencies/DependencyOrder.cs`: deterministic dependency-first preflight, rejecting missing/duplicate/cyclic edges and enforcing 4096 nodes / 256 edges per node.

Canonical encoding is not schema validation. At P01 no compiler existed; P02 now supplies the schema adapter and CPU collision diagnostic described below. `SourceHash` still requires schema-valid input. Generated visual topology remains P03 work. The dependency graph preflight does not execute or publish assets. Exact fixture identity and stream independence pass; same-input generated topology acceptance remains pending until P03 exists. No breaking contract/format changes occurred, so no migration or replacement goldens were introduced.

## Historical P01 validation and evidence

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

Offline restores use the existing pinned dependency cache; NuGet advisory lookup is PendingNetwork, not passed. Initial sandboxed .NET child processes exited before producing diagnostics; those failed attempts were retained, and the same required wrappers passed with normal build access. Documentation link/hash verification is recorded separately and is not game evidence. No new client/server player build, PlayMode rerun, topology output, cold/warm cache measurement, visual review or hardware benchmark is claimed by P01. Prior M0 player evidence remains historical. At that point shipping composition and CompileContent were unchanged; the P02 continuation below supersedes the unsupported compiler limitation for its explicitly named diagnostic only.

## Remaining step status and next assignment

- P01/T005: production seed/encoding/identity foundations implemented; exact vectors and failure tests pass. Full topology reproduction criterion remains pending P03.
- P02/T006: compiler/loader skeleton implemented. Numeric diagnostic cold/warm/force/dry-run, DAG/cache/error/concurrency/cancellation and lease tests pass. Production codec/manifest/recipe v1 freeze is recorded under ADR-018. Full-slice content and resource budgets remain pending.
- P03/T007,T009: next geometry task; P02 API/format is available. Implement primitive and rifle/pistol LODs, independent collision proxies and sockets with topology/count/silhouette evidence; no weapon generator or socket registry exists yet.
- P04/T008,T031: P02 API is available; can run in parallel with P03 after explicit ownership assignment. Seven CPU material families, cheap URP path, basic sun and distinct B01-materials-v1 measurement.
- P05/T010,T028: pending primitive/socket inputs; early test-only rig stubs allowed, then 1000-pose sweep, visual/grip/slide and eight-rig timing gates. Stop after two failed visual iterations and record scope decision as specified.

## Downstream and sign-off

Compiler manifest/codec v1 is now frozen under ADR-018; socket registry and skeleton C1 remain pending. M2/M3 remain unassigned. Keep schema/registry ownership serialized before another feature wave; preserve C0-v1 and pinned editor/packages. Full-M1 slice content, visual and performance suites remain pending. No M1 hardware exception was requested or accepted. Completed P01/P02 work is integrated into main; prior P01 rollback source is ce7abba, with historical M0 artifacts retained.

## P02 implementation continuation (2026-11-12)

Upstream P01 source was `ce7abba`. Ownership/format assignment `1627230`, codec worker `0b72f91`, recipe worker `50ea9bc`, runtime draft `5eb52a0`, integrated compiler/lease guards `b2450a4ca512ba133325747adebdbbdf99d46365`, final tested format/source freeze `912be99bdc47dd2112e0379cf641a6e1f5220e28`. All task branches are main ancestors. The three isolated worktrees and branches are retired after preserving six worker TRX reports and verifying SHA256 copies in `Artifacts/m1-p02/worker-preservation/preservation.json`. No worker editor was opened. The integrator completed the runtime draft after its worker usage interruption; no incomplete worker task remains outside main.

Implemented source:

- `Generation/Core/Canonical/JsonValue.cs` and `Recipes/BoxRecipe.cs`: immutable bounded JSON tree and strict exact-field recipe validation, original fixture/source identity preserved; uint64 seed overflow, dimensional ranges and sorted unique dependencies reject. `GeneratorFingerprint.cs` hashes explicit ID/version bytes.
- `Generation/Core/Codecs/{ArtifactCodec,CodecPrimitives,GeometryCodec}.cs`: SGAF v1 plus mesh, material, skeleton, collision and structural navigation codecs. `Tools/Generation.Tests/CodecFixtures/generate_vectors.py` independently reproduces the tiny diagnostic vector; no imported or manually modeled game geometry is introduced.
- `Generation/Core/Manifests/{ContentManifest,ArtifactBounds}.cs`: bounded deterministic production manifests, sorted artifact/registry IDs, conservative numeric bounds, full file hashes/counts, gameplay/visual key separation and provenance. New [schemas and exact layout](../../content-format.md) stay outside frozen C0 fixtures; root `content-format-lock.json` pins 32 initial source/schema/vector files and the toolchain.
- `Generation/Editor/Compilation/ContentCompiler.cs`: bounded DAG preflight/execution, cached external-dependency verification, per-key cross-process OS locks, durable staging files and size/hash rechecks, atomic immutable directory publication and successful-set index replacement. Warm loads verify cache; force regenerates and compares bytes without overwrite. Corruption fails and is retained. Cancellation/failed capacity cleans only this compiler's staging directory; completed earlier dependency outputs may remain, while the previous index remains valid.
- `Content/Runtime/{CacheFiles,FileContentStore}.cs`: bounded ancestor/reparse/path-safe reads, complete hash/envelope/typed/count/bounds validation before leasing; coalesced per-content IO, consumer-only cancellation, reservations/residency caps, independent reference-counted leases and eviction after the last release. Defaults: 256 MiB verified resident bytes, 64 pending loads, 256 waiters and bounded active leases. Kind-only ambiguity fails explicitly; concrete `AcquireVariantAsync` selects a named artifact. Lease bytes include the complete verified envelope. No Unity resources are created or destroyed by this adapter.
- `Recipes/CpuBoxCollisionGenerator.cs`, `Generation/Recipes/box-collision.json`, `Bootstrap/Editor/ShooterContentBuild.cs` and `Tools/Compile-Content.ps1`: real CPU numeric box-collision compilation and verified lease smoke. Only `box-collision-v1` is composed, with fixed fixture seed and windows64/gameplay identity. Low/Balanced/High are request probes, not different collision content. Full slice-v1/arbitrary sets remain unsupported. Foundation runtime scene remains the original code-generated harness.

Assembly boundaries are explicit: `Shooter.Content.Runtime` depends on pure Generation.Core format helpers and Contracts; `Shooter.AssetCompiler.Editor` is Editor-only and never referenced by runtime. `Tools/ContentRuntime` and `Tools/ContentCompiler` independently compile the same production source with C# 9/.NET Standard 2.1. The new pure test project references are locked without new package versions. [ADR-018](../../decisions.md#adr-018--accepted-initial-p02-compiler-format-and-byte-cache-boundary) records the additive dependency/format decision; no C0 or toolchain migration occurred.

### Actual P02 evidence

All paths below are repository-relative retained ignored files; Unity wrappers retain their historical m0-foundation directory name. Editor compilation and player build/smoke are distinct checks. Build manifests can record sourceDirty during temporary engine build settings; clean source checks and source hashes are separate, not inferred from that field.

| Check | Actual result / evidence |
|---|---|
| Final pure generation/content | 150/150 pass, zero skipped; `Artifacts/m1-procedural-feasibility/912be99bdc47dd2112e0379cf641a6e1f5220e28/20261112T180806840Z-dc9eaa8d/` via `Tools/Test-Generation.ps1 -Offline` |
| Existing pure contracts | 62/62 pass; `Artifacts/m0-foundation/912be99bdc47dd2112e0379cf641a6e1f5220e28/20261112T180817326Z-bc07927a/` |
| Initial P02 EditMode | 9/9 pass, including actual CPU source cold/forced compile and verified byte lease; `Artifacts/m0-foundation/b2450a4ca512ba133325747adebdbbdf99d46365/tests-editmode-20261112T180121127Z-ffbb73ae/` |
| Actual cold compile | PASS, 174.3369 ms measured Editor diagnostic compile/verification; `Artifacts/m0-foundation/b2450a4ca512ba133325747adebdbbdf99d46365/compile-content-20261112T180244452Z-7e7d1f06/` |
| Warm High request | PASS, reused gameplay content, 118.103 ms; `compile-content-20261112T180259063Z-08345d01/` under the same b2450a4 evidence root |
| Forced reproduction | PASS, exact existing bytes retained, 159.9915 ms; `compile-content-20261112T180313198Z-97855750/` under the same root |
| Dry run | PASS, no cache output; `compile-content-20261112T180327156Z-2334afb4/` under the same root |
| Full production schema validator | 7/7 actual/negative structural cases pass with pinned jsonschema 4.26.0; `Artifacts/m1-p02/content-schemas.json` |
| Initial provenance regeneration | Existing `ShooterBuild.WriteSettingsProvenance` emitted the changed source proof; `Artifacts/m0-foundation/b2450a4ca512ba133325747adebdbbdf99d46365/refresh-settings-provenance-20261112T180204437Z-ad4d9bbd/`; audit passes 208 entries/104 GUIDs |
| Server build and smoke | PASS; `Artifacts/m0-foundation/912be99bdc47dd2112e0379cf641a6e1f5220e28/build-server-20261112T180725614Z-33b2f39a/` and `smoke-server-20261112T180835411Z-5bc1de13/`; Mono Dedicated Server, no graphics payload |
| Client build and smoke | PASS; `Artifacts/m0-foundation/912be99bdc47dd2112e0379cf641a6e1f5220e28/build-client-20261112T180847318Z-d68ffd20/` and `smoke-client-20261112T181257125Z-de264c31/`; IL2CPP Release/D3D11 foundation harness, no newly installed gameplay content |
| Final Unity tests | 9/9 EditMode and 5/5 PlayMode pass; `tests-editmode-20261112T181309384Z-2e8568fe/` and `tests-playmode-20261112T181347943Z-49c162a3/` under the same 912be99 root |
| Final source provenance | PASS, 208 entries / 104 GUIDs; `Artifacts/source-provenance/result.json` after both player builds and test modes |
| Final evidence summary | `Artifacts/m1-p02/validation-summary.json` links and hashes every required current report; it distinguishes the diagnostic P02 pass from still-open M1 gates |

The three compiling probes share content ID `ee9304f2342e1607ce20afdef632a7d9d7ffaa0725d853cb840b90b427382f1b` and manifest SHA256 `5c41e6cd6ca54a8ad1d4fd53a1944e6f8422dec8f5ec91539fdcae1ec87d2ab3`. Actual immutable cache is retained under `Artifacts/m1-p02/compiler-smoke-cache/ee/<content-id>/`; generated artifact is a 170-byte SGAF collision file. These timings are for one tiny Editor/tools diagnostic, not the full-slice cold/warm target or release rendering. Source/toolchain/input/code hashes are in each compile proof. Offline advisory lookup remains PendingNetwork.

### Review, failures and remaining limits

Integrator reviewed both upstream worker diffs, manifest/publisher/runtime seams and final tests. Recipe worker's partial independent review identified an unbounded staged reread, allocation-count checks after typed materialization, and integer bounds inversion after float conversion; these were fixed before final validation. Initial integrated tests exposed a cache error-type boundary and a forced-reproduction artifact object-membership mismatch; both were corrected, with the failed `Artifacts/m1-p02/integration/p02-tests.trx` preserved alongside corrected/final runs. The initial analyzer failure and codec test-mutation mistake are also retained; no golden payload was changed to make tests pass. Final wrappers all require actual fresh results. Documentation hash checks are not runtime evidence.

The format and diagnostic P02 skeleton are ready for bounded P03/P04 ownership. Full slice recipe registration/content compilation, primitive/weapon LODs, sockets, seven material families, soldier C1 and 1000 poses, graphics adapters/install lifecycle, rig timing and native 720p hardware acceptance remain pending. Navigation blob decoding is structural; real adapter validation waits for M3. Cache capacity refuses new publication at the default 8 GiB instead of automatically evicting; LRU/purge and crash-orphan staging reclamation remain unimplemented. Peak transient compiler/decode memory and full-slice compile allocations/times remain unmeasured. Cooperative local filesystem checks are not adversarial race-proof isolation; future shipping Bootstrap must validate trusted build/content manifests before use. No M1 completion, realism, new rendered-asset or hardware-performance pass is claimed.

Final specification/docs checks: unchanged C0 fixtures pass 35 assertions over 17 JSON files, the pinned full C0 schema suite passes 6/6 cases, both source/format locks pass, and documentation links/anchors/hashes pass. These are separate from runtime evidence. All completed work is in main; only the main worktree remains.
