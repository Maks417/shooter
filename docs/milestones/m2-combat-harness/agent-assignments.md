# M2 — Combat harness — agent assignments

Packages: WP-05 with WP-04/WP-02 adapters. Follow [ownership rules](../../agent-coordination.md) and [package path table](../../backlog.md). Owner Max launched the M2 multi-agent wave on 2026-11-13 from base `925d23f` after M1 closure (ADR-025). Live ownership, branches and forbidden paths are recorded in root `coordination.json` (`m2Start`); this page summarizes the assignment cards.

## Assignment cards

### C01 Motor and input — Simulation owner

Prerequisites: M0 C0.

Deliver: Implement frame-input accumulation and one-shot edges; capsule sweep/slide, gravity, grounding, slope/step and crouch clearance. Keep root movement outside animation.

Acceptance: Script at 30/60/144 display FPS gives outcomes within 1 cm on static map; no double input edges.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### C02 Weapon and damage state — Simulation owner

Prerequisites: C01.

Deliver: Implement equip/fire/recovery/reload/disabled transitions, tick cadence, seeded spread, ammo conservation and idempotent damage/death events.

Acceptance: Exact tick shot counts, semi-auto edges and interruption tests pass; duplicate commands do not duplicate damage.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### C03 Hit pose and obstruction — Simulation owner

Prerequisites: C02; M1 skeleton/socket freeze.

Deliver: Compute gameplay hit capsules independent of visual rig LOD; implement eye aim then muzzle obstruction ray and canonical gameplay muzzle.

Acceptance: Wall/corner/muzzle-inside cases block hits appropriately; quality changes preserve identical hit profiles.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

### C04 Presentation integration — Character/render owners

Prerequisites: C02 event fixtures; integrate after C03.

Deliver: Bind procedural arms/ADS/reload IK, spring recoil and generated shot/impact proxies to events. Deduplicate event consumers and cap pools.

Acceptance: View does not apply damage or recoil twice; grip/pose tolerances pass with live weapon transitions.

Handoff: source commit, tested contract/fixture versions, changed owned paths, reports and limitations. Integrator assigns a named owner and branch before work starts.

## Parallel execution and ownership

Use the prerequisite column in [implementation](implementation.md) as a dependency graph: steps without a dependency on each other may proceed on separate worktrees, after shared contracts freeze. A shared owner/path requires sequential work or an explicit subfolder split before parallel starts. Integration and shared test/benchmark definition edits stay serialized through integrator. Worker-owned paths are exactly those assigned to its WP in the shared backlog; assignment does not expand those rights.

Every worker reads shared contracts and its feature page, uses approved fixture seeds, and submits one reviewable change at a time. No editor/package upgrades or global scene reserialization. Test-only fakes may unblock independent development; release composition must reject remaining stubs. Integrator checks merge order, updates coordination ownership and runs cross-module smoke tests. When a worker is blocked it reports dependency ID and works only on independent fixtures/tests until resolved.
