# M4 — Offline playable slice — implementation sequence

Read [pack entry](../../../milestones/m4-offline-slice/index.md) first. Scope: Complete objective loop, settings/UI/accessibility and procedural feedback; deliver measured native-720p 30 FPS on reference hardware with an offline release build. Use [repo paths](../../../repo-layout.md), [architecture](../../../architecture.md) and [original task dependencies](../../../backlog.md). All paths remain proposed until implemented. Do not add unrelated future features while a required check is failing.

| Step | Owner | Prerequisite | Completion check |
|---|---|---|---|
| O01 Objective and restart | Simulation/integrator | M3 | Repeated restart returns to same seed/state; death/objective events trigger exactly once. |
| O02 Settings and HUD | Presentation owner | O01 event/state fixtures | No clipping at 150% UI; focus/rebinding/contrast checks pass; profile switch preserves gameplay hashes. |
| O03 Generated feedback polish | Render/audio/character owners | M2 events; parallel O02 | No prohibited sprites/clips/images; pools bounded; visibility/ADS wall tests and visual review pass. |
| O04 Profile and optimize | Performance owner plus module owners | O01–O03 | Three native720p runs and soak pass declared frame, memory and allocation gates. |
| O05 Offline gate release | Integrator | O04 | Offline R01–R09 obligations complete; R10 networking remains pending M6. |

## O01 Objective and restart

Owner: Simulation/integrator. Dependencies: M3.

Implement spawn→combat→objective success/failure→restart with deterministic state reset and complete lease/entity cleanup.

Completion check: Repeated restart returns to same seed/state; death/objective events trigger exactly once.

## O02 Settings and HUD

Owner: Presentation owner. Dependencies: O01 event/state fixtures.

Implement readable 720p HUD/menu/loading, persisted versioned controls/FOV/quality, maximum UI scaling and reduced motion options.

Completion check: No clipping at 150% UI; focus/rebinding/contrast checks pass; profile switch preserves gameplay hashes.

## O03 Generated feedback polish

Owner: Render/audio/character owners. Dependencies: M2 events; parallel O02.

Replace diagnostic effects with capped analytic mesh FX and synthesized audio; review material scale and soldier/weapon motion in compound.

Completion check: No prohibited sprites/clips/images; pools bounded; visibility/ADS wall tests and visual review pass.

## O04 Profile and optimize

Owner: Performance owner plus module owners. Dependencies: O01–O03.

Measure release IL2CPP on actual reference iGPU. Fix pass/thread bottlenecks, shader/overdraw/LOD/upload cost without changing workload or collision.

Completion check: Three native720p runs and soak pass declared frame, memory and allocation gates.

## O05 Offline gate release

Owner: Integrator. Dependencies: O04.

Clean build/install with verified generated content, objective walkthrough and signed requirement/evidence review. Tag matching binary/content manifests.

Completion check: Offline R01–R09 obligations complete; R10 networking remains pending M6.

## Integration checkpoints

Commit small compilable changes. Validate contracts before feature adapters and run each step's checks before its dependents merge. Test fakes remain in test assemblies; fail shipping composition if a required real adapter is missing. Shared settings/Bootstrap/lock changes merge through integrator. Attach reports to [handoff](../../../milestones/m4-offline-slice/handoff.md). Any blocked prerequisite is explicitly pending; independent test/fixture work can continue without pretending integration passed.

## Failure and rollback

Keep previous passing source/content pair, capture failing seed and diagnostic stage, revert the scoped implementation change through review, and rerun affected checks. Corrupt generated content is rebuilt from approved source. Do not hand-edit caches or automatically accept new golden values. Record scope/contract changes in [ADRs](../../../decisions.md) and retest impacted upstream invariants.


## Revision 1.1 implementation refinements

[Task cards T029–T033](../../../task-supplement.md) assign objective, synthesized audio, simple lighting and analytic collapse; no implicit required ragdoll. [Compound Clear rules](../../../gameplay-baseline.md) fix O01. Use exact B03 and separate normal-AI soak.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
