# Simulation order, replay and prediction state

Normative refinement of [architecture](architecture.md), [contracts](contract-details.md) and [combat](features/combat.md). Offline and dedicated authority use the same 60 Hz Step algorithm. Networking clients replay only own reversible state; they do not predict authoritative remote damage.

The objective/win/loss precedence below belongs to historical Compound Clear. New offline [Team Deathmatch](features/team-deathmatch.md) keeps steps 1–5 and replaces step 6's objective evaluation with the [Game-v1 batch/deadline/respawn publication order](game-session-contracts.md#tick-and-publication-boundary). Resolve the entire death batch, advance active time, finish once, then schedule later respawns/AI only if still playing. No local-player-loss shortcut or prior-mode no-respawn rule applies to TDM. Existing N1/Compound Clear tests remain unchanged.

## Tick boundary

Tick t consumes commands referring to tick t and reads finalized state t−1. At start: validate world readiness and tick monotonicity; apply queued spawn/despawn/load-boundary operations in stable ID order. Capture actor alive-at-start set and previous stimuli. AI decisions scheduled at end of t−1 produce commands for t; no bot can use a damage event from t before t+1.

1. Drain/validate one combined command per live actor, deduplicated by sequence. Retain previous FireHeld in state. Explicit FirePressed is accepted only on a rising edge not previously consumed; switching away cancels reload before any completion transfer this tick.
2. Process slot/stance requests and active action completion at boundary. Equip completion allows shooting this tick. Reload completion transfers ammo this tick unless cancelled by slot change/death at start. Disabled/dead actor commands ignored.
3. Move all live actors in ascending EntityId against static geometry. First slice actors do not physically block one another; actor-vs-actor damage capsules are query-only. Update grounded/stance/velocity and authoritative recoil decay. Update gameplay pose and canonical muzzle for every actor from post-movement state.
4. Capture current hit volumes and collect accepted shot intents for all alive-at-start actors. Validate cadence/ammo, advance shot index and compute spread from declared stream. Each accepted intent consumes ammo exactly once. Post-movement volumes and immutable static collision are used for normal hitscan; server historical volumes may replace target sample under N3 rules.
5. Resolve all shot queries against the same immutable volume set, ordered shooter ID then shot index. Queue damage; apply damage in that order after all queries. Same-tick lethal shots may trade because shots were collected before damage. Actor killed in tick t can fire its already accepted t intent, but cannot fire at t+1. Emit one death on first crossing to zero, ignore further health reduction at zero. No corpse damage target after t completion.
6. Resolve objective/win/loss (player death takes precedence over objective success in same tick), produce stimuli and AI plans for t+1. Publish finalized snapshot/events. Hit-history sample t contains post-movement capsules plus alive-at-start and alive-at-end bits for rewind eligibility; exclude actors not yet spawned at the historical time.

Order events by processing phase then actor ID then per-actor ordinal. Accepted shot, impact, damage, death and match events receive unique EventId. Limit guards abort tick/session explicitly; no partially committed tick. Implement staged state or rollback-to-prestep on failure; never continue after partial damage application.

## Missing input and clock

Offline generates one command every tick even if render frames lag; edge events consumed once. Multiplayer missing command holds last normalized move/aim/held fire ≤6 ticks, clears all edge buttons, then neutral movement/fire; automatic held fire during that grace still obeys ammo/cadence. Paused offline stops tick time and clears pending one-shot inputs on resume. Network pause affects local menu only, sends neutral input, server continues.

Offline catch-up max 4 ticks/display frame. In diagnostic, smoke, gallery and benchmark modes the remaining debt is retained, simulation freezes and overload is fatal; never clears an unprocessed tick debt silently there. The normal offline product client (ADR-046) instead drops the unserviced whole-tick debt, keeps the fractional remainder and tick order, logs one `SHOOTER_HITCH` warning with elapsed/dropped debt, and continues in real time. The first sample after a resume or loading boundary is always zero elapsed. Server has no render-frame dependency: scheduler wakes against monotonic deadlines, max 4 catch-up ticks per service loop, then logs overload and prioritizes critical work; sustained >1s lag invalidates benchmark and terminates test session with reason. Diagnostic event states exact debt.

## Complete own-player reconciliation state

PlayerPredictionState contains schemaVersion (2 after ADR-057), tick, actorId, world/collision revision and definition hash, position, velocity, yaw/pitch input, grounded flag, groundSurfaceId, groundNormal, stance, capsuleHeight, lastGroundedTick, jumpConsumedSequence, activeSlot, desiredSlot, four complete WeaponState records (rifle/pistol/knife/grenade), recoilYaw/Pitch radians and recoil angular velocities, locomotionDistanceMeters, lastProcessedSequence, lastButtons, lastValidCommandTick, alive, health, and action phase state. Step/slope temporary cast results are recomputed, not persisted. Static surfaces only; ground frame transform is not required until moving platforms are supported.

Weapon action start/end fields fully define equip/reload phase; no separate hidden action timer. Grounded state determines motor grounding, while visual foot contact remains cosmetic. No hidden mutable motor cache outside this state. Future persistent fields require schema bump and restore/replay test. Cosmetic camera springs/foot planting/sound do not enter gameplay state and reset/blend on correction. Prediction snapshots need not broadcast full private state to remote peers; server sends this exact state to owning client at reconciliation boundaries with relevant definitions/revisions.

Restore authoritative t state, discard inputs ≤acked sequence, replay t+1 through current predicted tick using saved commands and static collision revision. If revision differs, stop replay, request required content/full state and hold until compatible; do not replay against arbitrary new geometry. Record correction before visual smoothing. History retains256 ticks; older correction triggers full reset instead of reading overwritten data.

## N3 historical shots

**Parked (ADR-076).** Networking is parked; this section and the multiplayer missing-command rule above are retained design for a future multiplayer decision, not current gates.

Server-approved shot timestamp = clamped synchronized command time, max 200 ms rewind; impossible future/stale timestamps rejected, not an entitlement to choose targets. Use historical target capsules only, static cover remains current because immutable. Shooter origin comes from server stored shooter state at approved shot time when available; reject if missing rather than accept client origin. Ammo/cadence/alive eligibility is validated at processing tick under the same alive-at-start rule; a command arriving after shooter died in a prior tick is rejected. This deliberately permits same-tick trades but does not resurrect previously dead shooters.

## Tests required

Permutation of actor allocation/order gives same tie-resolved combat outcome; mutual lethal same-tick hits trade; already-dead shooter never fires; boundary reload+switch cancels transfer; equip completion+fire works; two display frames per tick and four ticks per frame consume edges once. Snapshot restore/replay reproduces own-player state within 1cm static map tolerance and exact ammo/event count. Different visual rig/update rates cannot affect authoritative capsules.
