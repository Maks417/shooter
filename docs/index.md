# Procedural military FPS — implementation documentation

Unity 6 URP / C# / HLSL design for a realistic modern-world shooter generated from code and numeric recipes. No imported/manual meshes, painted textures, sprites or authored animation. Initial hardware target: Ryzen 5 4600H integrated Radeon, 16 GB DDR4-3200, NVMe; native 720p Low at 30 FPS, 60 FPS stretch.

**Status, 2026-11-13:** all eight original milestones M0–M7 are closed with the explicit owner waivers recorded in ADR-017/025–028/030–032. Closure does not establish a ready game: later Unity/player/reference gates remain Pending, M4 records no player binary, and M1 P05 remains FAIL / not accepted under ADR-024. C0-v1, content v1 and N1-v1 remain frozen; hosted CI is deferred. Historical handoffs and failed/pending evidence are preserved. The new [playable release roadmap](playable-release-plan.md) defines M8–M11, **planned/unassigned**, for two maps, offline 5v5/6v6 bots, 150 kills / 15 minutes, menus, results and settings. This planning update implements no gameplay and grants no new waiver.

## Next development

Read [the review and ordered plan](playable-release-plan.md), then [M8 runtime composition](milestones/m8-runtime-composition/index.md). Future multi-agent dispatch waits for the compiling [Game-v1 freeze](game-session-contracts.md) and named ownership. The four new packs include task order, exclusive paths, integration contracts, acceptance IDs and pending handoff records.

## Reading order

1. [Implementation start](implementation-start.md), then [vision and requirements](vision.md)
2. [Architecture](architecture.md), [domain model](domain-model.md), [contracts](contracts.md)
3. [Generation/compiler](generation.md) and [performance](performance.md)
4. [Agent coordination](agent-coordination.md), [backlog](backlog.md), [milestones](milestones.md)
5. Assigned feature page, [validation](validation.md), [build/CI](build-ci.md)

## Complete document map

| Document | Purpose |
|---|---|
| [Vision, scope and requirements](vision.md) | Scope, requirements, art constraint and non-goals |
| [Playable release review and order](playable-release-plan.md) | Current-code gaps, next four gates and deliverable boundary |
| [Planning validation record](planning-validation.md) | Actual documentation/fixture/tool/source audit outcomes and remaining baseline blockers |
| [Game session contract plan](game-session-contracts.md) | Planned Game-v1 records/ports, lifecycle, fixtures and compatibility freeze |
| [Architecture](architecture.md) | Boundaries, dependency rules and lifecycle |
| [Proposed repository and toolchain](repo-layout.md) | Proposed paths and exact-version pinning procedure |
| [Domain model](domain-model.md) | Entities, units and invariants |
| [Shared interfaces and data contracts — version 1 proposal](contracts.md) | Shared DTOs, ports, disk/wire formats and lifetimes |
| [Deterministic generation, compilation and caches](generation.md) | Seeds, compiler, LODs, hashing and caches |
| [Quality profiles, budgets and measurement](performance.md) | Profiles, allocations and real-hardware benchmark gates |
| [Feature: procedural materials and rendering](features/materials-rendering.md) | Feature implementation, edge cases and acceptance |
| [Feature: procedural geometry and modern weapons](features/geometry-weapons.md) | Feature implementation, edge cases and acceptance |
| [Feature: generated soldiers, rigging and motion](features/soldiers-animation.md) | Feature implementation, edge cases and acceptance |
| [Feature: generated environments and streaming](features/environment.md) | Feature implementation, edge cases and acceptance |
| [Feature: FPS movement and combat](features/combat.md) | Feature implementation, edge cases and acceptance |
| [Feature: combat AI](features/ai.md) | Feature implementation, edge cases and acceptance |
| [Feature: Team Deathmatch](features/team-deathmatch.md) | Rosters, scoring, timer, respawn, team AI and results |
| [Feature: two playable maps](features/playable-maps.md) | Approved map catalog, procedural layouts, packaging and safety |
| [Feature: game shell and settings](features/game-shell.md) | Menu flows, interactive tables, persistence, audio/video/controls |
| [Feature: staged authoritative networking](features/networking.md) | Feature implementation, edge cases and acceptance |
| [Visual direction and UI design system](brandbook.md) | Proposed visual direction, HUD and accessibility |
| [Architecture decision records](decisions.md) | Accepted/provisional ADRs and change process |
| [Parallel implementation and agent rules](agent-coordination.md) | Ownership, branches, handoffs and contract freezes |
| [Milestones and release gates](milestones.md) | Executable gate deliverables and acceptance |
| [Work packages and sequenced implementation backlog](backlog.md) | Dependency graph, work packages and ordered tasks |
| [Risk register and mitigation gates](risks.md) | Risk triggers, mitigation and unresolved spikes |
| [Validation strategy and requirement traceability](validation.md) | Tests, evidence and requirement traceability |
| [Build, CI and release strategy](build-ci.md) | Clean builds, caches, CI lanes and release strategy |
| [Sources and version-sensitive guidance](references.md) | Primary sources and version-sensitive cautions |

## Operating rule

Freeze contracts C0 at M0. Treat paths and signatures as specifications to implement. Each future PR updates affected docs with actual code references and measured evidence. Keep generated-only art provenance, performance gates and gameplay/quality separation intact. Use [decision records](decisions.md) for changes; do not hide scope reductions in tuning.

`documentation-manifest.json` records file hashes and `verification-report.json` records delivery integrity checks. Neither claims game tests have run. Re-run the delivered `verify_docs.py` after documentation edits; regenerate the hash manifest through the documented verifier command only when changes are intentional.

## Milestone implementation packs

[Open the milestone folders](milestones/index.md) for stage-specific implementation sequences, contracts, agent assignments, tests and handoff records. Each folder links to the shared specifications in the docs root. Root specifications remain authoritative; implementation status is planned until evidence is reviewed.


## Implementation readiness refinements — revision 1.1

Begin with [implementation start](implementation-start.md), then the assigned milestone; M8 is the recommended next assignment. M0 remains the historical bootstrap reference. Shared details remain in one place.

- [Start implementation here](implementation-start.md)
- [M0 bootstrap runbook](bootstrap-runbook.md)
- [Contract details — C0 freeze specification](contract-details.md)
- [Simulation order, replay and prediction state](simulation-protocol.md)
- [Gameplay baseline, objective and collision matrix](gameplay-baseline.md)
- [Deterministic format and asset identity v1](deterministic-format.md)
- [Soldier recipe and motion starting baseline](soldier-baseline.md)
- [Reproducible benchmark fixtures v1](benchmark-fixtures.md)
- [Additional task cards and ownership](task-supplement.md)
- [Machine-readable fixture catalog](fixtures/index.md)

- [Readiness review closure and remaining evidence](readiness-review.md)
