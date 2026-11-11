# M5 — Networking foundation — validation and acceptance

Status: not run. Use [global validation](../../validation.md) and [build lanes](../../build-ci.md); this file selects the checks required at this stage. Implement actual test/build entry points before reporting execution. Absence of hardware, credentials or dependencies means pending evidence, not PASS.

| Suite | Scenario | Required result |
|---|---|---|
| V11 | Codec fuzz, truncation, oversized input and incompatible join | No unbounded allocation/crash; no invalid entity ownership or forged command accepted. |
| V11 | Two clients at 0/50/100/150 ms RTT and 0/1/3/5% loss | Bounded prediction/interpolation and recovery; report correction distributions with static-map threshold. |
| V12,V06 | Headless/offline regression | Server builds without Presentation; offline motor/combat behavior unchanged and duplicate events suppressed. |

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

Final criterion: N1/N2 complete: chosen adapter pinned, dedicated two-peer sessions, validated command stream, motor prediction and remote interpolation pass impaired-network tests. Lag compensation/four-player acceptance intentionally remains M6 work.

## Evidence location

Store run artifacts under repository `Artifacts/m5-network-foundation/<source-commit>/<run-id>/` (proposed path). Include test XML, JSON reports, build/content manifests, toolchain lock, raw timing/memory logs and visual captures when relevant. [Handoff](handoff.md) links actual artifacts and records reviewer/date. Do not create fake passing reports from this checklist.


## Revision 1.1 implementation refinements

[Full prediction state and tick order](../../simulation-protocol.md) and [explicit DTO/lease rules](../../contract-details.md) are required before N03. Restore both weapon slots and all persistent motor/recoil state. Wire codec remains a pinned adapter decision at N01.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
