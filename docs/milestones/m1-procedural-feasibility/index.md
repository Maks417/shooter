# M1 — Procedural feasibility — implementation pack

Status: **IN PROGRESS**, 2026-11-12. P01-P04 now have compiler/cache foundations, primitive and weapon LODs, semantic sockets, seven CPU material families and a URP cache-backed diagnostic gallery. All 229 pure tests pass from a clean checkout. Remaining P04 benchmark/compression and formal visual/LOD acceptance, P05 rig and full-slice gates remain pending; no new playable content or performance claim. Canonical backlog tasks: T005–T010. Assigned packages: WP-01, WP-02, WP-03, WP-04.

## Goal and entry gate

Prove reproducible compilation, primitive/weapon LODs, procedural PBR and a believable generated soldier with analytical gait/IK before content expansion.

M0 is closed with the explicit owner hardware-validation waiver (ADR-017); its unverified gates are retained. The owner assigned M1 on 2026-11-12. Frozen C0-v1 fixtures and toolchain locks exist. Geometry/material/rig workers may prototype against fakes, but generated-output integration waits for compiler schema freeze.

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

Compiler/cache v1 and generation tests pass. Rifle, pistol and one helmeted soldier regenerate from numeric source. Soldier visual and performance spike passes or explicit scope revision is approved before expansion. Cold/warm slice compile allocations/times are recorded; full-map time gate remains for later content.

[Previous pack](../m0-foundation/index.md) · [Next pack](../m2-combat-harness/index.md) · [Milestone catalog](../index.md).

Shared requirements, contracts and feature specifications live once in the docs root and are linked above. This folder contains only milestone-specific implementation documents. Read the shared contracts and assigned feature pages before implementing; accepted prerequisite code and artifacts are still required. Local plans do not override shared requirements or budgets.


## Revision 1.1 implementation refinements

[Deterministic format](../../deterministic-format.md), [soldier baseline](../../soldier-baseline.md), [fixture catalog](../../fixtures/index.md) and T028 define P01/P02/P05. M1 reload/aim uses scripted action phases; no dependency on unimplemented M2 combat. B01-materials-v1 is a distinct workload.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
