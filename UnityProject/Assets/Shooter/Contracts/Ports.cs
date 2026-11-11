#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shooter.Contracts
{
    public interface IRecipeValidator { ValidationReport Validate(RecipeDocument recipe); }
    public interface IAssetGenerator { GenerationResult Generate(GenerationRequest request, CancellationToken cancel); }
    public interface IContentStore { Task<ContentLeaseResult> AcquireAsync(ContentId id, ContentKind kind, CancellationToken cancel); }
    // World, navigation and simulation calls are main-thread-only. Validate DTO defaults at adapter entry.
    public interface IWorldQueries
    {
        SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter);
        RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter);
        OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned);
    }
    public interface INavigation
    {
        PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision);
        PathResult Poll(PathTicket ticket, PathBuffer callerOwned);
        void Cancel(PathTicket ticket);
    }
    public interface ISimulation
    {
        StepResult Step(uint tick, ReadOnlyMemory<InputCommand> commands);
        SnapshotLease AcquireSnapshot();
        EventLease AcquireEvents();
    }
    public interface ICommandSource { DrainResult Drain(uint tick, CommandBuffer destination); }
    public interface ISnapshotConsumer { void Consume(SnapshotLease snapshot, EventLease events); }
    public interface INetworkChannel
    {
        bool TrySend(PeerId peer, Delivery delivery, ReadOnlyMemory<byte> payload);
        DrainResult Poll(PacketBuffer destination);
    }
}
