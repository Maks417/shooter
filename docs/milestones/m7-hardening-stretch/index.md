# M7 — Release hardening and 60 FPS experiment — implementation pack

Status: planned; no implementation or game test is claimed. Baseline 2026-11-11. Canonical backlog tasks: T023–T024; T025 deferred. Assigned packages: WP-09 integrator with all module owners.

## Goal and entry gate

Prove clean regeneration/install/release, broaden seeds and profile checks, complete regression evidence and evaluate 60 FPS separately from mandatory 30 FPS acceptance.

M4 offline and M6 network gates pass; previous passing binary/content pair retained; all mandatory issues triaged.

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

Mandatory release gates complete with archived evidence and tested rollback. 60 FPS has independent explicit status and may fail without blocking the approved30FPS release. Optional eight players and public deployment remain separate gates.

[Previous pack](../m6-network-slice/index.md) · [Next pack](../index.md) · [Milestone catalog](../index.md).

Shared requirements, contracts and feature specifications live once in the docs root and are linked above. This folder contains only milestone-specific implementation documents. Read the shared contracts and assigned feature pages before implementing; accepted prerequisite code and artifacts are still required. Local plans do not override shared requirements or budgets.


## Revision 1.1 implementation refinements

[Fixture catalog](../../fixtures/index.md), [benchmark definitions](../../benchmark-fixtures.md) and [task supplement](../../task-supplement.md) enter clean release audit. Stretch results require identical B03 workload/counters; optional ragdoll/public networking cannot silently become mandatory release scope.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
