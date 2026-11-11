# M2 — Combat harness — implementation sequence

Read [pack entry](index.md) first. Scope: Deliver 60 Hz movement, rifle/pistol mechanics, authoritative hit capsules, procedural first-person feedback and robust cover/muzzle rules in a small test space. Use [repo paths](../../repo-layout.md), [architecture](../../architecture.md) and [original task dependencies](../../backlog.md). All paths remain proposed until implemented. Do not add unrelated future features while a required check is failing.

| Step | Owner | Prerequisite | Completion check |
|---|---|---|---|
| C01 Motor and input | Simulation owner | M0 C0 | Script at 30/60/144 display FPS gives outcomes within 1 cm on static map; no double input edges. |
| C02 Weapon and damage state | Simulation owner | C01 | Exact tick shot counts, semi-auto edges and interruption tests pass; duplicate commands do not duplicate damage. |
| C03 Hit pose and obstruction | Simulation owner | C02; M1 skeleton/socket freeze | Wall/corner/muzzle-inside cases block hits appropriately; quality changes preserve identical hit profiles. |
| C04 Presentation integration | Character/render owners | C02 event fixtures; integrate after C03 | View does not apply damage or recoil twice; grip/pose tolerances pass with live weapon transitions. |

## C01 Motor and input

Owner: Simulation owner. Dependencies: M0 C0.

Implement frame-input accumulation and one-shot edges; capsule sweep/slide, gravity, grounding, slope/step and crouch clearance. Keep root movement outside animation.

Completion check: Script at 30/60/144 display FPS gives outcomes within 1 cm on static map; no double input edges.

## C02 Weapon and damage state

Owner: Simulation owner. Dependencies: C01.

Implement equip/fire/recovery/reload/disabled transitions, tick cadence, seeded spread, ammo conservation and idempotent damage/death events.

Completion check: Exact tick shot counts, semi-auto edges and interruption tests pass; duplicate commands do not duplicate damage.

## C03 Hit pose and obstruction

Owner: Simulation owner. Dependencies: C02; M1 skeleton/socket freeze.

Compute gameplay hit capsules independent of visual rig LOD; implement eye aim then muzzle obstruction ray and canonical gameplay muzzle.

Completion check: Wall/corner/muzzle-inside cases block hits appropriately; quality changes preserve identical hit profiles.

## C04 Presentation integration

Owner: Character/render owners. Dependencies: C02 event fixtures; integrate after C03.

Bind procedural arms/ADS/reload IK, spring recoil and generated shot/impact proxies to events. Deduplicate event consumers and cap pools.

Completion check: View does not apply damage or recoil twice; grip/pose tolerances pass with live weapon transitions.

## Integration checkpoints

Commit small compilable changes. Validate contracts before feature adapters and run each step's checks before its dependents merge. Test fakes remain in test assemblies; fail shipping composition if a required real adapter is missing. Shared settings/Bootstrap/lock changes merge through integrator. Attach reports to [handoff](handoff.md). Any blocked prerequisite is explicitly pending; independent test/fixture work can continue without pretending integration passed.

## Failure and rollback

Keep previous passing source/content pair, capture failing seed and diagnostic stage, revert the scoped implementation change through review, and rerun affected checks. Corrupt generated content is rebuilt from approved source. Do not hand-edit caches or automatically accept new golden values. Record scope/contract changes in [ADRs](../../decisions.md) and retest impacted upstream invariants.


## Revision 1.1 implementation refinements

[Gameplay baseline](../../gameplay-baseline.md), [tick/replay protocol](../../simulation-protocol.md) and T027/T028/T032 define C01–C04. Implement analytic death collapse; physics ragdoll optional. Check same-tick shots, rounding, masks and full prediction state now, not M5.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
