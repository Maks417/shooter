# M10 — Two maps, menus and settings — implementation pack

Status: **CLOSED (software)**, 2026-11-13. Tasks T044–T047. Two maps, shell menu, file settings on main; named UI/device review Pending. See [handoff](handoff.md).

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
