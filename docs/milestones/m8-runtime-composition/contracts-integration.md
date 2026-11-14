# M8 — Runtime composition and Game-v1 foundation — contracts and integration

**Current disposition — 2026-11-14: M8 integration incomplete.** RUN02–RUN05 remain unaccepted as complete product gates: verified full content, visible views/audio, pause and real lifecycle evidence are missing. Repairs: T052–T056, T061–T063 in the [shared implementation plan](../../release-remediation-plan.md), with [FIX acceptance](../../release-remediation-validation.md). The [audit](../../product-state-audit.md) supersedes earlier readiness interpretations; original requirements, dated ledgers and raw evidence below are retained. No runtime assignment or worker lease is created here.

Original implementation/port requirements retained. Current implementation is partial; product acceptance is open. [Game session contracts](../../game-session-contracts.md) are the single shared definition of new ports, DTOs, lifecycle and freeze/migration procedure. Existing [C0 details](../../contract-details.md), content v1 and N1-v1 remain frozen. The integrator records the actual accepted versions/hashes at entry; the documentation plan itself is not a compiling interface freeze.

## Input and output

Entry: M0–M7 closure dispositions have been reviewed without promoting waived evidence to PASS. The integrator can verify the locked editor/modules or records the exact missing tool and confines work to independent pure tasks. Existing C0/content/network locks and previous regression outputs are retained.

Output: Required next artifacts: accepted Game-v1 source/fixture/lock commit; authority lifecycle adapter; one packaged compound; actual runtime player/build manifest; path/input/audio/lifecycle evidence; frozen new workload definitions; named P05 remaining defects. M9 can then implement team match consumers without modifying frozen seams.

## Composition and failure boundaries

Bootstrap alone owns session creation and mutation wiring. Simulation is fixed60Hz authority; AI emits normal next-tick commands; views/settings/menu read publications and send validated service requests. Session/participant/life identity prevents stale death/load/respawn callbacks from crossing sessions. Verified collision/navigation precede Ready; missing critical content prevents spawn. Stop/cancel releases nav/cover/content/view/audio handles once, with Unity destruction on main thread.

## Integration checkpoint

Merge only from the accepted base with complete contract/fixture conformance, no forbidden file edits, no required fake adapter in shipping code and no unreviewed schema/generator/packet change. Preserve old diagnostics and explicit workload IDs; new team workloads cannot overwrite B01–B04. Shared compiler/contract/Bootstrap/project changes land alone through integrator before consumers rebase. Run [validation](validation-acceptance.md) and record actual statuses in [handoff](handoff.md); preserve previous binary/content pair for rollback.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m7-hardening-stretch/index.md) · [Next pack](../m9-team-deathmatch/index.md).
