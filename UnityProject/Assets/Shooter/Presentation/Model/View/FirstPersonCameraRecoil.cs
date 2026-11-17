#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Presentation-only camera recoil for the local shooter, driven by local ShotAccepted events and each
    /// weapon's own recoil from its definition file (<see cref="WeaponVisualProfile.RecoilPitchDegrees"/>).
    ///
    /// Two layers:
    /// <list type="bullet">
    /// <item><b>Climb</b> mirrors the authoritative recoil pitch (Simulation AuthoritativeRecoil: subtract the
    /// weapon's pitch per shot, clamp to 5°, decay 8/s). The simulation already deflects shots by this offset;
    /// showing it keeps the screen centre/scope reticle on where rounds actually go.</item>
    /// <item><b>Punch</b> is a short cosmetic spring per shot (proportional to the weapon's recoil) that returns
    /// to zero before the next automatic shot, so every report has a matching visible kick, including in ADS.
    /// Reduced camera motion removes the punch; the climb stays because it is gameplay truth.</item>
    /// </list>
    /// Never writes aim input or authority; the caller adds <see cref="PitchRadians"/>/<see cref="YawRadians"/>
    /// to the camera rotation only.
    /// </summary>
    public sealed class FirstPersonCameraRecoil
    {
        /// <summary>Mirrors AuthoritativeRecoil.DecayPerSecond.</summary>
        public const float ClimbDecayPerSecond = 8f;
        /// <summary>Mirrors AuthoritativeRecoil.MaxPitch (5°).</summary>
        public const float MaxClimbRadians = 5f * (float)Math.PI / 180f;
        public const float TicksPerSecond = 60f;
        /// <summary>Punch peak as a fraction of one shot's authoritative climb.</summary>
        public const float PunchPeakPerShotClimb = 0.9f;
        /// <summary>Horizontal punch peak as a fraction of the weapon's yaw half-range.</summary>
        public const float PunchYawPerHalfRange = 0.6f;

        // Stiff, slightly underdamped spring: peak ~31 ms after the impulse, ~85% settled by 100 ms (600 RPM).
        public const float PunchDamping = 18f;
        public const float PunchFrequency = 36f;

        private const int MaxRememberedEvents = 512;
        private readonly HashSet<string> consumed = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> consumedOrder = new Queue<string>();
        private float climb;
        private float climbAtLastShot;
        private uint lastShotTick;
        private bool hasShot;
        private float punchPitch, punchPitchVel, punchYaw, punchYawVel;

        /// <summary>Camera pitch offset in radians; negative looks up (same convention as aim pitch).</summary>
        public float PitchRadians => climb - punchPitch;
        public float YawRadians => punchYaw;
        public float ClimbRadians => climb;
        public float PunchPitchRadians => punchPitch;

        public void Reset()
        {
            climb = climbAtLastShot = 0f;
            hasShot = false;
            lastShotTick = 0;
            punchPitch = punchPitchVel = punchYaw = punchYawVel = 0f;
            consumed.Clear();
            consumedOrder.Clear();
        }

        /// <summary>
        /// Apply this frame's local shots, then advance by <paramref name="dtSeconds"/> of presentation time.
        /// Remote shots, knife strikes and weapons with no recoil leave the camera untouched.
        /// </summary>
        public void Step(IReadOnlyList<CombatEvent>? events, EntityId localEntity, float dtSeconds, bool reducedMotion)
        {
            if (!float.IsFinite(dtSeconds) || dtSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(dtSeconds));
            if (events != null && localEntity.IsValid)
            {
                for (int i = 0; i < events.Count; i++)
                {
                    var ev = events[i];
                    if (ev.Kind != CombatEventKind.ShotAccepted || !ev.Instigator.Equals(localEntity)) continue;
                    if (!(ev.Payload is ShotPayload shot)) continue;
                    if (!Remember(ev.Id)) continue;
                    if (!WeaponVisualProfiles.TryGet(shot.WeaponId, out var profile)) continue;
                    ApplyShot(ev.Id.Tick, shot.ShotIndex, profile, reducedMotion);
                }
            }
            Advance(dtSeconds);
        }

        private void ApplyShot(uint tick, uint shotIndex, in WeaponVisualProfile profile, bool reducedMotion)
        {
            float perShot = profile.RecoilPitchDegrees * (float)Math.PI / 180f;
            if (perShot <= 0f) return;
            // Mirror authority on the shot tick: per-tick decay since the previous shot, the extra decay that
            // ApplyImpulse performs, then the climb. Presentation time decay in Advance is reset by this.
            if (hasShot && tick > lastShotTick)
            {
                // Advance already decayed the climb in presentation time since the last shot; re-anchor on ticks so
                // long frames, pauses or hitches cannot drift away from authority.
                climb = climbAtLastShot * Decay((tick - lastShotTick) / TicksPerSecond);
            }
            climb *= Decay(1f / TicksPerSecond);
            climb -= perShot;
            if (climb < -MaxClimbRadians) climb = -MaxClimbRadians;
            if (climb > 0f) climb = 0f;
            climbAtLastShot = climb;
            lastShotTick = tick;
            hasShot = true;

            if (reducedMotion) return;
            // Impulse sized so the spring's first peak equals PunchPeakPerShotClimb × one shot's climb.
            punchPitchVel += perShot * PunchPeakPerShotClimb / PeakPerUnitVelocity;
            float yawHalf = profile.RecoilYawHalfDegrees * (float)Math.PI / 180f;
            float side = (shotIndex & 1u) == 0u ? -1f : 1f;
            punchYawVel += side * yawHalf * PunchYawPerHalfRange / PeakPerUnitVelocity;
        }

        private void Advance(float dt)
        {
            climb *= Decay(dt);
            DampedSpring.Step(ref punchPitch, ref punchPitchVel, dt, PunchDamping, PunchFrequency);
            DampedSpring.Step(ref punchYaw, ref punchYawVel, dt, PunchDamping, PunchFrequency);
        }

        private static float Decay(float seconds) => (float)Math.Exp(-ClimbDecayPerSecond * seconds);

        private bool Remember(EventId id)
        {
            string key = id.AuthoritySessionId + ":" + id.Tick + ":" + id.EntityId + ":" + id.Ordinal;
            if (!consumed.Add(key)) return false;
            consumedOrder.Enqueue(key);
            while (consumedOrder.Count > MaxRememberedEvents) consumed.Remove(consumedOrder.Dequeue());
            return true;
        }

        /// <summary>First peak displacement of the punch spring for a unit velocity impulse.</summary>
        internal static readonly float PeakPerUnitVelocity = DampedSpring.FirstPeakPerUnitVelocity(PunchDamping, PunchFrequency);
    }

    /// <summary>
    /// Exact solution of x'' + 2a x' + (a² + w²) x = 0 (underdamped). Frame rate and pause do not change the spring
    /// energy; large frame gaps decay rather than exploding.
    /// </summary>
    public static class DampedSpring
    {
        public static void Step(ref float position, ref float velocity, float dt, float damping, float frequency)
        {
            if (dt <= 0f) return;
            double a = damping, w = frequency;
            double decay = Math.Exp(-a * dt);
            double c = Math.Cos(w * dt);
            double s = Math.Sin(w * dt);
            double x0 = position, v0 = velocity;
            position = (float)(decay * (x0 * c + (v0 + a * x0) * s / w));
            velocity = (float)(decay * (v0 * c - (a * v0 + (a * a + w * w) * x0) * s / w));
            if (Math.Abs(position) < 1e-9f && Math.Abs(velocity) < 1e-7f) position = velocity = 0f;
        }

        /// <summary>Displacement at the first peak after a unit velocity impulse from rest.</summary>
        public static float FirstPeakPerUnitVelocity(float damping, float frequency)
        {
            double a = damping, w = frequency;
            double t = Math.Atan2(w, a) / w;
            return (float)(Math.Exp(-a * t) * Math.Sin(w * t) / w);
        }

        /// <summary>Seconds from a velocity impulse to the first peak.</summary>
        public static float FirstPeakSeconds(float damping, float frequency) =>
            (float)(Math.Atan2(frequency, damping) / frequency);
    }
}
