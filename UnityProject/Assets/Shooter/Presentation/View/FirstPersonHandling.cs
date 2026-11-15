#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Bounded cosmetic equipment motion. Reads displacement; never changes camera or aim.
    /// Near-field presence, idle sway, reload arcs and equipment-relative recoil kick are presentation-only.
    /// </summary>
    public sealed class FirstPersonHandling
    {
        // Bodycam-leaning near-field mass: slightly larger mesh scale + hip bias closer to the lens.
        public const float RifleNearFieldScale = 1.06f;
        public const float PistolNearFieldScale = 1.10f;

        private Float3 previousFeet;
        private bool hasPrevious;
        private double travel;
        private float movement;
        private float equip = 1f;
        private byte slot;
        private double idleClock;
        private float swayBlend;

        public float EquipBlend => equip;
        public float SwayBlend => swayBlend;

        public void Reset(byte weaponSlot)
        {
            slot = weaponSlot;
            previousFeet = default;
            hasPrevious = false;
            travel = 0d;
            movement = 0f;
            equip = 1f;
            idleClock = 0d;
            swayBlend = 0f;
        }

        public void Step(Float3 feet, byte weaponSlot, float dt, bool sliding)
        {
            if (!float.IsFinite(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            if (weaponSlot > ProductAuthorityActionView.MaxSlot) throw new ArgumentOutOfRangeException(nameof(weaponSlot));
            if (weaponSlot != slot) { slot = weaponSlot; equip = 0f; swayBlend = 0f; }
            if (dt > 0f)
            {
                // Slightly snappier raise so weapon swaps feel present without changing ADS truth.
                equip = Math.Min(1f, equip + dt / .20f);
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
                idleClock += dt;
                float idleWant = movement < 0.12f ? 1f : 0f;
                swayBlend += (idleWant - swayBlend) * (float)(1d - Math.Exp(-4d * dt));
                previousFeet = feet;
                hasPrevious = true;
            }
        }

        /// <summary>Camera-local hip bias layered on the procedural right-hand grip (meters).</summary>
        public static Float3 NearFieldHipBias(byte weaponSlot)
        {
            return ProductAuthorityActionView.IsRifleSlot(weaponSlot)
                ? new Float3(0.14f, -0.12f, 0.28f)
                : new Float3(0.08f, -0.085f, 0.11f);
        }

        public static float NearFieldScale(byte weaponSlot) =>
            ProductAuthorityActionView.IsRifleSlot(weaponSlot) ? RifleNearFieldScale : PistolNearFieldScale;

        /// <summary>Static hip composition; fade fully to identity for the verified ADS sight ray.</summary>
        public static Float3 HipRotationDegrees(byte slot, float ads)
        {
            if (slot > ProductAuthorityActionView.MaxSlot || !float.IsFinite(ads)) throw new ArgumentOutOfRangeException(nameof(slot));
            float hip = 1f - Math.Clamp(ads, 0f, 1f);
            return ProductAuthorityActionView.IsRifleSlot(slot)
                ? new Float3(0, -18f * hip, -5f * hip)
                : new Float3(0, -3f * hip, -2f * hip);
        }

        /// <summary>Camera-only support-hand reach toward the magazine; returns to its packaged grip at both endpoints.</summary>
        public static Float3 SupportHandOffset(byte slot, float reloadPhase, bool reducedMotion)
        {
            if (slot > ProductAuthorityActionView.MaxSlot) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!float.IsFinite(reloadPhase)) throw new ArgumentOutOfRangeException(nameof(reloadPhase));
            if (!ProductAuthorityActionView.IsGunSlot(slot)) return default;
            float phase = Math.Clamp(reloadPhase, 0f, 1f);
            float reach = (float)Math.Sin(Math.PI * phase);
            reach *= reach;
            float amplitude = reducedMotion ? .55f : 1f;
            var travel = FirstPersonMagazineTravel.Offset(phase, false, reducedMotion);
            return new Float3(.018f * reach * amplitude,
                -.085f * reach * amplitude + travel.Y * .5f,
                (slot == 0 ? -.275f : -.025f) * reach * amplitude + travel.Z * .5f);
        }

        /// <summary>
        /// Short equipment-relative kick from cosmetic recoil springs. At full ADS, only Z kick remains
        /// so the verified sight line stays on the camera ray.
        /// </summary>
        public static Float3 EquipmentRecoilKick(float recoilPitch, float recoilYaw, float ads, bool reducedMotion)
        {
            if (reducedMotion) return default;
            float a = Math.Clamp(ads, 0f, 1f);
            float hip = 1f - a;
            float pitch = recoilPitch;
            float yaw = recoilYaw;
            return new Float3(
                yaw * 0.0065f * hip,
                pitch * 0.0045f * (0.2f + 0.8f * hip),
                -Math.Abs(pitch) * 0.014f - Math.Abs(yaw) * 0.0035f);
        }

        /// <summary>Rotational recoil fades with ADS so optic alignment is not desynced.</summary>
        public static float RecoilRotationScale(float ads, bool reducedMotion) =>
            reducedMotion ? 0f : (1f - Math.Clamp(ads, 0f, 1f));

        public Float3 PositionOffset(float ads, float reloadPhase, bool reducedMotion) =>
            PositionOffset(ads, reloadPhase, reducedMotion, recoilEnergy: 0f, magazineEmpty: false);

        public Float3 PositionOffset(
            float ads,
            float reloadPhase,
            bool reducedMotion,
            float recoilEnergy,
            bool magazineEmpty)
        {
            float hip = 1f - Math.Clamp(ads, 0f, 1f);
            float reload = reloadPhase > 0f ? (float)Math.Sin(Math.PI * Math.Clamp(reloadPhase, 0f, 1f)) : 0f;
            float lower = 1f - Smooth(equip);
            float gait = reducedMotion ? 0f : movement * hip;
            double phase = travel * (Math.PI * 2d / 1.4d);

            Float3 mag = FirstPersonMagazineTravel.Offset(reloadPhase, magazineEmpty, reducedMotion);
            Float3 sway = IdleSway(ads, reloadPhase, reducedMotion, recoilEnergy);

            return new Float3(
                (float)Math.Sin(phase) * .005f * gait + .018f * reload + mag.X + sway.X,
                -(float)Math.Cos(phase * 2d) * .004f * gait - .055f * reload - .12f * lower + mag.Y + sway.Y,
                -.028f * reload - .075f * lower + mag.Z + sway.Z);
        }

        public Float3 RotationDegrees(float ads, float reloadPhase, bool reducedMotion) =>
            RotationDegrees(ads, reloadPhase, reducedMotion, recoilEnergy: 0f);

        public Float3 RotationDegrees(float ads, float reloadPhase, bool reducedMotion, float recoilEnergy)
        {
            if (reducedMotion) return default;
            float hip = 1f - Math.Clamp(ads, 0f, 1f);
            float reload = reloadPhase > 0f ? (float)Math.Sin(Math.PI * Math.Clamp(reloadPhase, 0f, 1f)) : 0f;
            Float3 sway = IdleSway(ads, reloadPhase, false, recoilEnergy);
            return new Float3(
                12f * (1f - Smooth(equip)) + 11f * reload + sway.X * 18f,
                sway.Y * 12f,
                -12f * reload + (float)Math.Sin(travel * Math.PI * 2d / 1.4d) * .5f * movement * hip + sway.Z * 14f);
        }

        private Float3 IdleSway(float ads, float reloadPhase, bool reducedMotion, float recoilEnergy)
        {
            if (reducedMotion || swayBlend < 1e-4f) return default;
            float hip = 1f - Math.Clamp(ads, 0f, 1f);
            if (hip < 0.05f || reloadPhase > 0f || recoilEnergy >= 0.04f) return default;
            float gate = swayBlend * hip;
            double t = idleClock;
            return new Float3(
                (float)Math.Sin(t * 1.35d) * 0.0018f * gate,
                (float)Math.Sin(t * 1.05d + 0.7d) * 0.0012f * gate,
                (float)Math.Cos(t * 0.9d) * 0.0009f * gate);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
