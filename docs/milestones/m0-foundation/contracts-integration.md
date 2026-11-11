# M0 — Foundation — contracts and integration

## Entry and frozen boundary

No implementation prerequisite. Inspect the installed editor and create a clean project; existing documentation is the specification, not evidence of working systems.

Freeze units, handedness, EntityId/ContentId, InputCommand, SimulationSnapshot, EventId, IWorldQueries, INavigation, GenerationRequest and content lease semantics. Publish fake implementations and boundary fixtures. Keep Unity and transport types out of Contracts.

The normative definitions live in the shared docs root: [DTOs and ports](../../contracts.md), [domain invariants](../../domain-model.md), [seed/cache rules](../../generation.md), [assembly boundaries](../../architecture.md). Do not fork these definitions inside feature code.

## Input and output checklist

- Record upstream source tag, toolchain lock, contract/schema versions and required content hashes in [handoff](handoff.md).
- Validate source recipes, settings and fixtures before allocating resources or running code generation. Unknown required versions fail explicitly.
- Publish only validated immutable artifacts; content leases and snapshot buffers follow declared ownership and cancellation rules.
- Record changed generator/schema/protocol versions and all new fixtures; never reinterpret existing seeds or units silently.
- Output includes executable/harness changes, tests, manifest/report evidence and updated implementation references; documents alone do not complete this milestone.

## Stage-specific integration checkpoints

- **F01 Toolchain lock:** Fresh checkout opens without upgrade prompts and client/server toolchains are available.
- **F02 Contracts C0:** Pure contract assembly compiles without Unity; fixtures round-trip and invalid inputs fail explicitly.
- **F03 Composition and adapters:** Blank player and headless server boot and exit cleanly; no graphics payload required by server.
- **F04 Quality and evidence harness:** B01 capture on actual iGPU exists; missing hardware is marked pending, never inferred from editor FPS.

## Budget and failure contracts

Use [performance allocations](../../performance.md); numbers are targets until measured on named hardware. All living collision/nav/hit rules stay independent of graphics quality. Reject invalid/nonfinite DTOs, cap queues/pools/payload sizes and expose diagnostics for cancellation/overload. Missing critical content blocks load/spawn. No prohibited imported art or authored motion is a valid fallback.

## Change control

An agent submits shared boundary changes to integrator with old/new fixtures, affected owners and migration plan before dependent merges. Re-run [validation](validation-acceptance.md), preserve previous passing tag, and update shared specifications in the docs root and verify all milestone links. Record pending integration honestly.
