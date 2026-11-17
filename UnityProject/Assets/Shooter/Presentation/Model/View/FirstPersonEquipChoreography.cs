#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Per-weapon draw styles and the holster/draw split of the authoritative equip window (ADR-099).
    /// The simulation swaps the slot instantly and blocks fire for the incoming weapon's equip ticks; presentation
    /// spends a short tuck on the outgoing mesh and fits the draw into the rest of that window.
    /// </summary>
    public static class FirstPersonEquipChoreography
    {
        /// <summary>Upper bound for the presentation-only tuck of the outgoing weapon.</summary>
        public const float MaxHolsterSeconds = 0.10f;
        /// <summary>The tuck never takes more than this share of the equip window.</summary>
        public const float HolsterShare = 0.35f;
        public const float MinDrawSeconds = 0.08f;

        /// <summary>Splits <paramref name="equipSeconds"/> into tuck + draw. Non-positive falls back to the legacy pair.</summary>
        public static (float holster, float draw) Split(float equipSeconds)
        {
            if (!float.IsFinite(equipSeconds) || equipSeconds <= 0f)
                return (FirstPersonHandling.HolsterSeconds, FirstPersonHandling.DrawSeconds);
            float holster = Math.Min(MaxHolsterSeconds, equipSeconds * HolsterShare);
            float draw = Math.Max(MinDrawSeconds, equipSeconds - holster);
            return (holster, draw);
        }

        /// <summary>Draw start pose offset for the incoming weapon at equip blend <paramref name="equip"/> (0 tucked, 1 ready).</summary>
        public static Float3 DrawPosition(byte slot, float equip)
        {
            if (equip >= 1f) return default;
            float u = Math.Clamp(equip, 0f, 1f);
            float remain = EaseOutRemaining(u);
            Float3 start = StartPosition(slot);
            float settle = Settle(u);
            return new Float3(start.X * remain, start.Y * remain + 0.004f * settle, start.Z * remain + 0.006f * settle);
        }

        public static Float3 DrawRotationDegrees(byte slot, float equip)
        {
            if (equip >= 1f) return default;
            float u = Math.Clamp(equip, 0f, 1f);
            float remain = EaseOutRemaining(u);
            Float3 start = StartRotation(slot);
            float settle = Settle(u);
            // The settle is a small overshoot past ready (muzzle up / wrist roll) that dies out exactly at u = 1.
            Float3 overshoot = slot switch
            {
                ProductAuthorityActionView.PistolSlot => new Float3(-6f, 0f, 2f),
                ProductAuthorityActionView.KnifeSlot => new Float3(-3f, 0f, 9f),
                ProductAuthorityActionView.GrenadeSlot => new Float3(-4f, 0f, -3f),
                _ => new Float3(-5f, -2f, 4f)
            };
            return new Float3(
                start.X * remain + overshoot.X * settle,
                start.Y * remain + overshoot.Y * settle,
                start.Z * remain + overshoot.Z * settle);
        }

        /// <summary>
        /// Rifle swings up from the low right with a roll; pistol rises muzzle-down from the holster side; knife comes
        /// up with a wrist flip; grenade comes up from the belt on the left.
        /// </summary>
        public static Float3 StartPosition(byte slot) => slot switch
        {
            ProductAuthorityActionView.PistolSlot => new Float3(0.06f, -0.22f, -0.04f),
            ProductAuthorityActionView.KnifeSlot => new Float3(0.04f, -0.20f, 0f),
            ProductAuthorityActionView.GrenadeSlot => new Float3(-0.03f, -0.22f, -0.06f),
            _ => new Float3(0.05f, -0.20f, -0.10f)
        };

        public static Float3 StartRotation(byte slot) => slot switch
        {
            ProductAuthorityActionView.PistolSlot => new Float3(45f, 10f, -10f),
            ProductAuthorityActionView.KnifeSlot => new Float3(25f, -20f, -150f),
            ProductAuthorityActionView.GrenadeSlot => new Float3(35f, 15f, 20f),
            _ => new Float3(30f, 25f, -35f)
        };

        // Fast out of the tuck, slow into ready: remaining share = (1 - u)^3.
        private static float EaseOutRemaining(float u)
        {
            float r = 1f - u;
            return r * r * r;
        }

        // Bump that peaks late in the draw (u ≈ 0.75) and is exactly zero at both ends.
        private static float Settle(float u) => u <= 0f || u >= 1f ? 0f : (float)Math.Sin(Math.PI * u) * u * u;
    }
}
