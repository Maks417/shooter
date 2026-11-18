#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.World.Runtime
{
    /// <summary>
    /// ADR-112: one authored cover object of a loaded map package (the <c>covers</c> rows of the package metadata):
    /// the axis-aligned box that blocks bullets and whether it is tall enough to hide a standing body.
    /// </summary>
    public readonly struct MapCoverProxy
    {
        public ulong StableId { get; }
        public Float3 Center { get; }
        public Float3 Size { get; }
        public bool HardCover { get; }

        public MapCoverProxy(ulong stableId, Float3 center, Float3 size, bool hardCover)
        {
            if (stableId == 0) throw new ArgumentOutOfRangeException(nameof(stableId));
            if (!(size.X > 0) || !(size.Y > 0) || !(size.Z > 0)) throw new ArgumentOutOfRangeException(nameof(size));
            StableId = stableId; Center = center; Size = size; HardCover = hardCover;
        }
    }

    /// <summary>
    /// ADR-112: optional side port of a map runtime that exposes the package's authored cover. Not part of the
    /// frozen Game-v1 <c>IMapRuntime</c>; the offline session asks for it with a type test (like
    /// <c>IActorStanceSource</c>) and runtimes without packaged cover simply do not implement it.
    /// </summary>
    public interface IMapCoverSource
    {
        IReadOnlyList<MapCoverProxy> Covers { get; }
    }
}
