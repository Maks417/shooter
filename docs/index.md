# Procedural military FPS — implementation documentation

Unity 6 URP / C# / HLSL design for a realistic modern-world shooter generated from code and numeric recipes. No imported/manual meshes, painted textures, sprites or authored animation. First version: one human plus local bots, offline Team Deathmatch on three maps, delivered as a Windows installer with in-place upgrades. Networking and the dedicated server are parked under [ADR-076](decisions.md#adr-076--accepted-offline-only-scope-and-bounded-local-evidence). Reference hardware: Ryzen 5 4600H integrated Radeon, 16 GB DDR4-3200, NVMe; native 720p Low at 30 FPS, 60 FPS stretch.

**Current state, build and next work:** [next playable build](next-playable-build.md). It is the only page that tracks the current installer; other pages link to it instead of repeating release banners. Visual work follows the [visual roadmap](visual-roadmap.md) toward the Battlefield V / Bodycam / Warface quality target in the [design milestones](design-milestones.md).

## Reading order

1. [Implementation start](implementation-start.md), then [vision and requirements](vision.md)
2. [Next playable build](next-playable-build.md) and [product state audit](product-state-audit.md)
3. [Architecture](architecture.md), [domain model](domain-model.md), [contracts](contract-details.md)
4. [Generation/compiler](generation.md) and [performance](performance.md)
5. Assigned feature page, [validation](validation.md), [build/CI](build-ci.md) and the [bootstrap runbook](bootstrap-runbook.md)

## Current state and plans

| Document | Purpose |
|---|---|
| [Next playable build](next-playable-build.md) | Current installer, product definition, task disposition and order of remaining work |
| [Launch and playtest guide](playtest-guide.md) | Install/launch steps, controls, known limits and manual test record |
| [Product state audit](product-state-audit.md) | Findings, evidence and milestone disposition |
| [Visual roadmap](visual-roadmap.md) | Current visual execution plan and ledger (ADR-084–095) |
| [Visual design milestones](design-milestones.md) | VD0–VD9 quality track, review rubric and evidence |
| [VD0 visual art brief](visual-art-brief.md) | Art direction and comparison register |
| [Product visual defect inventory](visual-defect-inventory.md) | P05 soldier defects and ADR-095 outcome |
| [Release remediation validation](release-remediation-validation.md) | FIX01–FIX14 evidence contract |

## Specifications

| Document | Purpose |
|---|---|
| [Vision, scope and requirements](vision.md) | Scope, requirements, art constraint and non-goals |
| [Architecture](architecture.md) | Assemblies, dependency rules and lifecycle |
| [Repository layout and toolchain](repo-layout.md) | Actual paths and exact-version pinning |
| [Domain model](domain-model.md) | Entities, units and invariants |
| [Contracts — C0 specification](contract-details.md) | Ports, DTOs, enums, buffers and envelopes |
| [Game session contracts](game-session-contracts.md) | Frozen Game-v1 ports and semantics |
| [Simulation protocol](simulation-protocol.md) | Tick order, replay and prediction state |
| [Gameplay baseline](gameplay-baseline.md) | Numeric gameplay, objective and collision matrix |
| [Deterministic generation, compilation and caches](generation.md) | Generators, compiler, LODs and caches |
| [Deterministic format and asset identity](deterministic-format.md) | Seeds, canonical JSON and identity rules |
| [Content format v1](content-format.md) | Binary artifact/codec format (hash-locked) |
| [Weapon recipe baseline](weapon-recipe-baseline.md) | Numeric weapon sockets (hash-locked) |
| [Soldier recipe and motion baseline](soldier-baseline.md) | Soldier recipe and motion (hash-locked) |
| [Quality profiles, budgets and measurement](performance.md) | Profiles, allocations and benchmark gates |
| [Benchmark fixtures](benchmark-fixtures.md) | Reproducible benchmark workloads |
| [Visual direction and UI design system](brandbook.md) | Visual direction, HUD and accessibility |
| [Machine-readable fixture catalog](fixtures/index.md) | Lock-pinned fixtures and schemas |

## Features

| Document | Purpose |
|---|---|
| [Procedural materials and rendering](features/materials-rendering.md) | Materials, shading, lighting and post-processing |
| [Procedural geometry and modern weapons](features/geometry-weapons.md) | Weapon geometry and visual profiles |
| [Generated soldiers, rigging and motion](features/soldiers-animation.md) | Soldier mesh, rig and analytic motion |
| [Generated environments and streaming](features/environment.md) | Environment generation and streaming |
| [FPS movement and combat](features/combat.md) | Motor, weapons and damage |
| [Combat AI](features/ai.md) | Bot perception, tactics and navigation |
| [Team Deathmatch](features/team-deathmatch.md) | Rosters, scoring, timer, respawn and results |
| [Playable maps](features/playable-maps.md) | Map catalog, packaging and safety |
| [Compact TDM map layouts](features/tdm-map-layouts.md) | 64 × 80 m arena rules and per-map layouts |
| [Game shell and settings](features/game-shell.md) | Menus, persistence, audio/video/controls |
| [Career ranks and service record](features/career.md) | Military ranks, unlocks, buffs and local statistics |
| [Combat audio](features/audio.md) | Stereo sample bank and distance model |
| [Windows installer and upgrades](features/windows-installer.md) | Installer, in-place updates and INS01–INS06 |
| [Staged authoritative networking](features/networking.md) | Parked under ADR-076 |

## Process and records

| Document | Purpose |
|---|---|
| [Architecture decision records](decisions.md) | ADR-001–095 and the change process |
| [Bootstrap runbook](bootstrap-runbook.md) | Wrapper commands, scopes and evidence rules |
| [Build, CI and release strategy](build-ci.md) | Clean builds, CI lanes and release flow |
| [Validation strategy](validation.md) | Tests, evidence and requirement traceability |
| [Milestones and release gates](milestones.md) | M0–M11 gate definitions |
| [Milestone packs](milestones/index.md) | Per-milestone handoffs and acceptance records |
| [Work packages and backlog](backlog.md) | Work packages and ordered tasks T001+ |
| [Task cards](task-supplement.md) | Task cards T026 onward |
| [Parallel implementation and agent rules](agent-coordination.md) | Ownership, branches and handoffs |
| [Risk register](risks.md) | Risks, triggers and mitigation |
| [Sources and version-sensitive guidance](references.md) | External references |
| [Documentation archive](archive/index.md) | Superseded plans and closed-milestone planning packs |

## Operating rule

C0 and Game-v1 are frozen; changes go through [decision records](decisions.md) with fixture migration. Each change updates affected docs with actual code references and measured evidence, and records results in the relevant milestone handoff. Keep generated-only art provenance, performance gates and gameplay/quality separation intact; do not hide scope reductions in tuning.

`documentation-manifest.json` records file hashes and `verification-report.json` records the docs integrity check. Neither claims game tests have run. Run `python docs/verify_docs.py` after documentation edits; refresh the manifest with `--refresh-manifest` only for intentional changes.
