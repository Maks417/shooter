# M6 — Four-player network slice — implementation pack

Status: **CLOSED WITH USER WAIVER** (ADR-031); pure S01–S05 PASS; Unity/B04 hardware PendingHardware waived for M6 only. Baseline 2026-11-11; closed 2026-11-13. Canonical backlog tasks: T021–T022. Assigned packages: WP-08, WP-09 with simulation owner.

## Goal and entry gate

Complete lag-compensated authoritative fire, robust join/resync/disconnect and four-player/eight-bot soak with measured server/bandwidth limits.

M5 prediction/interpolation and server command validation pass; static cover policy and gameplay pose version frozen.

## Read and execute

| Document | Purpose |
|---|---|
| [Implementation plan](implementation.md) | Ordered changes, dependencies and completion checks |
| [Contracts and integration](contracts-integration.md) | Shared interfaces, artifacts and merge checkpoints |
| [Agent assignments](agent-assignments.md) | Bounded ownership and parallel work |
| [Validation and acceptance](validation-acceptance.md) | Exact scenarios, evidence and gate checklist |
| [Handoff record](handoff.md) | Fill-in implementation status and next-milestone artifacts |
| [Shared specification](../../index.md) | Single shared baseline in the docs root |

## Completion

N3 four-player network slice accepted with actual B04 evidence, lag-compensation and robustness tests. Eight-player capacity, matchmaking, public internet deployment and services remain later scope.

[Previous pack](../m5-network-foundation/index.md) · [Next pack](../m7-hardening-stretch/index.md) · [Milestone catalog](../index.md).

Shared requirements, contracts and feature specifications live once in the docs root and are linked above. This folder contains only milestone-specific implementation documents. Read the shared contracts and assigned feature pages before implementing; accepted prerequisite code and artifacts are still required. Local plans do not override shared requirements or budgets.


## Revision 1.1 implementation refinements

[Historical shooter/trade rules](../../simulation-protocol.md) and [co-op/spectator rules](../../gameplay-baseline.md) define S01–S03. [B04 fixture](../../benchmark-fixtures.md) records separate-host placement; normal four-player combat soak remains required.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
