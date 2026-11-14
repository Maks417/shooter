#nullable enable
using Shooter.Contracts;

namespace Shooter.Simulation
{
    /// <summary>
    /// Authoritative out-of-world kill volume rules (single source of truth). A living actor whose feet are more than
    /// <see cref="MarginMeters"/> below the authored bounds floor, or more than <see cref="MarginMeters"/> outside the
    /// authored XZ bounds, dies an environmental death (no killer, no team point; see docs/features/team-deathmatch.md
    /// "Kill credit"). Comparisons are strict; non-finite feet count as out of world. Pure and deterministic.
    /// </summary>
    public static class WorldKillVolume
    {
        /// <summary>Margin in meters below the bounds floor and outside the XZ bounds.</summary>
        public const float MarginMeters = 10f;

        public static bool IsOutOfWorld(Float3 feet, Aabb bounds)
        {
            if (float.IsNaN(feet.X) || float.IsNaN(feet.Y) || float.IsNaN(feet.Z)) return true;
            return feet.Y < bounds.Min.Y - MarginMeters ||
                   feet.X < bounds.Min.X - MarginMeters || feet.X > bounds.Max.X + MarginMeters ||
                   feet.Z < bounds.Min.Z - MarginMeters || feet.Z > bounds.Max.Z + MarginMeters;
        }
    }
}
