# Deterministic generation, compilation and caches

The normative determinism rules are in [deterministic format v1](deterministic-format.md):
- PCG32 named streams;
- canonical JSON;
- integer units;
- content keys and identity;
- bit-identity and tolerance policy.

The binary artifact format is in [content format v1](content-format.md). Feature generators are in their feature pages ([weapons](features/geometry-weapons.md), [materials](features/materials-rendering.md), [soldiers](features/soldiers-animation.md), [maps](features/tdm-map-layouts.md)).

## Source → compiler → shipping content

Source is validated JSON plus generator code under `Generation/Core` (BCL-only, C# 9 / .NET Standard 2.1, compiled outside Unity by `Tools/Generation`).

The Editor/tools compiler (`Generation/Editor/Compilation/ContentCompiler.cs`) does the following:
1. resolves a bounded dependency graph (`DependencyOrder`: ≤4096 nodes, ≤256 dependencies per node, cycles rejected);
2. generates collision and render payloads;
3. builds LODs;
4. packs material arrays;
5. binds rigs;
6. bakes navigation;
7. validates the result;
8. publishes an immutable manifest.

Shipping clients load precompiled content only. At runtime they generate nothing beyond cheap bounded variation, particles and motion. Topology, skin-weight solves, texture baking and nav builds never run in the frame loop.

Generation rules:
- **CPU generation is the reproducibility baseline.** Optional GPU baking is validated by tolerance, fingerprinted, and never used for gameplay hashes.
- **Canonical generators never use `UnityEngine.Random` or `System.Random`.**
- **Collision and nav have their own gameplay key**, with no visual quality field.
- **Physics, NavMesh baking and GPU noise are not promised bit-identical across machines.** They are checked by tolerance and identity.

## Cache layout and publication

```text
cache/<first-two-hash-chars>/<content-id>/
  manifest.json
  mesh-lod0.bin ... mesh-lod3.bin
  material-albedo.bin material-normal.bin material-mask.bin
  skeleton.bin weights.bin
  collision.bin navigation.bin
```

**Manifest contents.** The manifest records:
- schema, content and recipe IDs;
- the source hash;
- generator versions and seed;
- the toolchain hash and platform/profile;
- dependencies;
- per-artifact kind, path, SHA-256, sizes and format version;
- bounds and counts;
- provenance and a validation summary.

Paths are relative and can never leave the cache. Build durations and logs go in a separate `Artifacts/.../build-report.json`, never in immutable content.

**Publication:**
1. Write into a unique temporary sibling.
2. Validate every payload and hash.
3. Publish by atomic rename.
4. Switch the index after the manifest is ready.

**Publication rules:**
- A known content ID is never overwritten with different bytes. The same key with different payload hashes is an error.
- Concurrent builds take a lock per key.
- Cancellation removes only its own temp directory.
- Purge respects live leases.

**At runtime,** `Content/Runtime/CacheFiles.cs` verifies data on disk, and `FileContentStore.cs` hands out cancellable byte leases within residency and queue bounds.

**Budgets:**
- ≤2 GiB of shipped generated content;
- ≤8 GiB local compiler cache (LRU);
- compiler peak working set ≤6 GiB.

## Geometry pipeline

The pipeline runs these stages in order:
1. parameter validation;
2. primitives and profiles;
3. constructive composition (closed parametric components preferred over general CSG);
4. normals, tangents and UVs;
5. collision proxies;
6. LOD chain;
7. bounds;
8. validation;
9. packing.

**Rejected outright:**
- negative dimensions;
- non-finite vertices;
- invalid indices;
- degenerate triangles above tolerance;
- excessive counts.

Decorative open surfaces must be tagged.

**LODs** come from construction rules first (fewer segments, removed detail). LOD0–3 target triangle ratios are 1, 0.5, 0.2 and 0.07. LODs switch on screen height with 10% hysteresis.

**Collision** is independent primitive/convex proxy data, never taken from a render LOD.

## Locks and registration

| Lock | What it pins | Checked by |
|---|---|---|
| `content-format-lock.json` | Content format v1, [content schemas](content-schemas/manifest.schema.json) and the map-package boundary ([ADR-035](decisions.md#adr-035--accepted-verified-release-map-packages-t055-boundary)) | `Tools/verify_content_format.py` |
| `feature-content-lock.json` | Every feature generator version (weapon, material, noise, flipbook, soldier, motion), its sources, its [feature schemas](feature-schemas/weapon.schema.json) and the [weapon](weapon-recipe-baseline.md) and [soldier](soldier-baseline.md) baselines. The lock file is authoritative for versions and counts. | `Tools/verify_feature_registry.py` |

`ValidatedRecipe` checks schema validity and exact source/document agreement before the compiler accepts a feature recipe ([ADR-019](decisions.md#adr-019--accepted-additive-m1-feature-recipes-and-reproducible-source-byte-lock)).

**Diagnostic compilers** produce diagnostic outputs only, never product content:
- `Compile-Content.ps1` (box collision);
- `Compile-Gallery.ps1` and `Smoke-Gallery.ps1` (weapons and material families).

Product maps are published by `Compile-GameContent.ps1 -Native`.

## Versioning

- A major recipe schema change needs a migration tool and before/after fixtures. Old fields are never silently reinterpreted.
- Old caches are disposable.
- Saves record world and generator versions, and fail clearly when no migration exists.
- A golden hash change needs a reviewed explanation. A new baseline is never accepted automatically.
