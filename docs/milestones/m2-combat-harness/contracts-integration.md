# M2 — Combat harness — contracts and integration

## Entry and frozen boundary

M0 C0 permits early motor/combat work against fakes. M1 rig/socket artifacts and reviews are required for final integration and milestone exit.

WeaponDefinition/WeaponState transitions, canonical muzzle and gameplay pose evaluator version become match-hashed data. Freeze combat event payloads and ordering. Presentation reads snapshot/event leases only; hit volumes are authoritative at 60 Hz regardless of rendering rate.

The normative definitions live in the shared docs root: [DTOs and ports](../../contracts.md), [domain invariants](../../domain-model.md), [seed/cache rules](../../generation.md), [assembly boundaries](../../architecture.md). Do not fork these definitions inside feature code.

## Input and output checklist

- Record upstream source tag, toolchain lock, contract/schema versions and required content hashes in [handoff](handoff.md).
- Validate source recipes, settings and fixtures before allocating resources or running code generation. Unknown required versions fail explicitly.
- Publish only validated immutable artifacts; content leases and snapshot buffers follow declared ownership and cancellation rules.
- Record changed generator/schema/protocol versions and all new fixtures; never reinterpret existing seeds or units silently.
- Output includes executable/harness changes, tests, manifest/report evidence and updated implementation references; documents alone do not complete this milestone.

## Stage-specific integration checkpoints

- **C01 Motor and input:** Script at 30/60/144 display FPS gives outcomes within 1 cm on static map; no double input edges.
- **C02 Weapon and damage state:** Exact tick shot counts, semi-auto edges and interruption tests pass; duplicate commands do not duplicate damage.
- **C03 Hit pose and obstruction:** Wall/corner/muzzle-inside cases block hits appropriately; quality changes preserve identical hit profiles.
- **C04 Presentation integration:** View does not apply damage or recoil twice; grip/pose tolerances pass with live weapon transitions.

## Budget and failure contracts

Use [performance allocations](../../performance.md); numbers are targets until measured on named hardware. All living collision/nav/hit rules stay independent of graphics quality. Reject invalid/nonfinite DTOs, cap queues/pools/payload sizes and expose diagnostics for cancellation/overload. Missing critical content blocks load/spawn. No prohibited imported art or authored motion is a valid fallback.

## Change control

An agent submits shared boundary changes to integrator with old/new fixtures, affected owners and migration plan before dependent merges. Re-run [validation](validation-acceptance.md), preserve previous passing tag, and update shared specifications in the docs root and verify all milestone links. Record pending integration honestly.
