# M5 — Networking foundation — implementation pack

Status: **CLOSED WITH USER WAIVER** (ADR-030); pure N01–N05 PASS; Unity/hardware PendingHardware waived for M5 only. Baseline 2026-11-11; closed 2026-11-13. Canonical backlog tasks: T019–T020. Assigned packages: WP-08 plus integrator.

## Goal and entry gate

Choose verified networking adapter, establish dedicated authority, handshake/codec, two-player command delivery and client prediction/reconciliation with remote interpolation.

M4 offline slice passes; simulation/physics queries and events isolated from presentation; headless build still clean.

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

N1/N2 complete: chosen adapter pinned, dedicated two-peer sessions, validated command stream, motor prediction and remote interpolation pass impaired-network tests. Lag compensation/four-player acceptance intentionally remains M6 work.

[Previous pack](../m4-offline-slice/index.md) · [Next pack](../m6-network-slice/index.md) · [Milestone catalog](../index.md).

Shared requirements, contracts and feature specifications live once in the docs root and are linked above. This folder contains only milestone-specific implementation documents. Read the shared contracts and assigned feature pages before implementing; accepted prerequisite code and artifacts are still required. Local plans do not override shared requirements or budgets.


## Revision 1.1 implementation refinements

[Full prediction state and tick order](../../simulation-protocol.md) and [explicit DTO/lease rules](../../contract-details.md) are required before N03. Restore both weapon slots and all persistent motor/recoil state. Wire codec remains a pinned adapter decision at N01.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
