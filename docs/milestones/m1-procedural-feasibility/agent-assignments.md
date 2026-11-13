# M1 — Procedural feasibility — agent assignments

Packages: WP-01, WP-02, WP-03, WP-04. Follow [ownership rules](../../agent-coordination.md) and [package path table](../../backlog.md). The owner requested M1 implementation with multiple agents on 2026-11-12. The first bounded P01 wave is integrated into main: `/root/random` implemented `Generation/Core/Random` and its tests on `codex/m1-random`; `/root/canonical` implemented `Generation/Core/Canonical` and its tests on `codex/m1-canonical`. The integrator owns Identity/Dependencies, assembly/test scaffolds, metadata, documentation and coordination. Both workers used isolated worktrees and no Unity editor; the integrator ran the single main-project editor. Their branches/worktrees were retired after merge and evidence preservation. The P02 wave subsequently assigned `/root/codecs` to Codecs/tests, `/root/recipes` to strict JSON/recipes/CPU collision generator, and `/root/runtime` to the cache reader/store draft, in isolated worktrees. The integrator completed the runtime work after its agent usage interruption, owned manifests/compiler/Editor entry/formats/shared docs, reviewed fixes and ran all final checks. All three branches are merged and retired with evidence preserved. The following P03/P04 wave assigned `/root/geometry`, `/root/weapons` and `/root/materials` disjoint pure folders/tests in isolated worktrees. All three branches are merged and retired after evidence preservation. The integrator owned compiler/registry/schemas, Bootstrap, shader/view, captures and release checks. Weapon/material visual corrections used version1.0.1 and ADR-020. P05 remains unassigned; no C0 contract edits were delegated.

## Assignment cards

### P01 Reproducible seeds — Content owner

Prerequisites: M0.

Deliver: Implement PCG32 with known vectors, named SHA-derived streams, canonical encoding and content keys. Validate boundary uint64 seeds and dependency cycle errors.

Acceptance: Same pinned input has stable topology/hash; cosmetic stream changes do not change gameplay layout.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### P02 Compiler and loader skeleton — Content owner

Prerequisites: P01.

Deliver: Implement DAG compilation, bounded manifest/artifact codecs, atomic temp publication, per-key locking, corruption rejection and reference-counted lease loader. Provide CPU bake path.

Acceptance: Cold/warm and concurrent/cancel tests pass without partial published artifacts or leaked leases.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### P03 Geometry and weapons — Geometry owner

Prerequisites: P02 API.

Deliver: Implement primitives, normals/tangents/UVs, generated LODs, collision proxies and rifle/pistol recipes with semantic sockets.

Acceptance: All required sockets stable within 1 mm/0.1 degrees; bounds/topology/count reports and silhouette review pass.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### P04 Materials — Render owner

Prerequisites: P02 API; parallel P03.

Deliver: Bake seven material families, seamless mips and packed channels. Implement cheap URP variant, compression check and B01 comparison.

Acceptance: No painted sources; normal orientation/roughness mapping correct; Low shader/texture allocation measured.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### P05 Soldier spike — Character owner

Prerequisites: P03 primitives/sockets; early test-only stubs allowed.

Deliver: Generate anatomy/rings/skeleton/weights; implement walk/run/crouch/aim/reload IK and eight-rig timing harness. Freeze semantic joints after review.

Acceptance: Finite 1000-pose sweep, grip/foot-slide limits and visual review pass; stop expansion after two failed iterations and record scope ADR.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

## Parallel execution and ownership

Use the prerequisite column in [implementation](implementation.md) as a dependency graph: steps without a dependency on each other may proceed on separate worktrees, after shared contracts freeze. A shared owner/path requires sequential work or an explicit subfolder split before parallel starts. Integration and shared test/benchmark definition edits stay serialized through integrator. Worker-owned paths are exactly those assigned to its WP in the shared backlog; assignment does not expand those rights.

Every worker reads shared contracts and its feature page, uses approved fixture seeds, and submits one reviewable change at a time. No editor/package upgrades or global scene reserialization. Test-only fakes may unblock independent development; release composition must reject remaining stubs. Integrator checks merge order, updates coordination ownership and runs cross-module smoke tests. When a worker is blocked it reports dependency ID and works only on independent fixtures/tests until resolved.
