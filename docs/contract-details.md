# Contracts — C0 specification

C0-v1 is frozen under `UnityProject/Assets/Shooter/Contracts`, compiled independently through `Tools/Contracts/Shooter.Contracts.csproj` and inside the Unity client. Root `contract-lock.json` pins source, metadata, specification-v1.1 fixture bytes and toolchain; `Tools/verify_contract_lock.py` and the test wrappers reject unreviewed drift. Breaking changes need an [ADR](decisions.md) plus fixture migration ([ADR-014](decisions.md#adr-014--accepted-initial-c0-source-freeze-and-failure-capture-lifecycle)). Names and fields below are normative; allocation/layout optimizations may not change semantics. All DTO fields are public read-only to consumers; serializers use explicit construction; no reflection-based arbitrary type deserialization.

This page merges the former contracts overview (ports and envelopes) with the exact C0 freeze details. Product session records (Game-v1 and ADR-036/037/038 extensions) are separate in [session contracts](game-session-contracts.md). Production compiler formats are frozen separately under `Generation/Core` and `content-format-lock.json`. Original M0/M1 results and waivers remain in the M0 handoff and M1 handoff.

## Common rules

Pure DTOs use BCL primitive fields and project Float2/Float3/Quat structs; no UnityEngine, GameObject, Transform, NativeArray or transport-specific types cross the Contracts boundary. Read-only arrays are immutable for the lifetime of a published snapshot; use buffer leases when pooling. Every async operation accepts CancellationToken, yields an explicit result/error, and releases partial resources on cancellation. Only adapters create Unity resources. Never pass engine objects to workers.

EntityId = nonzero uint64, match scoped. ContentId = lowercase 64-hex SHA-256. RecipeId = stable lowercase slash-separated name. Tick = uint32 with wrap-aware comparison and session length below 2^31 ticks. Sequence = uint32, same wrap rule. Seed = uint64. EventId = (authoritySessionId:uint64, tick:uint32, entityId:uint64, ordinal:uint16). Ordinal overflow is an explicit error. IDs are serialized field-by-field, never native struct memory.

## Generation and content ports

```csharp
// Port signatures; DTO declarations are described below.
interface IRecipeValidator { ValidationReport Validate(RecipeDocument recipe); }
interface IAssetGenerator {
    GenerationResult Generate(GenerationRequest request, CancellationToken cancel);
}
interface IContentStore {
    Task<ContentLeaseResult> AcquireAsync(ContentId id, ContentKind kind,
        CancellationToken cancel);
}
interface IWorldQueries {
    SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter);
    RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter);
    OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned);
}
interface INavigation {
    PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision);
    PathResult Poll(PathTicket ticket, PathBuffer callerOwned);
    void Cancel(PathTicket ticket);
}
```

`GenerationRequest`: recipeId, recipeSchema:uint32, generatorId, generatorVersion, seed:uint64, canonicalParameters:bytes, dependencyContentIds:sorted list, outputPlatform, visualProfile, toolchainHash. GenerationResult contains immutable arrays of vertices/indices/submeshes, optional packed PBR planes, skeleton/weights, semantic tags, collision primitives, bounds, metrics and diagnostics. Result is not an engine Mesh. Vertex layout is position float3, normal float3, tangent float4, UV float2; optional joints uint16×4 and normalized weights float4. Bounds must be conservative and finite. Index width is explicit per artifact.

ContentLeaseResult is Success(lease) or Failure(errorCode, contentId, safeMessage). Lease exposes validated read-only artifact data and Release/Dispose, with reference counting at store. Acquiring the same ID coalesces IO; cancellation removes only that consumer. Store enforces decompressed size, file count and total residency limits before allocation. Never return a partial successful lease.

Queries return explicit NoHit, Hit or BufferOverflow. Hits contain point, normal, distance, entityId or staticSurfaceId, surfaceTag and collisionRevision. Filters use layer mask, ignored entity, team-independent collision group and trigger policy. Hit ties within 0.1 mm resolve by collider stable ID. Authoritative adapter checks validity and overlap recovery. Navigation Pending/Complete/Partial/Failed/Stale statuses are explicit; revision mismatch triggers replan and a safe hold command.

## Simulation ports

```csharp
interface ISimulation {
    StepResult Step(uint tick, ReadOnlyMemory<InputCommand> commands);
    SnapshotLease AcquireSnapshot();
    EventLease AcquireEvents();
}
interface ICommandSource { DrainResult Drain(uint tick, CommandBuffer destination); }
interface ISnapshotConsumer { void Consume(SnapshotLease snapshot, EventLease events); }
interface INetworkChannel {
    bool TrySend(PeerId peer, Delivery delivery, ReadOnlyMemory<byte> payload);
    DrainResult Poll(PacketBuffer destination);
}
```

Simulation Step is called once per increasing tick and returns state hash for diagnostic replay, authoritative events and counters. It uses injected clock/world/nav configuration; no wall clock/random globals inside rules. It cannot promise bitwise cross-machine physics results. Commands sorted by entity then sequence; at most one combined movement command per actor/tick. Repeated fire edges with the same sequence are ignored. Tick order and restore state are in the [simulation protocol](simulation-protocol.md).

InputCommand fields: entityId, clientTick, sequence, moveX/moveY in [-1,1], aimYaw/aimPitch radians, buttons bitmask (fire held, fire pressed, reload pressed, sprint held, crouch held, jump pressed, aim held), selectedSlot byte. Aim pitch clamped to ±89 degrees converted to radians. Reject NaN/infinity, oversized movement, stale/far future tick and unauthorized entity ownership. Server supplies authoritative tick and validates input/rate limits. Baseline desktop mouse has no arbitrary angular speed clamp; finite/bounds validation still applies. Buttons do not embed damage, hit result, position or ammunition.

EntitySnapshot fields: ID, position Float3, rotation yaw radians, velocity Float3, stance enum, health uint16, team byte, active weapon ID, WeaponState, movement phase scalar and aim pitch. HitVolumeSnapshot contains stable body-region IDs, capsule endpoints/radius and tick, computed by a shared gameplay pose evaluator independent of visual rig LOD. SimulationSnapshot includes tick, worldRevision, entity array, event high-water mark and match status. Events describe accepted actions; views deduplicate by EventId.

WeaponState action enum: see the registry above; fields ammoInMagazine:uint16, reserve:uint16, nextAllowedFireTick, phaseStartTick, phaseEndTick, shotIndex:uint32. CombatEvent variants ShotAccepted, ProjectileSpawned, Impact, DamageApplied, ReloadStarted, ReloadCompleted, Death. Authoritative shot trace has origin, aim, seed, weapon definition ID and collision revision; impact gives surface tag, point and normal. Presentation never feeds hit animation back into damage.

## Primitive values and enum registry

Float2=(x,y:float32), Float3=(x,y,z:float32), Quat=(x,y,z,w:float32); identity quaternion (0,0,0,1), normalized within 1e-4. Vectors must be finite. IDs are value wrappers: EntityId/SurfaceId/PeerId/NavTicketId uint64 (0 invalid), ContentId 32 bytes rendered 64 lowercase hex, DefinitionId ASCII recipe-style string ≤128 bytes. Tick/Sequence uint32. EventOrdinal uint16; AuthoritySessionId uint64 nonzero. Array counts uint32 on disk, bounded int at allocation.

| Enum | Explicit values |
|---|---|
| QueryStatus | NoHit=0, Hit=1, BufferOverflow=2, InvalidInput=3 |
| Stance | Standing=0, Crouching=1, Dead=2 |
| WeaponAction | Ready=0, FiringRecovery=1, Reloading=2, Equipping=3, Disabled=4, Cooking=5 (grenade, ADR-057) |
| ContentKind | Mesh=1, Material=2, Skeleton=3, Collision=4, Navigation=5 |
| PathStatus | Pending=0, Complete=1, Partial=2, Failed=3, Stale=4, Cancelled=5 |
| MatchState | Loading=0, Ready=1, Playing=2, Won=3, Lost=4, Stopped=5 |
| CombatEventKind | ShotAccepted=1, ProjectileSpawned=2, Impact=3, DamageApplied=4, ReloadStarted=5, ReloadCompleted=6, Death=7, MatchChanged=8 |
| ErrorCode | None=0, InvalidInput=1, UnsupportedVersion=2, MissingContent=3, CorruptContent=4, LimitExceeded=5, Cancelled=6, StaleRevision=7, Busy=8, InternalError=9 |
| InputButtons | FireHeld=1, FirePressed=2, ReloadPressed=4, SprintHeld=8, CrouchHeld=16, JumpPressed=32, AimHeld=64 |
| Delivery | UnreliableSequenced=0, ReliableOrdered=1 |

Unknown enum value is invalid unless its containing versioned optional field explicitly permits skipping. Unknown button bits reject command. SelectedSlot 0 rifle / 1 pistol / 2 knife / 3 grenade; other values invalid (ADR-057 / prediction schema 2). Event kind payload types are explicit, not runtime C# type names.

## Physics query DTOs

Capsule=(bottomSphereCenter,topSphereCenter:Float3,radius:float); both endpoints are world-space, radius>0. QueryFilter=(layerMask:uint32,ignoredEntity:EntityId,includeTriggers:bool). Collision groups use the shared layer matrix; team does not change collision filtering. QueryHit=(distanceMeters:float,point:Float3,normal:Float3,entityId:EntityId,staticSurfaceId:SurfaceId,colliderStableId:uint64,surfaceTag:uint16,collisionRevision:uint32). Exactly one of entityId/staticSurfaceId nonzero for a hit.

RayHit and SweepHit both contain status:QueryStatus, hit:QueryHit. Hit field is only valid on status Hit. For raycast direction must be unit within 1e-4 and maxMeters>0. Capsule sweep displacement may be zero: perform no movement, return NoHit; overlap recovery uses Overlap explicitly.

OverlapResult=(status:QueryStatus,writtenCount:int,requiredCount:int). IWorldQueries.Overlap returns OverlapResult, never just an integer. Caller supplies HitBuffer with fixed capacity. Complete no-hit: NoHit/0/0; complete hits: Hit/n/n; overflow: BufferOverflow/capacity/total found. Partial results on overflow are diagnostic only, never authoritative gameplay. Adapter must count all hits within independent hard cap 256; if >256, requiredCount=257 sentinel and reject gameplay query. Caller retries once up to cap or fails safely. Sort complete results by colliderStableId. Closest ray ties quantize distance to 0.1 mm using midpoint-away-from-zero, then sort by stable ID; this avoids nontransitive epsilon comparators.

PathTicket=(id:uint64,actor:EntityId,navRevision:uint32). PathBuffer holds Float3 corners and fixed capacity. PathResult=(status:PathStatus,writtenCount:int,requiredCount:int,error:ErrorCode). Poll returns PathResult; insufficient corner capacity gives Failed/LimitExceeded with zero usable corners, not a fake complete path. Maximum 128 corners; Pending has zero counts. Failed/stale/cancelled consumes ticket; caller must request a new one. Queries/navigation adapter calls main-thread only.

## Simulation DTOs

InputCommand=(entityId:uint64,clientTick:uint32,sequence:uint32,moveX:float,moveY:float,aimYaw:float,aimPitch:float,buttons:uint16,selectedSlot:byte). Aim is raw player intended orientation; authoritative recoil offsets are separate state. move vector length≤1 after normalization by input adapter; reject components beyond [-1,1] rather than trusting client normalization. Yaw normalized [-pi,pi); pitch clamped ±89 degrees at input and rejected outside bounds on receive. No arbitrary angular speed cap for desktop mouse in offline baseline; server finite/rate/bandwidth checks still apply.

WeaponState=(definitionId:DefinitionId,action:WeaponAction,ammoInMagazine:uint16,reserve:uint16,nextAllowedFireTick:uint32,phaseStartTick:uint32,phaseEndTick:uint32,shotIndex:uint32,previousFireHeld:bool). Mechanical reload progress derives from start/end tick. Equip state for each slot is retained in actor state. See [complete prediction state](simulation-protocol.md).

EntitySnapshot=(id:EntityId,position:Float3,yaw:float,velocity:Float3,stance:Stance,health:uint16,team:byte,activeSlot:byte,weapon:WeaponState,locomotionDistanceMeters:float,aimPitch:float,gameplayPoseVersion:uint32). HitVolume=(entityId:EntityId,regionId:uint16,capsule:Capsule,tick:uint32). Region IDs Head=1,Torso=2,LeftArm=3,RightArm=4,LeftLeg=5,RightLeg=6. SimulationSnapshot=(tick:uint32,worldRevision:uint32,matchState:MatchState,eventHighWater:EventId,entities:read-only EntitySnapshot[],hitVolumes:read-only HitVolume[]). Arrays sorted by entity then region ID. Empty snapshots use zero EventId sentinel only for high-water before first event.

CombatEvent=(id:EventId,kind:CombatEventKind,instigator:EntityId,target:EntityId,payload:one of explicit payload DTOs). Event tick is in EventId, never contradictory payload tick. ShotPayload=(weaponId,shotIndex:uint32,origin:Float3,unitDirection:Float3,seed:uint64,collisionRevision:uint32); ImpactPayload=(position,normal:Float3,surfaceTag:uint16,regionId:uint16); DamagePayload=(amount:uint16,remainingHealth:uint16,regionId:uint16); ActionPayload=(slot:byte,startTick,endTick:uint32); DeathPayload=(killer:EntityId); MatchPayload=(oldState,newState:MatchState,reasonCode:uint16). ProjectileSpawned reserves ProjectilePayload=(projectileId:EntityId,origin,velocity:Float3,definitionId:DefinitionId); not used by hitscan slice. No arrays of arbitrary object payloads.

StepResult=(tick:uint32,stateDigest:ContentId,eventCount:int,counters:StepCounters). StepCounters=(commandsAccepted,commandsRejected,shots,damageEvents,aiRays,pathRequests:int,overload:bool). Digest hashes quantized diagnostic state, not cross-platform physics proof. Step preflights capacity before state changes and allocates no consumer-owned snapshot. ISimulation.AcquireSnapshot returns SnapshotLease; ISimulation.AcquireEvents returns EventLease for last completed tick. Remove ReadSnapshot from public port. Consumers acquire their own leases, Dispose idempotently, and cannot retain raw arrays after disposal. ISnapshotConsumer.Consume borrows passed leases for the call only; caller disposes. Retention requires explicit store Acquire, not a hidden reference.

## Buffer, thread and failure policy

Simulation/world/query methods main-thread-only. Pure generation may run worker threads with per-request RNG. Network poll/codec may run workers but forwards immutable commands via bounded queue; it never calls simulation there. Content IO/decompression worker, install/release Unity resources main thread. Dispose of a content lease from another thread enqueues main-thread finalization; never destroys Unity objects on worker.

Pool at most eight snapshot buffers and eight event buffers, ≤64 actors, six hit volumes/actor, 4096 combat events/tick, 256 commands/tick globally and 128 queries' hits/caller buffer initially. A command drain returns DrainResult(error:ErrorCode,writtenCount,requiredCount); no silently dropped commands on overflow. ICommandSource.Drain and INetworkChannel.Poll use this explicit result. Poll packet buffer bound 256 packets; TrySend=false means caller applies bounded backpressure policy (cosmetic drop, reliable retry queue with cap). Payload ownership remains caller until synchronous TrySend returns; adapter must copy/enqueue its own bytes before true.

If snapshot buffers exhausted, stop new step publication and report Busy; do not overwrite live leased memory or allocate unboundedly. If event capacity exceeded, fail tick/session with diagnostic, never silently omit damage/death. Test runner deliberately holds leases to exercise exhaustion. Fatal adapter exceptions become InternalError at composition boundary; ordinary invalid user/content input uses result errors.

## Content and generation DTO completion

RecipeDocument=(recipeId,schemaId,generatorId,generatorVersion:string,seed:decimal uint64 string,parameters:canonical JSON bytes,dependencies:sorted ContentId[]). ValidationReport=(valid:bool,issues:Diagnostic[]), Diagnostic=(code:ErrorCode,path:string,message:string), ≤128 issues each ≤512 chars. GenerationRequest adds outputPlatform:string,visualProfile:string,toolchainHash:ContentId. GenerationResult=(error:ErrorCode,artifacts:GeneratedArtifact[],bounds:Aabb,diagnostics:Diagnostic[]). Each GeneratedArtifact=(kind:ContentKind,variant:string,formatVersion:uint32,payload:read-only bytes,sha256:ContentId). Typed mesh/material/skeleton payload layouts are schema-versioned compiler formats; do not expose engine objects. Aabb=min/max Float3 with min≤max.

C0 requires typed in-memory MeshData=(positions:Float3[],normals:Float3[],tangents:Float4[],uv:Float2[],indices:uint32[],submeshes:Submesh[],joints:UInt16x4[] optional,weights:Float4[] optional), Submesh=(firstIndex,indexCount:uint32,materialSlot:uint16). Mesh array lengths agree; no binary native-struct dumping. SkeletonData=(joints:JointDefinition[]), JointDefinition=(id:uint16,parentId:uint16,localBindPosition:Float3,localBindRotation:Quat,minAngles,maxAngles:Float3); parent 65535=root sentinel. MaterialData=(width,height:uint16,channelFormat:string,mipBytes:byte[][]). CollisionData contains explicit tagged Capsule/Box/HeightGrid DTO lists. NavigationData is opaque validated adapter blob plus format/version/settings hash, not arbitrary serialized engine object.

ContentLease exposes ContentId, Kind, read-only verified payload and Dispose. ContentLeaseResult=(error:ErrorCode,lease:ContentLease|null,diagnostic:string). Only success has nonnull lease; payload max 64 MiB/artifact, manifest max 1 MiB, ≤256 artifacts/content and ≤256 MiB decompressed/content. IContentStore cancellation returns Cancelled with no lease; callers must not receive both success and cancellation. One request error must not dispose another consumer's lease.

## Freeze artifacts

New product/session records for M8–M11 are specified separately in [Game-v1](game-session-contracts.md). They do not extend C0 MatchState to represent Draw, repurpose Won/Lost into team outcomes, or promise N1 team-match wire compatibility. The integrator must compile/freeze that separate assembly and executable fixtures in T035 before dependent workers start; this prose plan leaves the existing lock unchanged.

At F02 implement all core port/DTO types and negative/lease tests; at P02 freeze exact typed binary geometry formats before worker adapters merge. Network wire schema would freeze at N01 after transport selection; networking is parked under ADR-076. Do not force future codecs into M0 or leave motor/prediction state unspecified until M5. [Fixtures](fixtures/index.md) provide independent examples, not an exhaustive replacement for C# contracts.

P02 now implements the initial field-by-field compiler codecs in `Generation/Core/Codecs`, separately frozen by root `content-format-lock.json`; see [format v1](content-format.md). C0 DTO/port bytes and specification fixtures are unchanged.

## Wire and disk envelopes

**Network part parked (ADR-076, 2026-11-16).** The first version is local-only with bots; the network wire schema below is retained as design but has no active work, CI lane or N01 freeze scheduled. The disk envelope is live and frozen by `content-format-lock.json` ([format v1](content-format.md)).

Both use magic, schemaVersion, payloadLength and checksum/hash, but disk and network codecs are separate. Disk header includes endianness (little), compression ID, compressed/uncompressed lengths, payload SHA-256, generator/toolchain hashes. No BinaryFormatter, runtime type names or arbitrary object construction. Disk payload max sizes are declared in manifest and independently bounded by loader policy.

Network initial proposed cap: 1200-byte application datagrams including application headers; verify transport overhead/fragmentation at M5. Header: protocol:uint16, messageType:uint8, flags:uint8, session:uint64, sequence:uint32, ackSequence:uint32, ackBits:uint32, payloadBytes:uint16. Reliable channel sequencing may be delegated to transport; do not duplicate ACK logic when transport provides it. Message types: Hello/Manifest, JoinResult, InputBatch, SnapshotDelta, EventBatch, ResyncRequest, Disconnect. Unknown required fields/schema cause explicit incompatibility; optional versioned fields are length-delimited.

Positions encode signed int32 centimeters relative to chunk origin, velocity int16 cm/s, yaw/pitch uint16 normalized ranges; clamp overflow and request full state, never wrap. Local prediction retains full float state. Define per-field round-trip tolerances (position ≤0.01 m, angle ≤0.006 degrees, velocity ≤0.01 m/s). Bounds and packet fuzz tests precede network exposure. Snapshot baseline tick and ack identify deltas; if absent request reliable bounded full state. No unbounded network allocations.

## Contract tests required

Compile pure assemblies without Unity; validate schema fixtures; reject invalid values; serialize/deserialize boundary IDs and tick wrap; test lease release/cancellation; adapter conformance with fake world; replay duplicate and reordered commands; missing nav revision; binary length/hash attacks; snapshot/event lifetime under pooling. Cross-language tools consume JSON schema fixtures with the same semantic constraints. See [validation](validation.md).
