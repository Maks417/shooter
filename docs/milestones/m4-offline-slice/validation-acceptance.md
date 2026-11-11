# M4 — Offline playable slice — validation and acceptance

Status: not run. Use [global validation](../../validation.md) and [build lanes](../../build-ci.md); this file selects the checks required at this stage. Implement actual test/build entry points before reporting execution. Absence of hardware, credentials or dependencies means pending evidence, not PASS.

| Suite | Scenario | Required result |
|---|---|---|
| V10 | 60 s warmup then 180 s capture, three runs | p95 ≤33.33 ms, p99 ≤50 ms, GPU p95 ≤27 ms, main p95 ≤12 ms; hitch rules from performance specification. |
| V10,V12 | 30-minute soak, cold/warm load and clean install | ≤3 GiB steady/≤4 GiB loading commit; cold/warm ≤30/10 s; no steady managed allocation or leak trend. |
| V09,V13 | Profiles, HUD/accessibility and visual review | Same gameplay hashes; readable UI, reduced-motion options and generated-only plausible visual set. |

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

Final criterion: Playable offline objective slice and clean build pass; actual Ryzen4600H iGPU native720p/30 evidence meets all budgets. Missing hardware evidence blocks gate. No 60 FPS or multiplayer success claim implied.

## Evidence location

Store run artifacts under repository `Artifacts/m4-offline-slice/<source-commit>/<run-id>/` (proposed path). Include test XML, JSON reports, build/content manifests, toolchain lock, raw timing/memory logs and visual captures when relevant. [Handoff](handoff.md) links actual artifacts and records reviewer/date. Do not create fake passing reports from this checklist.


## Revision 1.1 implementation refinements

[Task cards T029–T033](../../task-supplement.md) assign objective, synthesized audio, simple lighting and analytic collapse; no implicit required ragdoll. [Compound Clear rules](../../gameplay-baseline.md) fix O01. Use exact B03 and separate normal-AI soak.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
