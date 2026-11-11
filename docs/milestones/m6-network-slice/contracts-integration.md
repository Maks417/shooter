# M6 — Four-player network slice — contracts and integration

## Entry and frozen boundary

M5 prediction/interpolation and server command validation pass; static cover policy and gameplay pose version frozen.

Freeze rewind data shape, event ordering/death trade policy, accepted shot-time bounds and Ready/resync protocol. Static world occlusion is the only lag-compensated baseline; dynamic blockers require future history design. Match protocol/content hash changes reject incompatible clients.

The normative definitions live in the shared docs root: [DTOs and ports](../../contracts.md), [domain invariants](../../domain-model.md), [seed/cache rules](../../generation.md), [assembly boundaries](../../architecture.md). Do not fork these definitions inside feature code.

## Input and output checklist

- Record upstream source tag, toolchain lock, contract/schema versions and required content hashes in [handoff](handoff.md).
- Validate source recipes, settings and fixtures before allocating resources or running code generation. Unknown required versions fail explicitly.
- Publish only validated immutable artifacts; content leases and snapshot buffers follow declared ownership and cancellation rules.
- Record changed generator/schema/protocol versions and all new fixtures; never reinterpret existing seeds or units silently.
- Output includes executable/harness changes, tests, manifest/report evidence and updated implementation references; documents alone do not complete this milestone.

## Stage-specific integration checkpoints

- **S01 Historical hit query:** History boundaries, dead/despawned actors and cover queries have explicit tested outcomes.
- **S02 Authoritative fire ordering:** Replayed, forged or late input never creates extra damage/ammo; ordering fixtures freeze trade behavior.
- **S03 Session recovery:** Clients cannot act before Ready; outage/reconnect cannot replay prior-session events or leak reservations.
- **S04 Four-peer B04:** 150ms RTT/3% loss playable gate passes; no state divergence >1s after recovery; bandwidth/server budgets met.
- **S05 Exposure review:** LAN/private scope clear; no unsupported public security claim or client-side authority.

## Budget and failure contracts

Use [performance allocations](../../performance.md); numbers are targets until measured on named hardware. All living collision/nav/hit rules stay independent of graphics quality. Reject invalid/nonfinite DTOs, cap queues/pools/payload sizes and expose diagnostics for cancellation/overload. Missing critical content blocks load/spawn. No prohibited imported art or authored motion is a valid fallback.

## Change control

An agent submits shared boundary changes to integrator with old/new fixtures, affected owners and migration plan before dependent merges. Re-run [validation](validation-acceptance.md), preserve previous passing tag, and update shared specifications in the docs root and verify all milestone links. Record pending integration honestly.
