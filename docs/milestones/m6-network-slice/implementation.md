# M6 — Four-player network slice — implementation sequence

Read [pack entry](index.md) first. Scope: Complete lag-compensated authoritative fire, robust join/resync/disconnect and four-player/eight-bot soak with measured server/bandwidth limits. Use [repo paths](../../repo-layout.md), [architecture](../../architecture.md) and [original task dependencies](../../backlog.md). All paths remain proposed until implemented. Do not add unrelated future features while a required check is failing.

| Step | Owner | Prerequisite | Completion check |
|---|---|---|---|
| S01 Historical hit query | Network/simulation owners | M5 | History boundaries, dead/despawned actors and cover queries have explicit tested outcomes. |
| S02 Authoritative fire ordering | Simulation/network owners | S01 | Replayed, forged or late input never creates extra damage/ammo; ordering fixtures freeze trade behavior. |
| S03 Session recovery | Network owner | M5 snapshot codec | Clients cannot act before Ready; outage/reconnect cannot replay prior-session events or leak reservations. |
| S04 Four-peer B04 | Performance/test owner | S02,S03 | 150ms RTT/3% loss playable gate passes; no state divergence >1s after recovery; bandwidth/server budgets met. |
| S05 Exposure review | Integrator/network owner | S04 | LAN/private scope clear; no unsupported public security claim or client-side authority. |

## S01 Historical hit query

Owner: Network/simulation owners. Dependencies: M5.

Store 16 samples of authoritative gameplay capsules for 200ms history; interpolate query shapes without moving live colliders. Validate shot time against approved clock window and static cover.

Completion check: History boundaries, dead/despawned actors and cover queries have explicit tested outcomes.

## S02 Authoritative fire ordering

Owner: Simulation/network owners. Dependencies: S01.

Validate ammo/cadence/alive state, shot index and command duplication; define late shot/death trade policy and authoritative event order.

Completion check: Replayed, forged or late input never creates extra damage/ammo; ordering fixtures freeze trade behavior.

## S03 Session recovery

Owner: Network owner. Dependencies: M5 snapshot codec.

Implement bounded full-state late join then acknowledged deltas, readiness barrier, resync cooldown, disconnect cleanup and fresh reconnect session IDs.

Completion check: Clients cannot act before Ready; outage/reconnect cannot replay prior-session events or leak reservations.

## S04 Four-peer B04

Owner: Performance/test owner. Dependencies: S02,S03.

Run four clients plus eight bots and separate headless server; capture transport overhead, correction, snapshot age, server CPU and memory for 30 minutes.

Completion check: 150ms RTT/3% loss playable gate passes; no state divergence >1s after recovery; bandwidth/server budgets met.

## S05 Exposure review

Owner: Integrator/network owner. Dependencies: S04.

Check bounded peers/queues/rates, malformed packet resilience and private-session token configuration. Document N4 public-auth/encryption/deployment gate.

Completion check: LAN/private scope clear; no unsupported public security claim or client-side authority.

## Integration checkpoints

Commit small compilable changes. Validate contracts before feature adapters and run each step's checks before its dependents merge. Test fakes remain in test assemblies; fail shipping composition if a required real adapter is missing. Shared settings/Bootstrap/lock changes merge through integrator. Attach reports to [handoff](handoff.md). Any blocked prerequisite is explicitly pending; independent test/fixture work can continue without pretending integration passed.

## Failure and rollback

Keep previous passing source/content pair, capture failing seed and diagnostic stage, revert the scoped implementation change through review, and rerun affected checks. Corrupt generated content is rebuilt from approved source. Do not hand-edit caches or automatically accept new golden values. Record scope/contract changes in [ADRs](../../decisions.md) and retest impacted upstream invariants.


## Revision 1.1 implementation refinements

[Historical shooter/trade rules](../../simulation-protocol.md) and [co-op/spectator rules](../../gameplay-baseline.md) define S01–S03. [B04 fixture](../../benchmark-fixtures.md) records separate-host placement; normal four-player combat soak remains required.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
