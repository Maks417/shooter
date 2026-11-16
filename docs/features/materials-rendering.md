# Feature: procedural materials and rendering

Purpose: plausible modern materials at low bandwidth cost using generated textures and conventional URP rasterization. Boundaries: [generation](../generation.md), [performance](../performance.md), [contracts](../contracts.md). Owner: WP-03, proposed `Presentation/Rendering`, `Shaders`, material generator subfolder; compiler infrastructure stays WP-01.

## Implementation

Generate metal, polymer, concrete, plaster, asphalt, dirt and woven-fabric libraries from seeded value/gradient noise, cellular fields, analytic edge masks, oriented weave and physically sized layering. Each material declares meters-per-tile, albedo linear range, roughness, metallic coverage, normal amplitude and allowed variation. Shared edge-distance/curvature masks from geometry place wear at contact points; dirt collects by slope/cavity and deterministic exposure rules. Avoid white-noise splatter across every surface.

Compiler creates tileable albedo (sRGB), tangent normal (linear) and packed mask (R metallic, G occlusion, B roughness, A=255 in frozen v1; optional height requires a future version). URP adapter converts roughness to smoothness explicitly; custom shader reads documented channels. Correct normal orientation and tangent handedness verified on mirrored surfaces. Metallic values describe real substrate; exposed paint/dust are dielectric. Do not encode illumination into albedo.

Start with UV tiling and one material layer per surface. Triplanar is reserved for irregular terrain/near surfaces and compiled variants; three-direction sampling is too costly as a blanket default. One base/normal/mask set is baseline. Use texture arrays/atlases only with stable layer indices and mip bleed handling. Generate seamless mipmaps, validate seams across wrapped tiles, and select supported compressed formats for platform; compare compressed normals and ensure CPU fallback can bake without graphics.

HLSL follows pinned URP includes and SRP Batcher-compatible material constant buffers. Keep small keyword set: normal on/off, alpha-clip only where needed, optional near detail. Count/strip variants and retain those used by each profile. Most geometry is opaque; grass optional at later phase, bounded alpha overdraw and gameplay visibility unchanged. Avoid transparent smoke as large stacked quads; use sparse analytic mesh volumes/procedural particles with explicit fill-rate budget.

Lighting baseline: one directional sun, ambient sky approximation, baked/static probe solution only if generated reproducibly, simple fog by distance, reflection probes from generated scene with controlled refresh. No per-frame environment captures. Baked light/probe caches are generated outputs with toolchain and world hash; lighting data never changes hit rules. Use flat generated overcast/sun test scenes for material evaluation.

## Flow and faults

MaterialDefinition → generated tile planes → mip/packing validation → ContentId → runtime shared material/array slot → shader. Missing layer ID rejects recipe at compile; incompatible shader variant fails build validation. Runtime does not silently allocate a new material per object. CPU material proxy and texture handle release together.

## Acceptance

- Seven material families render consistently at known physical scale and daylight exposure; reviewer records roughness/metallic and seam checks.
- Tile-edge seam and normal handedness tests pass; shader survives zero/max scalar bounds without NaN.
- B01 compares flat, single-layer and detail variants; Low fits texture/pass budgets from performance spec.
- Generated-only provenance scan passes; cold CPU bake and restored-cache build give equivalent material semantics.
- Build includes required Low variants and no unsupported runtime shader compilation path; quality transition has no magenta/pink surfaces.

## Implemented CPU and renderer baseline (2026-11-13)

`Generation/Core/Materials` implements strict [numeric recipes](../feature-schemas/material.schema.json), periodic macro/grain/wear fields, seven families (soil is the canonical dirt-family ID), physical height-derivative tangent normals and complete mips. Albedo filters in linear light before sRGB encoding; normal mips renormalize; mask channels retain metallic/occlusion/roughness and A=255. Explicit resolutions32/64/128/256/512 and tile100..8000mm are source inputs. Small diagnostics downsample a resolved256 grid instead of aliasing fine weave. No imported images, lighting baked into albedo or gameplay-frame texture baking occurs.

`Shaders/ProceduralSurface.shader` uses the pinned URP PBR path, explicit smoothness=1-roughness, one generated texture triplet, a bounded normal keyword, directional shadow caster and matching material constant buffers. `Presentation/Rendering/GeneratedGallery.cs` uploads verified SGAF planes/meshes during loading, shares materials, releases byte leases and destroys owned Unity resources. The analytic floor uses metric UVs. The separate gallery is a read-only diagnostic, not B01-materials-v1. Its images show all seven families under fixed sun/ambient and two weapon models; gameplay authority/collision stays in the foundation harness.

Material generator1.0.1 reduces broad bright wear patches to sparse bounded scuffs after actual image review; before/after inputs and immutable old caches are retained under [ADR-020](../decisions.md#adr-020--accepted-versioned-first-visual-corrections-for-weapon-and-metal-generators). Current GPU uploads are uncompressed RGBA32. The continuation implements Editor BC7/BC5 compression/normal-error measurements and the separate exact B01-materials camera/workload. Its real build log records per-pass/stage stripping counts, distinct from pipeline variants. Runtime compressed upload/storage, full material comparison/three-run release performance and resource acceptance remain open. Generated sky-gradient/probe polish also remains pending. The gallery's approximately7MiB texture inventory and estimated mesh bytes are not measured frame-time or GPU allocation acceptance. See [handoff](../milestones/m1-procedural-feasibility/handoff.md).

## T075 product surface publication

[ADR-044](../decisions.md#adr-044--accepted-versioned-procedural-map-surface-publication) map generators 1.0.1 publish four fixed 128px, two-metre albedo/normal/mask triplets through the unchanged CPU MaterialBaker. `MapVisualClosure.RequiredPaths` retains all companions through verified in-memory/disk extraction. `ProductSurfaceMaterial` converts metallic/occlusion/roughness to URP Lit channels while retaining full mips and spatial response; normals retain opaque-alpha XYZ packing. Metric combined map UVs use 0.5 repeats/metre, while the 256m unit-box ground uses 128 repeats. Map resource replacement releases prior uploads. Numeric generated variant materials are preloaded only by Client builds. Menu stage variants share texture planes; static map cards capture after URP startup and have rendered-pixel regressions. [T075 delivery and limits](../milestones/m11-playable-release/handoff.md#t075--procedural-presentation-preview-034). Reference performance and named material/visual acceptance remain Pending.

## Post-processing per quality tier (ADR-080)

URP post-processing is code-built: `ShooterPostProcessingBuild` creates the post-process data with generated lookups only and gives Balanced and High their own renderer with SSAO (InterleavedGradient, so no stock noise images ship). `ProductPostProcessing` creates the global volume at runtime from `PostProcessingTier`:

| Tier | HDR | Tonemapping | Bloom | SSAO | Grading |
|---|---|---|---|---|---|
| Low | off | none | off | off | contrast +6, saturation −4, vignette 0.16 |
| Balanced | on | Neutral | 0.25 | half-res, 4 samples | exposure +0.25, contrast +10, saturation −6, vignette 0.2 |
| High | on | Neutral | 0.35, HQ filter | full-res, 8 samples | as Balanced |

To change a value, edit `PostProcessingTier` (volume numbers) or `ShooterPostProcessingBuild` (SSAO and HDR), then run `ShooterPostProcessingBuild.Configure` in the locked editor so settings provenance is regenerated. Baked ambient occlusion is ADR-081 (next section). Low cost on the reference laptop and a named visual review of all tiers are Pending.

## Baked ambient-occlusion volume (ADR-081)

Each map visual package carries `lighting-ambient-occlusion.bin`, a sky-visibility volume (160 × 16 × 192 cells, 0.5 m × 1 m × 0.5 m) baked by `MapAmbientOcclusionVolume` from the collision boxes at compile time. `ProductMapView` uploads it as a `Texture3D`, and the `Shooter/MapSurface` shader (URP Lit plus the volume on indirect light) draws every map surface. Strength is 1.0 on Low and 0.8 on Balanced/High (`PostProcessingTier.AoVolumeStrength`). To change the bake (ray length, weights, grid), edit `MapAmbientOcclusionVolume`, update the golden hashes in `AmbientOcclusionVolumeTests` and `AmbientOcclusionVolumeEditModeTests` together, and republish content. Actors are not occluded yet.



## Owner-assigned visual roadmap — ADR-088 to ADR-093

`Generation/Core/Materials/NoiseMaterial.cs` adds `material.noise` 1.0.0 without changing either frozen `material.surface` version. The map registry's four material roles choose periodic 128/256 px albedo/normal/mask triplets at 2 m. `NumericPlanes` supplies complete rectangular SGAF mip chains. New recipe schemas/fixtures and the explicitly refrozen feature/content inventories identify these bytes. `MapSurface` adds bounded grime, dirt, macro variation and triplanar detail using the existing normal/mask keyword set. The later owner-assigned roadmap explicitly authorizes that detail path; measured Low cost remains required.

`ProceduralDecals` bakes an original atlas and verified placement JSON. Arena layout documentVersion 2 adds visual-only decal operations; code planners can supply a separate visual layout. Removing the decal ops must reproduce the original collision/nav/spawn identities. `ProductDecals` uses the locked URP screen-space decal feature on Balanced/High and depth-biased quads on Low, with 64 pooled impact marks. No gameplay collider is created.

`AtmosphereBaker` builds numeric cloud cubes and 32-cube map grades. `GeneratedSky.ApplyMap` installs the matching sky/reflection and sun preset; map shading adds height fog. Low uses LDR ColorAdjustments, higher tiers the generated lookup. Actor visibility reads the packaged AO volume at the body root; Low has code-only contact shadows.

`Generation/Core/Fx/FxFlipbook` emits five 8x8 sequences from 3D FBm/curl fields into SGAF. `AnalyticFxPresenter` keeps the original capped lifetime pool and adapts it into manually advanced ParticleSystems. Surface tags choose impact response; weapon definitions own muzzle FX profiles. Explosion parts, scorch and movement dust remain cosmetic. No imported sprite or texture, authored clip or engine/package upgrade is introduced; only the pinned editor's built-in Particle System module is enabled. Exact checks/identities and unresolved player/reference evidence are recorded in the [roadmap](../visual-roadmap.md).
