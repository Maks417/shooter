# M3 — Generated compound and AI — validation and acceptance

Status: **CLOSED WITH USER WAIVER**, 2026-11-13. Pure W01–W05 suites PASS with archived evidence; Unity EditMode/PlayMode/smoke and reference-player B02/B03 remain Pending/PendingHardware and are Unverified/Waived for M3 under [ADR-027](../../decisions.md#adr-027--accepted-m3-owner-waiver-of-remaining-unity-tooling-and-hardwarereference-validation-and-milestone-close). See [handoff](handoff.md). Use [global validation](../../validation.md) and [build lanes](../../build-ci.md). Absence of hardware/tools means Pending, not PASS — waived for M3 closure only where ADR-027 applies.

| Suite | Scenario | Required result | Disposition |
|---|---|---|---|
| V07 | Seeds 460072030,1,42,18446744073709551615 | Connected objective, border error ≤1e-4 m, opening/collision error ≤1 cm, safe spawns. | **PASS** (pure `EnvironmentTests` / `CompoundValidation`) |
| V07,V08 | 100 traversals and eight-bot 30-minute soak | No leaks, unbounded queues or orphan cover claims; occluded target not acquired by vision. | **PASS** (pure streaming 100-traversal + 108000-tick AI soak + occlusion test) |
| V09,V10 | Cross-profile hashes and B02/B03 capture | Same collision/nav/hit semantics; timings/residency reported against budgets, full 30 FPS release gate evaluated at M4. | Hashes **PASS**; release timing **PendingHardware** (M4) |

## Gate checklist

- [x] Entry prerequisites verified and implementation steps complete.
- [x] Required pure suites above pass with archived machine-readable results.
- [x] No prohibited visual sources; generated outputs have versioned provenance.
- [x] Contract/schema unchanged (C0-v1); B02/B03 freeze outside C0 fixture inventory.
- [x] Resource/failure/cancellation paths bounded in pure streaming/nav/AI tests.
- [x] Shared docs updated with actual code references; milestone links verified.
- [x] Integrator reviews evidence and signs milestone status in handoff (owner waiver ADR-027).

Final criterion: Generated compound is traversable (pure), streamed visuals residency bounded (pure), eight soldiers fight under bounded AI scheduling (pure), and repeatable B02/B03 freeze exists. Required correctness checks pass; Unity player and reference performance remain Pending and are carried/waived per ADR-027; native-720p gate stays M4.

## Evidence location

`Artifacts/m3-generated-compound/<source-commit>/m3-software-*/` and `Artifacts/m3-generated-compound/owner-closure-20261113/`. [Handoff](handoff.md) links artifacts. Do not create fake passing Unity/hardware reports from this checklist.
