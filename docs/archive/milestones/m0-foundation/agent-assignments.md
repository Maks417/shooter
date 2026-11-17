# M0 — Foundation — agent assignments

Packages: WP-00, WP-09. Follow [ownership rules](../../../agent-coordination.md) and [package path table](../../../backlog.md). This pack plans future work; it does not launch agents or authorize concurrent edits to shared files.

## Assignment cards

### F01 Toolchain lock — Integrator

Prerequisites: None.

Deliver: Inspect available supported Unity 6 LTS, Windows/IL2CPP/server modules and license access. Create ProjectVersion.txt, package locks and toolchain-lock.json; import a blank URP project and save actual revisions.

Acceptance: Fresh checkout opens without upgrade prompts and client/server toolchains are available.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### F02 Contracts C0 — Integrator

Prerequisites: F01.

Deliver: Implement BCL-only IDs, vector DTOs, commands, snapshots, event envelopes, generation DTOs and ports. Add schema fixtures and validation of units, tick wrap and invalid floats. Freeze C0 before workers start.

Acceptance: Pure contract assembly compiles without Unity; fixtures round-trip and invalid inputs fail explicitly.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### F03 Composition and adapters — Simulation/build owner

Prerequisites: F02.

Deliver: Create asmdefs, injectable clock/world/navigation fakes, fixed-tick driver and client/headless entry points. Separate presentation references from server composition.

Acceptance: Blank player and headless server boot and exit cleanly; no graphics payload required by server.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### F04 Quality and evidence harness — Render/performance owner

Prerequisites: F02; integrate after F03.

Deliver: Create Low/Balanced/High settings and B01 workload/logger. Record adapter, resolution, seed and machine conditions. Implement provenance allowlist and CI/docs checks.

Acceptance: B01 capture on actual iGPU exists; missing hardware is marked pending, never inferred from editor FPS.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

## Parallel execution and ownership

Use the prerequisite column in [implementation](implementation.md) as a dependency graph: steps without a dependency on each other may proceed on separate worktrees, after shared contracts freeze. A shared owner/path requires sequential work or an explicit subfolder split before parallel starts. Integration and shared test/benchmark definition edits stay serialized through integrator. Worker-owned paths are exactly those assigned to its WP in the shared backlog; assignment does not expand those rights.

Every worker reads shared contracts and its feature page, uses approved fixture seeds, and submits one reviewable change at a time. No editor/package upgrades or global scene reserialization. Test-only fakes may unblock independent development; release composition must reject remaining stubs. Integrator checks merge order, updates coordination ownership and runs cross-module smoke tests. When a worker is blocked it reports dependency ID and works only on independent fixtures/tests until resolved.
