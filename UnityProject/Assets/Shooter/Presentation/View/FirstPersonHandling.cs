#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>Bounded cosmetic equipment motion. Reads displacement; never changes camera or aim.</summary>
    public sealed class FirstPersonHandling
    {
        private Float3 previousFeet;
        private bool hasPrevious;
        private double travel;
        private float movement;
        private float equip = 1f;
        private byte slot;

        public float EquipBlend => equip;

        public void Reset(byte weaponSlot)
        {
            slot = weaponSlot;
            previousFeet = default;
            hasPrevious = false;
            travel = 0d;
            movement = 0f;
            equip = 1f;
        }

        public void Step(Float3 feet, byte weaponSlot, float dt, bool sliding)
        {
            if (!float.IsFinite(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            if (weaponSlot > ProductAuthorityActionView.PistolSlot) throw new ArgumentOutOfRangeException(nameof(weaponSlot));
            if (weaponSlot != slot) { slot = weaponSlot; equip = 0f; }
            if (dt > 0f)
            {
                equip = Math.Min(1f, equip + dt / .22f);
                float distance = 0f;
                if (hasPrevious)
                {
                    float dx = feet.X - previousFeet.X, dz = feet.Z - previousFeet.Z;
                    distance = (float)Math.Sqrt(dx * dx + dz * dz);
                    // Do not interpret spawn/teleport, vertical travel or a slide as footfalls.
                    if (distance > 1f || Math.Abs(feet.Y - previousFeet.Y) > .025f || sliding) distance = 0f;
                }
                travel += distance;
                travel %= 1.4d;
                float target = Math.Min(1f, distance / dt / 3.5f);
                movement += (target - movement) * (float)(1d - Math.Exp(-12d * dt));
                previousFeet = feet;
                hasPrevious = true;
            }
        }

        public Float3 PositionOffset(float ads, float reloadPhase, bool reducedMotion)
        {
            float hip = 1f - Math.Clamp(ads, 0f, 1f);
            float reload = reloadPhase > 0f ? (float)Math.Sin(Math.PI * Math.Clamp(reloadPhase, 0f, 1f)) : 0f;
            float lower = 1f - Smooth(equip);
            float gait = reducedMotion ? 0f : movement * hip;
            double phase = travel * (Math.PI * 2d / 1.4d);
            return new Float3(
                (float)Math.Sin(phase) * .004f * gait + .025f * reload,
                -(float)Math.Cos(phase * 2d) * .003f * gait - .055f * reload - .12f * lower,
                -.035f * reload - .07f * lower);
        }

        public Float3 RotationDegrees(float ads, float reloadPhase, bool reducedMotion)
        {
            if (reducedMotion) return default;
            float hip = 1f - Math.Clamp(ads, 0f, 1f);
            float reload = reloadPhase > 0f ? (float)Math.Sin(Math.PI * Math.Clamp(reloadPhase, 0f, 1f)) : 0f;
            return new Float3(12f * (1f - Smooth(equip)) + 9f * reload, 0f,
                -10f * reload + (float)Math.Sin(travel * Math.PI * 2d / 1.4d) * .45f * movement * hip);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
