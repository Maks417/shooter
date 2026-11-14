# M10 — Two maps, menus and settings — implementation order

**Current disposition — 2026-11-14: M10 integration incomplete.** Numeric planners/minimal shell/store exist; compiled content, functional session screens/tables and complete applied settings remain open. Repairs: T055–T056, T058–T061, T064–T065 in the [shared implementation plan](../../release-remediation-plan.md), with [FIX acceptance](../../release-remediation-validation.md). The [audit](../../product-state-audit.md) supersedes earlier readiness interpretations; original requirements, dated ledgers and raw evidence below are retained. No runtime assignment or worker lease is created here.

Original implementation/port requirements retained. Current implementation is partial; product acceptance is open. Read [entry](index.md), [Game-v1](../../game-session-contracts.md) and the authoritative feature specs before editing. Task IDs extend T001–T034 without renumbering historical tasks. Start only assigned work; integrate through main in dependency order.

## T044 — Publish the catalog and team map metadata

Adapt training-compound for both teams' safe respawns, validate fixed catalog IDs/mode/team-size support, manifest/dependency closure and shipping catalog. Add corrupt/missing/incompatible catalog behavior; all selected map data is packaged.

## T045 — Build the second map

Implement depot-yard as a distinct numeric layout grammar under its own recipe folder using approved primitives/materials. Add team spawn slots, flank routes, nav/cover and generated visual package; validate actual Unity nav/LOS/clearance and clean cold/warm reproduction.

## T046 — Implement the shell and match tables

Build actual interactive Unity menus, setup selector, cancellable loading, pause/resume/restart/leave, live Tab scoreboard and final tables from immutable session data. Cover 12 participant rows, keyboard/mouse focus/cursor/input capture and replay/menu transitions.

## T047 — Persist and apply settings

Implement bounded versioned atomic user settings with last-good recovery/newer-schema preservation; stage Apply/Cancel, display confirmation/revert, real quality/device audio/input/FOV/accessibility adapters. Integrator binds providers and verifies persistence/relaunch across both maps.

## Merge and completion

Each task returns source changes, relevant test results, source/toolchain/content/seed/workload identity and a limitation record. Integrator handles shared files once, reruns affected integration checks, updates this pack's handoff and commits completed changes into main. A future-only path or wrapper in this plan must become real and tested before it counts as a deliverable.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m9-team-deathmatch/index.md) · [Next pack](../m11-playable-release/index.md).
