# M1 — Procedural feasibility — validation and acceptance

Status: The continuation has267 integrated pure tests, actual cache-backed Unity skinning and camera-driven LOD checks, full twelve-request cold/warm/forced content compilation, compression measurements and a real B01-materials release diagnostic. P05 soldier visual/clearance gate is FAIL and scope-stopped under ADR-024 after iteration2 arm/torso FAIL and ADR-023 third-candidate FAIL (~2.18mm forearm/strap; eye/sight unresolved); no fourth clearance iteration. Numeric grip/contact/1000-pose/eight-rig results remain spike evidence only. Full resource and three-run reference acceptance below remain open; remaining M1 polish may continue without pretending soldier PASS. See [handoff](handoff.md) for actual report directories and limits. Use [global validation](../../validation.md) and [build lanes](../../build-ci.md); this file selects the checks required at this stage. Implement actual test/build entry points before reporting execution. Absence of hardware, credentials or dependencies means pending evidence, not PASS.

| Suite | Scenario | Required result |
|---|---|---|
| V02,V04 | Cold/warm compile plus corrupt/cancel/concurrent cases | Deterministic keys; same key never different bytes; path traversal and oversized decode rejected. |
| V03 | Primitives, seven material families, rifle/pistol views | Valid bounds/indices/normals/mips; reviewed scale and silhouettes; no prohibited source art. |
| V05 | Pose sweep and eight-rig timing on reference | Weight sums within 1e-4; foot slide ≤5 cm; grip separation ≤2 cm; animation CPU p95 ≤2 ms/frame Low. |

## Test procedure

1. Start from clean accepted upstream tag and pinned toolchain; record machine/build/content hashes and seed.
2. Execute pure/adapter tests for changed boundaries, then Unity integration/player/headless checks relevant to stage. Fail immediately on missing required adapter or provenance errors.
3. Run deterministic fixtures and negative cases from the linked feature specifications; capture actual command/configuration and full results rather than a summary without evidence.
4. Run stage performance/soak/visual scenarios with fixed workloads. Hardware captures use the shared warmup/run/repeat protocol and named adapter. Do not count editor results as release acceptance.
5. Review every failed/pending item, link defect/owner, and rerun affected checks after fixes. Preserve previous passing baseline until integrator accepts replacements.

## Gate checklist

- [ ] Entry prerequisites verified and implementation steps complete.
- [ ] Required suites above pass with archived machine-readable results.
- [ ] No prohibited visual sources; generated outputs have versioned provenance.
- [ ] Contract/schema/generator changes reviewed and downstream fixtures synchronized.
- [ ] Resource/failure/cancellation paths bounded and relevant budget evidence attached.
- [ ] Shared docs updated with actual code references; milestone links verified.
- [ ] Integrator reviews evidence and signs milestone status in handoff.

Final criterion: Compiler/cache v1 and generation tests pass. Rifle, pistol and one helmeted soldier regenerate from numeric source. Soldier visual and performance spike passes or explicit scope revision is approved before expansion — ADR-024 records the visual/clearance scope stop (FAIL, not PASS). Cold/warm slice compile allocations/times are recorded; full-map time gate remains for later content.

## Evidence location

Store run artifacts under repository `Artifacts/m1-procedural-feasibility/<source-commit>/<run-id>/` (proposed path). Include test XML, JSON reports, build/content manifests, toolchain lock, raw timing/memory logs and visual captures when relevant. [Handoff](handoff.md) links actual artifacts and records reviewer/date. Do not create fake passing reports from this checklist.


## Revision 1.1 implementation refinements

[Deterministic format](../../deterministic-format.md), [soldier baseline](../../soldier-baseline.md), [fixture catalog](../../fixtures/index.md) and T028 define P01/P02/P05. M1 reload/aim uses scripted action phases; no dependency on unimplemented M2 combat. B01-materials-v1 is a distinct workload.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
