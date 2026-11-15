#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Simulation.Combat
{
    public static class DamageMath
    {
        public const ushort MaxHealth = 100;

        public static float RegionMultiplier(ushort regionId) => regionId switch
        {
            1 => 4.0f,   // Head — rifle/pistol one-shot (ADR-063)
            2 => 1.0f,   // Torso — ~4 rifle shots
            3 or 4 or 5 or 6 => 0.5f, // Limbs — ~7 rifle shots
            _ => throw new ArgumentOutOfRangeException(nameof(regionId))
        };

        /// <summary>floor(base×multiplier+0.5), clamp remaining health ≥ 0.</summary>
        public static ushort RoundDamage(ushort baseDamage, ushort regionId)
        {
            double value = baseDamage * RegionMultiplier(regionId) + 0.5;
            int rounded = (int)Math.Floor(value);
            if (rounded < 1) rounded = 1;
            if (rounded > ushort.MaxValue) rounded = ushort.MaxValue;
            return (ushort)rounded;
        }

        public static ushort Apply(ushort health, ushort amount, out ushort remaining)
        {
            if (health == 0) { remaining = 0; return 0; }
            int next = health - amount;
            if (next < 0) next = 0;
            remaining = (ushort)next;
            return amount;
        }
    }
}
