# Feature: generated environments and streaming

Owner WP-06; paths `Generation/Core/Environment`, `World/Runtime/Streaming`, `World/Runtime/Navigation`. Shared content pipeline is [generation](../generation.md); AI consumes [navigation port](../contract-details.md).

## World recipe

Historical M3 baseline: a 256×256 m bounded compound divided into 32 m chunks (8×8); the product maps are now compact 64 × 80 m arenas ([ADR-074](../decisions.md#adr-074--accepted-compact-64--80-m-arenas-with-distinct-per-map-layouts), [map layouts](tdm-map-layouts.md)). Root world plan chooses roads, terrain height function, building footprints and objective areas before detail generation. Grid/graph constraints enforce player capsule clearance, walkable connectivity, spawn visibility separation and at least two traversable approaches to the objective. Cosmetic seeds cannot alter doors, cover or terrain collision. Deterministic keys identify building/room/opening/prop slots.

Terrain uses a low-frequency analytic height field with bounded slopes, generated grid mesh, consistent border samples and crack-free LOD stitching. Combat compound stays predominantly flat for first navigation validation. Buildings use floor plans and modular procedural walls/floors/roofs/openings; structural rings align so doors and windows agree across chunks. Props include concrete barriers, crates, drums, fences and poles from primitive generators. Doors initially fixed open/closed static content; moving-door navigation is a later revision feature.

Gameplay material/cover metadata is generated alongside collision: hard cover, concealment and permeable surface are separate tags. Visual rubble/plants cannot become invisible aim blockers. Mandatory concealment volumes remain present in all profiles or are excluded from AI/player cover claims. Low reduces trim and debris, preserving silhouettes of meaningful cover. No destruction in slice; penetration is deferred until ballistic material thickness is validated.

## Collision and navigation

Generate primitive/convex static proxies; avoid runtime cooking of dense render meshes. Terrain collider comes from canonical collision height data at fixed resolution. Navigation is compiled from the same gameplay geometry using locked agent radius/height/step/slope. Store nav build settings/hash. Off-mesh links only for explicitly generated traversals; no accidental jump links. World validation spawns a capsule at sample points and checks reachability/spawn safety and cover clearance.

For slice, load authoritative collision/navigation for entire bounded map within memory budget; stream visuals by chunk. This avoids partial-nav seams early. Chunk-ready contract still applies and supports future gameplay streaming. Later phase partitions nav tiles and collision with one-chunk guard ring, load fences and revisioned links; cannot unload behind a live projectile or actor without defined retention.

## Rendering and residency

Shared repeated components instance by recipe/material hash. Chunk generation emits visibility clusters and conservative bounds. Start with engine frustum culling and simple distance LOD, add authored-by-algorithm room/portal connectivity only when measured. Occlusion caching is generated from the world and versioned. Keep enough distant geometry for grounded silhouettes and player recognition; no entire-scene SDF/ray marching.

Prefetch 3×3 visual chunks around player plus movement look-ahead within a ≤256 MiB visual residency allocation included in total memory budgets; refine with content sizes. Use priority queue nearest/visible first, cancel superseded requests. Upload split into bounded work units. During teleport, hold simulation in loading state until required collision and nearest visuals ready. Shared assets remain reference counted across chunks.

## Implementation (M3)

Pure planner/connectivity live in `UnityProject/Assets/Shooter/Generation/Core/Environment` (`CompoundPlanner` 1.0.0). Runtime streaming scheduler: `World/Runtime/Streaming/VisualStreamingScheduler`. Nav settings/compile/revisioned adapter: `World/Runtime/Navigation`. B02/B03 freeze: `Benchmarks/b02-b03-m3-freeze.json`. Milestone status: M3 handoff.

## Acceptance

All four seed fixtures produce connected spawn/objective graphs and collision-safe paths. Neighbor terrain vertices agree within 1e-4 m; building entrances align with collision within 1 cm. Nav routes have at least capsule radius clearance plus 5 cm margin. Full map collision/nav fit assigned budget and load gates. B02/B03 show no missing collision, cracks, unbounded uploads or content-handle leaks over 100 repeated traversals. Visual profile changes preserve collision and nav hashes byte-for-byte. Invalid recipe fails compiler with slot/building ID and reason; no silently unreachable objective.


## Objective and benchmark inputs

World planning consumes [Compound Clear spawn/zone rules](../gameplay-baseline.md) before M3, plus reserved corridors/placements in [benchmark fixtures](../benchmark-fixtures.md). Bot occupancy and cover tags follow the shared collision matrix. Planner failure rejects the seed; it never teleports an actor or silently moves gameplay cover according to profile.
