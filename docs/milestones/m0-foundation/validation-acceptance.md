# M0 — Foundation — validation and acceptance

Status: M0 closed with Max's explicit hardware-validation waiver, 2026-11-12. Pure/Unity tests, clean client/server builds, player smoke, allocation controls and failure shutdown pass. V10 render/thermal acceptance remains technically unverified; no automated result is upgraded. The owner decision, retained preconditioning failures and exact reports are in [handoff](handoff.md) and [ADR-017](../../decisions.md#adr-017--accepted-m0-owner-waiver-of-remaining-hardware-validation). Use [global validation](../../validation.md) and [build lanes](../../build-ci.md). This M0 exception does not waive future gameplay performance gates.

| Suite | Scenario | Required result |
|---|---|---|
| V01 | Pure assembly build, ID/sequence wrap and serialization boundaries | No Unity dependency; exact round trips for IDs, finite-value rejection and explicit invalid-schema diagnostics. |
| V12 | Clean client/server build and provenance scan | Both build artifacts boot; engine font allowlist explicit; no unexplained binary art. |
| V10 | B01 reference capture | 720p internal resolution and selected iGPU recorded; frame distributions, allocation and memory counters produced. |

## Test procedure

1. Start from clean accepted upstream tag and pinned toolchain; record machine/build/content hashes and seed.
2. Execute pure/adapter tests for changed boundaries, then Unity integration/player/headless checks relevant to stage. Fail immediately on missing required adapter or provenance errors.
3. Run deterministic fixtures and negative cases from the linked feature specifications; capture actual command/configuration and full results rather than a summary without evidence.
4. Run stage performance/soak/visual scenarios with fixed workloads. Hardware captures use the shared warmup/run/repeat protocol and named adapter. Do not count editor results as release acceptance.
5. Review every failed/pending item, link defect/owner, and rerun affected checks after fixes. Preserve previous passing baseline until integrator accepts replacements.

## Gate checklist

- [x] Entry prerequisites verified and implementation steps complete.
- [ ] All required suites pass without exceptions: V10 render/thermal acceptance remains unverified; owner waived it for M0 closure.
- [x] No prohibited visual sources in accepted builds; generated outputs have versioned provenance.
- [x] Contract/schema/generator changes reviewed and downstream fixtures synchronized (single-integrator initial C0-v1 freeze; no independent milestone review implied).
- [x] M0 resource/failure paths bounded and actual budget evidence attached; incomplete render and thermal acceptance remain pending in the required-suites item.
- [x] Shared docs updated with actual code references; milestone links verified.
- [x] Integrator reviews actual evidence and records owner acceptance with explicit waiver in handoff.

Final criterion: Exact toolchain recorded; C0 frozen; three profiles exist; clean client/headless builds and contract tests pass; CI entry points and actual B01 evidence available. No claim of 30 FPS for a combat scene at this stage.

## Evidence location

Wrappers store run artifacts under repository `Artifacts/m0-foundation/<source-commit>/<run-id>/`. Include test XML, JSON reports, build/content manifests, toolchain lock, raw timing/memory logs and visual captures when relevant. [Handoff](handoff.md) links actual artifacts and records reviewer/date. Do not create fake passing reports from this checklist.


## Revision 1.1 implementation refinements

[Bootstrap runbook](../../bootstrap-runbook.md), [C0 details](../../contract-details.md), [fixture catalog](../../fixtures/index.md) and T026/T033/T034 are required F01–F04 inputs. Freeze B01-minimal-v1 only; do not require the M1 material library at M0.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
