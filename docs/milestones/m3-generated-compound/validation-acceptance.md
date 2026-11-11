# M3 — Generated compound and AI — validation and acceptance

Status: not run. Use [global validation](../../validation.md) and [build lanes](../../build-ci.md); this file selects the checks required at this stage. Implement actual test/build entry points before reporting execution. Absence of hardware, credentials or dependencies means pending evidence, not PASS.

| Suite | Scenario | Required result |
|---|---|---|
| V07 | Seeds 460072030,1,42,18446744073709551615 | Connected objective, border error ≤1e-4 m, opening/collision error ≤1 cm, safe spawns. |
| V07,V08 | 100 traversals and eight-bot 30-minute soak | No leaks, unbounded queues or orphan cover claims; occluded target not acquired by vision. |
| V09,V10 | Cross-profile hashes and B02/B03 capture | Same collision/nav/hit semantics; timings/residency reported against budgets, full 30 FPS release gate evaluated at M4. |

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

Final criterion: Generated compound is traversable, streamed visuals stable, eight soldiers fight under bounded AI scheduling, and repeatable B02/B03 plus soak evidence exists. Required correctness and memory limits pass; unresolved performance issues are explicitly carried as M4 blockers.

## Evidence location

Store run artifacts under repository `Artifacts/m3-generated-compound/<source-commit>/<run-id>/` (proposed path). Include test XML, JSON reports, build/content manifests, toolchain lock, raw timing/memory logs and visual captures when relevant. [Handoff](handoff.md) links actual artifacts and records reviewer/date. Do not create fake passing reports from this checklist.


## Revision 1.1 implementation refinements

[Objective/layer rules](../../gameplay-baseline.md) and [exact benchmark fixtures](../../benchmark-fixtures.md) feed W01–W05. T029 rules are already fixed; T033 freezes B02/B03 manifests and validates route/shot counts. Run normal-AI soak separately from controlled B03 firing.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
