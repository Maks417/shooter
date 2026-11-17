# Start implementation here

Read [next playable build](next-playable-build.md) first: it holds the current installer, task disposition and the order of remaining work. The [product state audit](product-state-audit.md) holds finding status, and the assigned milestone pack holds handoff evidence.

## First assignment

Take the next item in the [order of remaining work](next-playable-build.md#order-of-remaining-work) that the owner has assigned. Read the affected original task card, its milestone pack, the feature page and Game-v1 before editing. Do not repeat landed work (T035, T066, T067 native publication, T068–T070 software, T073 installer); the remaining parts of those tasks are player, device and hardware evidence.

M0–M7 are historical owner-waived closures (ADR-017/025–028/030–032). M8–M11 are implemented but not accepted. C0-v1 is frozen in `contract-lock.json`, SGAF/P02 v1 in `content-format-lock.json` (ADR-018), and Game-v1 under ADR-034 with ADR-036 settings schema 2 and ADR-037 workloads; ADR-035/038 govern packages and the persisted catalog, ADR-083 the map registry. N1-v1 (ADR-029) and all networking are parked under ADR-076. P05 is numerically resolved by ADR-095 and awaits named visual review. No worker independently edits shared Contracts, Bootstrap, schemas, locks or project settings.

## Newly resolved design details

| Decision or artifact | Specification |
|---|---|
| Ports, query result types, leases, enums, DTOs, thread model | [Contracts — C0 specification](contract-details.md) |
| Tick ordering, full prediction state, death/shot ordering | [Simulation protocol](simulation-protocol.md) |
| Gameplay defaults, collision masks, objective and respawn | [Gameplay baseline](gameplay-baseline.md) |
| PCG variant, canonical encoding, IDs and telemetry separation | [Deterministic format](deterministic-format.md) |
| Seeded body, bind pose, topology, gait and IK defaults | [Soldier baseline](soldier-baseline.md) |
| Exact benchmark routes, action schedules, result semantics | [Benchmark fixtures](benchmark-fixtures.md) |
| Audio, collapse, lighting and pose-validation task owners | [Additional task cards](task-supplement.md) |
| Valid/invalid machine-readable examples and expected outcomes | [Fixture catalog](fixtures/index.md) |

The original feature docs remain shared architecture. These linked detail pages refine their underspecified fields; the explicit corrections in the original pages remove the earlier conflicts. New breaking changes require an ADR and fixture revision.

## What remains intentionally empirical

F01 selected the verified installed Unity 6000.3.28f1 revision 8f082d2fe476 with URP 17.3.0 and matching Windows modules; root `toolchain-lock.json` records actual resolution. Fresh machines must verify that lock rather than select another editor or accept upgrades. Actual soldier realism, shader cost, mesh quality, and reference-machine acceptance require their documented spikes (network framework suitability is parked with networking). They cannot be resolved by writing more documentation.

## Ready-to-parallelize checkpoint

C0 assembly builds without Unity, serialized fixtures pass, all port adapters/fakes have conformance tests, client smoke entry points work, ownership is assigned and fixture/generator versions recorded. At that checkpoint, each worker receives its milestone folder plus access to the shared docs root. Include source tag, permitted folders, fixture IDs and exact tests in each assignment.

For M8 onward this also requires the new Game-v1 compiling ports/fixtures, real actor lifecycle adapter ownership, map/settings seams and new workload definitions from T035. Historical C0 freeze alone does not authorize workers to invent TDM contracts or change shared Bootstrap. [Agent coordination](agent-coordination.md) defines the new bounded waves.

## Current limits

No imported art or animation; no replicated physics lockstep; static cover; offline first. Rig spike uses scripted test action phases before combat implementation. Cold regeneration and actual minimum-hardware acceptance remain mandatory gates. Temporary historical memory utilization is excluded.
