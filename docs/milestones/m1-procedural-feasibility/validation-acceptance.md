# M1 — Procedural feasibility — validation and acceptance

Status: **CLOSED WITH USER WAIVER**, 2026-11-13. Formal P03 silhouette/ADS is PASS-with-limits; LOD transition formal acceptance is PASS (numeric); P04 B01 hardware remains PendingHardware and is Unverified/Waived for M1 under ADR-025; P05 soldier visual/clearance remains FAIL / not accepted under ADR-024. Automated PendingHardware/FAIL reports are unchanged. See [handoff](handoff.md) for evidence paths and [ADR-025](../../decisions.md#adr-025--accepted-m1-owner-waiver-of-remaining-hardwareresource-validation-and-milestone-close). Use [global validation](../../validation.md) and [build lanes](../../build-ci.md); this file selects the checks required at this stage. Absence of hardware, credentials or dependencies means pending evidence, not PASS — waived for M1 closure only where ADR-025 applies.

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

- [x] Entry prerequisites verified and implementation steps complete.
- [x] Required suites above pass with archived machine-readable results (P05 visual FAIL retained; numeric spike evidence only).
- [x] No prohibited visual sources; generated outputs have versioned provenance.
- [x] Contract/schema/generator changes reviewed and downstream fixtures synchronized.
- [x] Resource/failure/cancellation paths bounded; B01 hardware counters remain PendingHardware and waived for M1 under ADR-025.
- [x] Shared docs updated with actual code references; milestone links verified.
- [x] Integrator reviews evidence and signs milestone status in handoff (owner-directed close; ADR-025).

Final criterion: Compiler/cache v1 and generation tests pass. Rifle, pistol and one helmeted soldier regenerate from numeric source. Soldier visual spike is FAIL / not accepted under ADR-024 (scope stop; not PASS). Cold/warm slice compile allocations/times are recorded; full-map time gate remains for later content. M1 closed with user waiver under ADR-025.

## Evidence location

Store run artifacts under repository `Artifacts/m1-procedural-feasibility/<source-commit>/<run-id>/` (proposed path). Include test XML, JSON reports, build/content manifests, toolchain lock, raw timing/memory logs and visual captures when relevant. Owner-closure package: `Artifacts/m1-procedural-feasibility/owner-closure-20261113/`. [Handoff](handoff.md) links actual artifacts and records reviewer/date. Do not create fake passing reports from this checklist.


## Revision 1.1 implementation refinements

[Deterministic format](../../deterministic-format.md), [soldier baseline](../../soldier-baseline.md), [fixture catalog](../../fixtures/index.md) and T028 define P01/P02/P05. M1 reload/aim uses scripted action phases; no dependency on unimplemented M2 combat. B01-materials-v1 is a distinct workload.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
