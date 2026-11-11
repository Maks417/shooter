# M7 — Release hardening and 60 FPS experiment — agent assignments

Packages: WP-09 integrator with all module owners. Follow [ownership rules](../../agent-coordination.md) and [package path table](../../backlog.md). This pack plans future work; it does not launch agents or authorize concurrent edits to shared files.

## Assignment cards

### H01 Clean reproducibility — Build/content owners

Prerequisites: M6.

Deliver: Run isolated clean-checkout build without Library/content caches; regenerate canonical recipes and validate provenance/hashes. Build client/server from matching manifest.

Acceptance: No local hand-edited asset dependency; clean install loads and server strips graphics payloads.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### H02 Regression matrix — Test/integrator

Prerequisites: H01.

Deliver: Run full validation suites, seed corpus, profile fairness, restart/load/quality switch, UI accessibility and network impairment tests. Review golden changes explicitly.

Acceptance: All required gates pass with no disabled core test or unexplained changed hash/image.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### H03 Release rehearsal — Integrator

Prerequisites: H02.

Deliver: Package licenses/manifests/content and private session instructions; test rollback to prior matching binary/content/protocol. Archive evidence and known limitations.

Acceptance: Candidate launches outside editor, objective completes, peers join correctly and rollback restores a valid pair.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### H04 60 FPS experiment — Performance owner

Prerequisites: H02; no blocking mandatory gate.

Deliver: Use same native720p B03 workload, profile variants clearly labeled and three reference-machine captures. Optimize only with measured fixes and full regression reruns.

Acceptance: Stretch passes only at p95 ≤16.67ms/p99 ≤25ms, GPU p95 ≤14ms/main ≤8ms. Failure recorded does not invalidate passed30FPS release.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### H05 Deferred scope ledger — Integrator

Prerequisites: H03,H04.

Deliver: Record eight-player capacity, projectiles/penetration and public networking as separate ADR/workloads if pursued; summarize passed/pending/failed evidence.

Acceptance: No unmeasured 60FPS, large-battle or public-deployment claim appears in release status.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

## Parallel execution and ownership

Use the prerequisite column in [implementation](implementation.md) as a dependency graph: steps without a dependency on each other may proceed on separate worktrees, after shared contracts freeze. A shared owner/path requires sequential work or an explicit subfolder split before parallel starts. Integration and shared test/benchmark definition edits stay serialized through integrator. Worker-owned paths are exactly those assigned to its WP in the shared backlog; assignment does not expand those rights.

Every worker reads shared contracts and its feature page, uses approved fixture seeds, and submits one reviewable change at a time. No editor/package upgrades or global scene reserialization. Test-only fakes may unblock independent development; release composition must reject remaining stubs. Integrator checks merge order, updates coordination ownership and runs cross-module smoke tests. When a worker is blocked it reports dependency ID and works only on independent fixtures/tests until resolved.
