# Feature: combat AI

Bots live in `Simulation/AI`:
- `CombatAiDirector`
- `PerceptionSystem`
- `AimController`
- `FireDiscipline`
- `CoverFinder`
- `CoverReservationBoard`

They plan through the Game-v1 `ITeamBotPlanner` seam and emit ordinary C0 `InputCommand` values. They never touch animators or renderers, and they never call damage directly. Bots use the same ammo, cadence, aim and movement rules as the human ([combat](combat.md)). Team roster rules are in [Team Deathmatch](team-deathmatch.md).

**Tuning and determinism:**
- All tuning is data in `Simulation/AI/AiTuning.cs`.
- Per-bot variation comes from the `AiNoise` SplitMix hash of entity id, tick bucket and salt. There is no RNG, so the same recording produces the same commands.
- Shipping composition (`CompositionPolicy.RequireReal`) rejects test fakes.

## Budgets

Every bot shares these global per-tick limits:
- ≤16 vision rays (cover probes and peeks count toward this; at most 4 probe rays per tick);
- ≤2 new path solves;
- a 30-tick replan cooldown.

When the budget runs out, sensing is deferred and its age is tracked. A hidden target is never marked visible. Every bot gets a sensing opportunity within 12 ticks.

Some checks are not vision rays and do not count against the budget:
- gunfire occlusion (a direct ear→source ray);
- ledge probes.

Footstep occlusion uses leftover budget, at most once per bot every 12 ticks.

## Behaviour

**State machine:** Idle/Patrol → Investigate → Engage → SeekCover → Reload/Reposition → Search → Patrol, with Dead as the terminal state. Each bot's blackboard holds:
- its goal;
- the last seen and last heard enemy positions and ticks;
- its confidence;
- its cover claim;
- health and ammo;
- its path ticket;
- a stuck timer.

### Perception

Bots use distance, a 120° FOV and authoritative occlusion rays to canonical target points. A target seen within the last 12 ticks is held through ticks the budget starves. A real "not visible" answer ends it ([ADR-112](../decisions.md#adr-112--accepted-crosshair-true-hits-rig-aligned-hit-volumes-and-bots-that-use-map-cover-flank-and-move-smoothly)).

### Hearing

Stimuli are consumed at t+1.

- **Gunfire** is heard within 45 m. The heard position carries 8% distance noise.
- **Footsteps** are heard from any direction: within 24 m when the enemy is sprinting, 13 m when running and 5 m when walking. Within 2.5 m they are always heard. Walls occlude footsteps beyond 2.5 m.
- **Damage from an unengaged attacker** turns the bot to face it.

Which stimulus wins:
- Ally gunfire points at that ally's last-seen enemy. A human teammate's gunfire is ignored.
- At most 2 allies respond to one noise.
- A fresh stimulus beats an older last-seen point ([ADR-054](../decisions.md#adr-054--accepted-bot-footstep-hearing-wider-vision-and-a-readable-lethality-envelope-t085), [ADR-070](../decisions.md#adr-070--accepted-bot-tactics-tranche-cover-peek-wall-hearing-ledge-strafe-route-spread-t090)).

### Aim and reaction

**Turning.** Turning has a bounded rate (about 420°/s) and bounded acceleration. The aim error glides between seeded points.

**Error:**

| Error | Value |
|---|---|
| Acquisition miss | 1.5 m, recovering over 1 s from the end of the reaction delay |
| Settled miss | 0.28 m + 0.5 cm/m |
| Own-movement penalty | 0.18 m |
| Flinch after being hit | Adds error for 0.5 s |
| Per-bot skill factor | 0.8–1.25 |

**Reaction.** A cold acquisition takes 300–600 ms, plus up to 200 ms at long range. A target switch takes 133–216 ms. A target seen again within 0.5 s keeps its state.

**Lethality.** `BotLethalityTests` measure the median time to kill a still target from sighting: about 1.6 s at 8 m, 2.5 s at 20 m and 3.9 s at 40 m.

### Trigger

A burst starts when the aim is within about a torso half-width (at least 1.5°). It continues while the aim stays within 2.5× that.

| Range | Fire pattern |
|---|---|
| Under 12 m | Sustained fire |
| Mid range | Bursts of 3–6 rounds |
| 30 m and beyond | Crouched taps of 1–2 rounds |

Weapon-specific rules:
- Shotguns (`PelletCount > 1`) never fire past the gun's falloff end.
- Precision rifles (ADS spread under 0.1°) count as long-range guns ([ADR-118](../decisions.md#adr-118--accepted-m4a1-mk-16-m24-sws-and-m870-pellets-shell-reloads-and-geometryweapon-150)).

### Targeting

The current target is sticky, and a recent attacker is checked first. Every 20 ticks, staggered per bot, one other candidate is scanned. It replaces the current target only if it is at least 30% closer.

### Movement

**Engaged.** Bots strafe within ±3 m of where the fight started. The strafe direction reverses on a seeded 0.4–1.2 s timer, at the leash, or when the bot is blocked. Bots back off below 4 m and close in beyond 55 m. Bots whose entity ids are divisible by 3 crouch-shoot at mid range.

**Smoothing:**
- Direction changes ease over about 3 ticks.
- Routes bend through corners.
- A ledge probe at ±0.75 m flips or cancels a strafe toward a drop of more than 0.55 m.

**Searching.** The last-seen point is projected along the target's velocity, by up to 1 s and 5 m. Bots face the threat and walk while searching within 30 m.

### Cover

**Choosing cover.** Authored package cover boxes reach the bots through `IMapCoverSource`. The bot picks the face away from the threat, nearest itself. Candidates are scored by:
- travel distance (≤18 m);
- hard cover;
- spacing from allies;
- never next to or past the threat.

Runtime probes are the fallback: up to 8 candidates 4–7 m away, preferring a clear left/right peek ([ADR-051](../decisions.md#adr-051--accepted-runtime-cover-probes-share-the-ai-ray-budget-t082)).

**When bots take cover:**
- after 2–4 s of trading fire in the open;
- below 35 health while losing the trade, with a 10 s cooldown;
- on an empty magazine.

**In cover:**
- Bots crouch, reload, then peek in a 1.5 s cycle and reposition.
- Claims use stable quantized ids. They are released on leaving cover, on path failure, on death and on cancel.

### Team

- **Callouts.** On first sighting, calm allies within 30 m get a callout.
- **No piling on.** Allies already pressing a contact count toward a crowd limit. Extra bots flank instead: they go to a lane point 8–30 m away and 45–135° off their own approach.
- **Destinations** are scored as follows:
  - legs of about 35 m are preferred;
  - each ally heading to a spot within 12 m costs −2;
  - a spot within about 9 m of an ally's path costs −1;
  - the own spawn is avoided;
  - the enemy side is preferred;
  - a seeded per-bot preference applies;
  - the last two picks are never repeated.

  On site maps the route corners form a patrol circuit through all three lanes ([maps](tdm-map-layouts.md#bots)).

### Reloads and loadout

- A tactical reload happens below 12 rounds after 2 s without contact.
- When all guns are empty, the bot uses the knife within about 2.5 m. Otherwise it uses its single grenade once, against a visible target 8–22 m away ([ADR-057](../decisions.md#adr-057--accepted-knife-and-grenade-loadout-slots-t087)).
- Bots draw random weighted primaries and sidearms each life.

## Fail-safes

**Stuck recovery.**
1. A bot is stuck when it makes less than 0.2 m progress over 2 s while it intends to move.
2. It first tries to jump, replan or change cover.
3. After three failures it sets `NeedsRelocation`, and `OfflineGameSession` moves it to a spawn-safety slot. Death, score and LifeId do not change.

**Paths.** Paths are retired when the nav revision changes. Reservations are released on death, despawn and cancel.

**Respawn.** A respawn rebinds only the replaced bot (`TryReplaceLife`).

**Counters.** `AiCounters` exposes:
- rays used;
- cover probes and peeks;
- bursts;
- stimuli;
- callouts;
- retreats;
- target switches;
- footsteps;
- flanks;
- visible holds.

## Acceptance

Seeded scripted scenarios must cover:
- an occluded enemy is never acquired;
- hearing leads to investigation;
- confidence decays;
- reloads never loop;
- no permanent double cover claim;
- an unreachable destination exits its retry within bounds.

**Full-roster runs** (TDM04–TDM05) keep the query caps, leak no reservation and keep the 1.5 ms mean AI CPU budget ([performance](../performance.md)).

**Known limits:**
- High ground, upper floors and lane identity are not published to bots.
- Peeking over tall cover is not modelled.
- A teleport counts as one fast footstep.

**Still Pending:** owner feel review in play.
