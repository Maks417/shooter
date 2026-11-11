# M0 — Foundation — implementation sequence

Read [pack entry](index.md) first. Scope: Pin Unity 6/URP; establish assembly boundaries, contracts C0, client/server composition, three quality profiles, tests and benchmark capture from day one. Use [repo paths](../../repo-layout.md), [architecture](../../architecture.md) and [original task dependencies](../../backlog.md). All paths remain proposed until implemented. Do not add unrelated future features while a required check is failing.

| Step | Owner | Prerequisite | Completion check |
|---|---|---|---|
| F01 Toolchain lock | Integrator | None | Fresh checkout opens without upgrade prompts and client/server toolchains are available. |
| F02 Contracts C0 | Integrator | F01 | Pure contract assembly compiles without Unity; fixtures round-trip and invalid inputs fail explicitly. |
| F03 Composition and adapters | Simulation/build owner | F02 | Blank player and headless server boot and exit cleanly; no graphics payload required by server. |
| F04 Quality and evidence harness | Render/performance owner | F02; integrate after F03 | B01 capture on actual iGPU exists; missing hardware is marked pending, never inferred from editor FPS. |

## F01 Toolchain lock

Owner: Integrator. Dependencies: None.

Inspect available supported Unity 6 LTS, Windows/IL2CPP/server modules and license access. Create ProjectVersion.txt, package locks and toolchain-lock.json; import a blank URP project and save actual revisions.

Completion check: Fresh checkout opens without upgrade prompts and client/server toolchains are available.

## F02 Contracts C0

Owner: Integrator. Dependencies: F01.

Implement BCL-only IDs, vector DTOs, commands, snapshots, event envelopes, generation DTOs and ports. Add schema fixtures and validation of units, tick wrap and invalid floats. Freeze C0 before workers start.

Completion check: Pure contract assembly compiles without Unity; fixtures round-trip and invalid inputs fail explicitly.

## F03 Composition and adapters

Owner: Simulation/build owner. Dependencies: F02.

Create asmdefs, injectable clock/world/navigation fakes, fixed-tick driver and client/headless entry points. Separate presentation references from server composition.

Completion check: Blank player and headless server boot and exit cleanly; no graphics payload required by server.

## F04 Quality and evidence harness

Owner: Render/performance owner. Dependencies: F02; integrate after F03.

Create Low/Balanced/High settings and B01 workload/logger. Record adapter, resolution, seed and machine conditions. Implement provenance allowlist and CI/docs checks.

Completion check: B01 capture on actual iGPU exists; missing hardware is marked pending, never inferred from editor FPS.

## Integration checkpoints

Commit small compilable changes. Validate contracts before feature adapters and run each step's checks before its dependents merge. Test fakes remain in test assemblies; fail shipping composition if a required real adapter is missing. Shared settings/Bootstrap/lock changes merge through integrator. Attach reports to [handoff](handoff.md). Any blocked prerequisite is explicitly pending; independent test/fixture work can continue without pretending integration passed.

## Failure and rollback

Keep previous passing source/content pair, capture failing seed and diagnostic stage, revert the scoped implementation change through review, and rerun affected checks. Corrupt generated content is rebuilt from approved source. Do not hand-edit caches or automatically accept new golden values. Record scope/contract changes in [ADRs](../../decisions.md) and retest impacted upstream invariants.


## Revision 1.1 implementation refinements

[Bootstrap runbook](../../bootstrap-runbook.md), [C0 details](../../contract-details.md), [fixture catalog](../../fixtures/index.md) and T026/T033/T034 are required F01–F04 inputs. Freeze B01-minimal-v1 only; do not require the M1 material library at M0.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
