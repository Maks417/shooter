# M0 — Foundation — implementation pack

Status: implemented, acceptance pending, 2026-11-12. Locked editor/C0-v1, clean client/server builds and smoke, 60 pure tests, 11 Unity tests, allocation controls and bounded failure shutdown pass. Three final monitored B01 captures have complete GPU/allocation evidence; four render timings and thermal stabilization remain pending. No milestone is closed. Exact evidence and owners are in [handoff](handoff.md). Canonical tasks T001–T004; WP-00/WP-09 held by one integrator. No combat performance claim is made.

## Goal and entry gate

Pin Unity 6/URP; establish assembly boundaries, contracts C0, client/server composition, three quality profiles, tests and benchmark capture from day one.

No implementation prerequisite. Inspect the installed editor and create a clean project; existing documentation is the specification, not evidence of working systems.

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

Exact toolchain recorded; C0 frozen; three profiles exist; clean client/headless builds and contract tests pass; CI entry points and actual B01 evidence available. No claim of 30 FPS for a combat scene at this stage.

[Previous pack](../index.md) · [Next pack](../m1-procedural-feasibility/index.md) · [Milestone catalog](../index.md).

Shared requirements, contracts and feature specifications live once in the docs root and are linked above. This folder contains only milestone-specific implementation documents. Read the shared contracts and assigned feature pages before implementing; accepted prerequisite code and artifacts are still required. Local plans do not override shared requirements or budgets.


## Revision 1.1 implementation refinements

[Bootstrap runbook](../../bootstrap-runbook.md), [C0 details](../../contract-details.md), [fixture catalog](../../fixtures/index.md) and T026/T033/T034 are required F01–F04 inputs. Freeze B01-minimal-v1 only; do not require the M1 material library at M0.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
