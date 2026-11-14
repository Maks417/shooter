#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.World.Navigation
{
    /// <summary>
    /// Truthful solver outcome (non-contract). FallbackComplete means a complete path came from a
    /// non-native fallback graph; it is counted separately and never reported as native Complete by diagnostics.
    /// </summary>
    public enum NavPathOutcome { Failed = 0, Complete = 1, Partial = 2, FallbackComplete = 3 }

    /// <summary>Pure revisioned path adapter used by AI tests; Unity NavMesh adapter remains separate.</summary>
    public sealed class RevisionedPathAdapter : INavigation
    {
        private sealed class Request
        {
            public PathTicket Ticket = default!;
            public Float3 Destination;
            public bool Cancelled;
        }

        private readonly Dictionary<NavTicketId, Request> requests = new Dictionary<NavTicketId, Request>();
        private readonly Func<EntityId, Float3?> locate;
        private readonly Func<Float3, Float3, List<Float3>?, NavPathOutcome> solve;
        private readonly int owner = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private ulong next;

        public uint Revision { get; private set; }
        public int PendingCount => requests.Count;
        public int PathsSolved { get; private set; }
        public int StaleRejects { get; private set; }
        public int PartialPathCount { get; private set; }
        public int FallbackPathCount { get; private set; }

        public RevisionedPathAdapter(uint revision, Func<EntityId, Float3?> locate, Func<Float3, Float3, List<Float3>?, bool> solve)
            : this(revision, locate, Wrap(solve))
        {
        }

        public RevisionedPathAdapter(uint revision, Func<EntityId, Float3?> locate, Func<Float3, Float3, List<Float3>?, NavPathOutcome> solve)
        {
            Revision = revision;
            this.locate = locate ?? throw new ArgumentNullException(nameof(locate));
            this.solve = solve ?? throw new ArgumentNullException(nameof(solve));
        }

        private static Func<Float3, Float3, List<Float3>?, NavPathOutcome> Wrap(Func<Float3, Float3, List<Float3>?, bool> solve)
        {
            if (solve == null) throw new ArgumentNullException(nameof(solve));
            return (a, b, c) => solve(a, b, c) ? NavPathOutcome.Complete : NavPathOutcome.Failed;
        }

        public void SetRevision(uint revision)
        {
            MainThread();
            Revision = revision;
        }

        private void MainThread()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != owner)
                throw new InvalidOperationException("Navigation requires main thread");
        }

        public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision)
        {
            MainThread();
            if (requests.Count >= 128) throw new InvalidOperationException("Navigation request capacity exhausted");
            var ticket = new PathTicket(new NavTicketId(checked(++next)), actor, navRevision);
            requests.Add(ticket.Id, new Request { Ticket = ticket, Destination = destination });
            return ticket;
        }

        public void Cancel(PathTicket ticket)
        {
            MainThread();
            if (requests.TryGetValue(ticket.Id, out var request) &&
                request.Ticket.Actor == ticket.Actor &&
                request.Ticket.NavRevision == ticket.NavRevision)
                request.Cancelled = true;
        }

        public PathResult Poll(PathTicket ticket, PathBuffer callerOwned)
        {
            MainThread();
            if (callerOwned == null) throw new ArgumentNullException(nameof(callerOwned));
            if (!requests.TryGetValue(ticket.Id, out var request) ||
                request.Ticket.Actor != ticket.Actor ||
                request.Ticket.NavRevision != ticket.NavRevision)
            {
                StaleRejects++;
                return new PathResult(PathStatus.Stale, 0, 0, ErrorCode.StaleRevision);
            }
            requests.Remove(ticket.Id);
            if (request.Cancelled) return new PathResult(PathStatus.Cancelled, 0, 0, ErrorCode.Cancelled);
            if (ticket.NavRevision != Revision)
            {
                StaleRejects++;
                return new PathResult(PathStatus.Stale, 0, 0, ErrorCode.StaleRevision);
            }
            Float3? origin = locate(ticket.Actor);
            if (!origin.HasValue) return new PathResult(PathStatus.Failed, 0, 0, ErrorCode.MissingContent);
            var corners = new List<Float3>();
            var outcome = solve(origin.Value, request.Destination, corners);
            if (outcome == NavPathOutcome.Failed || corners.Count == 0)
                return new PathResult(PathStatus.Failed, 0, 0, ErrorCode.MissingContent);
            if (corners.Count > callerOwned.Capacity)
                return new PathResult(PathStatus.Failed, 0, corners.Count, ErrorCode.LimitExceeded);
            for (int i = 0; i < corners.Count; i++) callerOwned[i] = corners[i];
            PathsSolved++;
            if (outcome == NavPathOutcome.FallbackComplete) FallbackPathCount++;
            if (outcome == NavPathOutcome.Partial)
            {
                PartialPathCount++;
                return new PathResult(PathStatus.Partial, corners.Count, corners.Count, ErrorCode.None);
            }
            return new PathResult(PathStatus.Complete, corners.Count, corners.Count, ErrorCode.None);
        }
    }
}
