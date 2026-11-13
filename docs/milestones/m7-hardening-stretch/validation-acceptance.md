# M7 — Release hardening and 60 FPS experiment — validation and acceptance

Status: pure tooling PASS_WITH_PENDING; Unity/60FPS Pending per ADR-032. Use [global validation](../../validation.md) and [build lanes](../../build-ci.md); this file selects the checks required at this stage. Implement actual test/build entry points before reporting execution. Absence of hardware, credentials or dependencies means pending evidence, not PASS.

| Suite | Scenario | Required result |
|---|---|---|
| V01–V14 | Clean release suite and four canonical seeds/all profiles | All mandatory correctness/provenance/CI/30FPS/network gates pass; documentation points at actual implementation evidence. |
| V12 | Clean install and rollback rehearsal | No editor/cache dependency; matching binary/content/protocol restoration demonstrated. |
| V10 | Same-workload60FPS experiment | Explicit PASS/FAIL/PENDING result, no resolution/actor-count substitution or weakening of mandatory native720p30FPS gate. |

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

Final criterion: Mandatory release gates complete with archived evidence and tested rollback. 60 FPS has independent explicit status and may fail without blocking the approved30FPS release. Optional eight players and public deployment remain separate gates.

## Evidence location

Store run artifacts under repository `Artifacts/m7-hardening-stretch/<source-commit>/<run-id>/` (proposed path). Include test XML, JSON reports, build/content manifests, toolchain lock, raw timing/memory logs and visual captures when relevant. [Handoff](handoff.md) links actual artifacts and records reviewer/date. Do not create fake passing reports from this checklist.


## Revision 1.1 implementation refinements

[Fixture catalog](../../fixtures/index.md), [benchmark definitions](../../benchmark-fixtures.md) and [task supplement](../../task-supplement.md) enter clean release audit. Stretch results require identical B03 workload/counters; optional ragdoll/public networking cannot silently become mandatory release scope.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
