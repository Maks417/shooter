#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Game.Session
{
    /// <summary>
    /// Counts every IWorldQueries invocation for product measurement querySamples (T070).
    /// Path requests remain on INavigation and are counted separately as pathRequests.
    /// </summary>
    public sealed class CountingWorldQueries : IWorldQueries
    {
        private readonly IWorldQueries inner;

        public CountingWorldQueries(IWorldQueries inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public long TotalSamples { get; private set; }

        public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter)
        {
            TotalSamples++;
            return inner.SweepCapsule(capsule, displacement, filter);
        }

        public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter)
        {
            TotalSamples++;
            return inner.Raycast(origin, unitDirection, maxMeters, filter);
        }

        public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned)
        {
            TotalSamples++;
            return inner.Overlap(capsule, filter, callerOwned);
        }
    }
}
