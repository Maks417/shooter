# M11 — Playable build and acceptance — contracts and integration

**Current disposition — 2026-11-14 review from 84befb9 / a54ed83:** M11 acceptance remains open. Clean `7cd7e81` Client/Server reports and verified local ZIP exist; T071 manual gameplay/recovery/rollback handoff remains partial. T067 native-navigation publication/query work, T062 visual disposition and T065 match/device/reference/soak/delivery exits remain open. REL01–REL06 are NotClaimed; P05 FAIL is retained. See the [audit](../../product-state-audit.md), [remaining queue](../../next-playable-build.md), [playtest guide](../../playtest-guide.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence remain; this review creates no coding lease or waiver.

Original implementation/port requirements retained. Current implementation is partial; product acceptance is open. [Game session contracts](../../game-session-contracts.md) are the single shared definition of new ports, DTOs, lifecycle and freeze/migration procedure. Existing [C0 details](../../contract-details.md), content v1 and N1-v1 remain frozen. The integrator records the actual accepted versions/hashes at entry; the documentation plan itself is not a compiling interface freeze.

## Input and output

Entry: M10 integrated two-map game is on main; exact content/contract/workload versions frozen, known defects triaged, locked editor/modules available for player build. A named owner/reference-machine session is scheduled for actual evidence; independent pure/release tooling can proceed if hardware is unavailable.

Output: Required delivery: usable local archive and extracted executable path; SHA256 inventory; source commit; versioned content/catalog/contract/toolchain manifests; controls/settings instructions; test/performance/visual review links; actual PASS/FAIL/Pending table; rollback pair. No required player, visual or reference check can be replaced with documentation hashes or an inherited waiver.

## Composition and failure boundaries

Bootstrap alone owns session creation and mutation wiring. Simulation is fixed60Hz authority; AI emits normal next-tick commands; views/settings/menu read publications and send validated service requests. Session/participant/life identity prevents stale death/load/respawn callbacks from crossing sessions. Verified collision/navigation precede Ready; missing critical content prevents spawn. Stop/cancel releases nav/cover/content/view/audio handles once, with Unity destruction on main thread.

## Integration checkpoint

Merge only from the accepted base with complete contract/fixture conformance, no forbidden file edits, no required fake adapter in shipping code and no unreviewed schema/generator/packet change. Preserve old diagnostics and explicit workload IDs; new team workloads cannot overwrite B01–B04. Shared compiler/contract/Bootstrap/project changes land alone through integrator before consumers rebase. Run [validation](validation-acceptance.md) and record actual statuses in [handoff](handoff.md); preserve previous binary/content pair for rollback.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m10-maps-shell/index.md) · [Next pack](../index.md).

## Planned final Windows installer (T073)

Owner requirement, 2026-11-14: the final game is delivered by a Windows installer. Running a newer installer must update the existing installed game, preserve settings/controls and stable app identity/path/shortcuts, and maintain a matching executable/content pair. ZIPs remain preview/internal artifacts. [Installer contract and INS01–INS06](../../features/windows-installer.md) extend REL01/REL06 and FIX13/FIX14. T073 is planned documentation scope only here; a future bounded release-integrator assignment implements installer/version/upgrade/repair/uninstall/rollback tooling. No installer, online service or acceptance waiver is delivered by this update.
