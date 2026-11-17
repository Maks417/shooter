# M8 — Runtime composition and Game-v1 foundation — contracts and integration

**Current disposition — 2026-11-14 review from 84befb9 / a54ed83:** M8 integration validation remains open. T066 provenance/cancellation and T067 packaged catalog/visual loading are repaired; T068 authority-driven weapon/action views are landed in the retained `7cd7e81` candidate. Native-navigation publication/query semantics and actual RUN/player/repeated-life evidence remain open. See the [audit](../../product-state-audit.md), [remaining queue](../../next-playable-build.md), [playtest guide](../../playtest-guide.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence remain; this review creates no coding lease or waiver.

Original implementation/port requirements retained. Current implementation is partial; product acceptance is open. [Game session contracts](../../game-session-contracts.md) are the single shared definition of new ports, DTOs, lifecycle and freeze/migration procedure. Existing [C0 details](../../contract-details.md), content v1 and N1-v1 remain frozen. The integrator records the actual accepted versions/hashes at entry; the documentation plan itself is not a compiling interface freeze.

## Input and output

Entry: M0–M7 closure dispositions have been reviewed without promoting waived evidence to PASS. The integrator can verify the locked editor/modules or records the exact missing tool and confines work to independent pure tasks. Existing C0/content/network locks and previous regression outputs are retained.

Output: Required next artifacts: accepted Game-v1 source/fixture/lock commit; authority lifecycle adapter; one packaged compound; actual runtime player/build manifest; path/input/audio/lifecycle evidence; frozen new workload definitions; named P05 remaining defects. M9 can then implement team match consumers without modifying frozen seams.

## Composition and failure boundaries

Bootstrap alone owns session creation and mutation wiring. Simulation is fixed60Hz authority; AI emits normal next-tick commands; views/settings/menu read publications and send validated service requests. Session/participant/life identity prevents stale death/load/respawn callbacks from crossing sessions. Verified collision/navigation precede Ready; missing critical content prevents spawn. Stop/cancel releases nav/cover/content/view/audio handles once, with Unity destruction on main thread.

## Integration checkpoint

Merge only from the accepted base with complete contract/fixture conformance, no forbidden file edits, no required fake adapter in shipping code and no unreviewed schema/generator/packet change. Preserve old diagnostics and explicit workload IDs; new team workloads cannot overwrite B01–B04. Shared compiler/contract/Bootstrap/project changes land alone through integrator before consumers rebase. Run [validation](validation-acceptance.md) and record actual statuses in [handoff](handoff.md); preserve previous binary/content pair for rollback.

[Release roadmap](../../next-playable-build.md) · [Shared documentation](../../index.md) · [Previous pack](../m7-hardening-stretch/index.md) · [Next pack](../m9-team-deathmatch/index.md).
