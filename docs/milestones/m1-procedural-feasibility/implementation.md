# M1 — Procedural feasibility — implementation sequence

Read [pack entry](index.md) first. Scope: Prove reproducible compilation, primitive/weapon LODs, procedural PBR and a believable generated soldier with analytical gait/IK before content expansion. Use [repo paths](../../repo-layout.md), [architecture](../../architecture.md) and [original task dependencies](../../backlog.md). All paths remain proposed until implemented. Do not add unrelated future features while a required check is failing.

| Step | Owner | Prerequisite | Completion check |
|---|---|---|---|
| P01 Reproducible seeds | Content owner | M0 | Same pinned input has stable topology/hash; cosmetic stream changes do not change gameplay layout. |
| P02 Compiler and loader skeleton | Content owner | P01 | Cold/warm and concurrent/cancel tests pass without partial published artifacts or leaked leases. |
| P03 Geometry and weapons | Geometry owner | P02 API | All required sockets stable within 1 mm/0.1 degrees; bounds/topology/count reports and silhouette review pass. |
| P04 Materials | Render owner | P02 API; parallel P03 | No painted sources; normal orientation/roughness mapping correct; Low shader/texture allocation measured. |
| P05 Soldier spike | Character owner | P03 primitives/sockets; early test-only stubs allowed | Finite 1000-pose sweep, grip/foot-slide limits and visual review pass; stop expansion after two failed iterations and record scope ADR. |

## P01 Reproducible seeds

Owner: Content owner. Dependencies: M0.

Implement PCG32 with known vectors, named SHA-derived streams, canonical encoding and content keys. Validate boundary uint64 seeds and dependency cycle errors.

Completion check: Same pinned input has stable topology/hash; cosmetic stream changes do not change gameplay layout.

## P02 Compiler and loader skeleton

Owner: Content owner. Dependencies: P01.

Implement DAG compilation, bounded manifest/artifact codecs, atomic temp publication, per-key locking, corruption rejection and reference-counted lease loader. Provide CPU bake path.

Completion check: Cold/warm and concurrent/cancel tests pass without partial published artifacts or leaked leases.

## P03 Geometry and weapons

Owner: Geometry owner. Dependencies: P02 API.

Implement primitives, normals/tangents/UVs, generated LODs, collision proxies and rifle/pistol recipes with semantic sockets.

Completion check: All required sockets stable within 1 mm/0.1 degrees; bounds/topology/count reports and silhouette review pass.

## P04 Materials

Owner: Render owner. Dependencies: P02 API; parallel P03.

Bake seven material families, seamless mips and packed channels. Implement cheap URP variant, compression check and B01 comparison.

Completion check: No painted sources; normal orientation/roughness mapping correct; Low shader/texture allocation measured.

## P05 Soldier spike

Owner: Character owner. Dependencies: P03 primitives/sockets; early test-only stubs allowed.

Generate anatomy/rings/skeleton/weights; implement walk/run/crouch/aim/reload IK and eight-rig timing harness. Freeze semantic joints after review.

Completion check: Finite 1000-pose sweep, grip/foot-slide limits and visual review pass; stop expansion after two failed iterations and record scope ADR.

## Integration checkpoints

Commit small compilable changes. Validate contracts before feature adapters and run each step's checks before its dependents merge. Test fakes remain in test assemblies; fail shipping composition if a required real adapter is missing. Shared settings/Bootstrap/lock changes merge through integrator. Attach reports to [handoff](handoff.md). Any blocked prerequisite is explicitly pending; independent test/fixture work can continue without pretending integration passed.

## Failure and rollback

Keep previous passing source/content pair, capture failing seed and diagnostic stage, revert the scoped implementation change through review, and rerun affected checks. Corrupt generated content is rebuilt from approved source. Do not hand-edit caches or automatically accept new golden values. Record scope/contract changes in [ADRs](../../decisions.md) and retest impacted upstream invariants.


## Revision 1.1 implementation refinements

[Deterministic format](../../deterministic-format.md), [soldier baseline](../../soldier-baseline.md), [fixture catalog](../../fixtures/index.md) and T028 define P01/P02/P05. M1 reload/aim uses scripted action phases; no dependency on unimplemented M2 combat. B01-materials-v1 is a distinct workload.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.


## Owner-authorized continuation

P05 reached the bounded two-iteration stop after actual skinned review found persistent arm/torso penetration despite passing numeric hand/foot tests. The owner then explicitly authorized one further bounded body-clearance iteration under [ADR-023](../../decisions.md#adr-023--accepted-owner-authorized-third-bounded-body-clearance-iteration). This changes the iteration bound only for that focused revision; tolerances, generated-source rules and visual/performance gates remain unchanged. It does not authorize an automatic fourth iteration or whole-game expansion.
