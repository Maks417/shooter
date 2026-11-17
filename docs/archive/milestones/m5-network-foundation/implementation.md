# M5 — Networking foundation — implementation sequence

Read [pack entry](../../../milestones/m5-network-foundation/index.md) first. Scope: Choose verified networking adapter, establish dedicated authority, handshake/codec, two-player command delivery and client prediction/reconciliation with remote interpolation. Use [repo paths](../../../repo-layout.md), [architecture](../../../architecture.md) and [original task dependencies](../../../backlog.md). All paths remain proposed until implemented. Do not add unrelated future features while a required check is failing.

| Step | Owner | Prerequisite | Completion check |
|---|---|---|---|
| N01 Framework spike | Network owner | M4 | Framework selected by measured behavior; no assumption anticipation equals rollback/replay. |
| N02 Session and commands | Network owner | N01 | Mismatch rejected before spawn; duplicate/stale/future input cannot cause unauthorized transitions. |
| N03 Local prediction | Network/simulation owners | N02 | Static-map correction p95 ≤0.1 m at 100 ms RTT/1% loss; no duplicate shots or ammo drift. |
| N04 Remote snapshots | Network owner | N02; parallel N03 | Reorder/loss causes bounded hold/resync, no runaway allocation or permanent baseline loss. |
| N05 Two-peer impairment review | Integrator/test owner | N03,N04 | Server has no graphics dependencies; state recovers and offline tests remain green. |

## N01 Framework spike

Owner: Network owner. Dependencies: M4.

Evaluate pinned Unity Transport and NGO adapter against existing ports; test two-player prediction, headless compatibility and codec fuzzing. Record ADR and exact versions.

Completion check: Framework selected by measured behavior; no assumption anticipation equals rollback/replay.

## N02 Session and commands

Owner: Network owner. Dependencies: N01.

Implement bounded codec, manifest handshake, ownership/tick/rate validation and 60Hz commands batched at 20Hz with redundancy. Separate host authority/view consumers.

Completion check: Mismatch rejected before spawn; duplicate/stale/future input cannot cause unauthorized transitions.

## N03 Local prediction

Owner: Network/simulation owners. Dependencies: N02.

Implement 256-tick history, input acknowledgements, authoritative restore and replay with shared motor. Clamp missing inputs; deduplicate provisional shot feedback.

Completion check: Static-map correction p95 ≤0.1 m at 100 ms RTT/1% loss; no duplicate shots or ammo drift.

## N04 Remote snapshots

Owner: Network owner. Dependencies: N02; parallel N03.

Implement 20Hz snapshots, bounded delta baselines, 50–150 ms interpolation buffer and ≤100 ms extrapolation. Rate-limit resync and cap queues.

Completion check: Reorder/loss causes bounded hold/resync, no runaway allocation or permanent baseline loss.

## N05 Two-peer impairment review

Owner: Integrator/test owner. Dependencies: N03,N04.

Run separate server/client processes under seeded delay/jitter/loss/outage matrix; archive correction and transport metrics.

Completion check: Server has no graphics dependencies; state recovers and offline tests remain green.

## Integration checkpoints

Commit small compilable changes. Validate contracts before feature adapters and run each step's checks before its dependents merge. Test fakes remain in test assemblies; fail shipping composition if a required real adapter is missing. Shared settings/Bootstrap/lock changes merge through integrator. Attach reports to [handoff](../../../milestones/m5-network-foundation/handoff.md). Any blocked prerequisite is explicitly pending; independent test/fixture work can continue without pretending integration passed.

## Failure and rollback

Keep previous passing source/content pair, capture failing seed and diagnostic stage, revert the scoped implementation change through review, and rerun affected checks. Corrupt generated content is rebuilt from approved source. Do not hand-edit caches or automatically accept new golden values. Record scope/contract changes in [ADRs](../../../decisions.md) and retest impacted upstream invariants.


## Revision 1.1 implementation refinements

[Full prediction state and tick order](../../../simulation-protocol.md) and [explicit DTO/lease rules](../../../contract-details.md) are required before N03. Restore both weapon slots and all persistent motor/recoil state. Wire codec remains a pinned adapter decision at N01.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
