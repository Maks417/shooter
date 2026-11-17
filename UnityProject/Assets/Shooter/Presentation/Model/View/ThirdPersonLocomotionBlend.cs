#nullable enable
using System;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// ADR-100 per-avatar third-person locomotion filter. It turns copied authority movement (grounded, velocity,
    /// slide) into the cosmetic SoldierMotion inputs: travel heading, airborne blend, landing phase and slide blend.
    /// Read-only presentation state; it never feeds back into authority.
    /// </summary>
    public sealed class ThirdPersonLocomotionBlend
    {
        /// <summary>Horizontal speed above which the travel heading follows the velocity (m/s).</summary>
        public const float HeadingMinSpeed = 0.3f;
        /// <summary>Speed below which a held heading is released back to "travel along aim" (m/s).</summary>
        public const float HeadingReleaseSpeed = 0.05f;
        /// <summary>Upward speed that counts as a jump at once (m/s).</summary>
        public const float JumpRiseSpeed = 0.5f;
        /// <summary>Ungrounded time before a step-off counts as airborne (s); keeps stairs and edges from flickering.</summary>
        public const float AirborneDelaySeconds = 0.10f;
        public const float AirborneBlendSeconds = 0.08f;
        /// <summary>Airtime needed before a touchdown plays the landing dip (s).</summary>
        public const float LandMinAirSeconds = 0.15f;
        public const float LandSeconds = 0.20f;
        public const float SlideInSeconds = 0.10f;
        public const float SlideOutSeconds = 0.15f;

        private float airTime;
        private bool wasAirborne;
        private bool hasHeading;

        /// <summary>World travel yaw (radians), or NaN when the actor is (nearly) still.</summary>
        public float HeadingRadians { get; private set; } = float.NaN;
        public float Airborne { get; private set; }
        /// <summary>-1 none; 0..1 landing phase.</summary>
        public float Land { get; private set; } = -1f;
        public float Slide { get; private set; }

        public void Reset()
        {
            airTime = 0f; wasAirborne = false; hasHeading = false;
            HeadingRadians = float.NaN; Airborne = 0f; Land = -1f; Slide = 0f;
        }

        public void Step(float dtSeconds, bool alive, bool grounded, float verticalSpeed, float velocityX, float velocityZ, bool sliding)
        {
            float dt = float.IsFinite(dtSeconds) && dtSeconds > 0f ? Math.Min(dtSeconds, 0.25f) : 0f;
            if (!alive) { Reset(); return; }
            if (!float.IsFinite(verticalSpeed)) verticalSpeed = 0f;
            if (!float.IsFinite(velocityX)) velocityX = 0f;
            if (!float.IsFinite(velocityZ)) velocityZ = 0f;

            float speed = (float)Math.Sqrt(velocityX * velocityX + velocityZ * velocityZ);
            if (speed > HeadingMinSpeed) { HeadingRadians = (float)Math.Atan2(velocityX, velocityZ); hasHeading = true; }
            else if (speed < HeadingReleaseSpeed || !hasHeading) { HeadingRadians = float.NaN; hasHeading = false; }

            airTime = grounded ? 0f : airTime + dt;
            bool airborne = !grounded && !sliding && (verticalSpeed > JumpRiseSpeed || airTime > AirborneDelaySeconds);
            Airborne = Toward(Airborne, airborne ? 1f : 0f, dt / AirborneBlendSeconds);

            if (wasAirborne && grounded && landingAirTime > LandMinAirSeconds) Land = 0f;
            else if (Land >= 0f)
            {
                Land += dt / LandSeconds;
                if (Land >= 1f) Land = -1f;
            }
            if (airborne) landingAirTime = airTime;
            else if (grounded) landingAirTime = 0f;
            wasAirborne = airborne || (wasAirborne && !grounded);

            Slide = Toward(Slide, sliding ? 1f : 0f, dt / (sliding ? SlideInSeconds : SlideOutSeconds));
        }
        private float landingAirTime;

        private static float Toward(float value, float target, float step)
            => value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);
    }
}
