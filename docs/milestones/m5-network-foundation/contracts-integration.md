# M5 — Networking foundation — contracts and integration

## Entry and frozen boundary

M4 offline slice passes; simulation/physics queries and events isolated from presentation; headless build still clean.

Freeze wire protocol version, selected delivery semantics, match manifest, snapshot baseline/ack rules and clock-offset policy. Transport-owned acknowledgements are not duplicated. Explicit packet size/queue bounds and unknown-version failure mandatory.

The normative definitions live in the shared docs root: [DTOs and ports](../../contracts.md), [domain invariants](../../domain-model.md), [seed/cache rules](../../generation.md), [assembly boundaries](../../architecture.md). Do not fork these definitions inside feature code.

## Input and output checklist

- Record upstream source tag, toolchain lock, contract/schema versions and required content hashes in [handoff](handoff.md).
- Validate source recipes, settings and fixtures before allocating resources or running code generation. Unknown required versions fail explicitly.
- Publish only validated immutable artifacts; content leases and snapshot buffers follow declared ownership and cancellation rules.
- Record changed generator/schema/protocol versions and all new fixtures; never reinterpret existing seeds or units silently.
- Output includes executable/harness changes, tests, manifest/report evidence and updated implementation references; documents alone do not complete this milestone.

## Stage-specific integration checkpoints

- **N01 Framework spike:** Framework selected by measured behavior; no assumption anticipation equals rollback/replay.
- **N02 Session and commands:** Mismatch rejected before spawn; duplicate/stale/future input cannot cause unauthorized transitions.
- **N03 Local prediction:** Static-map correction p95 ≤0.1 m at 100 ms RTT/1% loss; no duplicate shots or ammo drift.
- **N04 Remote snapshots:** Reorder/loss causes bounded hold/resync, no runaway allocation or permanent baseline loss.
- **N05 Two-peer impairment review:** Server has no graphics dependencies; state recovers and offline tests remain green.

## Budget and failure contracts

Use [performance allocations](../../performance.md); numbers are targets until measured on named hardware. All living collision/nav/hit rules stay independent of graphics quality. Reject invalid/nonfinite DTOs, cap queues/pools/payload sizes and expose diagnostics for cancellation/overload. Missing critical content blocks load/spawn. No prohibited imported art or authored motion is a valid fallback.

## Change control

An agent submits shared boundary changes to integrator with old/new fixtures, affected owners and migration plan before dependent merges. Re-run [validation](validation-acceptance.md), preserve previous passing tag, and update shared specifications in the docs root and verify all milestone links. Record pending integration honestly.
