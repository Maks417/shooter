# M4 — Offline playable slice — contracts and integration

## Entry and frozen boundary

M3 complete; generated soldier/weapon/world artifacts verified; B02/B03 definitions frozen and outstanding budget issues assigned.

Freeze offline baseline build/content/workload manifests and settings schema. Any generator optimization changing bytes bumps versions; gameplay changes need new reviewed fixtures, not hidden benchmark edits. Local settings never mutate match rules.

The normative definitions live in the shared docs root: [DTOs and ports](../../../contract-details.md), [domain invariants](../../../domain-model.md), [seed/cache rules](../../../generation.md), [assembly boundaries](../../../architecture.md). Do not fork these definitions inside feature code.

## Input and output checklist

- Record upstream source tag, toolchain lock, contract/schema versions and required content hashes in [handoff](../../../milestones/m4-offline-slice/handoff.md).
- Validate source recipes, settings and fixtures before allocating resources or running code generation. Unknown required versions fail explicitly.
- Publish only validated immutable artifacts; content leases and snapshot buffers follow declared ownership and cancellation rules.
- Record changed generator/schema/protocol versions and all new fixtures; never reinterpret existing seeds or units silently.
- Output includes executable/harness changes, tests, manifest/report evidence and updated implementation references; documents alone do not complete this milestone.

## Stage-specific integration checkpoints

- **O01 Objective and restart:** Repeated restart returns to same seed/state; death/objective events trigger exactly once.
- **O02 Settings and HUD:** No clipping at 150% UI; focus/rebinding/contrast checks pass; profile switch preserves gameplay hashes.
- **O03 Generated feedback polish:** No prohibited sprites/clips/images; pools bounded; visibility/ADS wall tests and visual review pass.
- **O04 Profile and optimize:** Three native720p runs and soak pass declared frame, memory and allocation gates.
- **O05 Offline gate release:** Offline R01–R09 obligations complete; R10 networking remains pending M6.

## Budget and failure contracts

Use [performance allocations](../../../performance.md); numbers are targets until measured on named hardware. All living collision/nav/hit rules stay independent of graphics quality. Reject invalid/nonfinite DTOs, cap queues/pools/payload sizes and expose diagnostics for cancellation/overload. Missing critical content blocks load/spawn. No prohibited imported art or authored motion is a valid fallback.

## Change control

An agent submits shared boundary changes to integrator with old/new fixtures, affected owners and migration plan before dependent merges. Re-run [validation](../../../milestones/m4-offline-slice/validation-acceptance.md), preserve previous passing tag, and update shared specifications in the docs root and verify all milestone links. Record pending integration honestly.
