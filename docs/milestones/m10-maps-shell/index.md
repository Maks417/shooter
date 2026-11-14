# M10 — Two maps, menus and settings — implementation pack

**Current disposition — 2026-11-14: M10 integration incomplete.** Numeric planners/minimal shell/store exist; compiled content, functional session screens/tables and complete applied settings remain open. Repairs: T055–T056, T058–T061, T064–T065 in the [shared implementation plan](../../release-remediation-plan.md), with [FIX acceptance](../../release-remediation-validation.md). The [audit](../../product-state-audit.md) supersedes earlier readiness interpretations; original requirements, dated ledgers and raw evidence below are retained. No runtime assignment or worker lease is created here.

Status corrected 2026-11-14: **M10 integration incomplete**. Historical software closeout is retained in [handoff](handoff.md); its labels do not establish acceptance. Follow [remediation tasks](../../release-remediation-plan.md) and [FIX validation](../../release-remediation-validation.md).

## Goal

Make the proven team match usable from launch: two distinct packaged maps, map/mode/team-size setup, pause, live scoreboard/final results and persistent working sound/video/control/accessibility settings.

## Entry gate

M9 one-map match exit and Game-v1 freeze are accepted on main. Map recipe/compiler and client session seams are real. Catalog/menu/settings model work may start against frozen fakes after T035 if explicitly assigned, but this gate closes only after M9 and real two-map integration.

## Execute

| Document | Purpose |
|---|---|
| [Implementation](implementation.md) | Dependency order and concrete task outputs |
| [Contracts and integration](contracts-integration.md) | Frozen interfaces, failure boundaries and merge gate |
| [Agent assignments](agent-assignments.md) | Exclusive paths, shared ownership and dispatch prerequisites |
| [Validation](validation-acceptance.md) | Scenario IDs, commands and required evidence |
| [Handoff](handoff.md) | Pending gate record and downstream artifacts |

Exit requires the real artifacts and validation in this pack, reviewed by the integrator. Missing tools/hardware remain Pending; historical owner waivers are not inherited. Future implementation needs an explicit assignment and actual base commit.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m9-team-deathmatch/index.md) · [Next pack](../m11-playable-release/index.md).
