# M3 — Generated compound and AI — agent assignments

Packages: WP-06, WP-07 with WP-01 integration. Follow [ownership rules](../../agent-coordination.md) and [package path table](../../backlog.md). This pack plans future work; it does not launch agents or authorize concurrent edits to shared files.

## Assignment cards

### W01 World plan and gameplay geometry — World owner

Prerequisites: M1 geometry; M2 capsule rules.

Deliver: Create 8×8 grid of 32 m chunks, global routes/footprints/objectives then room/building/terrain/prop recipes. Split gameplay and cosmetic streams.

Acceptance: Four canonical seeds produce collision-safe connected spawn/objective routes and matching terrain borders.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### W02 Navigation and metadata — World owner

Prerequisites: W01.

Deliver: Compile full-map collision/nav, cover candidates and surface/occlusion tags; validate nav settings, clearance and revisioned path adapter.

Acceptance: No unreachable mandatory objective; clearance radius+5 cm; gameplay hashes independent of quality.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### W03 Streaming and residency — Content/world owners

Prerequisites: W01; compiler leases.

Deliver: Implement visual prefetch queue, shared handles, upload scheduling, cancellation and teleport loading fence. Keep full gameplay map loaded initially.

Acceptance: 100 traversals have no missing collision/handle leaks; uploads split under 2 ms cap and residency bounded.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### W04 AI decisions — AI owner

Prerequisites: M2 combat plus W02 nav fixtures.

Deliver: Implement perception/blackboard/state machine, cover reservation, path scheduling, reload and stuck recovery through normal input commands.

Acceptance: Eight bots obey 16 global vision rays/tick and two new paths/tick; stale paths and dead reservations released.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### W05 Integration soak — Integrator/performance owner

Prerequisites: W02,W03,W04.

Deliver: Assemble B02/B03 deterministic workloads, run 30-minute offline soak and inspect memory/path/query counters.

Acceptance: No persistent invalid paths, unbounded queues or memory trend; timing deviations produce owner-assigned fixes before M4.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

## Parallel execution and ownership

Use the prerequisite column in [implementation](implementation.md) as a dependency graph: steps without a dependency on each other may proceed on separate worktrees, after shared contracts freeze. A shared owner/path requires sequential work or an explicit subfolder split before parallel starts. Integration and shared test/benchmark definition edits stay serialized through integrator. Worker-owned paths are exactly those assigned to its WP in the shared backlog; assignment does not expand those rights.

Every worker reads shared contracts and its feature page, uses approved fixture seeds, and submits one reviewable change at a time. No editor/package upgrades or global scene reserialization. Test-only fakes may unblock independent development; release composition must reject remaining stubs. Integrator checks merge order, updates coordination ownership and runs cross-module smoke tests. When a worker is blocked it reports dependency ID and works only on independent fixtures/tests until resolved.
