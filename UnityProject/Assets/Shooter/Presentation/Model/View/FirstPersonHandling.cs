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
        public static Float3 NearFieldHipBias(in WeaponVisualProfile profile) => profile.NearFieldHipBias;

        public static Float3 NearFieldHipBias(DefinitionId weaponId, byte weaponSlot) =>
            NearFieldHipBias(WeaponVisualProfiles.Resolve(weaponId, weaponSlot));

        public static Float3 NearFieldHipBias(byte weaponSlot) =>
            NearFieldHipBias(WeaponVisualProfiles.Resolve(default, weaponSlot));

        public static float NearFieldScale(in WeaponVisualProfile profile) => profile.NearFieldScale;

        public static float NearFieldScale(DefinitionId weaponId, byte weaponSlot) =>
            NearFieldScale(WeaponVisualProfiles.Resolve(weaponId, weaponSlot));

        public static float NearFieldScale(byte weaponSlot) =>
            NearFieldScale(WeaponVisualProfiles.Resolve(default, weaponSlot));

        /// <summary>Static hip composition; fade fully to identity for the verified ADS sight ray.</summary>
        public static Float3 HipRotationDegrees(in WeaponVisualProfile profile, float ads)
        {
            if (!float.IsFinite(ads)) throw new ArgumentOutOfRangeException(nameof(ads));
            float hip = 1f - Math.Clamp(ads, 0f, 1f);
            var baseRot = profile.HipRotationDegrees;
            return new Float3(baseRot.X * hip, baseRot.Y * hip, baseRot.Z * hip);
        }

        public static Float3 HipRotationDegrees(byte slot, float ads)
        {
            if (slot > ProductAuthorityActionView.MaxSlot) throw new ArgumentOutOfRangeException(nameof(slot));
            return HipRotationDegrees(WeaponVisualProfiles.Resolve(default, slot), ads);
        }

        /// <summary>ADS blend gated by equip so switches cannot pull the tucked gun onto the sight ray.</summary>
        public static float AdsPresentationBlend(float ads, float equipBlend) =>
            Math.Clamp(ads, 0f, 1f) * Smooth(Math.Clamp(equipBlend, 0f, 1f));

        /// <summary>Camera-only support-hand reach toward the magazine; returns to its packaged grip at both endpoints.</summary>
        public static Float3 SupportHandOffset(byte slot, float reloadPhase, bool reducedMotion, bool magazineEmpty = false)
        {
            if (slot > ProductAuthorityActionView.MaxSlot) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!float.IsFinite(reloadPhase)) throw new ArgumentOutOfRangeException(nameof(reloadPhase));
            if (!ProductAuthorityActionView.IsGunSlot(slot)) return default;
            float phase = Math.Clamp(reloadPhase, 0f, 1f);
            float Ramp(float start,float end) { float t=Math.Clamp((phase-start)/(end-start),0,1);return t*t*(3-2*t); }
            float reach=Ramp(.04f,.20f)*(1-Ramp(.84f,.98f));
            var travel=FirstPersonMagazineTravel.Offset(phase,magazineEmpty,reducedMotion);
            return new Float3(.018f*reach+travel.X,
                (slot==0?-.087f:-.044f)*reach+travel.Y,
                (slot==0?-.262f:-.017f)*reach+travel.Z);
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
            // The per-shot spring peaks at a few degrees, so the rearward punch carries most of the kick: ~1.7 cm
            // for the rifle at the peak. ADS keeps only Z (plus a trace of Y) so the sight stays on the camera ray;
            // the camera recoil layer supplies the visible climb there.
            return new Float3(
                yaw * 0.06f * hip,
                pitch * 0.05f * (0.2f + 0.8f * hip),
                -Math.Abs(pitch) * 0.30f - Math.Abs(yaw) * 0.04f);
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


            Float3 sway = IdleSway(ads, reloadPhase, reducedMotion, recoilEnergy);

            // Holster: down/back + outward hip swing; draw reverses with the same arc.
            return new Float3(
                (float)Math.Sin(phase) * .005f * gait + .018f * reload + sway.X
                    + (0.055f * tuck + 0.04f * swing) * switchAmp,
                -(float)Math.Cos(phase * 2d) * .004f * gait - .055f * reload + sway.Y
                    - (0.185f * tuck + 0.02f * swing) * switchAmp,
                -.028f * reload + sway.Z
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
