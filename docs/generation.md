# Deterministic generation, compilation and caches

See [contracts](contracts.md), [materials](features/materials-rendering.md), [environment](features/environment.md) and [CI](build-ci.md).

## P01 implementation status (2026-11-12)

`UnityProject/Assets/Shooter/Generation/Core` now contains `Random/Pcg32.cs`, `Random/NamedRandomStream.cs`, `Canonical/CanonicalJson.cs`, `Identity/ContentIdentity.cs` and `Dependencies/DependencyOrder.cs`. Production code is BCL-only .NET Standard 2.1/C# 9 and compiles in the locked Unity editor. `Tools/Test-Generation.ps1 -Offline` runs 69 pure tests against unchanged specification-v1.1 fixtures. Eight EditMode tests pass, including two generation checks. Evidence and limits are in the [M1 handoff](milestones/m1-procedural-feasibility/handoff.md).

`CanonicalJson.Encode` validates the bounded v1 JSON subset, not recipe schemas or physical ranges. `ContentIdentity.SourceHash` requires schema-valid input; the P02 box recipe adapter validates schema/range rules before calling it. `DependencyOrder.Sort` preflights at most 4096 nodes and 256 dependencies per node, rejects missing/duplicate/cyclic edges and returns a stable dependency-first order. It does not execute generators. P02 now implements the Editor/tools compiler, typed-codec freeze, cache publication and byte lease loader described below. Generated visual topology remains P03 work.

## Source → compiler → shipping content

Source is validated JSON plus generator code. Editor/compiler resolves dependencies, generates collision and render payloads, builds LODs, packs material arrays, binds rigs, builds navigation, validates and publishes a manifest. Shipping clients load precompiled content; runtime generation is limited to cheap bounded variations, particles, motion and optional background development preview. Expensive topology/remeshing, skin-weight solves, texture baking and navigation builds never occur in the combat frame loop.

CPU generation is the reproducibility baseline. GPU baking is optional acceleration whose output is validated within numerical tolerance and stored with GPU/backend fingerprint; it is not used for canonical gameplay hashes. Compute is not required for baseline Low rendering. Editor GPU tasks require a graphics-capable worker; headless CI must have CPU bake equivalents or restore verified artifacts.

## Determinism specification

Use a specified PCG32 implementation with checked-in algorithm source/test vectors, uint64 state and stream, explicit unchecked integer overflow. Do not use UnityEngine.Random or System.Random in canonical generators. A root seed and named substream derive from SHA-256 over length-prefixed UTF-8 domain, root seed little endian, stable recipe ID and feature key. Never use GetHashCode, thread ID, timestamp or iteration position to derive randomness. Stable logical IDs, such as building/room/slot, ensure adding grass does not move a doorway.

Canonical JSON encoding: UTF-8, sorted ordinal keys, no whitespace, no duplicate keys, invariant culture, integer seeds as decimal strings (avoid JS precision loss), finite scalar values quantized to declared units and integer-only physical units per [canonical v1 format](deterministic-format.md). Arrays preserve semantic order; unordered collections sort by stable ID. Unicode identifiers are restricted to ASCII allowed syntax. Source hash includes schema ID and canonical bytes. Tests provide golden bytes and digest; use standard SHA-256 known vectors before accepting the implementation.

Content key = SHA-256 of a length-prefixed binary tuple: domain `shooter.asset.v1`, recipe source hash, generator ID/version, root seed uint64, sorted dependency hashes, target platform, quality variant, output format version and toolchain hash. Collision/nav have a separate gameplay key with no visual quality field. Compiler implementation changes require generator version change even if source schema is unchanged. Compiler ABI/toolchain changes invalidate affected outputs.

Bit-identical output is required for pure integer/topology/seed tests on the pinned toolchain. Floating math uses fixed iteration order and explicit quantization at serialization boundaries; cross-CPU mesh equality is a tested tolerance, not an assumed guarantee. Physics, navmesh baking, GPU noise and full simulation are not claimed bit-deterministic across platforms. Match server distributes/validates gameplay artifact hashes rather than trusting independently generated client physics.

## Artifact layout and manifest

```text
cache/<first-two-hash-chars>/<content-id>/
  manifest.json
  mesh-lod0.bin ... mesh-lod3.bin
  material-albedo.bin material-normal.bin material-mask.bin
  skeleton.bin weights.bin
  collision.bin navigation.bin
```

Manifest fields: manifestSchema, contentId, recipeId, sourceHash, generatorVersions, seed string, toolchainHash, buildPlatform, visualProfile, dependencies[], artifacts[{kind,path,sha256,compressedBytes,uncompressedBytes,formatVersion}], bounds, vertex/triangle/submesh/bone counts, materialLayerIds, deterministic provenance and validationSummary. Only relevant artifacts exist for a recipe; required kinds are schema-defined. Paths must be relative and cannot contain traversal, roots or symlinks leaving cache.

Write into a unique temporary sibling; validate every payload/hash; fsync/close; publish immutable directory by atomic rename on same volume. Never overwrite a known content ID with different bytes. A separate index switches atomically after manifest readiness. Concurrent builds use per-key lock and deduplicate; stale lock recovery requires process check and timeout. Cancellation removes only its temp directory. Cache purge respects live leases. Corruption cannot trigger arbitrary file deletion.

## Geometry pipeline

Parameter validation → primitives/profiles → constructive composition → welded topology where necessary → normals/tangents/UVs → collision proxies → generated LOD chain → bounds → validation → packing. Prefer closed parametric component construction over fragile general CSG. Reject negative dimensions, impossible anatomy, self-intersection at critical joints, nonfinite vertices, invalid indices, zero-area triangles above declared tolerance and excessive counts. Decorative open surfaces are explicitly tagged rather than globally requiring manifold meshes.

Generate LODs from construction rules first (fewer radial segments, removed bevels/equipment detail). Optional quadric simplification preserves material boundaries, joint rings, hard edges and silhouettes. LOD0/1/2/3 target triangle ratios 1/.5/.2/.07, adjusted per feature. Collision is independent primitive/convex proxy data and never generated from whichever render LOD happens to load. Store projected error; switch using screen-height thresholds with 10% hysteresis. Bounds include animation displacement and accessories.

## Compiler interface to implement

`ShooterBuild.CompileContent` now delegates to `ShooterContentBuild.Compile`. `Tools/Compile-Content.ps1` supports the fixed `box-collision-v1` set, Low/Balanced/High requests, explicit `-CacheRoot`, `-Force` and `-DryRun`. Actual content profile is always gameplay for this collision diagnostic. The Editor entry validates the pinned toolchain, compiles the numeric source `Generation/Recipes/box-collision.json`, acquires a verified collision byte lease and writes a separate build proof with source hashes and elapsed time. It returns nonzero on failure. Arbitrary recipe/seed/platform sets and slice-v1 are not implemented.

Cache limits: first slice ≤2 GiB shipped generated content, ≤8 GiB local compiler cache default, LRU eviction outside active artifacts. Compiler peak working set ≤6 GiB target on reference machine; process one large recipe at a time when necessary. If cache too large, reduce variants and use shared arrays, not synchronous streaming recompilation. Report cold/warm compile time; initial gate ≤10 min cold / ≤60 s warm for the slice, revised only with evidence.

## Versioning and migrations

Recipe schema major changes require explicit migration tool and before/after fixtures; never silently reinterpret old fields. Old caches are disposable and rebuilt. Saves record world and generator versions and fail with a clear incompatibility message if migration unavailable. Content hash mismatch at join refuses spawn and lists required build, not a network stream of arbitrary generator code. Golden hash changes require reviewed explanation, not automatic baseline acceptance.


## Exact format refinement

[Deterministic format v1](deterministic-format.md) defines PCG-XSH-RR initialization, canonical integer units, binary tuple encoding, stable logical identity and independent artifact hashes. [Fixtures](fixtures/index.md) include golden bytes/vectors. Build duration/timestamps/worker/driver and validation run logs belong in separate Artifacts build-report.json; never inside immutable content manifests or cache byte comparisons. Same request key with different payload hashes is an error. Compression v1 is none. Exact geometry binary codec freezes at P02 before worker adapters merge.

P02 artifact layout is frozen in [compiler artifact format v1](content-format.md), [production schemas](content-schemas/manifest.schema.json) and root `content-format-lock.json`. `Tools/verify_content_format.py` rejects source/format/toolchain drift. `Generation/Editor/Compilation/ContentCompiler.cs` executes a bounded dependency graph and publishes immutable caches; `Content/Runtime/CacheFiles.cs` verifies disk data, and `FileContentStore.cs` coalesces consumer-cancellable byte leases under residency/queue bounds. Test-only generators cover general DAG scenarios; the real production composition has only the CPU collision diagnostic. Full slice compile time/working set, cache eviction and Unity object installation remain pending. Evidence and limits are in the [M1 handoff](milestones/m1-procedural-feasibility/handoff.md).

[ADR-035](decisions.md#adr-035--accepted-verified-release-map-packages-t055-boundary) registers the verified release map-package boundary (`shooter.map-package.v1`, `shooter.map-metadata.v1`, compound/depot recipes, `unity.navmesh.bake.v1`) without implementing Compile-GameContent publication yet. Map scaffolding schemas/examples live under `docs/content-schemas/`; placeholder fixture digests are not shipping goldens.

P03/P04 initial numeric construction and socket assignment is recorded in [weapon baseline](weapon-recipe-baseline.md); new feature generators use the frozen P02 codecs and preserve collision/profile separation.

## P03/P04 feature source registration

Root `feature-content-lock.json` pins27 LF source/schema/migration files for geometry.weapon1.0.1 and material.surface1.0.1. `Tools/verify_feature_registry.py` fails unreviewed drift. The [weapon](feature-schemas/weapon.schema.json) and [material](feature-schemas/material.schema.json) schemas live outside unchanged C0/P02 schemas. `Generation/Core/Compilation/ValidatedRecipe.cs` checks a real schema validator and exact whole-source/document agreement before the additive compiler overload accepts it. Existing box requests retain their constructor and identity.

`Tools/Compile-Gallery.ps1 -CacheRoot <Artifacts path> -Capture` compiles eleven requests (two visual weapons, two gameplay proxies and seven material families), verifies manifests and writes a separate numeric gallery plan. Force regeneration must reproduce every immutable payload; High requests retain identical gameplay keys. Editor captures and the gallery plan are diagnostic outputs; the gallery is not the B01-materials workload. `Tools/Smoke-Gallery.ps1` exercises actual IL2CPP cache/mesh/texture/shader loading with the unchanged empty authority. Full slice/world loading and performance acceptance remain pending. [ADR-019](decisions.md#adr-019--accepted-additive-m1-feature-recipes-and-reproducible-source-byte-lock) records the generic seam and verified LF lock correction; [ADR-020](decisions.md#adr-020--accepted-versioned-first-visual-corrections-for-weapon-and-metal-generators) records the two versioned visual fixes.
