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
