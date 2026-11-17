# M10 — Two maps, menus and settings — contracts and integration

**Current disposition — 2026-11-14 review from 84befb9 / a54ed83:** M10 integration validation remains open. Persisted catalog/visual packages, shell/schema-2/settings/audio and T069 editable applied bindings/accessibility are built into `7cd7e81`. Native-navigation publication and named map/UI/control/display/audio/20-cycle player review remain open. See the [audit](../../product-state-audit.md), [remaining queue](../../next-playable-build.md), [playtest guide](../../playtest-guide.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence remain; this review creates no coding lease or waiver.

Original implementation/port requirements retained. Current implementation is partial; product acceptance is open. [Game session contracts](../../game-session-contracts.md) are the single shared definition of new ports, DTOs, lifecycle and freeze/migration procedure. Existing [C0 details](../../contract-details.md), content v1 and N1-v1 remain frozen. The integrator records the actual accepted versions/hashes at entry; the documentation plan itself is not a compiling interface freeze.

## Input and output

Entry: M9 one-map match exit and Game-v1 freeze are accepted on main. Map recipe/compiler and client session seams are real. Catalog/menu/settings model work may start against frozen fakes after T035 if explicitly assigned, but this gate closes only after M9 and real two-map integration.

Output: Required next artifacts: two-map catalog/content manifests, complete menu/settings implementation, real UI captures and device checks, map safety/profile/load-cycle evidence and integrated player. M11 packages exactly these outputs; unresolved required settings, map or UI checks remain blockers.

## Composition and failure boundaries

Bootstrap alone owns session creation and mutation wiring. Simulation is fixed60Hz authority; AI emits normal next-tick commands; views/settings/menu read publications and send validated service requests. Session/participant/life identity prevents stale death/load/respawn callbacks from crossing sessions. Verified collision/navigation precede Ready; missing critical content prevents spawn. Stop/cancel releases nav/cover/content/view/audio handles once, with Unity destruction on main thread.

## Integration checkpoint

Merge only from the accepted base with complete contract/fixture conformance, no forbidden file edits, no required fake adapter in shipping code and no unreviewed schema/generator/packet change. Preserve old diagnostics and explicit workload IDs; new team workloads cannot overwrite B01–B04. Shared compiler/contract/Bootstrap/project changes land alone through integrator before consumers rebase. Run [validation](validation-acceptance.md) and record actual statuses in [handoff](handoff.md); preserve previous binary/content pair for rollback.

[Release roadmap](../../next-playable-build.md) · [Shared documentation](../../index.md) · [Previous pack](../m9-team-deathmatch/index.md) · [Next pack](../m11-playable-release/index.md).
