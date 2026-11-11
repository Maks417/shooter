# M7 — Release hardening and 60 FPS experiment — implementation sequence

Read [pack entry](index.md) first. Scope: Prove clean regeneration/install/release, broaden seeds and profile checks, complete regression evidence and evaluate 60 FPS separately from mandatory 30 FPS acceptance. Use [repo paths](../../repo-layout.md), [architecture](../../architecture.md) and [original task dependencies](../../backlog.md). All paths remain proposed until implemented. Do not add unrelated future features while a required check is failing.

| Step | Owner | Prerequisite | Completion check |
|---|---|---|---|
| H01 Clean reproducibility | Build/content owners | M6 | No local hand-edited asset dependency; clean install loads and server strips graphics payloads. |
| H02 Regression matrix | Test/integrator | H01 | All required gates pass with no disabled core test or unexplained changed hash/image. |
| H03 Release rehearsal | Integrator | H02 | Candidate launches outside editor, objective completes, peers join correctly and rollback restores a valid pair. |
| H04 60 FPS experiment | Performance owner | H02; no blocking mandatory gate | Stretch passes only at p95 ≤16.67ms/p99 ≤25ms, GPU p95 ≤14ms/main ≤8ms. Failure recorded does not invalidate passed30FPS release. |
| H05 Deferred scope ledger | Integrator | H03,H04 | No unmeasured 60FPS, large-battle or public-deployment claim appears in release status. |

## H01 Clean reproducibility

Owner: Build/content owners. Dependencies: M6.

Run isolated clean-checkout build without Library/content caches; regenerate canonical recipes and validate provenance/hashes. Build client/server from matching manifest.

Completion check: No local hand-edited asset dependency; clean install loads and server strips graphics payloads.

## H02 Regression matrix

Owner: Test/integrator. Dependencies: H01.

Run full validation suites, seed corpus, profile fairness, restart/load/quality switch, UI accessibility and network impairment tests. Review golden changes explicitly.

Completion check: All required gates pass with no disabled core test or unexplained changed hash/image.

## H03 Release rehearsal

Owner: Integrator. Dependencies: H02.

Package licenses/manifests/content and private session instructions; test rollback to prior matching binary/content/protocol. Archive evidence and known limitations.

Completion check: Candidate launches outside editor, objective completes, peers join correctly and rollback restores a valid pair.

## H04 60 FPS experiment

Owner: Performance owner. Dependencies: H02; no blocking mandatory gate.

Use same native720p B03 workload, profile variants clearly labeled and three reference-machine captures. Optimize only with measured fixes and full regression reruns.

Completion check: Stretch passes only at p95 ≤16.67ms/p99 ≤25ms, GPU p95 ≤14ms/main ≤8ms. Failure recorded does not invalidate passed30FPS release.

## H05 Deferred scope ledger

Owner: Integrator. Dependencies: H03,H04.

Record eight-player capacity, projectiles/penetration and public networking as separate ADR/workloads if pursued; summarize passed/pending/failed evidence.

Completion check: No unmeasured 60FPS, large-battle or public-deployment claim appears in release status.

## Integration checkpoints

Commit small compilable changes. Validate contracts before feature adapters and run each step's checks before its dependents merge. Test fakes remain in test assemblies; fail shipping composition if a required real adapter is missing. Shared settings/Bootstrap/lock changes merge through integrator. Attach reports to [handoff](handoff.md). Any blocked prerequisite is explicitly pending; independent test/fixture work can continue without pretending integration passed.

## Failure and rollback

Keep previous passing source/content pair, capture failing seed and diagnostic stage, revert the scoped implementation change through review, and rerun affected checks. Corrupt generated content is rebuilt from approved source. Do not hand-edit caches or automatically accept new golden values. Record scope/contract changes in [ADRs](../../decisions.md) and retest impacted upstream invariants.


## Revision 1.1 implementation refinements

[Fixture catalog](../../fixtures/index.md), [benchmark definitions](../../benchmark-fixtures.md) and [task supplement](../../task-supplement.md) enter clean release audit. Stretch results require identical B03 workload/counters; optional ragdoll/public networking cannot silently become mandatory release scope.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
