# M11 — Playable build and acceptance — contracts and integration

Status: planned. [Game session contracts](../../game-session-contracts.md) are the single shared definition of new ports, DTOs, lifecycle and freeze/migration procedure. Existing [C0 details](../../contract-details.md), content v1 and N1-v1 remain frozen. The integrator records the actual accepted versions/hashes at entry; the documentation plan itself is not a compiling interface freeze.

## Input and output

Entry: M10 integrated two-map game is on main; exact content/contract/workload versions frozen, known defects triaged, locked editor/modules available for player build. A named owner/reference-machine session is scheduled for actual evidence; independent pure/release tooling can proceed if hardware is unavailable.

Output: Required delivery: usable local archive and extracted executable path; SHA256 inventory; source commit; versioned content/catalog/contract/toolchain manifests; controls/settings instructions; test/performance/visual review links; actual PASS/FAIL/Pending table; rollback pair. No required player, visual or reference check can be replaced with documentation hashes or an inherited waiver.

## Composition and failure boundaries

Bootstrap alone owns session creation and mutation wiring. Simulation is fixed60Hz authority; AI emits normal next-tick commands; views/settings/menu read publications and send validated service requests. Session/participant/life identity prevents stale death/load/respawn callbacks from crossing sessions. Verified collision/navigation precede Ready; missing critical content prevents spawn. Stop/cancel releases nav/cover/content/view/audio handles once, with Unity destruction on main thread.

## Integration checkpoint

Merge only from the accepted base with complete contract/fixture conformance, no forbidden file edits, no required fake adapter in shipping code and no unreviewed schema/generator/packet change. Preserve old diagnostics and explicit workload IDs; new team workloads cannot overwrite B01–B04. Shared compiler/contract/Bootstrap/project changes land alone through integrator before consumers rebase. Run [validation](validation-acceptance.md) and record actual statuses in [handoff](handoff.md); preserve previous binary/content pair for rollback.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m10-maps-shell/index.md) · [Next pack](../index.md).
