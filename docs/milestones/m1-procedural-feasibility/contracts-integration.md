# M1 — Procedural feasibility — contracts and integration

## Entry and frozen boundary

M0 passes. C0 fixtures and toolchain locks exist. Geometry/material/rig workers may prototype against fakes, but generated-output integration waits for compiler schema freeze.

Freeze compiler manifest v1, recipe schemas, material channel layout, stable socket IDs and skeleton joint hierarchy. Visual LODs preserve gameplay socket semantics. Record generator/toolchain versions for all outputs; never use GPU results as canonical gameplay hashes.

The normative definitions live in the shared docs root: [DTOs and ports](../../contracts.md), [domain invariants](../../domain-model.md), [seed/cache rules](../../generation.md), [assembly boundaries](../../architecture.md). Do not fork these definitions inside feature code.

## Input and output checklist

- Record upstream source tag, toolchain lock, contract/schema versions and required content hashes in [handoff](handoff.md).
- Validate source recipes, settings and fixtures before allocating resources or running code generation. Unknown required versions fail explicitly.
- Publish only validated immutable artifacts; content leases and snapshot buffers follow declared ownership and cancellation rules.
- Record changed generator/schema/protocol versions and all new fixtures; never reinterpret existing seeds or units silently.
- Output includes executable/harness changes, tests, manifest/report evidence and updated implementation references; documents alone do not complete this milestone.

## Stage-specific integration checkpoints

- **P01 Reproducible seeds:** Same pinned input has stable topology/hash; cosmetic stream changes do not change gameplay layout.
- **P02 Compiler and loader skeleton:** Cold/warm and concurrent/cancel tests pass without partial published artifacts or leaked leases.
- **P03 Geometry and weapons:** All required sockets stable within 1 mm/0.1 degrees; bounds/topology/count reports and silhouette review pass.
- **P04 Materials:** No painted sources; normal orientation/roughness mapping correct; Low shader/texture allocation measured.
- **P05 Soldier spike:** Finite 1000-pose sweep, grip/foot-slide limits and visual review pass; stop expansion after two failed iterations and record scope ADR.

## Budget and failure contracts

Use [performance allocations](../../performance.md); numbers are targets until measured on named hardware. All living collision/nav/hit rules stay independent of graphics quality. Reject invalid/nonfinite DTOs, cap queues/pools/payload sizes and expose diagnostics for cancellation/overload. Missing critical content blocks load/spawn. No prohibited imported art or authored motion is a valid fallback.

## Change control

An agent submits shared boundary changes to integrator with old/new fixtures, affected owners and migration plan before dependent merges. Re-run [validation](validation-acceptance.md), preserve previous passing tag, and update shared specifications in the docs root and verify all milestone links. Record pending integration honestly.
