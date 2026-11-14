# M11 — Playable build and acceptance — implementation pack

**Current disposition — 2026-11-14 review from 84befb9 / a54ed83:** M11 acceptance remains open. Clean `7cd7e81` Client/Server reports and verified local ZIP exist; T071 manual gameplay/recovery/rollback handoff remains partial. T067 native-navigation publication/query work, T062 visual disposition and T065 match/device/reference/soak/delivery exits remain open. REL01–REL06 are NotClaimed; P05 FAIL is retained. See the [audit](../../product-state-audit.md), [remaining queue](../../next-playable-build.md), [playtest guide](../../playtest-guide.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence remain; this review creates no coding lease or waiver.

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
