# M6 — Four-player network slice — agent assignments

Packages: WP-08, WP-09 with simulation owner. Follow [ownership rules](../../../agent-coordination.md) and [package path table](../../../backlog.md). This pack plans future work; it does not launch agents or authorize concurrent edits to shared files.

## Assignment cards

### S01 Historical hit query — Network/simulation owners

Prerequisites: M5.

Deliver: Store 16 samples of authoritative gameplay capsules for 200ms history; interpolate query shapes without moving live colliders. Validate shot time against approved clock window and static cover.

Acceptance: History boundaries, dead/despawned actors and cover queries have explicit tested outcomes.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### S02 Authoritative fire ordering — Simulation/network owners

Prerequisites: S01.

Deliver: Validate ammo/cadence/alive state, shot index and command duplication; define late shot/death trade policy and authoritative event order.

Acceptance: Replayed, forged or late input never creates extra damage/ammo; ordering fixtures freeze trade behavior.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### S03 Session recovery — Network owner

Prerequisites: M5 snapshot codec.

Deliver: Implement bounded full-state late join then acknowledged deltas, readiness barrier, resync cooldown, disconnect cleanup and fresh reconnect session IDs.

Acceptance: Clients cannot act before Ready; outage/reconnect cannot replay prior-session events or leak reservations.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### S04 Four-peer B04 — Performance/test owner

Prerequisites: S02,S03.

Deliver: Run four clients plus eight bots and separate headless server; capture transport overhead, correction, snapshot age, server CPU and memory for 30 minutes.

Acceptance: 150ms RTT/3% loss playable gate passes; no state divergence >1s after recovery; bandwidth/server budgets met.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### S05 Exposure review — Integrator/network owner

Prerequisites: S04.

Deliver: Check bounded peers/queues/rates, malformed packet resilience and private-session token configuration. Document N4 public-auth/encryption/deployment gate.

Acceptance: LAN/private scope clear; no unsupported public security claim or client-side authority.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

## Parallel execution and ownership

Use the prerequisite column in [implementation](implementation.md) as a dependency graph: steps without a dependency on each other may proceed on separate worktrees, after shared contracts freeze. A shared owner/path requires sequential work or an explicit subfolder split before parallel starts. Integration and shared test/benchmark definition edits stay serialized through integrator. Worker-owned paths are exactly those assigned to its WP in the shared backlog; assignment does not expand those rights.

Every worker reads shared contracts and its feature page, uses approved fixture seeds, and submits one reviewable change at a time. No editor/package upgrades or global scene reserialization. Test-only fakes may unblock independent development; release composition must reject remaining stubs. Integrator checks merge order, updates coordination ownership and runs cross-module smoke tests. When a worker is blocked it reports dependency ID and works only on independent fixtures/tests until resolved.
