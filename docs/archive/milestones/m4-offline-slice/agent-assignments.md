# M4 — Offline playable slice — agent assignments

Packages: WP-09 integrator with feature owners. Follow [ownership rules](../../../agent-coordination.md) and [package path table](../../../backlog.md). This pack plans future work; it does not launch agents or authorize concurrent edits to shared files.

## Assignment cards

### O01 Objective and restart — Simulation/integrator

Prerequisites: M3.

Deliver: Implement spawn→combat→objective success/failure→restart with deterministic state reset and complete lease/entity cleanup.

Acceptance: Repeated restart returns to same seed/state; death/objective events trigger exactly once.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### O02 Settings and HUD — Presentation owner

Prerequisites: O01 event/state fixtures.

Deliver: Implement readable 720p HUD/menu/loading, persisted versioned controls/FOV/quality, maximum UI scaling and reduced motion options.

Acceptance: No clipping at 150% UI; focus/rebinding/contrast checks pass; profile switch preserves gameplay hashes.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### O03 Generated feedback polish — Render/audio/character owners

Prerequisites: M2 events; parallel O02.

Deliver: Replace diagnostic effects with capped analytic mesh FX and synthesized audio; review material scale and soldier/weapon motion in compound.

Acceptance: No prohibited sprites/clips/images; pools bounded; visibility/ADS wall tests and visual review pass.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### O04 Profile and optimize — Performance owner plus module owners

Prerequisites: O01–O03.

Deliver: Measure release IL2CPP on actual reference iGPU. Fix pass/thread bottlenecks, shader/overdraw/LOD/upload cost without changing workload or collision.

Acceptance: Three native720p runs and soak pass declared frame, memory and allocation gates.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### O05 Offline gate release — Integrator

Prerequisites: O04.

Deliver: Clean build/install with verified generated content, objective walkthrough and signed requirement/evidence review. Tag matching binary/content manifests.

Acceptance: Offline R01–R09 obligations complete; R10 networking remains pending M6.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

## Parallel execution and ownership

Use the prerequisite column in [implementation](implementation.md) as a dependency graph: steps without a dependency on each other may proceed on separate worktrees, after shared contracts freeze. A shared owner/path requires sequential work or an explicit subfolder split before parallel starts. Integration and shared test/benchmark definition edits stay serialized through integrator. Worker-owned paths are exactly those assigned to its WP in the shared backlog; assignment does not expand those rights.

Every worker reads shared contracts and its feature page, uses approved fixture seeds, and submits one reviewable change at a time. No editor/package upgrades or global scene reserialization. Test-only fakes may unblock independent development; release composition must reject remaining stubs. Integrator checks merge order, updates coordination ownership and runs cross-module smoke tests. When a worker is blocked it reports dependency ID and works only on independent fixtures/tests until resolved.
