#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.Fx
{
    /// <summary>Presentation-only impact material family (roadmap Step 4 / ADR-093).</summary>
    public enum ImpactSurface : byte { Concrete = 0, Metal = 1, Dirt = 2 }

    /// <summary>
    /// Maps a world impact to its surface family and the pooled FX it spawns. Inputs are read-only facts the caller
    /// derives from packaged collision data; nothing here feeds Simulation.
    /// </summary>
    public static class ImpactSurfaceFx
    {
        /// <summary>FX variant for grey concrete/asphalt dust and chips.</summary>
        public const byte ConcreteVariant = 1;
        /// <summary>FX variant for brown dirt kick-up and clods.</summary>
        public const byte DirtVariant = 2;
        /// <summary>Hits this close to the packaged floor top (y = 0) with an upward normal are ground hits.</summary>
        public const float GroundBandMeters = 0.2f;
        public const float GroundNormalMinY = 0.7f;

        /// <param name="metalSolid">The hit lies on a packaged solid whose material role is metal.</param>
        /// <param name="onPackagedSolid">The hit lies on any packaged solid box (not the floor).</param>
        /// <param name="asphaltGround">The map's registry ground surface is asphalt rather than soil.</param>
        public static ImpactSurface Resolve(bool metalSolid, bool onPackagedSolid, Float3 position, Float3 normal, bool asphaltGround)
        {
            if (metalSolid) return ImpactSurface.Metal;
            if (!onPackagedSolid && normal.Y >= GroundNormalMinY && Math.Abs(position.Y) <= GroundBandMeters)
                return asphaltGround ? ImpactSurface.Concrete : ImpactSurface.Dirt;
            return ImpactSurface.Concrete;
        }

        /// <summary>Spawns the surface's bullet-impact set; returns the number of pooled instances accepted.</summary>
        public static int Spawn(AnalyticFxPool pool, ImpactSurface surface, Float3 position, Float3 normal)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            int spawned = 0;
            switch (surface)
            {
                case ImpactSurface.Metal:
                    if (pool.TrySpawn(FxKind.ImpactSpark, position, normal)) spawned++;
                    break;
                case ImpactSurface.Dirt:
                    if (pool.TrySpawn(FxKind.Dust, position, normal, DirtVariant)) spawned++;
                    if (pool.TrySpawn(FxKind.Debris, position, normal, DirtVariant)) spawned++;
                    break;
                default:
                    if (pool.TrySpawn(FxKind.Dust, position, normal, ConcreteVariant)) spawned++;
                    if (pool.TrySpawn(FxKind.Debris, position, normal, ConcreteVariant)) spawned++;
                    break;
            }
            return spawned;
        }
    }
}
