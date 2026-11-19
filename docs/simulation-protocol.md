# Simulation order and replay

This page refines [architecture](architecture.md), [contracts](contract-details.md) and [combat](features/combat.md) with the normative tick order for offline authority.

[Team Deathmatch](features/team-deathmatch.md) uses steps 1–5 below. It replaces step 6's objective evaluation with the [Game-v1 batch/deadline/respawn publication order](game-session-contracts.md#tick-and-publication-boundary):
1. Resolve the entire death batch.
2. Advance active time.
3. Finish once.
4. Schedule later respawns and AI, only if the match is still playing.

Networked reconciliation and historical-shot rewinding are parked with networking ([ADR-076](decisions.md#adr-076--accepted-offline-only-scope-and-bounded-local-evidence)). Their design remains in git history.

## Tick boundary

Tick t consumes commands referring to tick t and reads finalized state t−1. At start: validate world readiness and tick monotonicity; apply queued spawn/despawn/load-boundary operations in stable ID order. Capture actor alive-at-start set and previous stimuli. AI decisions scheduled at end of t−1 produce commands for t; no bot can use a damage event from t before t+1.

1. Drain/validate one combined command per live actor, deduplicated by sequence. Retain previous FireHeld in state. Explicit FirePressed is accepted only on a rising edge not previously consumed; switching away cancels reload before any completion transfer this tick.
2. Process slot/stance requests and active action completion at boundary. Equip completion allows shooting this tick. Reload completion transfers ammo this tick unless cancelled by slot change/death at start. Disabled/dead actor commands ignored.
3. Move all live actors in ascending EntityId against static geometry. First slice actors do not physically block one another; actor-vs-actor damage capsules are query-only. Update grounded/stance/velocity and authoritative recoil decay. Update gameplay pose and canonical muzzle for every actor from post-movement state.
4. Capture current hit volumes and collect accepted shot intents for all alive-at-start actors. Validate cadence/ammo, advance shot index and compute spread from declared stream. Each accepted intent consumes ammo exactly once. Post-movement volumes and immutable static collision are used for hitscan.
5. Resolve all shot queries against the same immutable volume set, ordered shooter ID then shot index. Queue damage; apply damage in that order after all queries. Same-tick lethal shots may trade because shots were collected before damage. Actor killed in tick t can fire its already accepted t intent, but cannot fire at t+1. Emit one death on first crossing to zero, ignore further health reduction at zero. No corpse damage target after t completion.
6. Evaluate the match (TDM: see above), produce stimuli and AI plans for t+1, and publish the finalized snapshot and events.

ADR-105 offline session additions after step 6: the TDM death batch records each dying actor's primary (slot 0) as a dropped weapon at its feet before the life is retired; then the human's pickup request resolves (swap or capped ammo top-up), taking effect from tick t+1; dropped weapons expire at the end of the tick once their 900 active-tick lifetime has passed.

Order events by processing phase then actor ID then per-actor ordinal. Accepted shot, impact, damage, death and match events receive unique EventId. Limit guards abort tick/session explicitly; no partially committed tick. Implement staged state or rollback-to-prestep on failure; never continue after partial damage application.

## Missing input and clock

Offline play generates one command every tick, even when render frames lag. Each edge event is consumed once. Pause stops tick time and clears pending one-shot inputs on resume.

Catch-up is capped at 4 ticks per display frame:
- **Diagnostic, smoke, gallery and benchmark modes** keep the remaining debt, freeze the simulation and treat overload as fatal.
- **The product client** ([ADR-046](decisions.md#adr-046--accepted-offline-product-client-recovers-from-frame-hitches)) drops the unserviced whole-tick debt and keeps the fractional remainder and the tick order. It logs one `SHOOTER_HITCH` warning and continues in real time.

The first sample after a resume or loading boundary is always zero elapsed.

## Tests required

Permutation of actor allocation/order gives same tie-resolved combat outcome; mutual lethal same-tick hits trade; already-dead shooter never fires; boundary reload+switch cancels transfer; equip completion+fire works; two display frames per tick and four ticks per frame consume edges once. Different visual rig/update rates cannot affect authoritative capsules.
