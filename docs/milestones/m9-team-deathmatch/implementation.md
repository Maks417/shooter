# M9 — Team Deathmatch and bot teams — implementation order

**Current disposition — 2026-11-14 review from 84befb9 / a54ed83:** M9 integration validation remains open. Lifecycle, mixed-team AI/spawn policy and T070 finish/results/replay/counter software are in the retained `7cd7e81` candidate. Real-map combat/respawn/full-match evidence and reference captures/soaks remain Pending/PendingHardware; no online server is needed for this local-bot release. See the [audit](../../product-state-audit.md), [remaining queue](../../next-playable-build.md), [playtest guide](../../playtest-guide.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence remain; this review creates no coding lease or waiver.

Original implementation/port requirements retained. Current implementation is partial; product acceptance is open. Read [entry](index.md), [Game-v1](../../game-session-contracts.md) and the authoritative feature specs before editing. Task IDs extend T001–T034 without renumbering historical tasks. Start only assigned work; integrate through main in dependency order.

## T040 — Implement team match authority

Implement score/time outcome, participant↔life identity, death dedupe, full-tick finish ordering, live stats and immutable results exactly as team-deathmatch.md. Keep C0 enums and Compound Clear semantics untouched.

## T041 — Implement safe life cycles

Use the M8 lifecycle port for 180-tick respawn, team spawn safety/ranking/deferral, baseline health/ammo reset, monotonic entity IDs, dead-player camera/countdown and cancellation on finish. Enforce no after-finish state mutation.

## T042 — Extend and integrate team AI

Replace single-player-target/eight-bot assumptions behind the frozen planner seam; support 9/11 bots with both teams. Consume authoritative health/ammo and real path corners, persistent command sequences and reset/cancel on each life. Enforce global query budgets and sensing fairness; keep commands as the only gameplay action channel.

## T043 — Prove the whole match

Integrator binds match/life/bot services to runtime, adds a minimal data-driven score/timer/results diagnostic view for acceptance, runs 5v5 and 6v6 and all match boundary/capacity tests. Final public menus/tables are T046. Measure new workload without claiming old B03 equality.

## Merge and completion

Each task returns source changes, relevant test results, source/toolchain/content/seed/workload identity and a limitation record. Integrator handles shared files once, reruns affected integration checks, updates this pack's handoff and commits completed changes into main. A future-only path or wrapper in this plan must become real and tested before it counts as a deliverable.

[Release roadmap](../../next-playable-build.md) · [Shared documentation](../../index.md) · [Previous pack](../m8-runtime-composition/index.md) · [Next pack](../m10-maps-shell/index.md).
