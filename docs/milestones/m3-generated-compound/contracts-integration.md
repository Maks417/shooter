# M3 — Generated compound and AI — contracts and integration

## Entry and frozen boundary

M1 compiler/geometry and M2 motor/combat contracts stable. Character output exists for eight-soldier rendering; world planner may prototype earlier using fake primitives.

Freeze world/chunk schema, collision/nav hash separation, surface and cover tags, nav revision protocol and AI stimuli. Cosmetic objects cannot supply missing gameplay blockers; chunk Ready requires installed critical data.

The normative definitions live in the shared docs root: [DTOs and ports](../../contracts.md), [domain invariants](../../domain-model.md), [seed/cache rules](../../generation.md), [assembly boundaries](../../architecture.md). Do not fork these definitions inside feature code.

## Input and output checklist

- Record upstream source tag, toolchain lock, contract/schema versions and required content hashes in [handoff](handoff.md).
- Validate source recipes, settings and fixtures before allocating resources or running code generation. Unknown required versions fail explicitly.
- Publish only validated immutable artifacts; content leases and snapshot buffers follow declared ownership and cancellation rules.
- Record changed generator/schema/protocol versions and all new fixtures; never reinterpret existing seeds or units silently.
- Output includes executable/harness changes, tests, manifest/report evidence and updated implementation references; documents alone do not complete this milestone.

## Stage-specific integration checkpoints

- **W01 World plan and gameplay geometry:** Four canonical seeds produce collision-safe connected spawn/objective routes and matching terrain borders.
- **W02 Navigation and metadata:** No unreachable mandatory objective; clearance radius+5 cm; gameplay hashes independent of quality.
- **W03 Streaming and residency:** 100 traversals have no missing collision/handle leaks; uploads split under 2 ms cap and residency bounded.
- **W04 AI decisions:** Eight bots obey 16 global vision rays/tick and two new paths/tick; stale paths and dead reservations released.
- **W05 Integration soak:** No persistent invalid paths, unbounded queues or memory trend; timing deviations produce owner-assigned fixes before M4.

## Budget and failure contracts

Use [performance allocations](../../performance.md); numbers are targets until measured on named hardware. All living collision/nav/hit rules stay independent of graphics quality. Reject invalid/nonfinite DTOs, cap queues/pools/payload sizes and expose diagnostics for cancellation/overload. Missing critical content blocks load/spawn. No prohibited imported art or authored motion is a valid fallback.

## Change control

An agent submits shared boundary changes to integrator with old/new fixtures, affected owners and migration plan before dependent merges. Re-run [validation](validation-acceptance.md), preserve previous passing tag, and update shared specifications in the docs root and verify all milestone links. Record pending integration honestly.
