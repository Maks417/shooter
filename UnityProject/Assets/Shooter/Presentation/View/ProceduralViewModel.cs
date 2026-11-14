#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    public readonly struct ViewPose
    {
        public Float3 LeftHand { get; }
        public Float3 RightHand { get; }
        public float AdsBlend { get; }
        public float RecoilPitch { get; }
        public float RecoilYaw { get; }
        public ViewPose(Float3 leftHand, Float3 rightHand, float adsBlend, float recoilPitch, float recoilYaw)
        {
            LeftHand = leftHand; RightHand = rightHand; AdsBlend = adsBlend; RecoilPitch = recoilPitch; RecoilYaw = recoilYaw;
        }
    }

    /// <summary>
    /// Procedural first-person arms/ADS/recoil/reload driven only from snapshot+event leases.
    /// Does not apply damage or authoritative recoil twice; visual springs are cosmetic.
    /// </summary>
    public sealed class ProceduralViewModel
    {
        public const float GripToleranceMeters = 0.02f;
        private readonly HashSet<string> consumedEvents = new HashSet<string>(StringComparer.Ordinal);
        private float ads;
        private float recoilPitch;
        private float recoilYaw;
        private float recoilPitchVel;
        private float recoilYawVel;
        private float reloadPhase;
        private Float3 leftGrip = new Float3(-0.12f, -0.08f, 0.35f);
        private Float3 rightGrip = new Float3(0.08f, -0.10f, 0.40f);

        public ViewPose Pose => new ViewPose(leftGrip, rightGrip, ads, recoilPitch, recoilYaw);

        /// <summary>Cosmetic ADS/spring only when combat event leases are unavailable on the product path.</summary>
        public void TickAim(bool aimHeld, float dtSeconds)
        {
            if (!float.IsFinite(dtSeconds) || dtSeconds < 0) throw new ArgumentOutOfRangeException(nameof(dtSeconds));
            float targetAds = aimHeld ? 1f : 0f;
            ads = MoveTowards(ads, targetAds, dtSeconds / 0.15f);
            recoilPitchVel += -recoilPitch * 40f - recoilPitchVel * 12f;
            recoilYawVel += -recoilYaw * 40f - recoilYawVel * 12f;
            recoilPitch += recoilPitchVel * dtSeconds;
            recoilYaw += recoilYawVel * dtSeconds;
            float adsPull = ads * 0.08f;
            rightGrip = new Float3(0.08f - adsPull, -0.10f + ads * 0.02f, 0.40f + adsPull);
            leftGrip = new Float3(-0.12f - adsPull * 0.5f, -0.08f, 0.35f + adsPull * 0.5f);
        }

        public void Consume(SnapshotLease snapshot, EventLease events, bool aimHeld, float dtSeconds)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (!float.IsFinite(dtSeconds) || dtSeconds < 0) throw new ArgumentOutOfRangeException(nameof(dtSeconds));

            float targetAds = aimHeld ? 1f : 0f;
            ads = MoveTowards(ads, targetAds, dtSeconds / 0.15f);

            foreach (var ev in events.Events)
            {
                string key = ev.Id.AuthoritySessionId + ":" + ev.Id.Tick + ":" + ev.Id.EntityId + ":" + ev.Id.Ordinal;
                if (!consumedEvents.Add(key)) continue; // dedupe
                if (ev.Kind == CombatEventKind.ShotAccepted)
                {
                    recoilPitchVel += 2.5f;
                    recoilYawVel += ((ev.Id.Ordinal & 1) == 0 ? -1f : 1f) * 0.6f;
                }
                else if (ev.Kind == CombatEventKind.ReloadStarted)
                    reloadPhase = 0.01f;
                else if (ev.Kind == CombatEventKind.ReloadCompleted)
                    reloadPhase = 0f;
            }

            // Visual spring (cosmetic only).
            recoilPitchVel += -recoilPitch * 40f - recoilPitchVel * 12f;
            recoilYawVel += -recoilYaw * 40f - recoilYawVel * 12f;
            recoilPitch += recoilPitchVel * dtSeconds;
            recoilYaw += recoilYawVel * dtSeconds;

            if (reloadPhase > 0f)
            {
                reloadPhase = Math.Min(1f, reloadPhase + dtSeconds / 2.4f);
                leftGrip = new Float3(-0.12f, -0.08f - 0.05f * reloadPhase, 0.35f - 0.1f * reloadPhase);
            }
            else
                leftGrip = new Float3(-0.12f, -0.08f, 0.35f);

            float adsPull = ads * 0.08f;
            rightGrip = new Float3(0.08f - adsPull, -0.10f + ads * 0.02f, 0.40f + adsPull);
            leftGrip = new Float3(leftGrip.X - adsPull * 0.5f, leftGrip.Y, leftGrip.Z + adsPull * 0.5f);
        }

        public bool HandsWithinGripTolerance(Float3 weaponLeftSocket, Float3 weaponRightSocket) =>
            Distance(leftGrip, weaponLeftSocket) <= GripToleranceMeters
            && Distance(rightGrip, weaponRightSocket) <= GripToleranceMeters;

        public int ConsumedEventCount => consumedEvents.Count;

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        private static float Distance(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
