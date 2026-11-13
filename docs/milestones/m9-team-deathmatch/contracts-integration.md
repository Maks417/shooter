# M9 — Team Deathmatch and bot teams — contracts and integration

Status: planned. [Game session contracts](../../game-session-contracts.md) are the single shared definition of new ports, DTOs, lifecycle and freeze/migration procedure. Existing [C0 details](../../contract-details.md), content v1 and N1-v1 remain frozen. The integrator records the actual accepted versions/hashes at entry; the documentation plan itself is not a compiling interface freeze.

## Input and output

Entry: M8 real runtime exit has evidence; Game-v1 compiling freeze and real actor/map adapters are on main. Spawn metadata and test-only short-rule fixtures exist. Do not interpret historical M4 objective closure as TDM implementation or the M3 eight-bot result as eleven-bot capacity.

Output: Required next artifacts: accepted TDM rules and lifecycle/AI commits, all boundary fixtures, one-map 5v5/6v6 player evidence, immutable result sample, full-roster workload counters and remaining bot/spawn defects. M10 consumes public match/callback interfaces and adds two-map navigation/settings UI.

## Composition and failure boundaries

Bootstrap alone owns session creation and mutation wiring. Simulation is fixed60Hz authority; AI emits normal next-tick commands; views/settings/menu read publications and send validated service requests. Session/participant/life identity prevents stale death/load/respawn callbacks from crossing sessions. Verified collision/navigation precede Ready; missing critical content prevents spawn. Stop/cancel releases nav/cover/content/view/audio handles once, with Unity destruction on main thread.

## Integration checkpoint

Merge only from the accepted base with complete contract/fixture conformance, no forbidden file edits, no required fake adapter in shipping code and no unreviewed schema/generator/packet change. Preserve old diagnostics and explicit workload IDs; new team workloads cannot overwrite B01–B04. Shared compiler/contract/Bootstrap/project changes land alone through integrator before consumers rebase. Run [validation](validation-acceptance.md) and record actual statuses in [handoff](handoff.md); preserve previous binary/content pair for rollback.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m8-runtime-composition/index.md) · [Next pack](../m10-maps-shell/index.md).
