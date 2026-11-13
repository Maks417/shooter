# Start implementation here

Planning update 2026-11-13 (implementation). M0–M7 remain administratively closed with their recorded owner waivers. M8 is **in progress**: Game-v1 frozen (ADR-034), RUN00 provenance repaired, pure OfflineGameSession/TDM reducer landed, diagnostic client/server builds+smoke PASS. Default Bootstrap still composes the floor/box empty-actor diagnostic; product vertical (real map install, input/HUD path, OfflineGameSession as launch) remains open. Specification/fixture checks do not prove product or hardware acceptance.

## First assignment

**Current next step:** continue [M8 runtime composition](milestones/m8-runtime-composition/index.md) T037–T039 (real map runtime, client adapters, product vertical), then M9–M11. Game-v1 is frozen in `game-contract-lock.json`; do not invent parallel session contracts. Read [playable release review](playable-release-plan.md), [Game-v1 contracts](game-session-contracts.md) and the M8 handoff before edits.

M0–M7 are the eight closed historical milestones (ADR-017/025–028/030–032); M8–M11 are the four new gates (M8 started). C0-v1 remains frozen in root `contract-lock.json`, SGAF/P02 format v1 in `content-format-lock.json` under ADR-018, wire N1-v1 under ADR-029, Game-v1 under ADR-034. P05 FAIL under ADR-024 unchanged. T025 remains optional/deferred. No worker independently edits shared contracts, Bootstrap, schemas, locks or project settings.

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

For M8 onward this also requires the new Game-v1 compiling ports/fixtures, real actor lifecycle adapter ownership, map/settings seams and new workload definitions from T035. Historical C0 freeze alone does not authorize workers to invent TDM contracts or change shared Bootstrap. [Agent coordination](agent-coordination.md) defines the new bounded waves.

## Current limits

No imported art or animation; no replicated physics lockstep; static cover; offline first. Rig spike uses scripted test action phases before combat implementation. Cold regeneration and actual minimum-hardware acceptance remain mandatory gates. Temporary historical memory utilization is excluded.
