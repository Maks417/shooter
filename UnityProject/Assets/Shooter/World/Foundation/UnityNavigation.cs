using System;
using System.Collections.Generic;
using Shooter.Contracts;
using UnityEngine.AI;

namespace Shooter.World
{
    public sealed class UnityNavigation : INavigation
    {
        private sealed class Request { public PathTicket Ticket; public Float3 Destination; public bool Cancelled; }
        private readonly Dictionary<NavTicketId, Request> requests = new Dictionary<NavTicketId, Request>();
        private readonly Func<EntityId, Float3?> locateActor;
        private readonly int owner = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private ulong next;
        public uint Revision { get; }
        public UnityNavigation(uint revision, Func<EntityId, Float3?> locateActor)
        { Revision = revision; this.locateActor = locateActor ?? throw new ArgumentNullException(nameof(locateActor)); }
        private void MainThread()
        { if (System.Threading.Thread.CurrentThread.ManagedThreadId != owner) throw new InvalidOperationException("Navigation requires main thread"); }
        public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision)
        {
            MainThread(); if (requests.Count >= 128) throw new InvalidOperationException("Navigation request capacity exhausted");
            var ticket = new PathTicket(new NavTicketId(checked(++next)), actor, navRevision);
            requests.Add(ticket.Id, new Request { Ticket = ticket, Destination = destination }); return ticket;
        }
        public void Cancel(PathTicket ticket)
        { MainThread(); if (requests.TryGetValue(ticket.Id, out var request) && request.Ticket.Actor == ticket.Actor && request.Ticket.NavRevision == ticket.NavRevision) request.Cancelled = true; }
        public PathResult Poll(PathTicket ticket, PathBuffer callerOwned)
        {
            MainThread();
            if (!requests.TryGetValue(ticket.Id, out var request) || request.Ticket.Actor != ticket.Actor || request.Ticket.NavRevision != ticket.NavRevision) return new PathResult(PathStatus.Stale, 0, 0, ErrorCode.StaleRevision);
            requests.Remove(ticket.Id);
            if (request.Cancelled) return new PathResult(PathStatus.Cancelled, 0, 0, ErrorCode.Cancelled);
            if (ticket.NavRevision != Revision) return new PathResult(PathStatus.Stale, 0, 0, ErrorCode.StaleRevision);
            Float3? origin = locateActor(ticket.Actor);
            if (!origin.HasValue) return new PathResult(PathStatus.Failed, 0, 0, ErrorCode.MissingContent);
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(UnityWorldQueries.V(origin.Value), UnityWorldQueries.V(request.Destination), NavMesh.AllAreas, path) || path.status == NavMeshPathStatus.PathInvalid)
                return new PathResult(PathStatus.Failed, 0, 0, ErrorCode.MissingContent);
            var corners = path.corners;
            if (corners.Length > 128) return new PathResult(PathStatus.Failed, 0, 0, ErrorCode.LimitExceeded);
            if (corners.Length > callerOwned.Capacity) return new PathResult(PathStatus.Failed, 0, corners.Length, ErrorCode.LimitExceeded);
            for (int i = 0; i < corners.Length; i++) callerOwned[i] = UnityWorldQueries.F(corners[i]);
            return new PathResult(path.status == NavMeshPathStatus.PathComplete ? PathStatus.Complete : PathStatus.Partial, corners.Length, corners.Length, ErrorCode.None);
        }
    }
}
