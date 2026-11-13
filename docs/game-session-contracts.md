# Game session contracts — planned Game-v1 freeze

Status: normative design for T035; **not implemented or frozen**. Owner: sole M8 integrator. Existing C0-v1, SGAF/content v1 and N1-v1 remain unchanged by this documentation task. Read [contract details](contract-details.md), [simulation order](simulation-protocol.md) and [release plan](playable-release-plan.md). Future workers consume the compiling freeze on `main`, not signatures guessed from this prose.

## Placement and compatibility

Proposed `UnityProject/Assets/Shooter/Game/Contracts/` contains a new BCL-only `Shooter.Game.Contracts` assembly and pure build project, referencing C0. It owns immutable product/session DTOs and ports; no Unity objects, storage implementation, UI toolkit or network codec. Integrator creates asmdefs/project references and a separate Game-v1 source/fixture registry. Test-only fakes live outside shipping assemblies. Do not add fields or enum values to frozen C0 to express results/draws.

At T035 accept the implementation ADR and executable Game-v1 fixtures, with schema IDs, exact canonical encodings, bounds, digest rules and migration/rejection tests. Proposed independent files: `game-contract-lock.json`, `docs/game-fixtures/`, `docs/game-schemas/`; these do not exist yet and must not be reported as delivered. Existing fixtures stay immutable. Any need to change C0, SGAF, generator identities, nav encoding or N1 requires a separate reviewed ADR, fixtures/migration and impacted-suite validation before dependent workers rebase. New settings file schema is independent of `HudSettings`' existing semicolon diagnostic string.

## Values and immutable records

| Proposed value / record | Required semantics and bounds |
|---|---|
| MapId / ModeId | ASCII lower-case identifiers, 1–64 bytes; fixed initial maps `training-compound`, `depot-yard`; mode `team-deathmatch`; unknown IDs reject before load |
| ParticipantId / LifeId | ParticipantId uint32 nonzero, stable for a match; LifeId uint32 nonzero, increasing per participant; each life maps to a fresh C0 EntityId, never reused within the authority session |
| GameMatchPhase | Planned explicit byte values Loading=0, Ready=1, Playing=2, Finished=3, Stopped=4; pause is a local session service state, not a C0 MatchState enum addition |
| MatchOutcome / FinishReason | Outcome None=0, Team1=1, Team2=2, Draw=3; reason None=0, ScoreLimit=1, TimeLimit=2, Aborted=3; aborted sessions do not produce a victory |
| SessionRequest | MapId, ModeId, teamSize (5 or 6), seed uint64, ruleVersion=1; limits fixed 150 kills / 54000 active ticks; one local participant on Team 1; cannot carry user paths or arbitrary recipes |
| MapCatalogEntry | MapId, display name ≤64 characters, short description ≤256, supported ModeIds, supported team sizes, fixed recipe seed/version, gameplay root hash, per-profile visual roots; ≤16 maps, ≤8 modes/map; release requires exactly the two approved entries |
| MapGameplayDefinition | MapId, collision/nav revision+hash, bounds, team-tagged spawn slots and patrol/combat destinations; ≤128 spawn slots, stable nonzero IDs; all coordinates finite and in map bounds; nav encoding is versioned, verified content |
| ParticipantState | ParticipantId, display name ≤32 characters, Team byte 1/2, isHuman, current EntityId/LifeId, alive, kills/deaths uint32, respawnDueActiveTick; bounded ≤12 participants |
| MatchViewSnapshot | AuthoritySessionId, completed simulation tick, activeTicksElapsed, remaining ticks, phase/outcome/reason, two team kill counts, immutable participant rows, pending respawns; view reads one coherent publication |
| DeathResolution | AuthoritySessionId, C0 death EventId, victim EntityId/LifeId, optional killer EntityId, completed tick; adapter resolves ParticipantIds using that tick's mapping before retiring the victim |
| MatchResults | Request/catalog/gameplay/build identities, session ID, start/end ticks, active duration, scores, outcome/reason and immutable final rows sorted per feature rules; absent until Finished; never reconstructed from HUD labels |
| UserSettings | settings schema=1; audio, video, controls, accessibility groups with [shell ranges](features/game-shell.md); no gameplay tuning or match score mutation |

Use leased, read-only publication for per-tick data with the existing eight-buffer backpressure semantics; consumers must not retain borrowed arrays. Final results are an independent bounded immutable copy that survives session teardown. T035 fixtures pin the exact DTO layout and ownership APIs; strings/counts validate before allocation, unsupported versions fail with a stable user-facing error plus diagnostic code.

For product authority's C0 snapshot compatibility, map GameMatchPhase Loading/Ready/Playing to the same-named C0 values, and Finished/Stopped to C0 Stopped. Team victory/draw is carried only by Game-v1 outcome/results; it never reinterprets C0 Won/Lost. Compound Clear keeps its existing mapping. Validate SessionRequest seed against the selected packaged catalog entry; the release menu cannot request arbitrary seed generation or content paths.

## Ports to compile at T035

Names below are planned, not claims that these interfaces already exist. The freeze includes concrete method signatures, capacity/error returns and adapter-conformance tests.

| Port / owner | Responsibility and lifetime |
|---|---|
| IGameSession / Bootstrap service | Single session: validate request, begin cancellable load, signal Ready, start, pause/resume, request abort, acquire game snapshot/results, stop/dispose. Calls on main thread; one terminal/teardown notification; no reentrant start. Owns authority/session epoch |
| IActorLifecycle / Simulation/Session | Spawn/despawn/reset a life at a tick boundary; expose authoritative actor/weapon state to match and AI through read-only data. Mutations only in authority tick; cannot be called by presentation. Capacity 12 active actors plus bounded cosmetic corpses; aborted spawn cannot half-publish |
| ITeamMatch / Simulation/TeamDeathmatch | Begin from validated roster/map, process ordered death resolutions once, evaluate full tick/deadline, schedule respawns and publish coherent match state; no file/Unity dependency |
| ITeamBotPlanner / Simulation/AI | Read completed authoritative state plus known/visible information, output next-tick normal InputCommands with persistent per-entity sequence numbers. Team-aware target set, round-robin bounded queries, life reset/cancellation; no direct health/ammo mutation |
| IMapRuntime / World/Runtime | Load/install verified collision+nav before Ready, expose real queries/path adapter, validate spawn candidates, unload/cancel fenced to leases; Unity install/dispose on main thread |
| IMapCatalog / Content/Maps | Read validated immutable packaged catalog and approved content identities; rejects missing/incompatible map/mode; cannot compile on behalf of a release player |
| IUserSettingsStore / Game/Settings | Load/validate/migrate settings, save atomically, report corrupt/newer schema/read-only storage without blocking play; storage path supplied by platform adapter, never a content-cache mutation |
| IClientSettingsApplier / Bootstrap client adapter | Apply view/audio/input settings, report unsupported choices, stage display change with rollback; cannot reference or mutate match/world gameplay rules |

Existing `CombatHarnessSimulation` is sealed with private actor state. T036 must first extract reusable authority/lifecycle code into planned `Simulation/Session/`, retaining the harness as a compatibility adapter and preserving C0 test outcomes. Do not make a parallel second damage system or let an M9 worker access harness internals with reflection. Integrator alone owns this extraction and edits to `Simulation/Foundation/`.

Retire dead actor records from authority snapshots before reusing bounded storage, while issuing a new EntityId for every new life. C0's 64-actor cap is not permission to retain every historical respawn actor forever. Death/life dedupe uses bounded per-participant state, not an ever-growing event set. Cosmetic corpse views are capped at four globally and expire after five active seconds; eviction cannot affect obstruction, hit tests or score. Aborted async work carries a session epoch and cannot leak a slot into the next match.

## Tick and publication boundary

Use the C0 simulation order. At the start of a playing tick install due safe spawns before movement/pose construction. Then resolve all shots and queued damage against the same pose set. Deliver death resolutions in EventId order, deduplicated by authority session+EventId and victim life. Update both scores/stats, increment active play count once, evaluate score/time finish once, schedule remaining respawns only if still playing, plan next-tick AI, publish combat and match snapshots coherently. No respawn or new command executes after Finished.

On pause do not step authority, advance active time, age AI or consume buffered fire edges. Resume resets wall-clock baseline and input edges without losing authoritative tick order. Loading and results never advance match time. Full stop cancels async load/nav, drains or invalidates leased data safely, detaches audio/input/views and disposes services once. Requests/completions carry session epoch; stale completion from map A cannot install after map B starts.

## Compatibility and fixture checklist

T035 includes valid 5/6 rosters; invalid 4/7 sizes/IDs/counts/NaN; score-limit trade; 53999/54000 deadline; zero-kill draw; duplicate death; participant life/EntityId mapping; safe-spawn deferral; stale-load completion; pause/resume clock; results retained after unload; settings corruption/newer version/display revert. Existing Compound Clear and N1 regression expectations stay unchanged. Game-v1 carries no remote wire compatibility promise; future networked TDM needs a separately frozen protocol extension.
