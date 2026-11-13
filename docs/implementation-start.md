# Start implementation here

Design revision 1.1, 2026-11-11. M0 now has an importable Unity project, verified editor/package lock, C0 source, a code-generated box harness and cleanly booting release client/dedicated-server players. Pure and Unity checks have actual evidence. This is a foundation diagnostic, not playable combat. Specification JSON remains fixtures. Reference B01 measurement and remaining acceptance status are recorded in the [M0 handoff](milestones/m0-foundation/handoff.md); no combat-performance or realism claim is made.

## First assignment

M0, M1 and M2 are closed with the owner's explicit hardware-validation waivers (ADR-017, ADR-025, ADR-026); three of eight milestones are closed. Read the repository AGENTS.md, [vision](vision.md), [architecture](architecture.md), [contract details](contract-details.md), [bootstrap runbook](bootstrap-runbook.md), [M0 pack](milestones/m0-foundation/index.md), [M1 pack](milestones/m1-procedural-feasibility/index.md) and [M2 pack](milestones/m2-combat-harness/index.md). F01–F04, M1 P01–P05 and M2 C01–C04 implementation exist; C0-v1 is frozen in root `contract-lock.json`; P02 format v1 is frozen in `content-format-lock.json` under ADR-018. See the [M1 handoff](milestones/m1-procedural-feasibility/handoff.md) for M1 close disposition (P05 FAIL under ADR-024; B01 PendingHardware waived for M1) and the [M2 handoff](milestones/m2-combat-harness/handoff.md) for M2 close disposition (software PASS; reference-player PendingHardware waived for M2 under ADR-026). Do not treat the gallery as the B01 benchmark or invent hardware PASS. M3+ require a new explicit assignment and must not depend on accepting the failed soldier skin or treating waived hardware as PASS. Do not start whole-game implementation or independent agents modifying shared contracts.

## Newly resolved design details

| Decision or artifact | Specification |
|---|---|
| Exact query result types, leases, enums, DTOs, thread model | [Contract details](contract-details.md) |
| Tick ordering, full prediction state, death/shot ordering | [Simulation protocol](simulation-protocol.md) |
| Gameplay defaults, collision masks, objective and respawn | [Gameplay baseline](gameplay-baseline.md) |
| PCG variant, canonical encoding, IDs and telemetry separation | [Deterministic format](deterministic-format.md) |
| Seeded body, bind pose, topology, gait and IK defaults | [Soldier baseline](soldier-baseline.md) |
| Exact benchmark routes, action schedules, result semantics | [Benchmark fixtures](benchmark-fixtures.md) |
| Audio, collapse, lighting and pose-validation task owners | [Additional task cards](task-supplement.md) |
| Valid/invalid machine-readable examples and expected outcomes | [Fixture catalog](fixtures/index.md) |

The original feature docs remain shared architecture. These linked detail pages refine their underspecified fields; the explicit corrections in the original pages remove the earlier conflicts. New breaking changes require an ADR and fixture revision.

## What remains intentionally empirical

F01 selected the verified installed Unity 6000.3.28f1 revision 8f082d2fe476 with URP 17.3.0 and matching Windows modules; root `toolchain-lock.json` records actual resolution. Fresh machines must verify that lock rather than select another editor or accept upgrades. Actual soldier realism, shader cost, mesh quality, reference-machine acceptance and network framework suitability require their documented spikes. They cannot be resolved by writing more documentation.

## Ready-to-parallelize checkpoint

C0 assembly builds without Unity, serialized fixtures pass, all port adapters/fakes have conformance tests, client/server smoke entry points work, ownership is assigned and fixture/generator versions recorded. At that checkpoint, each worker receives its milestone folder plus access to the shared docs root. Include source tag, permitted folders, fixture IDs and exact tests in each assignment.

## Current limits

No imported art or animation; no replicated physics lockstep; static cover; offline first. Rig spike uses scripted test action phases before combat implementation. Cold regeneration and actual minimum-hardware acceptance remain mandatory gates. Temporary historical memory utilization is excluded.
