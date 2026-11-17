# M3 — Generated compound and AI — implementation pack

Status: **CLOSED WITH USER WAIVER** (2026-11-13, ADR-027). Pure W01–W05 software PASS; Unity/hardware Pending waived for M3 only. Canonical backlog tasks: T014–T016. Assigned packages: WP-06, WP-07 with WP-01 integration.

## Goal and entry gate

Generate connected 256 m compound, full-map canonical collision/nav, streamed visuals and eight bounded-query combat bots.

M1 compiler/geometry and M2 motor/combat contracts stable. Character output exists for eight-soldier rendering; world planner may prototype earlier using fake primitives.

## Read and execute

| Document | Purpose |
|---|---|
| [Archived planning pack](../../archive/index.md#m3-generated-compound) | Pre-work implementation plan, contracts/integration and agent assignments (historical; outcomes are in the handoff) |
| [Validation and acceptance](validation-acceptance.md) | Exact scenarios, evidence and gate checklist |
| [Handoff record](handoff.md) | Fill-in implementation status and next-milestone artifacts |
| [Shared specification](../../index.md) | Single shared baseline in the docs root |

## Completion

Generated compound is traversable, streamed visuals stable, eight soldiers fight under bounded AI scheduling, and repeatable B02/B03 plus soak evidence exists. Required correctness and memory limits pass; unresolved performance issues are explicitly carried as M4 blockers.

[Previous pack](../m2-combat-harness/index.md) · [Next pack](../m4-offline-slice/index.md) · [Milestone catalog](../index.md).

Shared requirements, contracts and feature specifications live once in the docs root and are linked above. This folder contains only milestone-specific implementation documents. Read the shared contracts and assigned feature pages before implementing; accepted prerequisite code and artifacts are still required. Local plans do not override shared requirements or budgets.


## Revision 1.1 implementation refinements

[Objective/layer rules](../../gameplay-baseline.md) and [exact benchmark fixtures](../../benchmark-fixtures.md) feed W01–W05. T029 rules are already fixed; T033 freezes B02/B03 manifests and validates route/shot counts. Run normal-AI soak separately from controlled B03 firing.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
