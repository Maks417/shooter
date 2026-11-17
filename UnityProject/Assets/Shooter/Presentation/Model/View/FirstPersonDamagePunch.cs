#nullable enable
using System;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// ADR-103 presentation-only view flinch when the local player is hit. Each authority damage event adds a kick
    /// shaped x(t) = A * (t/tau) * e^(1 - t/tau): it peaks at A after tau seconds and settles within ~6 tau. The
    /// camera tilts up and away from the attacker; aim input and authority are never touched. Reduced camera motion
    /// disables it entirely.
    /// </summary>
    public sealed class FirstPersonDamagePunch
    {
        public const float RiseSeconds = 0.055f;
        public const float LifeSeconds = RiseSeconds * 7f;
        public const float MinPitchDegrees = 1.2f;
        public const float MaxPitchDegrees = 4f;
        public const float MinYawDegrees = 0.8f;
        public const float MaxYawDegrees = 2.5f;
        public const float HeadshotScale = 1.5f;
        private const int MaxKicks = 4;
        private const float DegToRad = (float)(Math.PI / 180.0);

        private readonly float[] pitchAmp = new float[MaxKicks];
        private readonly float[] yawAmp = new float[MaxKicks];
        private readonly float[] age = new float[MaxKicks];
        private int next;

        public FirstPersonDamagePunch() => Reset();

        /// <summary>Camera pitch offset in radians; negative looks up (aim pitch convention).</summary>
        public float PitchRadians { get; private set; }
        public float YawRadians { get; private set; }

        public void Reset()
        {
            for (int i = 0; i < MaxKicks; i++) { pitchAmp[i] = 0f; yawAmp[i] = 0f; age[i] = LifeSeconds; }
            PitchRadians = 0f;
            YawRadians = 0f;
        }

        /// <summary>Peak pitch kick (degrees) for a hit of this size.</summary>
        public static float PitchKickDegrees(int damage, bool headshot)
        {
            float s = Severity(damage);
            float deg = MinPitchDegrees + (MaxPitchDegrees - MinPitchDegrees) * s;
            return headshot ? deg * HeadshotScale : deg;
        }

        /// <summary>
        /// Peak yaw kick (degrees, signed). relativeYawRadians is the attacker bearing relative to the view
        /// (positive = attacker to the right); the view turns away from the attacker.
        /// </summary>
        public static float YawKickDegrees(int damage, bool headshot, float relativeYawRadians)
        {
            if (!float.IsFinite(relativeYawRadians)) return 0f;
            float s = Severity(damage);
            float deg = MinYawDegrees + (MaxYawDegrees - MinYawDegrees) * s;
            if (headshot) deg *= HeadshotScale;
            return -deg * (float)Math.Sin(relativeYawRadians);
        }

        private static float Severity(int damage) => Math.Max(0f, Math.Min(1f, damage / 100f));

        public void Kick(int damage, bool headshot, float relativeYawRadians, bool reducedMotion)
        {
            if (reducedMotion || damage <= 0) return;
            pitchAmp[next] = -PitchKickDegrees(damage, headshot) * DegToRad;
            yawAmp[next] = YawKickDegrees(damage, headshot, relativeYawRadians) * DegToRad;
            age[next] = 0f;
            next = (next + 1) % MaxKicks;
        }

        public void Step(float deltaSeconds, bool reducedMotion)
        {
            if (reducedMotion) { Reset(); return; }
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds)) deltaSeconds = 0f;
            float pitch = 0f, yaw = 0f;
            for (int i = 0; i < MaxKicks; i++)
            {
                age[i] = Math.Min(LifeSeconds, age[i] + deltaSeconds);
                float k = Kernel(age[i]);
                pitch += pitchAmp[i] * k;
                yaw += yawAmp[i] * k;
            }
            PitchRadians = pitch;
            YawRadians = yaw;
        }

        /// <summary>Normalised kick shape: 0 at t=0, 1 at RiseSeconds, ~0 after LifeSeconds.</summary>
        public static float Kernel(float seconds)
        {
            if (!(seconds > 0f) || seconds >= LifeSeconds) return 0f;
            float u = seconds / RiseSeconds;
            return u * (float)Math.Exp(1.0 - u);
        }
    }
}
