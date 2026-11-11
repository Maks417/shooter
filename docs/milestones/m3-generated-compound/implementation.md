# M3 — Generated compound and AI — implementation sequence

Read [pack entry](index.md) first. Scope: Generate connected 256 m compound, full-map canonical collision/nav, streamed visuals and eight bounded-query combat bots. Use [repo paths](../../repo-layout.md), [architecture](../../architecture.md) and [original task dependencies](../../backlog.md). All paths remain proposed until implemented. Do not add unrelated future features while a required check is failing.

| Step | Owner | Prerequisite | Completion check |
|---|---|---|---|
| W01 World plan and gameplay geometry | World owner | M1 geometry; M2 capsule rules | Four canonical seeds produce collision-safe connected spawn/objective routes and matching terrain borders. |
| W02 Navigation and metadata | World owner | W01 | No unreachable mandatory objective; clearance radius+5 cm; gameplay hashes independent of quality. |
| W03 Streaming and residency | Content/world owners | W01; compiler leases | 100 traversals have no missing collision/handle leaks; uploads split under 2 ms cap and residency bounded. |
| W04 AI decisions | AI owner | M2 combat plus W02 nav fixtures | Eight bots obey 16 global vision rays/tick and two new paths/tick; stale paths and dead reservations released. |
| W05 Integration soak | Integrator/performance owner | W02,W03,W04 | No persistent invalid paths, unbounded queues or memory trend; timing deviations produce owner-assigned fixes before M4. |

## W01 World plan and gameplay geometry

Owner: World owner. Dependencies: M1 geometry; M2 capsule rules.

Create 8×8 grid of 32 m chunks, global routes/footprints/objectives then room/building/terrain/prop recipes. Split gameplay and cosmetic streams.

Completion check: Four canonical seeds produce collision-safe connected spawn/objective routes and matching terrain borders.

## W02 Navigation and metadata

Owner: World owner. Dependencies: W01.

Compile full-map collision/nav, cover candidates and surface/occlusion tags; validate nav settings, clearance and revisioned path adapter.

Completion check: No unreachable mandatory objective; clearance radius+5 cm; gameplay hashes independent of quality.

## W03 Streaming and residency

Owner: Content/world owners. Dependencies: W01; compiler leases.

Implement visual prefetch queue, shared handles, upload scheduling, cancellation and teleport loading fence. Keep full gameplay map loaded initially.

Completion check: 100 traversals have no missing collision/handle leaks; uploads split under 2 ms cap and residency bounded.

## W04 AI decisions

Owner: AI owner. Dependencies: M2 combat plus W02 nav fixtures.

Implement perception/blackboard/state machine, cover reservation, path scheduling, reload and stuck recovery through normal input commands.

Completion check: Eight bots obey 16 global vision rays/tick and two new paths/tick; stale paths and dead reservations released.

## W05 Integration soak

Owner: Integrator/performance owner. Dependencies: W02,W03,W04.

Assemble B02/B03 deterministic workloads, run 30-minute offline soak and inspect memory/path/query counters.

Completion check: No persistent invalid paths, unbounded queues or memory trend; timing deviations produce owner-assigned fixes before M4.

## Integration checkpoints

Commit small compilable changes. Validate contracts before feature adapters and run each step's checks before its dependents merge. Test fakes remain in test assemblies; fail shipping composition if a required real adapter is missing. Shared settings/Bootstrap/lock changes merge through integrator. Attach reports to [handoff](handoff.md). Any blocked prerequisite is explicitly pending; independent test/fixture work can continue without pretending integration passed.

## Failure and rollback

Keep previous passing source/content pair, capture failing seed and diagnostic stage, revert the scoped implementation change through review, and rerun affected checks. Corrupt generated content is rebuilt from approved source. Do not hand-edit caches or automatically accept new golden values. Record scope/contract changes in [ADRs](../../decisions.md) and retest impacted upstream invariants.


## Revision 1.1 implementation refinements

[Objective/layer rules](../../gameplay-baseline.md) and [exact benchmark fixtures](../../benchmark-fixtures.md) feed W01–W05. T029 rules are already fixed; T033 freezes B02/B03 manifests and validates route/shot counts. Run normal-AI soak separately from controlled B03 firing.

These refinements are specifications, not completed tasks. Record actual checks and pending evidence in the milestone handoff.
