# M5 — Networking foundation — agent assignments

Packages: WP-08 plus integrator. Follow [ownership rules](../../agent-coordination.md) and [package path table](../../backlog.md). This pack plans future work; it does not launch agents or authorize concurrent edits to shared files.

## Assignment cards

### N01 Framework spike — Network owner

Prerequisites: M4.

Deliver: Evaluate pinned Unity Transport and NGO adapter against existing ports; test two-player prediction, headless compatibility and codec fuzzing. Record ADR and exact versions.

Acceptance: Framework selected by measured behavior; no assumption anticipation equals rollback/replay.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### N02 Session and commands — Network owner

Prerequisites: N01.

Deliver: Implement bounded codec, manifest handshake, ownership/tick/rate validation and 60Hz commands batched at 20Hz with redundancy. Separate host authority/view consumers.

Acceptance: Mismatch rejected before spawn; duplicate/stale/future input cannot cause unauthorized transitions.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### N03 Local prediction — Network/simulation owners

Prerequisites: N02.

Deliver: Implement 256-tick history, input acknowledgements, authoritative restore and replay with shared motor. Clamp missing inputs; deduplicate provisional shot feedback.

Acceptance: Static-map correction p95 ≤0.1 m at 100 ms RTT/1% loss; no duplicate shots or ammo drift.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### N04 Remote snapshots — Network owner

Prerequisites: N02; parallel N03.

Deliver: Implement 20Hz snapshots, bounded delta baselines, 50–150 ms interpolation buffer and ≤100 ms extrapolation. Rate-limit resync and cap queues.

Acceptance: Reorder/loss causes bounded hold/resync, no runaway allocation or permanent baseline loss.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### N05 Two-peer impairment review — Integrator/test owner

Prerequisites: N03,N04.

Deliver: Run separate server/client processes under seeded delay/jitter/loss/outage matrix; archive correction and transport metrics.

Acceptance: Server has no graphics dependencies; state recovers and offline tests remain green.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

## Parallel execution and ownership

Use the prerequisite column in [implementation](implementation.md) as a dependency graph: steps without a dependency on each other may proceed on separate worktrees, after shared contracts freeze. A shared owner/path requires sequential work or an explicit subfolder split before parallel starts. Integration and shared test/benchmark definition edits stay serialized through integrator. Worker-owned paths are exactly those assigned to its WP in the shared backlog; assignment does not expand those rights.

Every worker reads shared contracts and its feature page, uses approved fixture seeds, and submits one reviewable change at a time. No editor/package upgrades or global scene reserialization. Test-only fakes may unblock independent development; release composition must reject remaining stubs. Integrator checks merge order, updates coordination ownership and runs cross-module smoke tests. When a worker is blocked it reports dependency ID and works only on independent fixtures/tests until resolved.
