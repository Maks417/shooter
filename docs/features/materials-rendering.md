# Feature: procedural materials and rendering

Purpose: plausible modern materials at low bandwidth cost using generated textures and conventional URP rasterization. Boundaries: [generation](../generation.md), [performance](../performance.md), [contracts](../contracts.md). Owner: WP-03, proposed `Presentation/Rendering`, `Shaders`, material generator subfolder; compiler infrastructure stays WP-01.

## Implementation

Generate metal, polymer, concrete, plaster, asphalt, dirt and woven-fabric libraries from seeded value/gradient noise, cellular fields, analytic edge masks, oriented weave and physically sized layering. Each material declares meters-per-tile, albedo linear range, roughness, metallic coverage, normal amplitude and allowed variation. Shared edge-distance/curvature masks from geometry place wear at contact points; dirt collects by slope/cavity and deterministic exposure rules. Avoid white-noise splatter across every surface.

Compiler creates tileable albedo (sRGB), tangent normal (linear) and packed mask (R metallic, G occlusion, B roughness, A optional height). URP adapter converts roughness to smoothness explicitly; custom shader reads documented channels. Correct normal orientation and tangent handedness verified on mirrored surfaces. Metallic values describe real substrate; exposed paint/dust are dielectric. Do not encode illumination into albedo.

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
