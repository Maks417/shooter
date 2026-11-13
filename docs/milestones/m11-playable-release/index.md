# M11 — Playable build and acceptance — implementation pack

Status: **CLOSED (software package tooling)**, 2026-11-13. Tasks T048–T051. Local package tooling PASS; named visual/720p/soak PendingHardware. See [handoff](handoff.md).

## Goal

Deliver a source-matched Windows release archive the owner can extract and play offline on either map, with verified match rules, working settings, reviewed procedural soldiers and honest native-720p performance evidence. Administrative closeouts of earlier milestones do not satisfy this delivery gate.

## Entry gate

M10 integrated two-map game is on main; exact content/contract/workload versions frozen, known defects triaged, locked editor/modules available for player build. A named owner/reference-machine session is scheduled for actual evidence; independent pure/release tooling can proceed if hardware is unavailable.

## Execute

| Document | Purpose |
|---|---|
| [Implementation](implementation.md) | Dependency order and concrete task outputs |
| [Contracts and integration](contracts-integration.md) | Frozen interfaces, failure boundaries and merge gate |
| [Agent assignments](agent-assignments.md) | Exclusive paths, shared ownership and dispatch prerequisites |
| [Validation](validation-acceptance.md) | Scenario IDs, commands and required evidence |
| [Handoff](handoff.md) | Pending gate record and downstream artifacts |

Exit requires the real artifacts and validation in this pack, reviewed by the integrator. Missing tools/hardware remain Pending; historical owner waivers are not inherited. Future implementation needs an explicit assignment and actual base commit.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m10-maps-shell/index.md) · [Next pack](../index.md).
