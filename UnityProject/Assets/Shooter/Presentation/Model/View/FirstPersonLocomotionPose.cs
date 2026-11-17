#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>Per-frame movement inputs copied from the authoritative motor view (ADR-099).</summary>
    public readonly struct FirstPersonLocomotionInput
    {
        public bool Sprinting { get; }
        public bool Grounded { get; }
        public bool Crouching { get; }
        public bool Sliding { get; }
        public float VerticalSpeed { get; }
        public float LocomotionDistanceMeters { get; }
        public float HorizontalSpeed { get; }

        public FirstPersonLocomotionInput(bool sprinting, bool grounded, bool crouching, bool sliding, float verticalSpeed,
            float locomotionDistanceMeters, float horizontalSpeed)
        {
            Sprinting = sprinting;
            Grounded = grounded;
            Crouching = crouching;
            Sliding = sliding;
            VerticalSpeed = float.IsFinite(verticalSpeed) ? verticalSpeed : 0f;
            LocomotionDistanceMeters = float.IsFinite(locomotionDistanceMeters) ? locomotionDistanceMeters : 0f;
            HorizontalSpeed = float.IsFinite(horizontalSpeed) && horizontalSpeed > 0f ? horizontalSpeed : 0f;
        }

        public static FirstPersonLocomotionInput Standing => new FirstPersonLocomotionInput(false, true, false, false, 0f, 0f, 0f);
    }

    /// <summary>
    /// Movement-driven viewmodel layers (ADR-099): sprint carry with a figure-eight bob, jump lift and landing dip,
    /// crouch tuck, slide tilt and look inertia. Cosmetic and camera-local only; it never moves the camera or aim.
    /// </summary>
    public sealed class FirstPersonLocomotionPose
    {
        /// <summary>Seconds (time constant) for the sprint carry to come in; a shot or ADS drops it faster.</summary>
        public const float SprintBlendSeconds = 0.12f;
        public const float SprintDropSeconds = 0.05f;
        /// <summary>Meters of authoritative locomotion per sprint bob cycle (two footfalls).</summary>
        public const float SprintStrideMeters = 2.2f;
        public const float LandDamping = 9f;
        public const float LandFrequency = 15f;
        public const float LookDamping = 11f;
        public const float LookFrequency = 13f;
        /// <summary>Velocity impulse (rad/s) per radian of camera turn in one frame.</summary>
        public const float LookGain = 3.4f;
        /// <summary>Largest look lag (radians) so a 180° flick reads as weight, not as a detached gun.</summary>
        public const float MaxLookLagRadians = 0.07f;

        private float sprint;
        private float crouch;
        private float slide;
        private float air;
        private float vertical;
        private float verticalVelocity;
        private float lookYaw;
        private float lookYawVelocity;
        private float lookPitch;
        private float lookPitchVelocity;
        private bool wasGrounded = true;
        private float lastAirSpeed;
        private float distance;

        public float SprintBlend => sprint;
        public float CrouchBlend => crouch;
        public float SlideBlend => slide;
        public float AirBlend => air;
        /// <summary>Spring displacement of the jump/land layer (meters, negative = dipped).</summary>
        public float VerticalOffset => vertical;
        public float LookYawLag => lookYaw;
        public float LookPitchLag => lookPitch;

        public void Reset()
        {
            sprint = crouch = slide = air = 0f;
            vertical = verticalVelocity = 0f;
            lookYaw = lookYawVelocity = lookPitch = lookPitchVelocity = 0f;
            wasGrounded = true;
            lastAirSpeed = 0f;
            distance = 0f;
        }

        /// <param name="suppressSprint">A shot, reload, special action or switch lowers the sprint carry at once.</param>
        /// <param name="lookYawDelta">Camera yaw change this frame (radians, + = right).</param>
        /// <param name="lookPitchDelta">Camera pitch change this frame (radians, + = down, Unity convention).</param>
        public void Step(in FirstPersonLocomotionInput input, float dt, float ads, bool suppressSprint,
            float lookYawDelta, float lookPitchDelta)
        {
            if (!float.IsFinite(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            if (dt <= 0f) return;
            float a = Math.Clamp(ads, 0f, 1f);
            bool sprintWanted = input.Sprinting && input.Grounded && !suppressSprint && a < 0.5f;
            sprint = Approach(sprint, sprintWanted ? 1f : 0f, dt, sprintWanted ? SprintBlendSeconds : SprintDropSeconds);
            crouch = Approach(crouch, input.Crouching ? 1f : 0f, dt, 0.10f);
            slide = Approach(slide, input.Sliding ? 1f : 0f, dt, 0.08f);
            air = Approach(air, input.Grounded ? 0f : 1f, dt, input.Grounded ? 0.06f : 0.14f);
            distance = input.LocomotionDistanceMeters;

            // Jump: the body leaves first, so the gun lags low. Land: a dip scaled by the fall speed, then a settle.
            if (wasGrounded && !input.Grounded && input.VerticalSpeed > 0.5f)
                verticalVelocity -= Math.Min(input.VerticalSpeed, 8f) * 0.045f;
            if (!input.Grounded) lastAirSpeed = Math.Min(lastAirSpeed, input.VerticalSpeed);
            if (!wasGrounded && input.Grounded)
            {
                float fall = Math.Min(-lastAirSpeed, 14f);
                if (fall > 0.5f) verticalVelocity -= fall * 0.07f;
                lastAirSpeed = 0f;
            }
            if (input.Grounded) lastAirSpeed = 0f;
            wasGrounded = input.Grounded;
            DampedSpring.Step(ref vertical, ref verticalVelocity, dt, LandDamping, LandFrequency);
            vertical = Math.Clamp(vertical, -0.06f, 0.04f);

            // Look inertia: each frame's turn is a velocity impulse on a stiff spring, so the gun trails a quick flick.
            float yawDelta = Math.Clamp(float.IsFinite(lookYawDelta) ? lookYawDelta : 0f, -0.5f, 0.5f);
            float pitchDelta = Math.Clamp(float.IsFinite(lookPitchDelta) ? lookPitchDelta : 0f, -0.5f, 0.5f);
            // Steady turning at w rad/s settles at a lag of w * LookGain / (damping² + frequency²): ~2° at 3 rad/s.
            lookYawVelocity -= yawDelta * LookGain;
            lookPitchVelocity -= pitchDelta * LookGain;
            DampedSpring.Step(ref lookYaw, ref lookYawVelocity, dt, LookDamping, LookFrequency);
            DampedSpring.Step(ref lookPitch, ref lookPitchVelocity, dt, LookDamping, LookFrequency);
            lookYaw = Math.Clamp(lookYaw, -MaxLookLagRadians, MaxLookLagRadians);
            lookPitch = Math.Clamp(lookPitch, -MaxLookLagRadians, MaxLookLagRadians);
        }

        /// <summary>Camera-local weapon translation (meters). ADS keeps only what cannot move the sight off the ray.</summary>
        public Float3 PositionOffset(byte slot, float ads, bool reducedMotion)
        {
            float hip = 1f - Math.Clamp(ads, 0f, 1f);
            float amp = reducedMotion ? 0.4f : 1f;
            Float3 carry = SprintCarryPosition(slot);
            double phase = distance * (Math.PI * 2d / SprintStrideMeters);
            float bob = reducedMotion ? 0f : sprint;
            float look = reducedMotion ? 0f : hip;
            return new Float3(
                (carry.X * sprint + (float)Math.Sin(phase) * 0.012f * bob - 0.012f * crouch * hip - 0.015f * slide * hip
                    - lookYaw * 0.05f * look) * amp,
                (carry.Y * sprint - (float)Math.Abs(Math.Cos(phase)) * 0.010f * bob - 0.012f * crouch * hip
                    + vertical * (0.35f + 0.65f * hip) - 0.006f * air * hip + lookPitch * 0.04f * look) * amp,
                (carry.Z * sprint + 0.010f * crouch * hip) * amp);
        }

        /// <summary>Weapon Euler offset (degrees). Rotation fades fully at ADS so the optic stays aligned.</summary>
        public Float3 RotationDegrees(byte slot, float ads, bool reducedMotion)
        {
            if (reducedMotion) return default;
            float hip = 1f - Math.Clamp(ads, 0f, 1f);
            Float3 carry = SprintCarryRotation(slot);
            double phase = distance * (Math.PI * 2d / SprintStrideMeters);
            const float Rad = 57.29578f;
            return new Float3(
                (carry.X * sprint + (float)Math.Cos(phase * 2d) * 1.6f * sprint - vertical * 120f + 2.5f * air
                    + lookPitch * Rad * 0.55f) * hip,
                (carry.Y * sprint + (float)Math.Sin(phase) * 2.0f * sprint + lookYaw * Rad * 0.55f) * hip,
                (carry.Z * sprint + (float)Math.Sin(phase) * 3.0f * sprint - 3f * crouch - 10f * slide
                    - lookYaw * Rad * 0.35f) * hip);
        }

        /// <summary>Lowered, canted sprint carry: rifle across the chest, pistol tucked low, knife/grenade pumping.</summary>
        public static Float3 SprintCarryPosition(byte slot) => slot switch
        {
            ProductAuthorityActionView.PistolSlot => new Float3(0f, -0.06f, -0.02f),
            ProductAuthorityActionView.KnifeSlot => new Float3(0.01f, -0.03f, 0f),
            ProductAuthorityActionView.GrenadeSlot => new Float3(0.01f, -0.03f, 0f),
            _ => new Float3(-0.03f, -0.045f, -0.03f)
        };

        public static Float3 SprintCarryRotation(byte slot) => slot switch
        {
            ProductAuthorityActionView.PistolSlot => new Float3(25f, 5f, -10f),
            ProductAuthorityActionView.KnifeSlot => new Float3(10f, 0f, -8f),
            ProductAuthorityActionView.GrenadeSlot => new Float3(10f, 0f, -8f),
            _ => new Float3(12f, 28f, -18f)
        };

        // Exponential approach with time constant tau: frame-rate independent.
        private static float Approach(float current, float target, float dt, float tau)
        {
            float k = 1f - (float)Math.Exp(-dt / Math.Max(tau, 1e-4f));
            float next = current + (target - current) * k;
            return Math.Abs(next - target) < 1e-4f ? target : next;
        }
    }
}
