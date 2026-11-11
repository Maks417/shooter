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

## ADR-013 — Accepted: initial M0 toolchain and explicit engine resource curation

Date 2026-11-11; owner single integrator/WP-00. Installed and verified editor 6000.3.28f1, revision 8f082d2fe476; URP/Core RP 17.3.0; Input System 1.20.1; Test Framework 1.6.0; AI Navigation 2.0.0. Exact portable values and dependency graph live in root `toolchain-lock.json` and `UnityProject/Packages`. This resolves ADR-011's selection; actual build/hardware gates remain separately recorded. The bundled URP template's Input System 1.12.0 failed on removed `BuildTarget.ReservedCFE`; the documented 6000.3 release package 1.20.1 compiled. This was initial dependency resolution before freeze, not an upgrade during gameplay work.

A first release build demonstrated that Core RP's `Resources/DebugUICanvas.prefab` and `DebugUIPersistentCanvas.prefab` pull authored debug UI sprites into players. This conflicts with the art policy and server payload boundary; [Unity's issue record](https://issuetracker.unity.com/issues/2697) describes the same inclusion. `Tools/prepare_unity_packages.py` therefore copies the exact installed Core RP source into ignored `.local/engine-packages/com.unity.render-pipelines.core`, omitting those two prefabs and their metadata. It verifies upstream and curated file inventories against the lock. It never edits installed packages or the Library cache. The UPM manifest references this reproducible local source dependency, retaining version 17.3.0; no source API or shader is modified. Runtime debugging UI is explicitly disabled. Fresh setup must prepare this source before package resolution; an unexpected existing curation fails rather than being overwritten.

URP's image-backed dithering slots are assigned code-generated recursive Bayer textures (8 and 64 pixels) through its public Editor resource API. The unused debug-font slot receives a persistent one-pixel zero-data asset generated by code: the engine's transient black texture serialized as null and was reloaded as the stock atlas, so it was insufficient. The 64-pixel ordered lookup is explicitly not a blue-noise-quality claim; M0 uses no LOD crossfade or post effects. Both splash and Unity-logo flags are disabled: disabling splash alone left a built-in logo texture/sprite in packed reports. Tools wrappers reject these extensionless built-in entries; final packed-source inspection confirms their removal. No painted/photo/AI texture or sprite exception is introduced. Settings/scene/material/lookup outputs have source/toolchain hashes in `Assets/Shooter/Settings/provenance.json`; native URP shader-prefilter cache fields generated during a real build are recorded after that stage. Native Unity YAML trailing spaces on empty scalars are retained through Git whitespace attributes, not removed from generated payloads. No golden payload is edited to match an old hash.

Migration: no C0 DTO, unit, seed stream, gameplay pose or wire schema changes; no accepted shipping content exists. Recreate only the ignored local curation from its pinned inputs, resolve packages, regenerate numeric settings, then rerun pure/Unity tests, player art/dependency audits and client/server smoke. Rollback is the preceding source/lock pair; do not retain a player whose package/manifest differs. Future curation changes require a new inventory and decision review. [Runbook](bootstrap-runbook.md) and [M0 evidence](milestones/m0-foundation/handoff.md) contain commands and acceptance status.
