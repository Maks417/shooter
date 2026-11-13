# M9 — Team Deathmatch and bot teams — implementation pack

Status: **PLANNED / UNASSIGNED**, 2026-11-13. Tasks T040–T043. This pack defines future implementation; no workers, tests or closure are claimed by publishing it.

## Goal

Deliver the complete one-map team match through authoritative rules: 5v5/6v6, nine/eleven bots, allies and enemies, 150 kills or 15 active minutes, safe respawn and immutable results. Full shell and second map integrate in M10.

## Entry gate

M8 real runtime exit has evidence; Game-v1 compiling freeze and real actor/map adapters are on main. Spawn metadata and test-only short-rule fixtures exist. Do not interpret historical M4 objective closure as TDM implementation or the M3 eight-bot result as eleven-bot capacity.

## Execute

| Document | Purpose |
|---|---|
| [Implementation](implementation.md) | Dependency order and concrete task outputs |
| [Contracts and integration](contracts-integration.md) | Frozen interfaces, failure boundaries and merge gate |
| [Agent assignments](agent-assignments.md) | Exclusive paths, shared ownership and dispatch prerequisites |
| [Validation](validation-acceptance.md) | Scenario IDs, commands and required evidence |
| [Handoff](handoff.md) | Pending gate record and downstream artifacts |

Exit requires the real artifacts and validation in this pack, reviewed by the integrator. Missing tools/hardware remain Pending; historical owner waivers are not inherited. Future implementation needs an explicit assignment and actual base commit.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m8-runtime-composition/index.md) · [Next pack](../m10-maps-shell/index.md).
