# Architecture

## Current runtime versus intended architecture

The component/flow/lifecycle rules below remain the intended release architecture. Current Bootstrap loads persisted ADR-038 catalog/gameplay metadata through `StaticMapCatalog.TryLoadPublished`; `ProductMapView` and `Presentation/Rig/ProductActorPresentation` consume verified visual payloads. Native editor bake bridge and runtime NavMesh installer now exist, but `Compile-GameContent.ps1` still publishes software-grid NVB1 through a pure .NET process. Installer constructs NavMeshData at runtime from packaged triangulation and permits graph fallback; partial-path semantics/load cost/clearance need T067 review. See [audit findings C01–C03](product-state-audit.md). Copied Game-v1 snapshots remain distinct from C0 leased publication; do not relax intended architecture to hide these boundaries.

Read [vision](vision.md), [contracts](contract-details.md) and [repo layout](repo-layout.md) before changing boundaries.

Implementation status, reviewed 2026-11-14 (T059 shell + T061 audio + ADR-036 settings bind): default Bootstrap prepares OfflineGameSession/map services but does not auto-start a match; `ProductShellHost` owns menu→load→play→pause→results and binds `UserSettingsEditor` / `ProductClientSettingsApplier` to `shooter-user-settings.json`. Floor/box remains an explicit smoke/gallery/benchmark diagnostic. Client composition binds map renderers from packaged collision plus packaged soldier/rifle/pistol views driven by ActiveWeaponSlot and combat action events (T056/T067/T068; FIX05 player captures Pending); synthesized combat/footstep/UI events reach `UnitySynthAudioOutput` with schema-2 master/effects/UI gains (named device review Pending). Pause/input repaired under T054. See the [audit](product-state-audit.md) and [remediation plan](archive/release-remediation-plan.md). P05 remains FAIL; no fresh player/server-isolation or hardware acceptance is inferred.

Current paths include Game/Contracts, Game/Session, Game/Settings, Simulation/Session, Simulation/TeamDeathmatch, Content/Maps, World/Runtime and World/Foundation/CompoundMapRuntime.cs. T053 adds CombatHarnessSimulation.RetireActor and monotonic IDs; HarnessActorLifecycle retires authoritative lives, and OfflineGameSession rebinds human/bots. Actual-player evidence remains pending. The [next-build queue](next-playable-build.md) tracks remaining native publication/query validation, player/device proof and measurement acceptance; preserve one damage authority and read-only presentation.

## Components and dependency rules

| Layer / assembly or intended boundary | Owns | May depend on |
|---|---|---|
| Shooter.Contracts | IDs, immutable DTOs, schema constants, ports | BCL only; no UnityEngine |
| Shooter.Game.Contracts (frozen ADR-034) | Map/session/participant/results/settings records and ports; separate Game-v1 freeze | C0 + BCL only; no Unity or remote wire requirement |
| Shooter.Generation.Core | deterministic primitives, recipes, validation | Contracts; pure math adapter |
| Shooter.AssetCompiler.Editor | Unity conversion, LOD/material/nav baking, manifests | Generation.Core, Contracts, Unity Editor |
| Shooter.Content.Runtime | verified cache loading, residency, async handles | Contracts, BCL-only Generation.Core format helpers; future Unity runtime adapter |
| Shooter.Simulation | tick orchestration, motor rules, weapons, damage, AI decisions | Contracts; world-query interfaces |
| Shooter.World.Runtime | pure navigation compile/settings, revisioned path adapter, visual streaming scheduler, fake map runtime (`World/Runtime/**`) | Contracts, Game.Contracts |
| Shooter.World.Unity | PhysX collision queries, NavMesh adapter, compound map runtime (`World/Foundation`) | World.Runtime, Generation.Core, Content.Maps, Unity |
| Shooter.Presentation.Model | Unity-free view, HUD, menus, scoreboard, audio mixer and FX pool models (`Presentation/Model/**`); client-only by policy | Contracts, Game.Contracts, VectorMath |
| Shooter.Presentation | URP rendering, procedural rig, input, Unity audio/FX adapters, product session views | Presentation.Model, Contracts, Content.Runtime, Unity; reads simulation snapshots |
| Shooter.Networking (parked, ADR-076) | command/snapshot codec, prediction history, authority adapter; no Unity assembly references it while the product is offline-only | Contracts, Bootstrap.Core, Simulation |
| Shooter.Bootstrap | composition root, mode selection, tick driver | All runtime assemblies |
| Shooter.Tools / Tests | benchmarks, provenance and verification | Respective public APIs |

Every pure asmdef has a matching `Tools/<Assembly>/Shooter.<Assembly>.csproj` with the same name and references, and `Tools/verify_asmdef_graph.py` (fast CI lane) fails when the two graphs drift. The .NET lane therefore rejects a boundary violation that only Unity used to catch. Test projects reference these projects instead of linking source folders (ADR-076).

Contracts never reference features. Simulation does not reference renderers, animation, Editor APIs or transport packages. Presentation cannot apply damage or mutate authoritative state. Content compiler does not depend on simulation implementation. Server excludes presentation assembly and all graphics cache payloads. Jobs/Burst are optional optimizations after profiling, not a requirement to convert the entire game to ECS.

```mermaid
flowchart TD
  Definitions[Versioned numeric definitions] --> Core[Pure generators]
  Core --> Compiler[Editor asset compiler]
  Compiler --> Cache[Manifest and generated cache]
  Cache --> Loader[Runtime content loader]
  Loader --> World[Collision and navigation adapter]
  World --> Sim[Fixed tick simulation]
  Commands[Input or network commands] --> Sim
  Sim --> Snapshots[Read-only snapshots and events]
  Snapshots --> View[Render rig HUD and audio]
  Loader --> View
  Sim --> Network[Authority and snapshot transport]
```

## Offline frame and simulation loop

Input samples each display frame and accumulates button edges into commands for a 60 Hz fixed simulation clock. Each tick follows [simulation protocol](simulation-protocol.md): validate commands; process action/stance boundaries; move all actors; refresh gameplay hit poses; collect and resolve shots against one immutable pose set; apply queued damage; evaluate match; plan AI commands for next tick; publish leased snapshots/events. Same-tick shots may trade; dead-in-prior-tick shooters cannot fire. Physics queries are serviced by the world adapter on the main thread. Dynamic rigid bodies initially remain cosmetic or server-only; do not let asynchronous physics callbacks establish hit ordering.

Use a kinematic capsule sweep motor with explicit gravity, slopes, steps and overlap recovery. Fixed tick order is stable by entity ID. Dynamic interactions use world-query results from that tick. Rendering interpolates between completed snapshots; the local camera may add visual look and recoil without changing authoritative aim. AI plans produce commands through the same rules as players.

Tick catch-up is capped at four ticks per display frame. Offline pauses and exposes overload telemetry instead of silently dropping combat ticks; network server uses a separate monotonic service loop, logs overload, skips noncritical AI planning and terminates an invalid test session after sustained one-second debt. Network timestamps always refer to simulation ticks, never render frame numbers.

## Loading path

Bootstrap validates build manifest, settings and quality selection. Loader reads collision/navigation plus nearest visual chunk before spawning. Background threads perform bounded file read/decompression/pure generation; Unity object creation and GPU upload run on main thread under an upload budget. A chunk becomes playable only after collision and navigation are installed atomically. Display a loading boundary until then; never expose an empty collision region. Prefetch ahead of player movement and retain adjacent chunks.

## Boundaries and failure modes

- Missing critical collision/weapon/skeleton content: fail session load with diagnostic IDs. No random regeneration in a live match.
- Missing cosmetic LOD: use verified lower-detail cache; missing all visual variants uses a generated diagnostic shape only in developer builds.
- Corrupt cache: reject before deserialization, quarantine and rebuild in editor/tools; shipping clients offer repair/re-download, not arbitrary compilation.
- Generation and GPU work have cancellation, size limits and timeout telemetry. Results enter runtime only through validated manifests.
- Saves/replays record schema, build, generator versions, seed, inputs and relevant authority state. They are diagnostics, not cross-version deterministic gameplay promises.

## Ownership and lifecycle

Only Bootstrap creates services. Constructor injection or explicit factory parameters replace global mutable singletons. Content handles are reference-counted leases; dispose on chunk unload, scene change and cancellation. Simulation owns entity lifetime and emits despawn before views release resources. Unity objects are destroyed on main thread. Pool high-frequency combat events/FX and reuse snapshot buffers after consumer completion. See [contract lifetime rules](contract-details.md).

For the new game, the client shell requests validated sessions from Bootstrap; the session loads catalog/content through real map adapters, installs collision/nav before Ready, then feeds local/bot commands into shared authority. Match state and actor snapshots publish coherently at the completed tick. Views read them; settings affect only presentation/input devices. Team match owns scoring, active-time and respawn policy through the actor lifecycle seam, never by mutating HUD data. Final results are an immutable bounded copy that survives map unload. Pause stops offline authority and active-time; stale asynchronous load completions carry an epoch and cannot enter a later session. Details and failure cases live once in [Game-v1](game-session-contracts.md).

## Integration seams

The first vertical harness runs a generated box compound, motor and rifle through fake ports before real art. Every external service (transport, clock, storage, physics, navigation) has an adapter and in-memory test double. A host runs authority and view as separate consumers; avoid code paths that only work when server and local player share objects.

The P02 runtime format-helper dependency and initial codec freeze are recorded in [ADR-018](decisions.md#adr-018--accepted-initial-p02-compiler-format-and-byte-cache-boundary). Runtime never references `Shooter.AssetCompiler.Editor`; the compiler remains Editor/tools-only. [ADR-035](decisions.md#adr-035--accepted-verified-release-map-packages-t055-boundary) accepts the verified release map-package root/metadata/nav-adapter boundary for T055; software publication exists; data-only catalog/visual loading and native bake/install remain T067 / FIX04 work.

## Local slide intent and read-only movement view (ADR-039)

T072 adds a local `LocalPlayerCommand` envelope at the human sampler, retaining the frozen C0 InputCommand inside it and a separately buffered SlidePressed edge. OfflineGameSession validates the current human entity and sends the slide actor only with its normal command batch. CombatHarnessSimulation accepts the ability only when that actor has a fresh accepted command; KinematicMotor owns slide duration/momentum/cooldown and normal collision. N1 wire/prediction cannot originate this local-only ability.

The session's copied `LocalMovementView` reads authority stance/eye/slide state for ProductSessionView camera height and ProductActorPresentation procedural crouch/held-gait pose. Presentation never sets stance, translates the capsule or applies a speed boost. Existing C0 snapshot/hit poses remain authoritative and quality-independent; no new contract bytes or content format is introduced. Numeric rules and migration are in [combat](features/combat.md#local-sprint-slide--t072--adr-039) and [ADR-039](decisions.md#adr-039--accepted-local-authoritative-sprint-slide-t072).
