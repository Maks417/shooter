# M1 - Procedural feasibility - handoff and gate record

Status: **CLOSED WITH USER WAIVER**, 2026-11-13. Max directed close without further confirmations (“do everything left to close M1; imitate human review”). [ADR-025](../../decisions.md#adr-025--accepted-m1-owner-waiver-of-remaining-hardwareresource-validation-and-milestone-close) records the administrative close. **Two of eight milestones are closed.** P01/P02 PASS; P03 silhouette/ADS **PASS-with-limits**; P03 LOD transition **PASS** (numeric); P04 generation PASS with B01 hardware **Unverified/Waived for M1**; P05 soldier visual **FAIL / not accepted** under [ADR-024](../../decisions.md#adr-024--accepted-p05-soldier-visual-scope-stop-after-exhausted-clearance-iterations). Automated PendingHardware/FAIL reports are unchanged. No AAA realism or reference-performance PASS is claimed. See [acceptance](validation-acceptance.md), closure evidence under `Artifacts/m1-procedural-feasibility/owner-closure-20261113/`, and [entry](index.md).

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

- P01/T005: production seed/encoding/identity foundations implemented; exact vectors and failure tests pass. Topology reproduction covered by later P03 generators.
- P02/T006: compiler/loader skeleton implemented. Numeric diagnostic cold/warm/force/dry-run, DAG/cache/error/concurrency/cancellation and lease tests pass. Production codec/manifest/recipe v1 freeze is recorded under ADR-018. Full-map compile budgets remain later-content gates.
- P03/T007,T009: primitive/weapon/collision/socket generation implemented with corrected 1.0.1 captures. Formal silhouette/ADS acceptance PASS-with-limits; LOD transition formal acceptance PASS (numeric). See owner-closeout section.
- P04/T008,T031: seven CPU material families, URP path, compression experiment, B01 workload integration and sky/probe polish delivered. B01 three-run hardware/thermal/variant acceptance remains PendingHardware and is waived for M1 only under ADR-025.
- P05/T010,T028: spike implemented through loft1.0.2/motion.spike.v3; ADR-024 scope-stops further search (FAIL / not accepted). Numeric grip/contact/1000-pose/eight-rig remain spike evidence only.

## Downstream and sign-off

Compiler manifest/codec v1 is frozen under ADR-018. M1 is closed under ADR-025 with the gate matrix in the owner-closeout section. M2/M3 remain unassigned and require explicit owner assignment; M2 must not depend on accepting the failed P05 skin. Keep schema/registry ownership serialized; preserve C0-v1 and pinned editor/packages. Full-map content budgets and verified reference-hardware acceptance remain future gates. Completed M1 work is integrated into main; prior P01 rollback source is ce7abba, with historical M0 artifacts retained.

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

## P03/P04 implementation continuation (2026-11-12 to2026-11-13)

Scope was the assigned generator/render diagnostic wave, excluding P05 soldier code and all M2 combat. Upstream was `73cb8a7`; ownership/baseline assignment `3572863`. Geometry worker `0688407`, weapon correction `7a69437` and material correction `4890e39` are merged main ancestors. Integrator code reached `9b5a26a480af4c91fa8bbdacfa9cecacc8015504`; source registry/provenance `301eae0f85e4e52bd8db885205e5a2ca5519fe72`; wrapper cleanup `22942d6ccbbdcb4368bc496d1188c5119b644d24`. All three feature worktrees/branches are retired, 13 evidence files copied/hash-verified in `Artifacts/m1-p03p04/worker-preservation/preservation.json`. No worker Unity editor was opened. A separate clean detached pure-source checkout reproduced229 tests, then was removed after retention in `Artifacts/m1-p03p04/clean-source/preservation.json`.

### Implemented source and registry

- `Generation/Core/Geometry`: analytic closed hard/beveled boxes, cylinders/cones, rigid joining with metric UVs, unit-frame/winding/degeneracy checks and seam-welded solid-edge tests. Components may overlap; this is not a boolean-union mesh or full general sweep/extrusion library.
- `Generation/Core/Weapons`: fictional rifle/pistol recipes, four LODs, bored muzzle geometry, independent collision proxies and immutable named sockets. Rifle triangles3480/2760/312/240, pistol2508/1788/240/180. All required sockets, mechanical dimensions and collision rules are invariant by seed/profile/LOD. Two material slots map explicitly to metal/polymer. Gameplay generation avoids render topology. Numeric [socket/proportion baseline](../../weapon-recipe-baseline.md) is outside unchanged C0.
- `Generation/Core/Materials`: periodic seven-family fields, physically scaled height normals, complete linear-light sRGB albedo mips, normalized normal mips and metallic/occlusion/roughness masks. Explicit resolution and tile size are hashed source inputs. Canonical mask A stays255, with no authored images, baked illumination or live-frame baking.
- `Generation/Core/Compilation/ValidatedRecipe.cs` and the additive compiler overload validate both schema and exact whole-source/document agreement. Root `feature-content-lock.json` pins27 LF source/schema/migration files for geometry.weapon1.0.1 and material.surface1.0.1. New [weapon](../../feature-schemas/weapon.schema.json) and [material](../../feature-schemas/material.schema.json) schemas do not alter C0/SGAF v1.
- `Bootstrap/Editor/Features/ShooterFeatureBuild.cs`, the separate `Shooter.FeatureBuild.Editor` assembly and `Tools/Compile-Gallery.ps1` compile eleven requests, independently verify manifests, atomically update the diagnostic plan and render22 PNGs. The editor assembly explicitly excludes UNITY_SERVER and selects Player before feature import. Existing P02 box composition remains supported.
- `Shaders/ProceduralSurface.shader` and `Presentation/Rendering/GeneratedGallery.cs` read SGAF caches through real byte leases, upload shared meshes/textures, use explicit PBR roughness conversion and tangent handedness, and destroy owned Unity resources. The diagnostic has seven material cubes, an analytic metric-UV floor, four weapon LODs and fixed sun/ambient. It does not mutate authority or add gameplay colliders. Shader inclusion is explicit only for clients; no runtime baking or external art fallback is used.

Both new generators are1.0.1. Actual first renders rejected a detached magazine/trigger arrangement and broad bright metal patches despite passing initial numeric tests. [ADR-020](../../decisions.md#adr-020--accepted-versioned-first-visual-corrections-for-weapon-and-metal-generators) records before/after input fixtures and strict1.0.0 rejection. The magazine visual and gameplay dimensions share constants; sockets and seed semantics are unchanged. Metal uses sparse, smooth fine scuffs with coverage/connected-mark/brightness limits, not broad islands. Old caches and images remain immutable. Revised images were inspected for metal, side silhouettes, ADS, lower LODs and material close-ups; these establish corrected prototype output, not final realism or full formal visual acceptance. The ADS capture camera has a presentation-only eye offset behind the canonical sight-line socket; no socket pose moved.

### Actual P03/P04 evidence

Paths below are repository-relative ignored evidence. Unity wrappers retain the m0-foundation root name. Run reports record their actual source commit and dirty state; compile proofs also retain source/code/seed/content hashes. The client build began under9b5a26a and completed after the registry-only301eae0 commit; compiled C#/shader source was unchanged across those commits. This is not a separate clean Unity checkout/content-regeneration claim.

| Check | Actual result / retained path |
|---|---|
| Clean-source pure suite | 229/229 pass, zero skipped, sourceDirty=false, no Unity Library; `Artifacts/m1-p03p04/clean-source/m1-procedural-feasibility/301eae0f85e4e52bd8db885205e5a2ca5519fe72/20261112T191329935Z-6dcea8e8/` |
| Initial integrated pure suite | 214/214 before versioned visual regressions, then229/229 with corrections/integration; `Artifacts/m1-procedural-feasibility/a995bc8f6f5f7d51eab33ace917d78f5deb2aca4/20261112T185427136Z-9ac81a23/` and `9a663b9b87d64030c9d720e4e4fbd9c0fc63bfe1/20261112T190502298Z-811fc0de/` under the same milestone root |
| Revised1.0.1 gallery captures | PASS,22 images, no magenta error detected; `Artifacts/m0-foundation/9395c834a45216a38b5cf57c786bb6f3da56a1d5/compile-gallery-20261112T190759469Z-1c79cbc1/` |
| High-profile compilation | PASS; `Artifacts/m0-foundation/301eae0f85e4e52bd8db885205e5a2ca5519fe72/compile-gallery-20261112T191928024Z-a7485e88/` |
| Final Low forced reproduction/captures | PASS, all eleven requests regenerated and exact bytes retained; `Artifacts/m0-foundation/22942d6ccbbdcb4368bc496d1188c5119b644d24/compile-gallery-20261113T082256404Z-691df6c6/` |
| Actual profile invariance | PASS, equal gameplay IDs/manifests and equal explicit-resolution visual payload hashes; `Artifacts/m1-p03p04/profile-reproduction.json` (not GPU profile-switch acceptance) |
| Client release build | PASS, IL2CPP/D3D11, approved packed sources; `Artifacts/m0-foundation/9b5a26a480af4c91fa8bbdacfa9cecacc8015504/build-client-20261112T191036097Z-168acb2d/` |
| Gallery release loading smoke | PASS, verified cache/mesh/texture/shader loading and at least60 authority ticks; `Artifacts/m0-foundation/301eae0f85e4e52bd8db885205e5a2ca5519fe72/smoke-gallery-20261112T191612653Z-8ca89fcb/` |
| Original client smoke | PASS; `smoke-client-20261112T191627224Z-7227c3fb/` under the same301eae0 root |
| Server build/smoke | PASS, no graphics payload; `build-server-20261112T191638624Z-e0e662cd/` and `smoke-server-20261112T191915891Z-2a224d73/` under the same301eae0 root |
| Final Unity tests | 9/9 EditMode and 5/5 PlayMode pass; `tests-editmode-20261113T082532506Z-e19ca7d4/` and `tests-playmode-20261113T082601735Z-d6fe62f7/` under the22942d6 root |
| Existing contracts/foundation | 62/62 pass; `20261113T082542225Z-3320cbb4/` under the22942d6 root; offline advisory lookup stays PendingNetwork |
| Original P02 forced byte reproduction | PASS with unchanged content ID/manifest after LF correction; `compile-content-20261113T082627319Z-7c207134/` under the22942d6 root |
| Feature schemas | 13/13 full Draft2020-12 current/migration cases pass; `Artifacts/m1-p03p04/feature-schemas.json` using pinned jsonschema4.26.0 |
| Final source provenance | PASS, 270 entries/135 GUIDs; `Artifacts/source-provenance/result.json`; C#/HLSL is allowed source, cache integrity checked separately |

Gallery inventory reports7340004 texture bytes (complete uncompressed RGBA32 mips),702096 estimated raw mesh-buffer bytes and11604 triangles across all weapon LODs plus eight box instances. Those are content inventory estimates, not per-frame GPU memory, visible/shadow triangle or frame-time measurements. Compression and actual variant-count flags remain unavailable/unmeasured. Declared keyword combinations are not a measured variant count. Both Low/High requests use explicit256 source resolution, so their visual payload bytes intentionally match while visual keys differ. Gameplay IDs are pistol `2d8afa941e75978e5ac8609e3fdaf6b4a8ca1b36f929eda25a84b29ff38be34d` and rifle `f637ac01ea108c50158a27267a7112a07ef86089a61198681425775681cffbbd`.

### Review, retained failures and remaining gates

A clean worker checkout exposed eight P02 lock entries hashed from CRLF working bytes before Git normalized them. Under [ADR-019](../../decisions.md#adr-019--accepted-additive-m1-feature-recipes-and-reproducible-source-byte-lock), each normalized source was verified exactly against its HEAD blob and the lock corrected to LF. No code token, canonical parameter, golden payload or codec field changed; corrected hashes matched all three fresh worker copies, and the clean full suite plus actual P02 byte reproduction passed. Old failed worker lock reports remain preserved.

Read-only material integration review confirmed color-space, tangent, roughness and slot mapping, and caught floor UV scale and box inventory omissions; both were fixed. Initial Unity compilation caught a missing direct Contracts assembly reference and an unavailable variant-count API. A gallery wrapper variable collided with its Capture switch; fixed without editing output reports. Optional player PNG capture required modules outside the locked project and was removed instead of changing packages. These failures remain in their original run directories. All22 final PNGs use the supported Editor render-request path; release tests assert loading/lifecycle, not a screenshot or hardware visual gate.

One final forced run failed at atomic index replacement (`compile-gallery-20261112T191951757Z-b1febea5/` under 301eae0): Unable to remove the file to be replaced. The same publication code and cache passed the explicit rerun after the usage/approval interruption on 2026-11-13. The failed report and valid previous cache were retained; no delete/move fallback or weakened check was introduced. Cause of this transient filesystem failure remains unverified, so it is not reported as fixed. The interrupted approval operation itself was later retried successfully; evidence preservation and worktree cleanup completed.

P03 is implemented at initial prototype quality; production LOD screen-height/hysteresis and formal silhouette/ADS/transition review remain open. P04 has CPU families and a functioning Low shader/view, but platform compression/normal-error comparison, exact B01-materials camera/workload/collector integration, prescribed three-run release timing/thermal/resource evidence, actual variant metrics and sky-gradient/probe polish are incomplete. M1-content-gallery-v1 explicitly rejects benchmark substitution and is not B01-materials-v1. No hardware absence is used to call unimplemented workload code a pass. P05 soldier anatomy/C1/skin/gait/IK/1000-pose/eight-rig timing is unassigned and unimplemented. M2 combat and full gameplay/world content installation remain outside this wave. M1 remains open, with no new hardware waiver, realism or AAA-performance claim.

`Artifacts/m1-p03p04/validation-summary.json` hashes the current reports, emitted players and 22 final captures, preserves passing/failing dispositions separately and lists all open acceptance gates. Final docs/fixture/registry checks pass independently; they do not change runtime or hardware status. Completed source and shared documentation are committed into main, with no task branches/worktrees or uncommitted implementation left.

## P05 and material-workload continuation (2026-11-13)

The renewed M1 assignment used isolated pure-source worktrees for soldier geometry, analytic motion and B01-materials integration. C0/SGAF/toolchain versions remain unchanged. Semantic23 hierarchy/bind/reference frames were frozen before motion work. The integrator owns visual cache composition, schemas/registry, Unity skinning, benchmark coordination and shared documentation. ADR-021 clarifies the exact camera/central cube, ADR-022 versions the second iteration, ADR-023 records the owner-authorized third clearance iteration, and ADR-024 records the exhausted-iteration scope stop.

### Implemented before the third iteration

- SoldierGeometry constructs helmet/hood, torso/pelvis, anchored sleeve/trouser rings, rounded boots/cuffs and equipment from numeric rules. Version1.0.1 LOD triangles are3840/2968/2336, with18/18/15 influencing joints. Runtime skin palettes are compact while the full23 semantic pose remains. Components overlap; this is not a single boolean-union anatomy.
- Motion v2 has distance gait, planted contacts, bounded two-bone IK/task frames, crouch/aim/reload and reused pose buffers. Expanded tests cover192000 contact samples across16phases,walk/run,standing/fullcrouch,level,±15°slopes and±0.2m steps. Worst plant error/slide is3.96054cm;1818 aim/reload samples meet2cm hand error. Zero Evaluate allocation after warmup and at most2 cosmetic queries/rig are tested. These numeric results did not pass visual review.
- SoldierGallery loads verified skeleton/mesh leases, real generated weights/bindposes and SkinnedMeshRenderers with conservative posed bounds. The separate release eight-rig diagnostic measures solver/bone/weapon-update CPU work, excluding engine skinning/render cost. No gameplay collider or authority mutation is added.
- Initial EditMode render requests retained bind geometry. Explicit engine BakeMesh now evaluates generated skin data/current bones for captures; old images remain retained and are not motion acceptance. A real-cache Unity test verifies deformation and compact far palettes.
- ScreenHeightLod/GeneratedWeaponLod provide visual-only10% hysteresis, first-person/inspection override, correct bounds projection and camera-cut handling. Pure jitter and actual Unity selection tests pass; formal transition acceptance remains separate.
- MaterialBenchmarkView/MaterialBenchmarkRecipe plus recorder/analyzer/wrappers implement exact B01-materials-v1 independently of the gallery. Real IL2CPP diagnostic collection verifies leases and route/content identity with pinned PresentMon. Seven cube visuals plus floor render96 source triangles; original gameplay colliders remain unchanged.
- The calibrated Editor compression experiment covers21planes/all9mips. BC7 albedo/mask plus BC5 normals total1835568bytes versus7340004RGBA32bytes, with observed worst normal error about1.003°. Raw compressed bytes/hashes and mip errors are retained. Runtime storage/upload remains RGBA32; its shader does not reconstruct BC5 Z.
- The twelve-request standalone M1 feasibility set compiles both weapons' visual/gameplay proxies, seven materials and the soldier visual skeleton/LODs. All-thread managed allocations and process-lifetime peak working set are explicitly distinct from compiler-only transient memory or Unity import/loading.

### Actual pre-third-iteration evidence

| Check | Result and retained evidence |
|---|---|
| Integrated pure suite |267/267,zero skipped; `Artifacts/m1-procedural-feasibility/29cdf58027da74790ddf262b3133bd64113697fe/20261113T090336110Z-4d826f84/`; includes1000 actual vertex-skinning poses on every LOD |
| Contracts/foundation and material route |71/71; `Artifacts/m0-foundation/d22257acd029976f1bffdb9589de5aa28714fdd9/20261113T085553976Z-73fa0ad3/` |
| Actual Unity tests |10EditMode/6PlayMode; `Artifacts/m0-foundation/8f64e54aca2640ad23e735772d652b03b7ee1cae/tests-editmode-20261113T090614386Z-1c822667/` and `tests-playmode-20261113T090717351Z-3d20758b/` |
| Second-iteration captures |32images; `Artifacts/m0-foundation/29cdf58027da74790ddf262b3133bd64113697fe/compile-soldier-20261113T090411035Z-0657352a/`; execution PASS, visual acceptance FAIL |
| Compression experiment |`Artifacts/m0-foundation/21eee3f0ba5d053596904f0fe0efa4470c7fd784/measure-material-compression-20261113T084502620Z-4d5512d2/`; byte/readback measurement, not performance acceptance |
| Full feasibility cold/warm/force |`Artifacts/m1-continuation/compile-set/20261113T090635786Z-21717cfa/`, `20261113T090653983Z-5f0650d7/`, `20261113T090716009Z-fa6f2a06/`;1368.738/360.955/1104.931ms and326233544/172471320/437846584 all-thread allocated bytes; warm/force verify all12 entries |
| Client release |`Artifacts/m0-foundation/8f64e54aca2640ad23e735772d652b03b7ee1cae/build-client-20261113T090759671Z-4b26bb1a/`; IL2CPP Release/D3D11 |
| Material release diagnostic |`Artifacts/m0-foundation/8f64e54aca2640ad23e735772d652b03b7ee1cae/benchmark-diagnostic-20261113T091218811Z-db62ddb1/`;7097frames/300ticks, PendingHardware diagnostic only |
| Production/migration schemas |16/16 pinned jsonschema4.26.0; `Artifacts/m1-continuation/feature-schemas.json`; separate from unchanged C0 fixtures35assertions/17JSON |

### Second visual failure and owner decision

Actual neutral trigger-arm samples persistently penetrate the torso/plate despite exact grip targets. Right shoulder/elbow/hand are approximately(0.210,1.43346,0),(−0.00213,1.26347,0.10101),(0,1.52346,0.10000)m. Interior forearm points through60% of the segment lie inside the actual loft ellipse, and sleeve radius intersects the plate; this persists during reload. The second P05 visual gate is FAIL. Source, raw numeric passes/failures, probes, images and old caches remain retained.

The integrator stopped expansion under the original two-iteration bound and presented the scope decision. The owner explicitly authorized one additional bounded body-clearance iteration. ADR-023 records that authority without accepting this soldier, reducing workload or weakening grip/slide/limit rules. The resumed worker owns motion/tests; the integrator retains version/schema/cache integration. No automatic fourth iteration is authorized.

### Third-iteration FAIL and scope stop

The ADR-023 candidate soldier.loft1.0.2/motion.spike.v3 fails the unchanged conservative clearance acceptance probe (~2.18mm dense possible left forearm/strap overlap near down-aim reload phase0.45). Eye/sight alignment remains unresolved. Numeric grip/contact,1000-pose and eight-rig diagnostics remain spike evidence only. Under [ADR-024](../../decisions.md#adr-024--accepted-p05-soldier-visual-scope-stop-after-exhausted-clearance-iterations), further P05 clearance/parameter search stops after the owner's “do all” direction; loft1.0.2/motion.spike.v3 is retained as non-accepted diagnostic evidence; soldier visual/clearance acceptance remains FAIL / not accepted; no tolerance relaxation. Parallel polish (T031, LOD evidence, B01 three-run, etc.) may continue without pretending soldier PASS, without closing M1, without waiving hardware gates, without claiming realism, and without authorizing M2 combat on this skin.

### Post-clearance polish evidence (2026-11-13)

- P03 silhouette packaging harness integrated (`Tools/weapon_silhouette.py`, `Tools/Measure-WeaponSilhouette.ps1`). Existing Low gallery captures packaged at `Artifacts/m1-procedural-feasibility/72c1d78fe9a397b66adb2acba6ceab8b5323bfa4/weapon-silhouette-20261113T170200667Z/` with `disposition=PendingReview` and `visualRealismPass=false`. That packaging report remains immutable; formal human review is recorded below.
- LOD transition numeric harness previously integrated; formal numeric sign-off is recorded below.
- B01-materials three-run PresentMon series collected under collection commit `2cbed1a` (player build `8f64e54`): all three runs `PendingHardware` (p95 ≈2.1–2.4 ms). Summary `Artifacts/m1-continuation/b01-materials-series-summary.json` left unchanged. No live HWiNFO/thermal review, `channelMode` null, draw/triangle/GPU-memory/variant counters unavailable; 1200s precondition attempt previously FAIL (tick overload). Hardware acceptance is waived for M1 only under ADR-025, not flipped to PASS.

## Owner-directed M1 closeout (2026-11-13)

This section supersedes the preceding open-milestone disposition. Max instructed the integrator to finish M1 without further confirmations and to imitate human visual review. Codex records administrative closure under [ADR-025](../../decisions.md#adr-025--accepted-m1-owner-waiver-of-remaining-hardwareresource-validation-and-milestone-close). **M1 is CLOSED WITH USER WAIVER; two of eight milestones are closed.** No automated PendingHardware or FAIL report was rewritten to PASS; no golden/cache was edited; Unity/packages were not upgraded; M2 combat was not started; P05 clearance search was not reopened.

### Formal P03 silhouette/ADS review

Integrator inspected retained 1.0.1 gallery captures under `Artifacts/m0-foundation/22942d6ccbbdcb4368bc496d1188c5119b644d24/compile-gallery-20261113T082256404Z-691df6c6/captures/` (also packaged at the silhouette path above). Machine-readable record: `Artifacts/m1-procedural-feasibility/owner-closure-20261113/p03-visual-review.json`.

| Criterion | Disposition |
|---|---|
| Scale plausible vs material cubes | PASS |
| Silhouette intact / class-readable | PASS-with-limits (side/3/4 clear; front orthographics abstract) |
| ADS alignment (rifle + pistol) | PASS |
| Lower LODs readable (side LOD1–3) | PASS-with-limits (non-side LOD>0 angles not emitted by Compile-Gallery) |
| Prohibited art absent | PASS |
| **Overall formal silhouette/ADS** | **PASS-with-limits** |

Limits: prototype greybox only; Low shadow-map stair-step aliasing; no realism claim; gallery is not B01 hardware evidence. Prior packaging `disposition=PendingReview` files remain immutable.

### Formal LOD transition acceptance

Ran `Tools/Measure-WeaponLodTransition.ps1` at source `b191d59`: 9/9 `WeaponLodTransitionTests` PASS; analysis reports 112 samples, 22 switches, zero policy violations, zero flicker events for 10% hysteresis thresholds `[0.5, 0.2, 0.08]`. Evidence: `Artifacts/m1-procedural-feasibility/b191d590b322570b33310141b485e5c02f63bcca/weapon-lod-transition-20261113T175104270Z/` and formal record `Artifacts/m1-procedural-feasibility/owner-closure-20261113/lod-transition-acceptance.json`. Unity transition-frame PNG sequences remain Pending/unavailable and were not fabricated. **Formal transition acceptance: PASS** (numeric policy + documented harness).

### P04 hardware / P05 / matrix

| Gate | Disposition |
|---|---|
| P01 foundations | PASS |
| P02 compiler/cache diagnostic | PASS |
| P03 silhouette/ADS | PASS-with-limits |
| P03 LOD transition | PASS (numeric) |
| P04 material generation/gallery/compression | PASS (implementation) |
| P04 B01 hardware/thermal/variant counters | Unverified / **Waived for M1** (ADR-025); reports stay PendingHardware |
| P05 soldier visual/clearance | **FAIL / not accepted** (ADR-024) |
| P05 numeric grip/contact/1000-pose/eight-rig | Spike evidence only |

Closure package: `Artifacts/m1-procedural-feasibility/owner-closure-20261113/closure-disposition.json`. Marker name: `m1-procedural-feasibility-owner-accepted` (qualified closure, not a passing-performance tag). Closeout pure suite: 283/283 PASS via `Tools/Test-Generation.ps1 -Offline` at `Artifacts/m1-procedural-feasibility/b191d590b322570b33310141b485e5c02f63bcca/20261113T175607370Z-0ec09175/` — including the ADR-024 clearance regression guard that still asserts third-candidate arm/body clearance FAIL and unresolved eye/sight residual (does not flip visual acceptance to PASS). `feature-content-lock.json` synced only to already-committed ADR-024 `docs/soldier-baseline.md` LF bytes. Future M2 assignment must not depend on accepting the failed soldier skin. Completed closeout documentation is committed on main; leftover task worktrees are retired after verifying they match main.
