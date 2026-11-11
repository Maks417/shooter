# Shared interfaces and data contracts — version 1 proposal

Freeze these shapes in M0 through compilable C# contracts and fixtures before dependent work merges. The pseudocode below is normative about semantics; C# representation can use structs/classes without silently changing behavior. [Agents](agent-coordination.md) must propose an ADR for breaking changes.

Current implementation is C0-candidate.1 under `UnityProject/Assets/Shooter/Contracts`; `Tools/Test-Contracts.ps1` builds the same source as .NET Standard 2.1/C# 9 and executes pure fixture/conformance tests. Test-only diagnostic codecs and reference PRNG/encoding live under `Tools/Contracts.Tests`. They do not freeze production geometry/wire formats or implement T005. Adapter and Unity smoke acceptance still prevent a C0 freeze; see [handoff](milestones/m0-foundation/handoff.md).

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

Simulation Step is called once per increasing tick and returns state hash for diagnostic replay, authoritative events and counters. It uses injected clock/world/nav configuration; no wall clock/random globals inside rules. It cannot promise bitwise cross-machine physics results. Commands sorted by entity then sequence; at most one combined movement command per actor/tick. Repeated fire edges with the same sequence are ignored. Exact enums, DTOs, buffer results, leases and thread rules are specified in [contract details](contract-details.md); tick order and restore state in [simulation protocol](simulation-protocol.md).

InputCommand fields: entityId, clientTick, sequence, moveX/moveY in [-1,1], aimYaw/aimPitch radians, buttons bitmask (fire held, fire pressed, reload pressed, sprint held, crouch held, jump pressed, aim held), selectedSlot byte. Aim pitch clamped to ±89 degrees converted to radians. Reject NaN/infinity, oversized movement, stale/far future tick and unauthorized entity ownership. Server supplies authoritative tick and validates input/rate limits. Baseline desktop mouse has no arbitrary angular speed clamp; finite/bounds validation still applies. Buttons do not embed damage, hit result, position or ammunition.

EntitySnapshot fields: ID, position Float3, rotation yaw radians, velocity Float3, stance enum, health uint16, team byte, active weapon ID, WeaponState, movement phase scalar and aim pitch. HitVolumeSnapshot contains stable body-region IDs, capsule endpoints/radius and tick, computed by a shared gameplay pose evaluator independent of visual rig LOD. SimulationSnapshot includes tick, worldRevision, entity array, event high-water mark and match status. Events describe accepted actions; views deduplicate by EventId.

WeaponState action enum: Ready, FiringRecovery, Reloading, Equipping, Disabled; fields ammoInMagazine:uint16, reserve:uint16, nextAllowedFireTick, phaseStartTick, phaseEndTick, shotIndex:uint32. CombatEvent variants ShotAccepted, ProjectileSpawned, Impact, DamageApplied, ReloadStarted, ReloadCompleted, Death. Authoritative shot trace has origin, aim, seed, weapon definition ID and collision revision; impact gives surface tag, point and normal. Presentation never feeds hit animation back into damage.

## Wire and disk envelopes

Both use magic, schemaVersion, payloadLength and checksum/hash, but disk and network codecs are separate. Disk header includes endianness (little), compression ID, compressed/uncompressed lengths, payload SHA-256, generator/toolchain hashes. No BinaryFormatter, runtime type names or arbitrary object construction. Disk payload max sizes are declared in manifest and independently bounded by loader policy.

Network initial proposed cap: 1200-byte application datagrams including application headers; verify transport overhead/fragmentation at M5. Header: protocol:uint16, messageType:uint8, flags:uint8, session:uint64, sequence:uint32, ackSequence:uint32, ackBits:uint32, payloadBytes:uint16. Reliable channel sequencing may be delegated to transport; do not duplicate ACK logic when transport provides it. Message types: Hello/Manifest, JoinResult, InputBatch, SnapshotDelta, EventBatch, ResyncRequest, Disconnect. Unknown required fields/schema cause explicit incompatibility; optional versioned fields are length-delimited.

Positions encode signed int32 centimeters relative to chunk origin, velocity int16 cm/s, yaw/pitch uint16 normalized ranges; clamp overflow and request full state, never wrap. Local prediction retains full float state. Define per-field round-trip tolerances (position ≤0.01 m, angle ≤0.006 degrees, velocity ≤0.01 m/s). Bounds and packet fuzz tests precede network exposure. Snapshot baseline tick and ack identify deltas; if absent request reliable bounded full state. No unbounded network allocations.

## Contract tests required

Compile pure assemblies without Unity; validate schema fixtures; reject invalid values; serialize/deserialize boundary IDs and tick wrap; test lease release/cancellation; adapter conformance with fake world; replay duplicate and reordered commands; missing nav revision; binary length/hash attacks; snapshot/event lifetime under pooling. Cross-language tools consume JSON schema fixtures with the same semantic constraints. See [validation](validation.md).
