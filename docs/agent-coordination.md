# Parallel implementation and agent rules

This is a future implementation protocol, not authorization for this documentation task to start coding agents. Work packages in [backlog](backlog.md) use the [contracts](contracts.md), [layout](repo-layout.md) and [milestones](milestones.md).

## Entry rules for repository AGENTS.md

Read docs/index.md, vision.md, architecture.md, contracts.md and your assigned feature before editing. Confirm toolchain lock and owned paths. Treat documentation as proposed design until implementation updates record evidence. Never import prohibited assets or modify generated payloads manually. Do not change package locks, scene GUIDs, shared contracts, physics layers or quality profiles without integrator review. No agent may claim measured success on hardware it has not used.

## Roles and ownership

One human/integrator owns main, shared Contracts, Bootstrap, project/package settings, seed/schema registry and benchmark definitions. Each worker owns one package branch/worktree and only assigned paths. Compiler owner controls artifact schema; simulation owner controls command/tick/hit semantics; renderer owner controls shader variant registry. Tests belong with owner package; cross-package tests are integrator-owned. Document temporary ownership in `coordination.json` (package ID, owner, branch, base commit, paths, dependencies, contract version, status, lease expiry). Only integrator edits this file to avoid coordination conflicts.

## Workflow

1. Integrator freezes C0 contracts, creates compiling stubs/fakes, and merges WP-00 before parallel work.
2. Worker receives task card with exact scope, permitted paths, input fixture hashes, output interface, acceptance tests and dependency versions.
3. Worker checks interface fixture locally, implements against fakes if dependency not merged, and keeps a small runnable feature harness. Fakes are test-only and cannot be default shipping adapters.
4. Pull request states behavior, contract/schema impact, generated artifacts provenance, tested build/seed/profile, measured budget deltas, limitations and rollback. Evidence includes tests; visuals alone insufficient.
5. CI checks contracts, art audit, generation and feature tests. Integrator merges in dependency order, resolves shared files and runs integration smoke.
6. Worker rebases on new contract version explicitly, reruns impacted tests; integrator deletes retired stubs after real adapter passes conformance.
7. Commit all completed task changes into `main` before finishing. Integrate all task worktrees/branches, verify required checks and preservation of their contents in `main`, then remove the temporary worktrees and branches. Preserve required ignored evidence before cleanup; completed work must not remain only in a workspace, task branch or uncommitted working copy. Repository AGENTS.md makes this a standing rule.

Use separate git worktrees; never concurrent Unity editors on one project directory. Per-worktree Library/cache paths; share only immutable verified cache artifacts with locks. Unity-generated `.meta` and scenes change only under single owner. Avoid monolithic scenes: bootstrap scene composed by code from deterministic recipes. Workers do not auto-upgrade editor/packages or reserialize whole project.

## Compatibility procedure

Contract change request includes failing use case, old/new schema diff, affected packages, fixture/migration and validation. Additive optional fields may be backward-compatible if old readers safely skip them. Changing seed consumption, units, enum values, packet layout, hit pose, generator algorithms or collision hash is breaking even if C# compiles. Freeze new contract version on main; migrate dependent workers; do not simultaneously maintain undocumented variants. Package lock/ProjectSettings changes merge alone before feature PRs.

## Definition of ready / done

Ready: upstream contract accepted, fixtures and harness available, task fits owned paths, requirements and resource budget named. Done: feature acceptance plus required CI pass; no prohibited source art; all version/hash changes explained; bounded failure/cancel path tested; docs reflect actual implementation; integrator confirms no gameplay/profile divergence. Performance claims include actual reference-machine evidence; when unavailable, mark awaiting hardware validation and do not close milestone.

## Task-card template

Package ID/title; owner; base commit; contract version; allowed paths; forbidden shared files; objective; input/output types; dependency IDs; fixture seed/hash; CPU/GPU/memory allocation; edge/failure behavior; exact acceptance commands/scenarios; artifacts to attach; docs to update; integration order; rollback. Stop dependent edits on contract conflict and work on independent tests while integrator resolves; do not patch another owner's code silently.

## Parallel waves

- Wave 0: integrator WP-00, then compiler WP-01. Others may design test fixtures but not fork contracts.
- Wave 1: geometry WP-02, materials WP-03, soldier spike WP-04 and combat WP-05 use frozen core contracts/fakes. Compiler infrastructure merges before their artifact adapters.
- Wave 2: environment WP-06 and AI WP-07 after required collision/navigation/combat ports; WP-04 expands motion after skeleton freeze. Integrator assembles slice.
- Wave 3: networking WP-08 and validation/optimization WP-09 use stable offline slice. Performance engineer can measure throughout but cannot change workload to improve numbers.

When staffing is limited, preserve dependencies rather than running all tasks simultaneously. Parallel work reduces contention only after contracts exist; no requirement for a fixed number of agents or simultaneous unreviewed merges.

## New playable release waves — M8–M11

The original waves above describe M0–M7. The M8–M11 waves below retain the original integration dependency model, not a command to restart T035. Game-v1 is frozen and product work exists; the [current T066–T071 queue](next-playable-build.md) supersedes their dispatch order after committed T052–T064. Only an explicitly assigned integrator records active owners/leases/readiness in coordination.json. Historical closeout entries and false readiness are not current coding authorization.

1. Integrator alone implements T035 Game-v1 and freezes compiling ports, executable fixtures, lifecycle/thread/lease rules, exact input semantics and new workload identities on main. Integrator handles T036 extraction from sealed/private combat harness into Simulation/Session. Changes to C0/content schemas require ADR/migration first.
2. After freeze, T037 map/runtime and T038 client adapter work in disjoint paths against compiling fakes, integrating only real adapters. T039 proves actual player composition. New settings/map/menu models may be prepared from these seams if explicitly assigned, but no milestone closes before upstream real integration.
3. M9: rules T040, spawn policy T041 and AI T042 have separate subfolders/tests and a shared frozen lifecycle port. Integrator owns facade/Bootstrap and cross-package tests; merge rules → spawn → AI → T043 real match. The global query budget is shared across both teams.
4. M10: catalog/compound T044 and depot T045 own distinct map folders; settings T047 and shell T046 communicate through Game-v1. Integrator alone updates compiler entry points, registries, project files and device-service wiring. Limit concurrent workers to available slots rather than launching every role.
5. M11: one release integrator owns packaging/evidence, one character owner remediates named visual defects, and one validator runs the frozen matrix. Production optimization goes back to the owning module; validators do not tune the workload or relax gates.

Exact source commit, contract/fixture/toolchain hashes, acceptance IDs, per-task budgets, base branch, rollback and evidence retention are required dispatch fields. Do not use `ad4544f` automatically after this planning commit; that SHA identifies the reviewed pre-plan code, and dispatch uses current accepted main. Historical waivers do not authorize new closure. A worker finishing a pure task can merge with explicit Pending player evidence, but the milestone remains open until its required actual exit.
