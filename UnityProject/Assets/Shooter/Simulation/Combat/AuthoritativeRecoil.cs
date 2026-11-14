#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Simulation.Combat
{
    public static class AuthoritativeRecoil
    {
        public const float DecayPerSecond = 8f;
        public const float MaxPitch = 5f * (float)Math.PI / 180f;
        public const float MaxYaw = 2f * (float)Math.PI / 180f;
        public const float TickSeconds = 1f / 60f;

        public static void Decay(ref float yaw, ref float pitch)
        {
            float k = (float)Math.Exp(-DecayPerSecond * TickSeconds);
            yaw *= k; pitch *= k;
        }

        public static void ApplyImpulse(WeaponDefinition def, uint shotIndex, ulong seed, ref float yaw, ref float pitch)
        {
            Decay(ref yaw, ref pitch);
            // Positive pitch looks DOWN, so recoil kicks UP by accumulating negative pitch.
            pitch -= def.RecoilPitchRadians;
            ulong sample = SpreadSampling.MixShot(seed, shotIndex, 3);
            float u = ((sample >> 11) * (1f / (1UL << 53))) * 2f - 1f;
            yaw += u * def.RecoilYawHalfRadians;
            if (pitch > 0) pitch = 0;
            if (pitch < -MaxPitch) pitch = -MaxPitch;
            if (yaw > MaxYaw) yaw = MaxYaw;
            if (yaw < -MaxYaw) yaw = -MaxYaw;
        }
    }
}
