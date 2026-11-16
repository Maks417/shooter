#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Subtle Bodycam-leaning camera bob from observed feet travel. Presentation-only;
    /// never mutates aim input. Fully suppressed by reduced-motion, ADS, death, slide, or teleport.
    /// </summary>
    public sealed class FirstPersonCameraBob
    {
        public const float StrideMeters = 1.4f;
        public const float VerticalAmplitudeMeters = 0.011f;
        public const float LateralAmplitudeMeters = 0.0075f;
        public const float RollAmplitudeDegrees = 0.32f;

        private Float3 previousFeet;
        private bool hasPrevious;
        private double travel;
        private float movement;

        public float MovementBlend => movement;

        public void Reset()
        {
            previousFeet = default;
            hasPrevious = false;
            travel = 0d;
            movement = 0f;
        }

        public void Step(Float3 feet, float dt, bool sliding)
        {
            if (!float.IsFinite(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            if (dt <= 0f) return;

            float distance = 0f;
            if (hasPrevious)
            {
                float dx = feet.X - previousFeet.X, dz = feet.Z - previousFeet.Z;
                distance = (float)Math.Sqrt(dx * dx + dz * dz);
                // Spawn/teleport, vertical travel, or slide must not create footfalls.
                if (distance > 1f || Math.Abs(feet.Y - previousFeet.Y) > 0.025f || sliding) distance = 0f;
            }

            travel += distance;
            travel %= StrideMeters;
            float target = Math.Min(1f, distance / dt / 3.5f);
            movement += (target - movement) * (float)(1d - Math.Exp(-12d * dt));
            previousFeet = feet;
            hasPrevious = true;
        }

        /// <summary>Camera-local meters (X lateral, Y vertical, Z unused). Zero at full ADS / reduced motion / dead.</summary>
        public Float3 LocalOffsetMeters(float ads, bool reducedMotion, bool alive)
        {
            float gate = MotionGate(ads, reducedMotion, alive);
            if (gate <= 0f) return default;
            double phase = travel * (Math.PI * 2d / StrideMeters);
            return new Float3(
                (float)Math.Sin(phase) * LateralAmplitudeMeters * gate,
                -(float)Math.Cos(phase * 2d) * VerticalAmplitudeMeters * gate,
                0f);
        }

        /// <summary>Cosmetic roll only; fades with ADS so optic alignment stays truthful.</summary>
        public float RollDegrees(float ads, bool reducedMotion, bool alive)
        {
            float gate = MotionGate(ads, reducedMotion, alive);
            if (gate <= 0f) return 0f;
            double phase = travel * (Math.PI * 2d / StrideMeters);
            return (float)Math.Sin(phase) * RollAmplitudeDegrees * gate;
        }

        private float MotionGate(float ads, bool reducedMotion, bool alive)
        {
            if (reducedMotion || !alive) return 0f;
            float hip = 1f - Math.Clamp(ads, 0f, 1f);
            if (hip < 0.02f) return 0f;
            return movement * hip;
        }
    }
}
