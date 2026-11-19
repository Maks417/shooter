# Feature: procedural materials and rendering

The goal is plausible modern materials at low bandwidth cost, using generated textures and conventional URP rasterization. The code lives in:
- `Generation/Core/Materials`;
- `Generation/Core/Fx`;
- `Presentation/Rendering`;
- `Shaders`.

Budgets are in [performance](../performance.md). Lighting and textures never change hit rules.

## Material generation

**Baseline families.** Seven families: metal, polymer, concrete, plaster, asphalt, soil and woven fabric (`material.surface`, [schema](../feature-schemas/material.schema.json)). They are built from seeded periodic macro, grain and wear fields, with physical height-derived normals and complete mips. Albedo filters in linear light before sRGB encoding. Lighting is never baked into albedo. Wear sits at edges and contact points, never as white-noise splatter.

**Map surfaces.** Map surfaces use `material.noise` 1.2.0 ([ADR-088](../decisions.md#adr-088--accepted-seeded-noise-material-family), [ADR-106](../decisions.md#adr-106--accepted-kernel-built-weapons-gloves-soldiers-and-structured-map-surfaces), [ADR-116](../decisions.md#adr-116--accepted-realistic-map-colour-texture-and-shadows-materialnoise-120)).

| Property | Value |
|---|---|
| Tile | 4 m (`MapPackageBuilder.MapNoiseTileMm`) |
| Noise cell | At least six texels |
| Planes | 256 px on Low, 512 px on Balanced/High |
| Mask mips | Toksvig roughness, so distant bumps widen highlights instead of glittering |

The roles have realistic, varied albedo:
- warm grey concrete with joints, tie holes, cracks and damp;
- ochre plaster with brick spalls;
- steel chipped to red-oxide primer;
- galvanized corrugated sheet with rust in the troughs;
- asphalt with aggregate;
- mixed gravel.

Architectural accents divide their target colour by the plane's mean albedo, so tank white and safety yellow read correctly.

**Texture layout.** Every material is an albedo (sRGB) + tangent normal (linear) + mask triplet. The mask packs R metallic, G occlusion and B roughness, with A=255. The URP adapter converts roughness to smoothness explicitly. Frozen generator versions stay buildable.

**Near-field finishes.** `FirstPersonSurfaceBaker` bakes the near-field finish triplets for weapons, gloves and props at a 100 mm tile ([ADR-053](../decisions.md#adr-053--accepted-procedural-near-field-finish-triplets-and-hip-composition-t084)).

## Shaders and lighting

**Shaders.**
- `Shaders/ProceduralSurface.shader` is URP PBR, SRP Batcher compatible, with a small keyword set.
- `Shooter/MapSurface` draws every map surface. It adds:
  - bounded grime, dirt and macro bands;
  - triplanar detail at 0.55/m;
  - warm/cool hue drift;
  - rain streaks;
  - height fog;
  - the baked AO volume.

**Baked AO** ([ADR-081](../decisions.md#adr-081--accepted-baked-ambient-occlusion-volume-per-map)). Each map package carries `lighting-ambient-occlusion.bin`. It is a 160 × 16 × 192 sky-visibility volume baked from collision by `MapAmbientOcclusionVolume`. Its strength is 1.0 on Low and 0.8 on higher tiers. Actor visibility samples it at the body root. If you change the bake, update the golden hashes in `AmbientOcclusionVolumeTests` and `AmbientOcclusionVolumeEditModeTests` together.

**Sky and sun** ([ADR-091](../decisions.md#adr-091--accepted-numeric-sky-atmospheres-map-grades-and-actor-grounding-light)). `AtmosphereBaker` builds numeric cloud cubes and 32-cube map grades. `GeneratedSky.ApplyMap` installs the per-map sky, reflection and sun preset. Shadows are given per tier in [performance](../performance.md#profiles).

**Post-processing** ([ADR-080](../decisions.md#adr-080--accepted-code-built-post-processing-hdr-and-ssao-per-quality-tier)). It is code-built by `ShooterPostProcessingBuild` and `ProductPostProcessing` / `PostProcessingTier`. SSAO uses InterleavedGradient, so no stock noise images ship.

| Tier | HDR / tonemap | Bloom | SSAO | Grading |
|---|---|---|---|---|
| Low | LDR, none | Off | Off | `ColorAdjustments` only, saturation +10 |
| Balanced | HDR, Neutral | 0.25 | Half-res, 4 samples | Generated LUT |
| High | HDR, Neutral | 0.35 HQ | Full-res, 8 samples | Generated LUT |

To change post-processing, edit `PostProcessingTier` or `ShooterPostProcessingBuild`. Then run `ShooterPostProcessingBuild.Configure` in the locked editor, which regenerates settings provenance.

**Reflections and lights.** The product reflection probe is 256 px. On Balanced and High, four pooled point lights light muzzle flashes and explosions.

## Decals and effects

**Decals** ([ADR-090](../decisions.md#adr-090--accepted-packaged-procedural-decals-and-versioned-visual-layout-operations)). `ProceduralDecals` bakes an original atlas and a verified placement JSON. Decal operations are visual-only, and removing them reproduces the original collision, nav and spawn identities. `ProductDecals` draws them two ways:
- **Balanced and High:** URP screen-space decals out to 200 m.
- **Low:** depth-biased quads.

Up to 64 impact marks are pooled.

**Effects** ([ADR-092](../decisions.md#adr-092--accepted-numeric-volume-noise-combat-flipbooks), ADR-093). `FxFlipbook` emits five 8×8 sequences from 3D FBm and curl fields. `AnalyticFxPresenter` drives a capped lifetime pool through manually advanced ParticleSystems.
- Surface tags choose the impact response.
- Weapon definitions own their muzzle profiles.
- Low reduces the particle count.
- Reduced flash suppresses fire and flash.

No imported sprite or texture is used. The engine Particle System module is the only addition.

## Flow and faults

The material flow runs:
1. MaterialDefinition;
2. generated planes;
3. mip and packing validation;
4. ContentId;
5. a shared runtime material.

**Failure handling:**
- A missing layer is rejected at compile time.
- A missing shader variant fails build validation.
- Runtime never allocates a material per object.
- A map change releases the prior uploads.

## Acceptance

**Tests and checks:**
- seam and normal-handedness tests;
- no NaN at scalar bounds;
- a generated-only provenance scan;
- cold CPU bake equals the restored cache;
- no magenta surface on a quality change;
- required Low variants are present.

**Still Pending:**
- Low cost on the reference laptop;
- a named visual review of every tier (VD6, [visual direction](../visual-direction.md)).
