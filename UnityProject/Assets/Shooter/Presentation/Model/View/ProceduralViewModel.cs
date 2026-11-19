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
    /// Reload duration comes from ActionPayload start/end ticks, never from ammo drop.
    /// Knife slash/stab and grenade throw phases are presentation-only.
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
        private float reloadDurationSeconds = 2.4f;
        private float cycleOpen;
        /// <summary>ADR-102 bolt-action: 0..1 progress of working the bolt after a shot; 0 when idle.</summary>
        private float boltPhase;
        private float boltSeconds = 0.9f;
        private float meleePhase;
        private bool meleeStab;
        private float meleeDurationSeconds = FirstPersonSpecialActions.MeleeDurationSeconds;
        private uint meleeSwings;
        private float throwPhase;
        private Float3 throwStartPosition;
        private Float3 throwStartRotation;
        private bool cooking;
        private float cookSeconds;
        private float fuseSeconds;
        private float adsSeconds = WeaponVisualProfile.DefaultAdsSeconds;
        private float cycleSeconds = DefaultCycleSeconds;
        private bool reducedMotionHint;
        private byte activeSlot;
        private Float3 leftGrip = new Float3(-0.12f, -0.08f, 0.35f);
        private Float3 rightGrip = new Float3(0.08f, -0.10f, 0.40f);

        /// <summary>Bolt/slide close time after a shot when the weapon's fire interval is unknown.</summary>
        public const float DefaultCycleSeconds = 0.09f;

        public ViewPose Pose => new ViewPose(leftGrip, rightGrip, ads, recoilPitch, recoilYaw);
        public float ReloadPhase => reloadPhase > 0f ? reloadPhase : 0f;
        /// <summary>
        /// 0 = bolt/slide closed, 1 = fully open after a shot impulse. Eased so the part lingers at the rear and
        /// slams home (ADR-099).
        /// </summary>
        public float CycleOpen01 => boltPhase > 0f ? (pumpAction ? PumpOpen(boltPhase) : BoltOpen(boltPhase))
            : 1f - (1f - cycleOpen) * (1f - cycleOpen);

        /// <summary>True while the bolt (or, ADR-118, the pump) is being worked after a manual-action shot.</summary>
        public bool BoltCycling => boltPhase > 0f;

        /// <summary>True while the ADR-118 pump stroke runs (the bolt-phase clock drives it).</summary>
        public bool PumpCycling => boltPhase > 0f && pumpAction;

        private bool pumpAction;
        private float pumpTravel;

        /// <summary>ADR-118 pump stroke over its share of the fire interval: back fast after the recoil, then home.</summary>
        public static float PumpOpen(float phase) =>
            MotionCurve.Ramp(phase, .18f, .46f) * (1f - MotionCurve.Ramp(phase, .54f, .86f));

        /// <summary>Share of the fire interval the pump stroke takes (the audio router schedules its sound to match).</summary>
        public const float PumpCycleShare = 0.70f;

        // Bolt knob relative to the support hand on the forend (geometry.weapon 1.2.0 bolt archetype).
        private static readonly Float3 BoltKnobReach = new Float3(0.052f, 0.082f, -0.410f);

        /// <summary>Bolt travel over the cycle: closed while the hand reaches, opens on the pull, closes on the push.</summary>
        public static float BoltOpen(float phase) =>
            MotionCurve.Ramp(phase, .34f, .52f) * (1f - MotionCurve.Ramp(phase, .60f, .76f));

        /// <summary>Support hand travel to the bolt knob, lift, pull, push, lower and back to the forend.</summary>
        public static Float3 BoltHandReach(float phase)
        {
            if (phase <= 0f || phase >= 1f) return default;
            float reach = MotionCurve.Ramp(phase, .04f, .26f) * (1f - MotionCurve.Ramp(phase, .80f, .98f));
            float lift = MotionCurve.Ramp(phase, .26f, .34f) * (1f - MotionCurve.Ramp(phase, .76f, .82f));
            float pull = BoltOpen(phase);
            return new Float3(BoltKnobReach.X * reach, BoltKnobReach.Y * reach + .022f * lift, BoltKnobReach.Z * reach - .09f * pull);
        }
        /// <summary>Linear cycle state (tests and diagnostics).</summary>
        public float CycleLinear01 => cycleOpen;
        public float CycleSeconds => cycleSeconds;
        public float AdsSeconds => adsSeconds;
        public float MeleeDurationSeconds => meleeDurationSeconds;
        /// <summary>+1 / -1 alternating cut direction of the current or last knife swing.</summary>
        public int MeleeDirection => (meleeSwings & 1u) == 1u ? 1 : -1;
        /// <summary>Seconds since the current cook started (0 when not cooking).</summary>
        public float CookSeconds => cooking ? cookSeconds : 0f;
        /// <summary>True while the throw follow-through plays; it outlives the authority switch back to the rifle.</summary>
        public bool Throwing => throwPhase > 0f;
        public byte ActiveWeaponSlot => activeSlot;
        /// <summary>0 idle; (0,1] active knife melee cosmetic phase.</summary>
        public float MeleePhase01 => meleePhase > 0f ? meleePhase : 0f;
        public bool MeleeIsStab => meleeStab;
        /// <summary>0 idle; (0,1] active grenade throw release cosmetic phase.</summary>
        public float ThrowPhase01 => throwPhase > 0f ? throwPhase : 0f;
        public bool Cooking => cooking;

        public void ResetForLife(byte weaponSlot = ProductAuthorityActionView.RifleSlot)
        {
            if (weaponSlot > ProductAuthorityActionView.MaxSlot)
                throw new ArgumentOutOfRangeException(nameof(weaponSlot));
            activeSlot = weaponSlot;
            reloadPhase = 0f;
            cycleOpen = 0f;
            meleePhase = 0f;
            meleeStab = false;
            meleeSwings = 0;
            throwPhase = 0f;
            cooking = false;
            cookSeconds = 0f;
            recoilPitch = recoilYaw = recoilPitchVel = recoilYawVel = 0f;
            ads = 0f;
            consumedEvents.Clear();
            ApplyProfile(WeaponVisualProfiles.Resolve(default, weaponSlot));
            ApplyGripPose();
        }

        public void SetActiveWeaponSlot(byte weaponSlot)
        {
            if (weaponSlot > ProductAuthorityActionView.MaxSlot)
                throw new ArgumentOutOfRangeException(nameof(weaponSlot));
            if (activeSlot != weaponSlot)
            {
                activeSlot = weaponSlot;
                reloadPhase = 0f;
                cycleOpen = 0f;
                boltPhase = 0f;
                meleePhase = 0f;
                meleeStab = false;
                // The authority switches back to the rifle on the release tick; the throw follow-through keeps
                // playing on the outgoing grenade (ADR-099) while FirstPersonHandling holds the swap.
                cooking = false;
                cookSeconds = 0f;
                recoilPitch = recoilYaw = recoilPitchVel = recoilYawVel = 0f;
                ads = 0f;
                ApplyGripPose();
            }
        }

        /// <summary>Per-weapon timing from the definition-file profile (ADS raise, bolt cycle, fuse) - ADR-099.</summary>
        public void ApplyProfile(in WeaponVisualProfile profile)
        {
            adsSeconds = profile.AdsSeconds >= 0.05f ? profile.AdsSeconds : WeaponVisualProfile.DefaultAdsSeconds;
            float interval = profile.FireIntervalSeconds;
            cycleSeconds = interval > 0f ? Math.Min(DefaultCycleSeconds, interval * 0.8f) : DefaultCycleSeconds;
            if (profile.Family == WeaponVisualFamily.Throwable) fuseSeconds = profile.FuseSeconds;
            if (profile.Family == WeaponVisualFamily.Melee && interval > 0f) meleeDurationSeconds = interval;
        }

        /// <summary>Mirror authority Cooking for grenade raise pose (ActorLifecycleState.WeaponAction).</summary>
        public void SetCooking(bool isCooking)
        {
            bool next = isCooking && ProductAuthorityActionView.IsGrenadeSlot(activeSlot);
            if (next && !cooking) cookSeconds = 0f;
            cooking = next;
            if (!cooking) return;
            // Cook cancels an unfinished throw pose if authority restarted a cook (should not happen mid-flight).
            if (throwPhase > 0f && throwPhase < 1f) throwPhase = 0f;
        }

        /// <summary>Cosmetic ADS/spring only when combat event leases are unavailable on the product path.</summary>
        public void TickAim(bool aimHeld, float dtSeconds)
        {
            if (!float.IsFinite(dtSeconds) || dtSeconds < 0) throw new ArgumentOutOfRangeException(nameof(dtSeconds));
            float targetAds = aimHeld ? 1f : 0f;
            ads = MoveTowards(ads, targetAds, dtSeconds / adsSeconds);
            IntegrateSpring(ref recoilPitch, ref recoilPitchVel, dtSeconds);
            IntegrateSpring(ref recoilYaw, ref recoilYawVel, dtSeconds);
            AdvanceCycle(dtSeconds);
            AdvanceBolt(dtSeconds);
            AdvanceReload(dtSeconds);
            AdvanceMelee(dtSeconds);
            AdvanceThrow(dtSeconds);
            if (cooking) cookSeconds += dtSeconds;
            ApplyGripPose();
        }

        public void Consume(SnapshotLease snapshot, EventLease events, bool aimHeld, float dtSeconds)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (events == null) throw new ArgumentNullException(nameof(events));
            ConsumeEvents(events.Events, default, aimHeld, dtSeconds);
        }

        /// <summary>
        /// Product-path consume: optional local-entity filter so remote combat events do not drive FP springs.
        /// </summary>
        public void ConsumeEvents(IReadOnlyList<CombatEvent> events, EntityId localEntity, bool aimHeld, float dtSeconds)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (!float.IsFinite(dtSeconds) || dtSeconds < 0) throw new ArgumentOutOfRangeException(nameof(dtSeconds));

            float targetAds = aimHeld ? 1f : 0f;
            ads = MoveTowards(ads, targetAds, dtSeconds / adsSeconds);

            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (localEntity.IsValid && !ev.Instigator.Equals(localEntity)) continue;
                string key = ev.Id.AuthoritySessionId + ":" + ev.Id.Tick + ":" + ev.Id.EntityId + ":" + ev.Id.Ordinal;
                if (!consumedEvents.Add(key)) continue;

                if (ev.Kind == CombatEventKind.ShotAccepted)
                {
                    if (ProductAuthorityActionView.IsGunSlot(activeSlot))
                    {
                        // Per-shot kick sized from the firing weapon's own recoil (its definition file), on a
                        // stiff spring that peaks ~31 ms after the report and settles before the next auto round.
                        var shot = ev.Payload as ShotPayload;
                        var profile = WeaponVisualProfiles.Resolve(shot?.WeaponId ?? default, activeSlot);
                        var (pitchImpulse, yawImpulse) = ShotKickImpulse(profile);
                        uint shotIndex = shot?.ShotIndex ?? ev.Id.Ordinal;
                        recoilPitchVel += pitchImpulse;
                        recoilYawVel += ((shotIndex & 1u) == 0u ? -1f : 1f) * yawImpulse;
                        float interval = profile.FireIntervalSeconds;
                        cycleSeconds = interval > 0f ? Math.Min(DefaultCycleSeconds, interval * 0.8f) : DefaultCycleSeconds;
                        cycleOpen = Math.Max(cycleOpen, FirstPersonWeaponMechanics.CycleImpulse);
                        if (profile.HandlingStyle == WeaponHandlingStyle.Bolt && interval > 0f)
                        {
                            // Work the bolt inside the authoritative fire interval, after the recoil settles.
                            boltSeconds = Math.Max(0.4f, interval * 0.85f);
                            boltPhase = 0.01f;
                            pumpAction = false;
                            cycleOpen = 0f;
                        }
                        else if (profile.HandlingStyle == WeaponHandlingStyle.Pump && interval > 0f)
                        {
                            // ADR-118: rack the pump inside the fire interval; the support hand rides the forend.
                            boltSeconds = Math.Max(0.25f, interval * PumpCycleShare);
                            boltPhase = 0.01f;
                            pumpAction = true;
                            pumpTravel = profile.CycleTravelMeters;
                            cycleOpen = 0f;
                        }
                    }
                    else if (ProductAuthorityActionView.IsKnifeSlot(activeSlot))
                    {
                        // Swing length is the knife's authoritative fire interval; cuts alternate direction.
                        var shot = ev.Payload as ShotPayload;
                        var profile = WeaponVisualProfiles.Resolve(shot?.WeaponId ?? default, activeSlot);
                        if (profile.FireIntervalSeconds > 0f) meleeDurationSeconds = profile.FireIntervalSeconds;
                        meleeSwings++;
                        meleePhase = 0.01f;
                        meleeStab = HasMeleeHit(events, ev.Instigator, ev.Id.Tick);
                    }
                }
                else if (ev.Kind == CombatEventKind.ProjectileSpawned
                    && (ProductAuthorityActionView.IsGrenadeSlot(activeSlot) || IsThrowable(ev.Payload)))
                {
                    // Start from the pose the grenade had when it left the cook, so the release never pops.
                    float cooked = cooking ? cookSeconds : FirstPersonSpecialActions.CookRaiseSeconds;
                    throwStartPosition = FirstPersonSpecialActions.GrenadeCookPosition(cooked, fuseSeconds, reducedMotionHint);
                    throwStartRotation = FirstPersonSpecialActions.GrenadeCookRotation(cooked, fuseSeconds, reducedMotionHint);
                    throwPhase = 0.01f;
                    cooking = false;
                    cookSeconds = 0f;
                }
                else if (ev.Kind == CombatEventKind.ReloadStarted && ev.Payload is ActionPayload started)
                {
                    activeSlot = started.Slot;
                    reloadDurationSeconds = ProductAuthorityActionView.DurationSeconds(started.StartTick, started.EndTick);
                    reloadPhase = 0.01f;
                }
                else if (ev.Kind == CombatEventKind.ReloadCompleted)
                {
                    reloadPhase = 0f;
                    if (ev.Payload is ActionPayload completed)
                        activeSlot = completed.Slot;
                }
            }

            IntegrateSpring(ref recoilPitch, ref recoilPitchVel, dtSeconds);
            IntegrateSpring(ref recoilYaw, ref recoilYawVel, dtSeconds);
            AdvanceCycle(dtSeconds);
            AdvanceReload(dtSeconds);
            AdvanceMelee(dtSeconds);
            AdvanceThrow(dtSeconds);
            if (cooking) cookSeconds += dtSeconds;
            ApplyGripPose();
        }

        public bool HandsWithinGripTolerance(Float3 weaponLeftSocket, Float3 weaponRightSocket) =>
            Distance(leftGrip, weaponLeftSocket) <= GripToleranceMeters
            && Distance(rightGrip, weaponRightSocket) <= GripToleranceMeters;

        public int ConsumedEventCount => consumedEvents.Count;

        /// <summary>Combined equipment offset for knife/grenade special actions (meters).</summary>
        public Float3 SpecialActionPosition(bool reducedMotion)
        {
            reducedMotionHint = reducedMotion;
            if (throwPhase > 0f)
                return FirstPersonSpecialActions.GrenadeThrowPosition(throwPhase, throwStartPosition, reducedMotion);
            if (ProductAuthorityActionView.IsKnifeSlot(activeSlot) && meleePhase > 0f)
                return FirstPersonSpecialActions.MeleePositionOffset(meleePhase, meleeStab, MeleeDirection, reducedMotion);
            if (ProductAuthorityActionView.IsGrenadeSlot(activeSlot) && cooking)
                return FirstPersonSpecialActions.GrenadeCookPosition(cookSeconds, fuseSeconds, reducedMotion);
            return default;
        }

        public Float3 SpecialActionRotation(bool reducedMotion)
        {
            if (throwPhase > 0f)
                return FirstPersonSpecialActions.GrenadeThrowRotation(throwPhase, throwStartRotation, reducedMotion);
            if (ProductAuthorityActionView.IsKnifeSlot(activeSlot) && meleePhase > 0f)
                return FirstPersonSpecialActions.MeleeRotationDegrees(meleePhase, meleeStab, MeleeDirection, reducedMotion);
            if (ProductAuthorityActionView.IsGrenadeSlot(activeSlot) && cooking)
                return FirstPersonSpecialActions.GrenadeCookRotation(cookSeconds, fuseSeconds, reducedMotion);
            if (boltPhase > 0f && pumpAction)
            {
                // A short dip and back as the pump is racked.
                float pump = PumpOpen(boltPhase);
                return new Float3((reducedMotion ? 1f : 2f) * pump, 0f, -(reducedMotion ? 1f : 2f) * pump);
            }
            if (boltPhase > 0f)
            {
                float roll = MotionCurve.Ramp(boltPhase, .06f, .26f) * (1f - MotionCurve.Ramp(boltPhase, .80f, .98f));
                return new Float3(2f * roll, -3f * roll, (reducedMotion ? 5f : 10f) * roll);
            }
            return default;
        }

        /// <summary>Support-hand wrist offset for special actions (the pin pull); hand-root local meters.</summary>
        public Float3 SpecialSupportWrist =>
            cooking && ProductAuthorityActionView.IsGrenadeSlot(activeSlot)
                ? FirstPersonSpecialActions.PinPullWrist(cookSeconds)
                : boltPhase > 0f ? (pumpAction ? new Float3(0f, 0f, -pumpTravel * PumpOpen(boltPhase)) : BoltHandReach(boltPhase)) : default;

        /// <summary>Support-hand finger openness for special actions.</summary>
        public float SpecialSupportOpen =>
            cooking && ProductAuthorityActionView.IsGrenadeSlot(activeSlot)
                ? FirstPersonSpecialActions.PinPullOpen(cookSeconds)
                : boltPhase > 0f && !pumpAction ? .7f * MotionCurve.Ramp(boltPhase, .04f, .16f) * (1f - MotionCurve.Ramp(boltPhase, .22f, .30f))
                    + .7f * MotionCurve.Ramp(boltPhase, .78f, .86f) * (1f - MotionCurve.Ramp(boltPhase, .92f, 1f)) : 0f;

        /// <summary>
        /// Hide the held grenade once it leaves the hand, or when the active grenade slot is empty and not cooking.
        /// The hands stay visible through the follow-through.
        /// </summary>
        public bool HideHeldGrenade(ushort magazine) =>
            throwPhase > FirstPersonSpecialActions.ThrowReleasePhase
            || (ProductAuthorityActionView.IsGrenadeSlot(activeSlot) && magazine == 0 && !cooking);

        private void AdvanceReload(float dtSeconds)
        {
            if (reloadPhase <= 0f) return;
            float duration = reloadDurationSeconds > 1e-4f ? reloadDurationSeconds : 2.4f;
            reloadPhase = Math.Min(1f, reloadPhase + dtSeconds / duration);
            if (reloadPhase >= 1f) reloadPhase = 0f;
        }

        private void AdvanceBolt(float dtSeconds)
        {
            if (boltPhase <= 0f) return;
            // A reload takes over the bolt (its own track works it).
            if (reloadPhase > 0f) { boltPhase = 0f; return; }
            boltPhase = Math.Min(1f, boltPhase + dtSeconds / boltSeconds);
            if (boltPhase >= 1f) boltPhase = 0f;
        }

        private void AdvanceCycle(float dtSeconds)
        {
            if (cycleOpen <= 0f) { cycleOpen = 0f; return; }
            // Bolt/slide closes within the weapon's own cycle time (at most ~90 ms, under its fire interval).
            cycleOpen = MoveTowards(cycleOpen, 0f, dtSeconds / cycleSeconds);
        }

        private void AdvanceMelee(float dtSeconds)
        {
            if (meleePhase <= 0f) return;
            float duration = meleeDurationSeconds;
            meleePhase = Math.Min(1f, meleePhase + (duration > 1e-4f ? dtSeconds / duration : 1f));
            if (meleePhase >= 1f)
            {
                meleePhase = 0f;
                meleeStab = false;
            }
        }

        private void AdvanceThrow(float dtSeconds)
        {
            if (throwPhase <= 0f) return;
            float duration = FirstPersonSpecialActions.ThrowDurationSeconds;
            throwPhase = Math.Min(1f, throwPhase + (duration > 1e-4f ? dtSeconds / duration : 1f));
            if (throwPhase >= 1f) throwPhase = 0f;
        }

        private static bool IsThrowable(CombatPayload? payload) =>
            payload is ProjectilePayload projectile
            && WeaponVisualProfiles.TryGet(projectile.DefinitionId, out var profile)
            && profile.Family == WeaponVisualFamily.Throwable;

        private static bool HasMeleeHit(IReadOnlyList<CombatEvent> events, EntityId instigator, uint tick)
        {
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.Id.Tick != tick || !ev.Instigator.Equals(instigator)) continue;
                if (ev.Kind == CombatEventKind.DamageApplied && ev.Target.IsValid) return true;
                if (ev.Kind == CombatEventKind.Impact && ev.Target.IsValid) return true;
            }
            return false;
        }

        private void ApplyGripPose()
        {
            // Near-field Bodycam mass: grips sit a touch lower/closer; ADS still pulls to the sight plane.
            float adsPull = ads * 0.09f;
            leftGrip = new Float3(-0.12f, -0.09f, 0.33f);

            float hipZ = ProductAuthorityActionView.IsRifleSlot(activeSlot) ? 0.36f : 0.28f;
            float hipY = ProductAuthorityActionView.IsRifleSlot(activeSlot) ? -0.12f : -0.09f;
            rightGrip = new Float3(0.085f - adsPull, hipY + ads * 0.025f, hipZ + adsPull);
            leftGrip = new Float3(leftGrip.X - adsPull * 0.5f, leftGrip.Y, leftGrip.Z + adsPull * 0.5f);
        }

        /// <summary>Equipment-relative kick meters from the cosmetic springs (ADS-safe).</summary>
        public Float3 EquipmentKickMeters(bool reducedMotion) =>
            FirstPersonHandling.EquipmentRecoilKick(recoilPitch, recoilYaw, ads, reducedMotion);

        /// <summary>Viewmodel kick spring: peak ~31 ms after a shot, ~85% settled within one 600 RPM interval.</summary>
        public const float KickDamping = 18f;
        public const float KickFrequency = 36f;
        /// <summary>Visual muzzle-rise at the kick peak per degree of the weapon's authoritative recoil pitch.</summary>
        public const float KickPeakRadiansPerRecoilDegree = 0.16f;
        /// <summary>Visual sideways kick at the peak per degree of the weapon's yaw half-range.</summary>
        public const float KickYawRadiansPerRecoilDegree = 0.30f;

        /// <summary>Velocity impulses (rad/s) for one shot of <paramref name="profile"/>; zero for recoil-less weapons.</summary>
        public static (float pitch, float yaw) ShotKickImpulse(in WeaponVisualProfile profile)
        {
            float peak = DampedSpring.FirstPeakPerUnitVelocity(KickDamping, KickFrequency);
            return (profile.RecoilPitchDegrees * KickPeakRadiansPerRecoilDegree / peak,
                profile.RecoilYawHalfDegrees * KickYawRadiansPerRecoilDegree / peak);
        }

        // Frame-rate independent exact spring solution (see DampedSpring).
        private static void IntegrateSpring(ref float position, ref float velocity, float dt) =>
            DampedSpring.Step(ref position, ref velocity, dt, KickDamping, KickFrequency);

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
