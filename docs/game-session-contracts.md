# Game session contracts — Game-v1 freeze

Game-v1 is implemented and frozen ([ADR-034](decisions.md#adr-034--accepted-initial-game-v1-freeze-t035), T035). It is extended additively by:
- ADR-036, which adds settings schema 2 and Keep/Revert staging;
- ADR-037 and ADR-101, which add workloads and team sizes;
- ADR-039, which adds local slide;
- ADR-105, which adds the pickup binding.

The source is under `UnityProject/Assets/Shooter/Game/Contracts/`, locked by `game-contract-lock.json` with fixtures in `docs/game-fixtures/`. C0, SGAF/content v1 and the parked N1 wire format are unchanged. Also read [contract details](contract-details.md) and [simulation order](simulation-protocol.md).

## Placement and compatibility

`UnityProject/Assets/Shooter/Game/Contracts/` contains BCL-only Shooter.Game.Contracts referencing C0. It defines immutable product/session DTOs and ports, without Unity/storage/UI/transport implementation. Shipping `OfflineGameSession` rejects `FakeMapRuntime` unless tests allow it.

Lock/fixtures: `game-contract-lock.json`, `docs/game-fixtures/`. Verify with `python Tools/verify_game_contract_lock.py` and `Tools/Test-Game.ps1 -Offline`. Existing C0 fixtures stay immutable. Any change to C0, SGAF, generator identities or nav encoding requires a separate reviewed ADR, fixtures/migration and impacted-suite validation before dependent workers rebase. New settings file schema is independent of `HudSettings`' existing semicolon diagnostic string.

## Values and immutable records

| Value / record | Required semantics and bounds |
|---|---|
| MapId / ModeId | ASCII lower-case identifiers, 1–64 bytes; fixed initial maps `training-compound`, `depot-yard`, `oil-depot`; mode `team-deathmatch`; unknown IDs reject before load |
| ParticipantId / LifeId | ParticipantId uint32 nonzero, stable for a match; LifeId uint32 nonzero, increasing per participant; each life maps to a fresh C0 EntityId, never reused within the authority session |
| GameMatchPhase | Explicit byte values Loading=0, Ready=1, Playing=2, Finished=3, Stopped=4; pause is a local session service state, not a C0 MatchState enum addition |
| MatchOutcome / FinishReason | Outcome None=0, Team1=1, Team2=2, Draw=3; reason None=0, ScoreLimit=1, TimeLimit=2, Aborted=3; aborted sessions do not produce a victory |
| SessionRequest | MapId, ModeId, teamSize (5, 8, 10 or 15; ADR-101), seed uint64, ruleVersion=1; limits fixed 150 kills / 54000 active ticks; one local participant on Team 1; cannot carry user paths or arbitrary recipes |
| MapCatalogEntry | MapId, display name ≤64 characters, short description ≤256, supported ModeIds, supported team sizes, fixed recipe seed/version, gameplay root hash, per-profile visual roots; ≤16 maps, ≤8 modes/map; release requires every map in the registry (`Content/Maps/<id>.map.json`, ADR-083) — currently three |
| MapGameplayDefinition | MapId, collision/nav revision+hash, bounds, team-tagged spawn slots and patrol/combat destinations; ≤128 spawn slots, stable nonzero IDs; all coordinates finite and in map bounds; nav encoding is versioned, verified content |
| ParticipantState | ParticipantId, display name ≤32 characters, Team byte 1/2, isHuman, current EntityId/LifeId, alive, kills/deaths uint32, personal Score uint32 (ADR-059/097; kill=100, headshot kill +20 / assist=HP; not team win), respawnDueActiveTick; bounded ≤30 participants (`GameContractVersion.MaxParticipants`) |
| MatchViewSnapshot | AuthoritySessionId, completed simulation tick, activeTicksElapsed, remaining ticks, phase/outcome/reason, two team kill counts, immutable participant rows, pending respawns; view reads one coherent publication |
| DeathResolution | AuthoritySessionId, C0 death EventId, victim EntityId/LifeId, optional killer EntityId, completed tick; adapter resolves ParticipantIds using that tick's mapping before retiring the victim |
| MatchResults | Request/catalog/gameplay/build identities, session ID, start/end ticks, active duration, scores, outcome/reason and immutable final rows sorted per feature rules; absent until Finished; never reconstructed from HUD labels |
| UserSettings | settings schema=2 (`shooter.user-settings.v2`, ADR-036); master/effects/UI volumes, quality, resolution/display/VSync/FPS cap, hip FOV, mouse/invert, bindings, UI scale, reduced motion/flash, contrast, subtitles with [shell ranges](features/game-shell.md); schema-1 migrates; no gameplay tuning or match score mutation |

Use leased, read-only publication for per-tick data with the existing eight-buffer backpressure semantics; consumers must not retain borrowed arrays. Final results are an independent bounded immutable copy that survives session teardown. T035 fixtures pin the exact DTO layout and ownership APIs; strings/counts validate before allocation, unsupported versions fail with a stable user-facing error plus diagnostic code.

For product authority's C0 snapshot compatibility, map GameMatchPhase Loading/Ready/Playing to the same-named C0 values, and Finished/Stopped to C0 Stopped. Team victory/draw is carried only by Game-v1 outcome/results; it never reinterprets C0 Won/Lost. Compound Clear keeps its existing mapping. Validate SessionRequest seed against the selected packaged catalog entry; the release menu cannot request arbitrary seed generation or content paths.

## Ports compiled at T035

Concrete method signatures live in `UnityProject/Assets/Shooter/Game/Contracts/GamePorts.cs`. `Tools/Game.Contracts.Tests` covers the reducer, settings migration, workload and fake-map scenarios; it does not certify every lifecycle, thread or device requirement below.

| Port / owner | Responsibility and lifetime |
|---|---|
| IGameSession / Bootstrap service | Single session: validate request, begin cancellable load, signal Ready, start, pause/resume, request abort, acquire game snapshot/results, stop/dispose. Calls on main thread; one terminal/teardown notification; no reentrant start. Owns authority/session epoch |
| IActorLifecycle / Simulation/Session | Spawn/despawn/reset a life at a tick boundary; expose authoritative actor/weapon state to match and AI through read-only data. Mutations only in authority tick; cannot be called by presentation. Capacity 30 active actors plus bounded cosmetic corpses; aborted spawn cannot half-publish |
| ITeamMatch / Simulation/TeamDeathmatch | Begin from validated roster/map, process ordered death resolutions once, evaluate full tick/deadline, schedule respawns and publish coherent match state; no file/Unity dependency |
| ITeamBotPlanner / Simulation/AI | Read completed authoritative state plus known/visible information, output next-tick normal InputCommands with persistent per-entity sequence numbers. Team-aware target set, round-robin bounded queries, life reset/cancellation; no direct health/ammo mutation |
| IMapRuntime / World/Runtime | Load/install verified collision+nav before Ready, expose real queries/path adapter, validate spawn candidates, unload/cancel fenced to leases; Unity install/dispose on main thread |
| IMapCatalog / Content/Maps | Read validated immutable packaged catalog and approved content identities; rejects missing/incompatible map/mode; cannot compile on behalf of a release player |
| IUserSettingsStore / Game/Settings | Load/validate/migrate settings, save atomically, report corrupt/newer schema/read-only storage without blocking play; storage path supplied by platform adapter, never a content-cache mutation |
| IClientSettingsApplier / Bootstrap client adapter | Apply full UserSettings (audio/display/quality/FOV/input/accessibility), report unsupported choices, stage/confirm/revert display change with 15s real-time clock; cannot reference or mutate match/world gameplay rules. T059 menus and T061 audio bind devices to this port; pure `MemoryClientSettingsApplier` / `UserSettingsEditor` cover Apply/Cancel/defaults without Presentation ownership |

Existing `CombatHarnessSimulation` is sealed with private actor state. Simulation/Session/HarnessActorLifecycle.cs wraps the harness; T053 added `RetireActor` and monotonic IDs so it retires authoritative lives and resets cleanly, retaining diagnostic compatibility and C0 test outcomes. Do not make a parallel second damage system or let an M9 worker access harness internals with reflection. Integrator alone owns this extraction and edits to `Simulation/Foundation/`.

Retire dead actor records from authority snapshots before reusing bounded storage, while issuing a new EntityId for every new life. C0's 64-actor cap is not permission to retain every historical respawn actor forever. Death/life dedupe uses bounded per-participant state, not an ever-growing event set. Cosmetic corpse views are capped at four globally and expire after five active seconds; eviction cannot affect obstruction, hit tests or score. Aborted async work carries a session epoch and cannot leak a slot into the next match.

## Tick and publication boundary

Use the C0 simulation order. At the start of a playing tick install due safe spawns before movement/pose construction. Then resolve all shots and queued damage against the same pose set. Deliver death resolutions in EventId order, deduplicated by authority session+EventId and victim life. Update both scores/stats, increment active play count once, evaluate score/time finish once, schedule remaining respawns only if still playing, plan next-tick AI, publish combat and match snapshots coherently. No respawn or new command executes after Finished.

On pause do not step authority, advance active time, age AI or consume buffered fire edges. Resume resets wall-clock baseline and input edges without losing authoritative tick order. Loading and results never advance match time. Full stop cancels async load/nav, drains or invalidates leased data safely, detaches audio/input/views and disposes services once. Requests/completions carry session epoch; stale completion from map A cannot install after map B starts.

## Compatibility and fixture checklist

Fixtures cover valid rosters at every allowed size; invalid sizes/IDs/counts/NaN; score-limit trade; 53999/54000 deadline; zero-kill draw; duplicate death; participant life/EntityId mapping; safe-spawn deferral; stale-load completion; pause/resume clock; results retained after unload; settings corruption/newer version/display revert. Game-v1 carries no remote wire compatibility promise.

## ADR-039 — additive local slide intent and settings action

Owner-assigned T072 adds `Game/Session/LocalPlayerCommand` (frozen C0 InputCommand plus a local SlidePressed edge) to the human sampler, and `LocalMovementView` copied from authority for camera/rig stance/eye/slide presentation. These local application records are outside the frozen C0/N1 layouts; no new wire bit, Stance enum or packet field is added. Local authority requires a fresh accepted command for the current human entity and applies the request at its simulation tick.

Game-v1 has an additive optional Slide binding (default Key.F), combined with applied Sprint (default LeftShift). Settings schema stays 2: legacy 14-action bindings remain valid and normalize to include Slide, using Unbound if F was already assigned. The old valid fixture is retained for migration coverage, `user-settings-slide.valid.json` covers new defaults, and the reviewed Game-v1 lock inventories both. See [ADR-039](decisions.md#adr-039--accepted-local-authoritative-sprint-slide-t072) and [combat](features/combat.md).
