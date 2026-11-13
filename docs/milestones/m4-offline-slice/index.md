# M4 — Offline playable slice — implementation pack

Status: **CLOSED WITH USER WAIVER** (2026-11-13, ADR-028). Pure O01–O03 software PASS; native-720p hardware Pending waived for M4 only. Canonical backlog tasks: T017–T018. Assigned packages: WP-09 integrator with feature owners.

## Goal and entry gate

Complete objective loop, settings/UI/accessibility and procedural feedback; deliver measured native-720p 30 FPS on reference hardware with an offline release build.

M3 complete; generated soldier/weapon/world artifacts verified; B02/B03 definitions frozen and outstanding budget issues assigned.

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

Playable offline objective slice and clean build pass; actual Ryzen4600H iGPU native720p/30 evidence meets all budgets. Missing hardware evidence blocks gate. No 60 FPS or multiplayer success claim implied.

[Previous pack](../m3-generated-compound/index.md) · [Next pack](../m5-network-foundation/index.md) · [Milestone catalog](../index.md).

Shared requirements, contracts and feature specifications live once in the docs root and are linked above. This folder contains only milestone-specific implementation documents. Read the shared contracts and assigned feature pages before implementing; accepted prerequisite code and artifacts are still required. Local plans do not override shared requirements or budgets.


## Revision 1.1 implementation refinements

[Task cards T029–T033](../../task-supplement.md) assign objective, synthesized audio, simple lighting and analytic collapse; no implicit required ragdoll. [Compound Clear rules](../../gameplay-baseline.md) fix O01. Use exact B03 and separate normal-AI soak.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
