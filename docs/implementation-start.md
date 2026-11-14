# Start implementation here

Status reviewed 2026-11-14 at 59316d3: T052–T064 remediation is committed on main. M0–M7 historical waivers remain; M8–M10 integration and M11 acceptance stay open for specific current gaps. The [audit](product-state-audit.md) records fresh Contracts 72/72, Simulation 101/101 and Game 38/38 PASS, Generation 310/311 with one failure, and source provenance FAIL on 19 missing metadata entries. No current Unity/player acceptance was performed by this documentation review.

## First assignment

**Current next step:** bounded T066 baseline repair, then T067 compiled content/native navigation and T068–T069 presentation/preferences in the [next playable-build plan](next-playable-build.md). T071 delivers a real owner-playable archive; T070 and T062/T065 complete measurement/visual/final acceptance. Do not repeat the already implemented T052–T054 or T035 freeze. Read affected original tasks, milestone pack and Game-v1. This documentation task creates no runtime assignment or coding lease.

M0–M7 are the eight historical closures (ADR-017/025–028/030–032). M8–M11 are implemented in part but not technically accepted. C0-v1 remains frozen in contract-lock.json, SGAF/P02 v1 in content-format-lock.json (ADR-018), N1-v1 under ADR-029 and Game-v1 under ADR-034 with ADR-036 settings schema 2 / ADR-037 workloads. ADR-035 package publication software exists; actual visual closure and native bake/install remain T067 / FIX04 work. P05 FAIL and the ADR-024 stop on further clearance search remain unchanged. T025 stays deferred. No worker independently edits shared Contracts, Bootstrap, schemas, locks or project settings.

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
