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
        /// <summary>Legacy tuck seconds, used only when the incoming weapon's equip window is unknown.</summary>
        public const float HolsterSeconds = 0.14f;
        /// <summary>Legacy draw seconds, used only when the incoming weapon's equip window is unknown.</summary>
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
        private float holsterSeconds = HolsterSeconds;
        private float drawSeconds = DrawSeconds;
        private float switchElapsed;
        private bool heldOutgoing;
        private float drawOverrideSeconds;
        // Pose difference carried across a mid-phase reversal (switch during a draw, or back during a tuck); it fades
        // out over the new phase so the gun never jumps on screen.
        private Float3 carryPosition;
        private Float3 carryRotation;
        private float carryFrom = 1f;
        private bool hasCarry;
        /// <summary>Shortest draw after a held swap (the throw follow-through already used part of the equip window).</summary>
        public const float MinDrawAfterHoldSeconds = 0.18f;

        public float EquipBlend => equip;
        public float SwayBlend => swayBlend;
        /// <summary>Authority-desired slot (may differ from the mesh mid-switch).</summary>
        public byte DesiredWeaponSlot => desiredSlot;
        /// <summary>Mesh/hands slot currently shown; lags desired until holster completes.</summary>
        public byte VisibleWeaponSlot => visibleSlot;
        /// <summary>The gun on screen (ADR-102 random loadouts); invalid falls back to the slot's default profile.</summary>
        public DefinitionId VisibleWeaponId { get; set; }
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
            holsterSeconds = HolsterSeconds;
            drawSeconds = DrawSeconds;
            switchElapsed = 0f;
            heldOutgoing = false;
            drawOverrideSeconds = 0f;
            ClearCarry();
            carryFrom = 1f;
        }

        /// <summary>True while the incoming weapon is rising (false during the outgoing tuck).</summary>
        public bool Drawing => drawing;
        /// <summary>Seconds of the current tuck and draw (ADR-099: fitted to the incoming weapon's equip ticks).</summary>
        public float CurrentHolsterSeconds => holsterSeconds;
        public float CurrentDrawSeconds => drawSeconds;

        public void Step(Float3 feet, byte weaponSlot, float dt, bool sliding) =>
            Step(feet, weaponSlot, dt, sliding, reducedMotion: false);

        public void Step(Float3 feet, byte weaponSlot, float dt, bool sliding, bool reducedMotion) =>
            Step(feet, weaponSlot, dt, sliding, reducedMotion, equipSeconds: 0f);

        /// <summary>
        /// <paramref name="equipSeconds"/> is the incoming weapon's authoritative equip window (definition equipTicks);
        /// tuck + draw fill it (ADR-099). Non-positive keeps the legacy fixed timing.
        /// </summary>
        public void Step(Float3 feet, byte weaponSlot, float dt, bool sliding, bool reducedMotion, float equipSeconds) =>
            Step(feet, weaponSlot, dt, sliding, reducedMotion, equipSeconds, holdOutgoing: false);

        /// <summary>
        /// <paramref name="holdOutgoing"/> keeps the outgoing weapon on screen while its own action finishes (the grenade
        /// throw follow-through, which ends with the hand already out of view). When the hold ends the mesh swaps without
        /// a tuck and the draw uses what is left of the equip window, never less than <see cref="MinDrawAfterHoldSeconds"/>.
        /// </summary>
        public void Step(Float3 feet, byte weaponSlot, float dt, bool sliding, bool reducedMotion, float equipSeconds,
            bool holdOutgoing)
        {
            if (!float.IsFinite(equipSeconds)) throw new ArgumentOutOfRangeException(nameof(equipSeconds));
            if (!float.IsFinite(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            if (weaponSlot > ProductAuthorityActionView.MaxSlot) throw new ArgumentOutOfRangeException(nameof(weaponSlot));
            if (weaponSlot != desiredSlot)
            {
                desiredSlot = weaponSlot;
                // Begin (or continue) holstering the currently visible mesh; swap only at the bottom.
                if (drawing) BeginPhase(nextDrawing: false);
                swayBlend = 0f;
                switchElapsed = 0f;
                drawOverrideSeconds = 0f;
            }
            (holsterSeconds, drawSeconds) = FirstPersonEquipChoreography.Split(equipSeconds);
            float equipDt = dt;
            if (desiredSlot != visibleSlot)
            {
                if (holdOutgoing)
                {
                    heldOutgoing = true;
                    drawing = false;
                    equip = 1f;
                    equipDt = 0f;
                }
                else if (heldOutgoing)
                {
                    // The follow-through left the hand out of view: swap now and draw in the rest of the window.
                    heldOutgoing = false;
                    visibleSlot = desiredSlot;
                    drawing = true;
                    equip = 0f;
                    ClearCarry();
                    float remaining = equipSeconds > 0f ? equipSeconds - switchElapsed : DrawSeconds;
                    drawOverrideSeconds = Math.Max(MinDrawAfterHoldSeconds, remaining);
                }
            }
            else heldOutgoing = false;
            if (drawOverrideSeconds > 0f) drawSeconds = drawOverrideSeconds;
            if (dt > 0f) switchElapsed += dt;
            if (dt > 0f)
            {
                if (equipDt > 0f) AdvanceEquip(equipDt, reducedMotion);
                if (equip >= 1f && desiredSlot == visibleSlot) drawOverrideSeconds = 0f;
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
            else if (!holdOutgoing && !drawing && desiredSlot != visibleSlot && equip <= SwapEquipThreshold)
            {
                // Zero-dt tests / paused frames can still complete the mesh swap at the bottom.
                visibleSlot = desiredSlot;
                drawing = true;
                equip = 0f;
                ClearCarry();
            }
        }

        private void AdvanceEquip(float dt, bool reducedMotion)
        {
            // Reduced motion shortens the tuck; the draw stays on the authoritative window (fire is blocked until then).
            float holster = holsterSeconds * (reducedMotion ? 0.55f : 1f);
            float draw = drawSeconds;
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
                        // The swap happens fully tucked, out of view: the new weapon starts on its own draw pose.
                        ClearCarry();
                        continue;
                    }

                    equip = Math.Max(0f, equip - remaining / holster);
                    return;
                }

                // Switched back to the visible weapon mid-tuck: rise again from where it is.
                if (!drawing) BeginPhase(nextDrawing: true);
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

        /// <summary>
        /// Presentation aim target: a reload lowers the sights for its whole window, whether the reload is known from
        /// the ReloadStarted event or only from the authoritative weapon state (a missed event edge must not keep a
        /// scope up and hide the reload). Aim re-raises after the reload if the button is still held.
        /// </summary>
        public static bool ViewAimTarget(bool aimHeld, bool reloadEventActive, bool authorityReloading) =>
            aimHeld && !reloadEventActive && !authorityReloading;

        /// <summary>ADR-111: the knife and grenade have no sights, so aiming never pulls them to the screen centre.</summary>
        public static bool ViewAimTarget(bool aimHeld, bool reloadEventActive, bool authorityReloading, byte slot) =>
            ProductAuthorityActionView.IsGunSlot(slot) && ViewAimTarget(aimHeld, reloadEventActive, authorityReloading);

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

        /// <summary>Weapon-specific reload pose for the visible slot (ADR-099).</summary>
        public ReloadPose ReloadPoseFor(float reloadPhase, bool magazineEmpty, bool reducedMotion)
        {
            var profile = WeaponVisualProfiles.Resolve(VisibleWeaponId, visibleSlot);
            return FirstPersonReloadChoreography.Evaluate(profile, reloadPhase, magazineEmpty,
                profile.ReloadSeconds, reducedMotion);
        }

        public Float3 PositionOffset(
            float ads,
            float reloadPhase,
            bool reducedMotion,
            float recoilEnergy,
            bool magazineEmpty)
        {
            float hip = 1f - Math.Clamp(ads, 0f, 1f);
            Float3 reload = ReloadPoseFor(reloadPhase, magazineEmpty, reducedMotion).WeaponPosition;
            float switchAmp = reducedMotion ? 0.55f : 1f;
            Float3 draw = MotionCurve.Scale(SwitchPosition(), switchAmp);
            float gait = reducedMotion ? 0f : movement * hip * Smooth(equip);
            double phase = travel * (Math.PI * 2d / 1.4d);


            Float3 sway = IdleSway(ads, reloadPhase, reducedMotion, recoilEnergy);

            return new Float3(
                (float)Math.Sin(phase) * .005f * gait + reload.X + sway.X + draw.X,
                -(float)Math.Cos(phase * 2d) * .004f * gait + reload.Y + sway.Y + draw.Y,
                reload.Z + sway.Z + draw.Z);
        }

        public Float3 RotationDegrees(float ads, float reloadPhase, bool reducedMotion) =>
            RotationDegrees(ads, reloadPhase, reducedMotion, recoilEnergy: 0f);

        public Float3 RotationDegrees(float ads, float reloadPhase, bool reducedMotion, float recoilEnergy) =>
            RotationDegrees(ads, reloadPhase, reducedMotion, recoilEnergy, magazineEmpty: false);

        public Float3 RotationDegrees(float ads, float reloadPhase, bool reducedMotion, float recoilEnergy, bool magazineEmpty)
        {
            if (reducedMotion) return default;
            float hip = 1f - Math.Clamp(ads, 0f, 1f);
            Float3 reload = ReloadPoseFor(reloadPhase, magazineEmpty, false).WeaponRotationDegrees;
            Float3 draw = SwitchRotation();
            Float3 sway = IdleSway(ads, reloadPhase, false, recoilEnergy);
            // Short mechanical roll/pitch oscillation while the cosmetic spring is still settling.
            float energy = Math.Min(Math.Max(recoilEnergy, 0f), 0.45f);
            float rollKick = energy * 28f * hip;
            float pitchKick = energy * 10f * hip;
            return new Float3(
                reload.X + draw.X + sway.X * 18f + pitchKick,
                reload.Y + draw.Y + sway.Y * 12f,
                reload.Z + draw.Z
                    + (float)Math.Sin(travel * Math.PI * 2d / 1.4d) * .5f * movement * hip + sway.Z * 14f
                    + rollKick);
        }

        /// <summary>Switch translation: generic tuck while holstering, the weapon's own style while drawing (ADR-099).</summary>
        private Float3 SwitchPosition()
        {
            Float3 raw = drawing ? FirstPersonEquipChoreography.DrawPosition(visibleSlot, equip) : TuckPosition(equip);
            return MotionCurve.Add(raw, MotionCurve.Scale(carryPosition, CarryWeight()));
        }

        private Float3 SwitchRotation()
        {
            Float3 raw = drawing ? FirstPersonEquipChoreography.DrawRotationDegrees(visibleSlot, equip) : TuckRotation(equip);
            return MotionCurve.Add(raw, MotionCurve.Scale(carryRotation, CarryWeight()));
        }

        // Holster: down/back with an outward hip swing.
        private static Float3 TuckPosition(float equipBlend)
        {
            float tuck = 1f - Smooth(equipBlend), swing = SwitchSwing(equipBlend);
            return new Float3(0.055f * tuck + 0.04f * swing, -(0.185f * tuck + 0.02f * swing), -(0.125f * tuck + 0.035f * swing));
        }

        private static Float3 TuckRotation(float equipBlend)
        {
            float tuck = 1f - Smooth(equipBlend), swing = SwitchSwing(equipBlend);
            return new Float3(34f * tuck + 10f * swing, 14f * tuck + 18f * swing, -18f * tuck - 8f * swing);
        }

        /// <summary>1 where the phase reversed, 0 at the phase's end (fully tucked or fully ready).</summary>
        private float CarryWeight()
        {
            if (!hasCarry) return 0f;
            float u = drawing
                ? (carryFrom >= 1f - 1e-4f ? 0f : (1f - equip) / (1f - carryFrom))
                : (carryFrom <= 1e-4f ? 0f : equip / carryFrom);
            return Smooth(Math.Clamp(u, 0f, 1f));
        }

        private void BeginPhase(bool nextDrawing)
        {
            Float3 position = SwitchPosition(), rotation = SwitchRotation();
            drawing = nextDrawing;
            carryFrom = equip;
            Float3 rawPosition = drawing ? FirstPersonEquipChoreography.DrawPosition(visibleSlot, equip) : TuckPosition(equip);
            Float3 rawRotation = drawing ? FirstPersonEquipChoreography.DrawRotationDegrees(visibleSlot, equip) : TuckRotation(equip);
            carryPosition = MotionCurve.Sub(position, rawPosition);
            carryRotation = MotionCurve.Sub(rotation, rawRotation);
            hasCarry = true;
        }

        private void ClearCarry()
        {
            carryPosition = carryRotation = default;
            hasCarry = false;
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
            // ADR-099 breathing: a slow ~4.2 s rise and fall with a quicker inhale, layered on the drift.
            double breath = Math.Sin(t * (Math.PI * 2d / 4.2d));
            breath = breath > 0d ? breath : breath * 0.7d;
            return new Float3(
                (float)Math.Sin(t * 1.35d) * 0.0018f * gate,
                ((float)Math.Sin(t * 1.05d + 0.7d) * 0.0012f + (float)breath * 0.0016f) * gate,
                (float)Math.Cos(t * 0.9d) * 0.0009f * gate);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
