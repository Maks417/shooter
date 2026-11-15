# Procedural military FPS — implementation documentation

**Visual design goal:** quality comparable to Battlefield V, Bodycam and Warface. [VD0–VD9 design milestones](design-milestones.md) define concrete deliverables, dependencies, comparison evidence and owner/technical exit gates. Begin with art direction and one representative art slice; current software does not establish visual parity.

**Current visual preview — 2026-11-14:** [T074 Windows installer 0.3.1 and handoff](milestones/m11-playable-release/handoff.md#t074--scene-led-menu-and-tactical-hud-preview), binary source b531da9, includes the scene-led menu/map cards and compact tactical HUD. Clean Unity 29/16 and Client/Server/package/installer checks pass. Final interactive review was stopped by the owner; gameplay/device/reference/P05 acceptance stays open. This entry supersedes the older candidate references below.

Unity 6 URP / C# / HLSL design for a realistic modern-world shooter generated from code and numeric recipes. No imported/manual meshes, painted textures, sprites or authored animation. Initial hardware target: Ryzen 5 4600H integrated Radeon, 16 GB DDR4-3200, NVMe; native 720p Low at 30 FPS, 60 FPS stretch.

**Latest, 2026-11-14:** slide-enabled `5eaced5` preview is built/packaged, hash-verified and starts independently to Main; see [guide](playtest-guide.md) and [M11 delivery](milestones/m11-playable-release/handoff.md#t072--slide-enabled-playtest-delivery). Manual game/device/performance acceptance remains pending; final installer/upgrades are planned T073. **Earlier review from 84befb9 / a54ed83:** a clean `7cd7e81` Windows playtest archive exists; its SHA256 and 245 inventoried file hashes were rechecked. T066 baseline and T068/T069/T070 software repairs are landed. Manual gameplay is still Pending, and T067 native-navigation publication is incomplete. M8–M10 integration validation and M11 acceptance remain open; M0–M7 waivers and P05 FAIL remain. Start with the [launch/playtest guide](playtest-guide.md), [current audit](product-state-audit.md) and [remaining-work queue](next-playable-build.md).

First-version owner priority: **play locally with bots; no online multiplayer or server purchase/hosting required**. Existing network/server architecture is retained, with online delivery deferred. See [scope](vision.md#first-version-scope-local-bots-only).

**T072 follow-up:** the owner authorized local sprint slide implementation (default Left Shift + F) under [ADR-039](decisions.md#adr-039--accepted-local-authoritative-sprint-slide-t072). See [combat rules](features/combat.md#local-sprint-slide--t072--adr-039) and [guide](playtest-guide.md) for actual build availability; the older 7cd7e81 archive has no slide.

**Final delivery requirement:** the Windows game must have an [installer that updates an existing installation](features/windows-installer.md) when a newer installer is run, preserving settings/controls. ZIPs are playtest artifacts; T073 installer implementation is planned.

## Next development

Use the [remaining T067/T071 work](next-playable-build.md): play the existing archive, finish native-navigation publication/query validation, repair observed blockers and complete independent recovery/rollback handoff. T068/T069 need player/device evidence; T070 hardware and T062/T065 visual/final acceptance remain open. Original [T052–T065 cards](release-remediation-plan.md) and [FIX01–FIX14](release-remediation-validation.md) remain applicable. This docs review creates no leases.

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
| [Visual design milestones](design-milestones.md) | Battlefield V / Bodycam / Warface quality target; VD0–VD9 deliverables, dependencies, review rubric and evidence |
| [Playable release review and order](playable-release-plan.md) | Committed software, remaining build queue and deliverable boundary |
| [Next playable build](next-playable-build.md) | Remaining T066–T071 cards, owner-playtest handoff and final acceptance boundary |
| [Launch and playtest guide](playtest-guide.md) | Verified archive location/hash, menu steps, controls, limits and manual test record |
| [Current product state audit](product-state-audit.md) | Findings, actual source/evidence and current milestone disposition |
| [Release remediation implementation](release-remediation-plan.md) | Bounded tasks T052–T065, ownership, dependencies and compatibility |
| [Release remediation validation](release-remediation-validation.md) | FIX01–FIX14, real-player acceptance and evidence requirements |
| [Product visual defect inventory](visual-defect-inventory.md) | T062/FIX11 capture-overlay tooling pointer; P05 FAIL retained (ADR-024); VD presentation gaps |
| [VD0 visual art brief](visual-art-brief.md) | Working art direction, comparison register, gaps and presentation ownership (draft until owner approval) |
| [Relocated view EditMode note](notes/procedural-viewmodel-editmode.md) | Historical RUN00 source-layout note |
| [Planning validation record](planning-validation.md) | Actual documentation/fixture/tool/source audit outcomes and remaining baseline blockers |
| [Game session contract plan](game-session-contracts.md) | Frozen Game-v1 ports, intended semantics and documented integration gaps |
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
| [Feature: compact TDM map layouts](features/tdm-map-layouts.md) | T076 three-lane 5v5/6v6 arena design rules and per-map layouts |
| [Feature: game shell and settings](features/game-shell.md) | Menu flows, interactive tables, persistence, audio/video/controls |
| [Feature: Windows installer and upgrades](features/windows-installer.md) | Final installer, in-place updates, preference preservation and install/upgrade acceptance |
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

[Open the milestone folders](milestones/index.md) for stage-specific implementation sequences, contracts, agent assignments, tests and handoff records. Each folder links to the shared specifications in the docs root. Root specifications remain authoritative; current M8–M11 acceptance is governed by the audit and repair matrix, not historical software-close labels.


## Implementation readiness refinements — revision 1.1

Begin with [implementation start](implementation-start.md), then the assigned milestone; the next owner step is T071 playtest of the retained archive; T067 native-navigation work needs its bounded implementation assignment. M0 remains the historical bootstrap reference. Shared details remain in one place.

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
