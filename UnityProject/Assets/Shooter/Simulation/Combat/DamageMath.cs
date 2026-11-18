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

        /// <summary>Base damage after range falloff, rounded like region damage and never below 1 (ADR-102).</summary>
        public static ushort FalloffDamage(ushort baseDamage, float scale)
        {
            if (scale >= 1f) return baseDamage;
            int rounded = (int)Math.Floor(baseDamage * (double)scale + 0.5);
            return (ushort)(rounded < 1 ? 1 : rounded);
        }

        /// <summary>ADR-110 body armor pool every actor spawns and respawns with.</summary>
        public const ushort MaxArmor = 100;

        /// <summary>ADR-110: armor protects the torso (2) and limbs (3–6); headshots (1) bypass it.</summary>
        public static bool ArmorCovers(ushort regionId) => regionId >= 2 && regionId <= 6;

        /// <summary>
        /// ADR-110 50/50 split: armor takes floor(amount/2), capped by the armor left; health takes the rest,
        /// which is always ≥ 1 for amount ≥ 1 so a DamagePayload keeps amount &gt; 0.
        /// </summary>
        public static ushort SplitArmor(ushort amount, ushort armor, out ushort armorTaken)
        {
            int half = amount / 2;
            armorTaken = (ushort)(half < armor ? half : armor);
            return (ushort)(amount - armorTaken);
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
