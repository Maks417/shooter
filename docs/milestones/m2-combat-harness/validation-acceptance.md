# M2 — Combat harness — validation and acceptance

Status: **CLOSED WITH USER WAIVER**, 2026-11-13. Pure C01–C04 / simulation suites PASS (23/23 including cover hitscan); Unity EditMode 13/13, PlayMode 11/11 with dedicated V05/V06/V09 scenario harnesses PASS on `474427d` evidence tree (2026-11-13); prior client/server smoke PASS on `59bd8c6`. Reference-player budget remains PendingHardware and is Unverified/Waived for M2 under [ADR-026](../../decisions.md#adr-026--accepted-m2-owner-waiver-of-remaining-hardwarereference-validation-and-milestone-close). Automated PendingHardware reports are unchanged. See [handoff](handoff.md). Use [global validation](../../validation.md) and [build lanes](../../build-ci.md). Absence of hardware means PendingHardware, not PASS — waived for M2 closure only where ADR-026 applies.

| Suite | Scenario | Required result |
|---|---|---|
| V06 | Cadence/reload/death/duplicate input scripts | Ammo never negative; reload transfers only at completion; one damage/death event per accepted transition. |
| V06,V09 | Cover, step, ceiling and cross-profile scripts | No wall firing, capsule gaps or quality-based hit changes; render-FPS-independent rules. |
| V05 | Weapon handling plus hit-volume overlays | Head/torso centers ≤5 cm and limb centers ≤10 cm from defined sample poses; hands within grip tolerance. |

## Test procedure

1. Start from clean accepted upstream tag and pinned toolchain; record machine/build/content hashes and seed.
2. Execute pure/adapter tests for changed boundaries, then Unity integration/player/headless checks relevant to stage. Fail immediately on missing required adapter or provenance errors.
3. Run deterministic fixtures and negative cases from the linked feature specifications; capture actual command/configuration and full results rather than a summary without evidence.
4. Run stage performance/soak/visual scenarios with fixed workloads. Hardware captures use the shared warmup/run/repeat protocol and named adapter. Do not count editor results as release acceptance.
5. Review every failed/pending item, link defect/owner, and rerun affected checks after fixes. Preserve previous passing baseline until integrator accepts replacements.

## Gate checklist

- [x] Entry prerequisites verified and C01–C04 / T027 implementation steps complete (pure + composition).
- [x] Required suites above pass with archived machine-readable results — pure + dedicated V05/V06/V09 Unity scenario harnesses PASS; see handoff evidence table.
- [x] No prohibited visual sources; generated outputs have versioned provenance (unchanged toolchain/provenance).
- [x] Contract/schema/generator changes reviewed — C0-v1 frozen unchanged for M2.
- [x] Resource/failure/cancellation paths bounded; M2 reference-player budget remains PendingHardware and waived for M2 under ADR-026 (diagnostic attempt recorded; no invented PASS).
- [x] Shared docs updated with actual code references; milestone links verified.
- [x] Integrator reviews evidence and signs milestone status in handoff (owner-directed close; ADR-026).

Final criterion: Movement/combat tests pass; two weapons playable; living hit poses match visible body within declared tolerances; feedback is generated-only. Simulation tests show bounded allocations. Full eight-bot B03 performance gate belongs to M3/M4. M2 closed with user waiver under ADR-026.

## Evidence location

Store run artifacts under repository `Artifacts/m2-combat-harness/<source-commit>/<run-id>/` (proposed path). Include test XML, JSON reports, build/content manifests, toolchain lock, raw timing/memory logs and visual captures when relevant. [Handoff](handoff.md) links actual artifacts and records reviewer/date. Owner closure package: `Artifacts/m2-combat-harness/owner-closure-20261113/`. Do not create fake passing reports from this checklist.


## Revision 1.1 implementation refinements

[Gameplay baseline](../../gameplay-baseline.md), [tick/replay protocol](../../simulation-protocol.md) and T027/T028/T032 define C01–C04. Implement analytic death collapse; physics ragdoll optional. Check same-tick shots, rounding, masks and full prediction state now, not M5.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
