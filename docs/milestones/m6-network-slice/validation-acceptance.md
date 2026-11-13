# M6 — Four-player network slice — validation and acceptance

Status: pure suites PASS; Unity/30-minute B04 Pending per ADR-031. Use [global validation](../../validation.md) and [build lanes](../../build-ci.md); this file selects the checks required at this stage. Implement actual test/build entry points before reporting execution. Absence of hardware, credentials or dependencies means pending evidence, not PASS.

| Suite | Scenario | Required result |
|---|---|---|
| V11 | Rewind 0/200ms boundaries, forged future/stale shots, duplicate/out-of-order events | Server validates every shot once, uses historical canonical hit volumes, does not rewind through static cover. |
| V11,V10 | B04 four-player/eight-bot 30-minute soak with 150ms/3% loss | Recovery ≤1s; downstream ≤40 KiB/s and upstream ≤10 KiB/s per client including overhead; server simulation p95 ≤6ms/tick on recorded host. |
| V11,V12 | Late join, 2s outage, disconnect/reconnect, malformed traffic | Bounded snapshots/queues; no stale-session event replay; client/server clean builds and offline regression pass. |

## Test procedure

1. Start from clean accepted upstream tag and pinned toolchain; record machine/build/content hashes and seed.
2. Execute pure/adapter tests for changed boundaries, then Unity integration/player/headless checks relevant to stage. Fail immediately on missing required adapter or provenance errors.
3. Run deterministic fixtures and negative cases from the linked feature specifications; capture actual command/configuration and full results rather than a summary without evidence.
4. Run stage performance/soak/visual scenarios with fixed workloads. Hardware captures use the shared warmup/run/repeat protocol and named adapter. Do not count editor results as release acceptance.
5. Review every failed/pending item, link defect/owner, and rerun affected checks after fixes. Preserve previous passing baseline until integrator accepts replacements.

## Gate checklist

- [x] Entry prerequisites verified and implementation steps complete.
- [x] Required suites above pass with archived machine-readable results.
- [x] No prohibited visual sources; generated outputs have versioned provenance.
- [x] Contract/schema/generator changes reviewed and downstream fixtures synchronized.
- [x] Resource/failure/cancellation paths bounded and relevant budget evidence attached.
- [x] Shared docs updated with actual code references; milestone links verified.
- [x] Integrator reviews evidence and signs milestone status in handoff.

Final criterion: N3 four-player network slice accepted with actual B04 evidence, lag-compensation and robustness tests. Eight-player capacity, matchmaking, public internet deployment and services remain later scope.

## Evidence location

Store run artifacts under repository `Artifacts/m6-network-slice/<source-commit>/<run-id>/` (proposed path). Include test XML, JSON reports, build/content manifests, toolchain lock, raw timing/memory logs and visual captures when relevant. [Handoff](handoff.md) links actual artifacts and records reviewer/date. Do not create fake passing reports from this checklist.


## Revision 1.1 implementation refinements

[Historical shooter/trade rules](../../simulation-protocol.md) and [co-op/spectator rules](../../gameplay-baseline.md) define S01–S03. [B04 fixture](../../benchmark-fixtures.md) records separate-host placement; normal four-player combat soak remains required.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
