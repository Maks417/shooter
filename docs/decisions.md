# Architecture decision records

Statuses: Accepted = architectural requirement for implementation; Provisional = build as specified until evidence gate resolves; Deferred = excluded pending separate approval/design. Changes append a dated superseding record, preserving prior rationale. All decisions dated 2026-11-11; none implies code already exists.

## ADR-001 — Accepted: Unity 6 URP rasterization

Context: realistic procedural art must run on integrated Radeon. Decision: Unity 6 URP, C#, HLSL; conventional meshes and generated PBR caches, Forward baseline D3D11. Reject HDRP baseline and full-scene ray marching for compatibility/cost. Consequence: constrain lighting/overdraw and measure from M0. Revisit renderer only on measured evidence without abandoning iGPU gate. [Architecture](architecture.md), [performance](performance.md).

## ADR-002 — Accepted: zero authored visual source assets

Code/numeric recipes are source of meshes, textures, rigs and movement. Generated caches allowed with provenance, engine font exception explicit. Imported models/photos/painted textures/sprites/mocap/hand-keyed joint clips disallowed. Consequence: soldier feasibility is a milestone gate, not an outsourcing task. [Vision](vision.md).

## ADR-003 — Accepted: compiler/runtime separation

Expensive geometry/material/LOD/nav/weight generation runs in tools; runtime loads immutable versioned artifacts and runs cheap motion. Alternative generate everything on demand rejected for CPU/GPU contention and unpredictable hitches. Consequence: cache schema, migrations and cold-build CI are first-class. [Generation](generation.md).

## ADR-004 — Accepted: gameplay independent of visual quality

Canonical collision/nav/hit profiles and weapon definitions identical across profiles. No render-mesh hit detection or AI visibility derived from absent low-tier foliage. Consequence: maintain separate gameplay artifact hash and fixed gameplay pose evaluator. [Contracts](contracts.md).

## ADR-005 — Accepted: 60 Hz authoritative simulation

30 FPS display baseline with 60 Hz simulation; command/snapshot architecture from M0, offline first. Reject variable-delta combat and physics lockstep. Consequence: interpolation, replay and bounded catch-up needed; simulation determinism limited to tested envelope. [Combat](features/combat.md).

## ADR-006 — Provisional: GameObjects and kinematic motor

Begin with managed modular simulation and engine adapters, not full ECS. Kinematic motor supports explicit replay; rigidbody movement rejected as first prediction target. Revisit after M0 motor spike/B03 CPU measurements; converting to DOTS is a scoped migration, not a parallel-agent local choice.

## ADR-007 — Provisional: generated humanoid plus analytical IK

Parametric ring meshes, distance-based weights and gait/IK motion; no imported fallback. Reject full muscle/cloth simulation and learned motion datasets for slice complexity/provenance. M1 exits with reviewed poses/perf; failure reduces visual complexity through a recorded scope change. [Soldiers](features/soldiers-animation.md).

## ADR-008 — Provisional: transport behind pure codec port

Unity Transport candidate, evaluate NGO adapter at M5; custom FPS prediction not assumed free from middleware. NFE requires explicit ECS choice. Consequence: codec/authority work stays framework independent; choose exact package only after pinned-version spike. [Networking](features/networking.md).

## ADR-009 — Accepted: reproducible source, not universal deterministic physics

PCG32 named streams, canonical encoding, semantic versions, content addressing. Reject cross-platform lockstep promise for PhysX/nav/GPU. Server supplies authoritative state and gameplay hashes. Consequence: golden topology tests plus tolerance tests and version handshake. [Generation](generation.md).

## ADR-010 — Provisional: bounded compound and offline-first scope

256 m map, eight bots, rifle/pistol, static cover before four-player network slice. Reject open world/destruction/vehicles at foundation. Entire gameplay collision/nav loads initially; visuals stream. Consequence: later capacity and gameplay streaming need new evidence, not hidden expansion.

## ADR-011 — Provisional: exact toolchain resolved at M0

Use available supported Unity 6 LTS patch with compatible URP, pin all packages before implementation contracts freeze. No invented patch/version. M0 owner records lock and proves client/headless builds; [references](references.md) are guidance to check against that lock, not package compatibility guarantees.

## Decision change template

ID/title; date; owner; status; requirement affected; measured problem; alternatives; chosen behavior; contract/schema impact; migration; affected work packages; validation and rollback; supersedes ID. Integrator approves compatibility changes; an agent cannot relax art or performance requirements by silently updating a baseline.


## ADR-012 — Accepted: review closure defaults (revision 1.1)

Separate LogicalAssetId/Unity GUID from versioned request ContentId/payload hashes; exclude build telemetry from deterministic manifests. Explicit result/lease types replace ambiguous integer query returns and direct snapshot reads. Post-movement hit poses, batched same-tick shot intents and delayed AI commands define tick order. Compound Clear/no-respawn cooperative slice and collision matrix are fixed in [gameplay baseline](gameplay-baseline.md). Analytic death collapse is required; physics ragdoll optional. [Implementation start](implementation-start.md) links exact specifications and fixtures. These close ambiguity without claiming implementation or empirical feasibility.
