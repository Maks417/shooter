# Start implementation here

Planning update 2026-11-13. M0–M7 are administratively closed with their recorded owner waivers. Actual sources now include pure combat, compound/nav/AI, objective/HUD/audio, network and release tooling; default Bootstrap still composes the floor/box empty-actor diagnostic. The next product work is [M8–M11](playable-release-plan.md), planned/unassigned, to deliver a real offline team-match build. Specification/fixture checks do not prove player or hardware acceptance.

## First assignment

**Current next step:** assign [M8 runtime composition](milestones/m8-runtime-composition/index.md), T035–T039. Read [playable release review](playable-release-plan.md), [Game-v1 contract plan](game-session-contracts.md) and M8's complete pack before edits. This documentation assignment does not launch implementation agents or grant worker leases. Integrator freezes compiling Game-v1 and fixture hashes on main before parallel feature dispatch. Then M9 proves TDM, M10 integrates two maps/menu/settings, and M11 delivers the tested build. Original task/milestone IDs and evidence are preserved.

M0–M7 are the eight closed historical milestones (ADR-017/025–028/030–032); M8–M11 are the four planned new gates. C0-v1 is frozen in root `contract-lock.json`, SGAF/P02 format v1 in `content-format-lock.json` under ADR-018, and wire N1-v1 under ADR-029. Their compatibility and historical evidence remain binding. P05 FAIL under ADR-024 and waived Unity/hardware limits remain unchanged. T025 alone remains optional/deferred; T035–T051 are the new product plan and require continuing assignment. No worker independently edits shared contracts, Bootstrap, schemas, locks or project settings.

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
