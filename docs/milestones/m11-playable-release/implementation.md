# M11 — Playable build and acceptance — implementation order

**Current disposition — 2026-11-14 review from 84befb9 / a54ed83:** M11 acceptance remains open. Clean `7cd7e81` Client/Server reports and verified local ZIP exist; T071 manual gameplay/recovery/rollback handoff remains partial. T067 native-navigation publication/query work, T062 visual disposition and T065 match/device/reference/soak/delivery exits remain open. REL01–REL06 are NotClaimed; P05 FAIL is retained. See the [audit](../../product-state-audit.md), [remaining queue](../../next-playable-build.md), [playtest guide](../../playtest-guide.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence remain; this review creates no coding lease or waiver.

Original implementation/port requirements retained. Current implementation is partial; product acceptance is open. Read [entry](index.md), [Game-v1](../../game-session-contracts.md) and the authoritative feature specs before editing. Task IDs extend T001–T034 without renumbering historical tasks. Start only assigned work; integrate through main in dependency order.

## T048 — Build and rehearse clean delivery

Regenerate both maps plus all dependency content in an empty cache, run locks/provenance/schema/pure/Unity checks, build release IL2CPP client and stripped headless compatibility target. Package client/content/licenses/manifests/defaults/controls; extract into a clean path without editor/cache/network and launch through the normal menu.

## T049 — Resolve and review visible quality

Fix tracked source causes of P05/live rig/weapon/skin defects with controlled recipe/generator revisions; regenerate verified caches and rerun impacted pose/skin tests. Named reviewer accepts both-team silhouettes, no persistent invalid animation, weapon/grip alignment, readable 720p HUD and two-map route/spawn playability. Preserve historical FAIL; record new evidence separately.

## T050 — Validate complete gameplay and performance

Run the full map×roster acceptance matrix, timer/score boundaries, settings/device checks, three-run frozen workload captures for each map and size, and ≥30-minute normal-AI soak per map at 6v6 across replay. Optimize only measured sources within unchanged gameplay/visual policies and record every failed/pending attempt.

## T051 — Publish local release handoff

Integrator links immutable binary/content pair and SHA256 inventory, exact source/toolchain/manifests, gate matrix and controls/known issues. Rehearse previous-pair rollback, archive evidence and commit all accepted work to main; verify task branches/worktrees preserved before cleanup. No public upload/distribution is implied.

## Merge and completion

Each task returns source changes, relevant test results, source/toolchain/content/seed/workload identity and a limitation record. Integrator handles shared files once, reruns affected integration checks, updates this pack's handoff and commits completed changes into main. A future-only path or wrapper in this plan must become real and tested before it counts as a deliverable.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m10-maps-shell/index.md) · [Next pack](../index.md).
