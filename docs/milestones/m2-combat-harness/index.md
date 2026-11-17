# M2 — Combat harness — implementation pack

Status: **CLOSED WITH USER WAIVER**, 2026-11-13. C01–C04 + V05/V06/V09 Unity scenarios, EditMode/PlayMode and client/server smoke PASS. Reference-player hardware remains PendingHardware and is Unverified/Waived for M2 under ADR-026. Base `925d23f`; software HEAD `97f10ef`. Canonical backlog tasks: T011–T013 (+ T027 defaults via integrator). Assigned packages: WP-05 with WP-04/WP-02 adapters. See [handoff](handoff.md) and root `coordination.json`.

## Goal and entry gate

Deliver 60 Hz movement, rifle/pistol mechanics, authoritative hit capsules, procedural first-person feedback and robust cover/muzzle rules in a small test space.

M0 C0 permits early motor/combat work against fakes. M1 rig/socket artifacts and reviews are required for final integration and milestone exit.

## Read and execute

| Document | Purpose |
|---|---|
| [Archived planning pack](../../archive/index.md#m2-combat-harness) | Pre-work implementation plan, contracts/integration and agent assignments (historical; outcomes are in the handoff) |
| [Validation and acceptance](validation-acceptance.md) | Exact scenarios, evidence and gate checklist |
| [Handoff record](handoff.md) | Fill-in implementation status and next-milestone artifacts |
| [Shared specification](../../index.md) | Single shared baseline in the docs root |

## Completion

Movement/combat tests pass; two weapons playable; living hit poses match visible body within declared tolerances; feedback is generated-only. Simulation tests show bounded allocations. Full eight-bot B03 performance gate belongs to M3/M4. M2 closed under ADR-026 with remaining reference-hardware gates waived for this milestone only.

[Previous pack](../m1-procedural-feasibility/index.md) · [Next pack](../m3-generated-compound/index.md) · [Milestone catalog](../index.md).

Shared requirements, contracts and feature specifications live once in the docs root and are linked above. This folder contains only milestone-specific implementation documents. Read the shared contracts and assigned feature pages before implementing; accepted prerequisite code and artifacts are still required. Local plans do not override shared requirements or budgets.


## Revision 1.1 implementation refinements

[Gameplay baseline](../../gameplay-baseline.md), [tick/replay protocol](../../simulation-protocol.md) and T027/T028/T032 define C01–C04. Implement analytic death collapse; physics ragdoll optional. Check same-tick shots, rounding, masks and full prediction state now, not M5.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
