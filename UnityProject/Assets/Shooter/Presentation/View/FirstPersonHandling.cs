#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Bounded cosmetic equipment motion. Reads displacement; never changes camera or aim.
    /// Near-field presence, idle sway, reload arcs, equipment-relative recoil kick and
    /// two-phase holster→draw weapon switches are presentation-only.
    /// </summary>
    public sealed class FirstPersonHandling
    {
        // Bodycam-leaning near-field mass: slightly larger mesh scale + hip bias closer to the lens.
        public const float RifleNearFieldScale = 1.06f;
        public const float PistolNearFieldScale = 1.10f;
        /// <summary>Seconds to lower the outgoing weapon before the mesh swap.</summary>
        public const float HolsterSeconds = 0.14f;
        /// <summary>Seconds to raise the incoming weapon after the mesh swap.</summary>
        public const float DrawSeconds = 0.20f;
        /// <summary>Swap mesh when holster reach this equip value (fully tucked).</summary>
        public const float SwapEquipThreshold = 0.02f;

        private Float3 previousFeet;
        private bool hasPrevious;
        private double travel;
        private float movement;
        /// <summary>0 = fully tucked/holstered, 1 = ready at hip/ADS.</summary>
        private float equip = 1f;
        private byte desiredSlot;
        private byte visibleSlot;
        private bool drawing = true;
        private double idleClock;
        private float swayBlend;

        public float EquipBlend => equip;
        public float SwayBlend => swayBlend;
        /// <summary>Authority-desired slot (may differ from the mesh mid-switch).</summary>
        public byte DesiredWeaponSlot => desiredSlot;
        /// <summary>Mesh/hands slot currently shown; lags desired until holster completes.</summary>
        public byte VisibleWeaponSlot => visibleSlot;
        public bool IsSwitching => desiredSlot != visibleSlot || equip < 1f - 1e-4f;

        public void Reset(byte weaponSlot)
        {
            if (weaponSlot > ProductAuthorityActionView.MaxSlot)
                throw new ArgumentOutOfRangeException(nameof(weaponSlot));
            desiredSlot = weaponSlot;
            visibleSlot = weaponSlot;
            previousFeet = default;
            hasPrevious = false;
            travel = 0d;
            movement = 0f;
            equip = 1f;
            drawing = true;
            idleClock = 0d;
            swayBlend = 0f;
        }

        public void Step(Float3 feet, byte weaponSlot, float dt, bool sliding) =>
            Step(feet, weaponSlot, dt, sliding, reducedMotion: false);

        public void Step(Float3 feet, byte weaponSlot, float dt, bool sliding, bool reducedMotion)
        {
            if (!float.IsFinite(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            if (weaponSlot > ProductAuthorityActionView.MaxSlot) throw new ArgumentOutOfRangeException(nameof(weaponSlot));
            if (weaponSlot != desiredSlot)
            {
                desiredSlot = weaponSlot;
                // Begin (or continue) holstering the currently visible mesh; swap only at the bottom.
                drawing = false;
                swayBlend = 0f;
            }
            if (dt > 0f)
            {
                AdvanceEquip(dt, reducedMotion);
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
            else if (!drawing && desiredSlot != visibleSlot && equip <= SwapEquipThreshold)
            {
                // Zero-dt tests / paused frames can still complete the mesh swap at the bottom.
                visibleSlot = desiredSlot;
                drawing = true;
                equip = 0f;
            }
        }

        private void AdvanceEquip(float dt, bool reducedMotion)
        {
            float holster = HolsterSeconds * (reducedMotion ? 0.55f : 1f);
            float draw = DrawSeconds * (reducedMotion ? 0.55f : 1f);
            if (holster < 1e-4f) holster = 1e-4f;
            if (draw < 1e-4f) draw = 1e-4f;
            float remaining = dt;

            // Holster→swap→draw can finish inside one Step when dt covers both phases
            // (tests and hitch frames); leftover time after the tuck starts the draw rise.
            while (remaining > 1e-6f)
            {
                if (desiredSlot != visibleSlot)
                {
                    drawing = false;
                    float timeToTuck = equip * holster;
                    if (remaining + 1e-6f >= timeToTuck || equip <= SwapEquipThreshold)
                    {
                        remaining = Math.Max(0f, remaining - timeToTuck);
                        visibleSlot = desiredSlot;
                        drawing = true;
                        equip = 0f;
                        continue;
                    }

                    equip = Math.Max(0f, equip - remaining / holster);
                    return;
                }

                drawing = true;
                float timeToReady = (1f - equip) * draw;
                if (remaining + 1e-6f >= timeToReady)
                {
                    equip = 1f;
                    return;
                }

                equip = Math.Min(1f, equip + remaining / draw);
                return;
            }
        }

        /// <summary>Camera-local hip bias layered on the procedural right-hand grip (meters).</summary>
        public static Float3 NearFieldHipBias(byte weaponSlot)
        {
            if (ProductAuthorityActionView.IsRifleSlot(weaponSlot))
                return new Float3(0.14f, -0.12f, 0.28f);
            if (weaponSlot == ProductAuthorityActionView.PistolSlot)
                return new Float3(0.08f, -0.085f, 0.11f);
            if (weaponSlot == ProductAuthorityActionView.KnifeSlot)
                return new Float3(0.10f, -0.10f, 0.16f);
            return new Float3(0.09f, -0.09f, 0.14f);
        }

        public static float NearFieldScale(byte weaponSlot)
        {
            if (ProductAuthorityActionView.IsRifleSlot(weaponSlot)) return RifleNearFieldScale;
            if (weaponSlot == ProductAuthorityActionView.PistolSlot) return PistolNearFieldScale;
            return 1.08f;
        }

        /// <summary>Static hip composition; fade fully to identity for the verified ADS sight ray.</summary>
        public static Float3 HipRotationDegrees(byte slot, float ads)
        {
            if (slot > ProductAuthorityActionView.MaxSlot || !float.IsFinite(ads)) throw new ArgumentOutOfRangeException(nameof(slot));
            float hip = 1f - Math.Clamp(ads, 0f, 1f);
            return ProductAuthorityActionView.IsRifleSlot(slot)
                ? new Float3(0, -18f * hip, -5f * hip)
                : new Float3(0, -3f * hip, -2f * hip);
        }

        /// <summary>ADS blend gated by equip so switches cannot pull the tucked gun onto the sight ray.</summary>
        public static float AdsPresentationBlend(float ads, float equipBlend) =>
            Math.Clamp(ads, 0f, 1f) * Smooth(Math.Clamp(equipBlend, 0f, 1f));

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
            // ~1.4× prior hip kick so rifle fire reads as noticeable rearward punch (ADS still Z-only).
            return new Float3(
                yaw * 0.0091f * hip,
                pitch * 0.0063f * (0.2f + 0.8f * hip),
                -Math.Abs(pitch) * 0.020f - Math.Abs(yaw) * 0.005f);
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
            // Ease tuck with a soft mid-bump so holster swings out then settles, and draw rises cleanly.
            float tuck = 1f - Smooth(equip);
            float swing = SwitchSwing(equip);
            float switchAmp = reducedMotion ? 0.55f : 1f;
            float gait = reducedMotion ? 0f : movement * hip * Smooth(equip);
            double phase = travel * (Math.PI * 2d / 1.4d);

            Float3 mag = FirstPersonMagazineTravel.Offset(reloadPhase, magazineEmpty, reducedMotion);
            Float3 sway = IdleSway(ads, reloadPhase, reducedMotion, recoilEnergy);

            // Holster: down/back + outward hip swing; draw reverses with the same arc.
            return new Float3(
                (float)Math.Sin(phase) * .005f * gait + .018f * reload + mag.X + sway.X
                    + (0.055f * tuck + 0.04f * swing) * switchAmp,
                -(float)Math.Cos(phase * 2d) * .004f * gait - .055f * reload + mag.Y + sway.Y
                    - (0.175f * tuck + 0.02f * swing) * switchAmp,
                -.028f * reload + mag.Z + sway.Z
                    - (0.125f * tuck + 0.035f * swing) * switchAmp);
        }

        public Float3 RotationDegrees(float ads, float reloadPhase, bool reducedMotion) =>
            RotationDegrees(ads, reloadPhase, reducedMotion, recoilEnergy: 0f);

        public Float3 RotationDegrees(float ads, float reloadPhase, bool reducedMotion, float recoilEnergy)
        {
            if (reducedMotion) return default;
            float hip = 1f - Math.Clamp(ads, 0f, 1f);
            float reload = reloadPhase > 0f ? (float)Math.Sin(Math.PI * Math.Clamp(reloadPhase, 0f, 1f)) : 0f;
            float tuck = 1f - Smooth(equip);
            float swing = SwitchSwing(equip);
            Float3 sway = IdleSway(ads, reloadPhase, false, recoilEnergy);
            // Short mechanical roll/pitch oscillation while the cosmetic spring is still settling.
            float energy = Math.Min(Math.Max(recoilEnergy, 0f), 0.45f);
            float rollKick = energy * 28f * hip;
            float pitchKick = energy * 10f * hip;
            return new Float3(
                34f * tuck + 10f * swing + 11f * reload + sway.X * 18f + pitchKick,
                14f * tuck + 18f * swing + sway.Y * 12f,
                -12f * reload - 18f * tuck - 8f * swing
                    + (float)Math.Sin(travel * Math.PI * 2d / 1.4d) * .5f * movement * hip + sway.Z * 14f
                    + rollKick);
        }

        /// <summary>Peaks mid-switch so the gun arcs outward before the tuck/draw settles.</summary>
        private static float SwitchSwing(float equipBlend)
        {
            float t = Math.Clamp(equipBlend, 0f, 1f);
            float mid = 1f - Math.Abs(2f * t - 1f);
            return mid * mid;
        }

        private Float3 IdleSway(float ads, float reloadPhase, bool reducedMotion, float recoilEnergy)
        {
            if (reducedMotion || swayBlend < 1e-4f || equip < 0.95f) return default;
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
