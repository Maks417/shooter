# Procedural military FPS — documentation

This is a Unity 6 URP / C# / HLSL modern-world shooter. Meshes, materials, rigs and motion are generated from code and numeric recipes. The one exception is the licensed art track ([ADR-124](decisions.md#adr-124--accepted-licensed-authored-art-track-characters-first)): CC0/CC-BY soldier bodies and animation clips, dressed in generated gear.

The first version is offline Team Deathmatch for one human plus local bots, on three maps. It ships as a Windows installer that upgrades in place. Networking is parked under [ADR-076](decisions.md#adr-076--accepted-offline-only-scope-and-bounded-local-evidence).

The reference machine is a Ryzen 5 4600H with integrated Radeon. The target is native 720p Low at 30 FPS.

Start with [next playable build](next-playable-build.md), the only page that tracks current status. Then read [vision](vision.md) and [architecture](architecture.md). Git history keeps every removed or superseded page.

## Status and acceptance

| Document | Purpose |
|---|---|
| [Next playable build](next-playable-build.md) | Current installer, open findings and tasks, order of remaining work |
| [Acceptance](acceptance.md) | Milestone gates M8–M11, scenario IDs, FIX matrix, final release sequence |
| [Playtest guide](playtest-guide.md) | Install, controls, T071 record sheet |
| [Visual direction](visual-direction.md) | Quality target, references, VD0–VD9 gates and review rubric |
| [Visual defect inventory](visual-defect-inventory.md) | P05 soldier status and open visual review items |
| [Risks](risks.md) | Open risks and mitigations |
| Milestone handoffs: [M8](milestones/m8-runtime-composition/handoff.md), [M9](milestones/m9-team-deathmatch/handoff.md), [M10](milestones/m10-maps-shell/handoff.md), [M11](milestones/m11-playable-release/handoff.md) | Evidence logs per delivery |

## Specifications

| Document | Purpose |
|---|---|
| [Vision](vision.md) | Scope, requirements R01–R15, art boundary, non-goals |
| [Architecture](architecture.md) | Assemblies, dependency rules, runtime composition, domain model |
| [Repository layout](repo-layout.md) | Tree and toolchain |
| [Contracts — C0](contract-details.md) | Frozen ports, DTOs, enums and buffers |
| [Game session contracts](game-session-contracts.md) | Frozen Game-v1 records, ports and tick boundary |
| [Simulation protocol](simulation-protocol.md) | Normative tick order |
| [Gameplay baseline](gameplay-baseline.md) | Motor, weapon slot and layer defaults |
| [Generation](generation.md) | Compiler pipeline, caches and locks |
| [Deterministic format](deterministic-format.md) | PRNG, canonical JSON and identity |
| [Content format v1](content-format.md) | Binary artifact format (hash-locked) |
| [Weapon recipe baseline](weapon-recipe-baseline.md) | Weapon generator versions (hash-locked) |
| [Soldier baseline](soldier-baseline.md) | Soldier recipe and motion log (hash-locked) |
| [Performance](performance.md) | Profiles, budgets, workloads and protocol |
| [UI design system](brandbook.md) | Tokens, typography, screens and accessibility |
| [Fixture catalog](fixtures/index.md) | Lock-pinned fixtures and schemas |

## Features

| Document | Purpose |
|---|---|
| [Team Deathmatch](features/team-deathmatch.md) | Rosters, scoring, timer, respawn, results |
| [Maps](features/tdm-map-layouts.md) | Catalog, site plans, rules, packages, adding a map |
| [Combat](features/combat.md) | Motor, weapons, damage |
| [Combat AI](features/ai.md) | Bot perception, tactics, navigation |
| [Weapons geometry](features/geometry-weapons.md) | Procedural weapon meshes and sights |
| [Soldiers and animation](features/soldiers-animation.md) | Soldier mesh, rig, analytic motion |
| [Materials and rendering](features/materials-rendering.md) | Materials, lighting, post-processing |
| [Audio](features/audio.md) | Sample bank, music, distance model |
| [Game shell](features/game-shell.md) | Menus, settings, Armory |
| [Career](features/career.md) | Ranks, unlocks, perks, service record |
| [Windows installer](features/windows-installer.md) | Installer, upgrades, INS01–INS06 |

## Process

| Document | Purpose |
|---|---|
| [Decisions](decisions.md) | ADR-001 onward and the change template |
| [Runbook](bootstrap-runbook.md) | Wrapper commands and scopes |
| [Build and CI](build-ci.md) | Lanes, gates, release |
| [Validation](validation.md) | Test suites and requirement traceability |
| [References](references.md) | External sources |

## Rules for these docs

C0 and Game-v1 are frozen. To change either, write an [ADR](decisions.md) and migrate the fixtures.

Every change updates the affected page using real code paths. Status goes only in [next playable build](next-playable-build.md) and the handoffs. Feature pages describe current behavior, not a changelog; history lives in ADRs and git.

After editing docs:
1. Run `python docs/verify_docs.py`.
2. For an intentional change, run `--refresh-manifest`.
3. Commit the updated `documentation-manifest.json`.

Documentation hashes are never game test results.
