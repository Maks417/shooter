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
        private float meleePhase;
        private bool meleeStab;
        private float throwPhase;
        private bool cooking;
        private byte activeSlot;
        private Float3 leftGrip = new Float3(-0.12f, -0.08f, 0.35f);
        private Float3 rightGrip = new Float3(0.08f, -0.10f, 0.40f);

        public ViewPose Pose => new ViewPose(leftGrip, rightGrip, ads, recoilPitch, recoilYaw);
        public float ReloadPhase => reloadPhase > 0f ? reloadPhase : 0f;
        /// <summary>0 = bolt/slide closed, 1 = fully open after a shot impulse.</summary>
        public float CycleOpen01 => cycleOpen;
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
            throwPhase = 0f;
            cooking = false;
            recoilPitch = recoilYaw = recoilPitchVel = recoilYawVel = 0f;
            ads = 0f;
            consumedEvents.Clear();
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
                meleePhase = 0f;
                meleeStab = false;
                throwPhase = 0f;
                cooking = false;
                recoilPitch = recoilYaw = recoilPitchVel = recoilYawVel = 0f;
                ads = 0f;
                ApplyGripPose();
            }
        }

        /// <summary>Mirror authority Cooking for grenade raise pose (ActorLifecycleState.WeaponAction).</summary>
        public void SetCooking(bool isCooking)
        {
            cooking = isCooking && ProductAuthorityActionView.IsGrenadeSlot(activeSlot);
            if (!cooking) return;
            // Cook cancels an unfinished throw pose if authority restarted a cook (should not happen mid-flight).
            if (throwPhase > 0f && throwPhase < 1f) throwPhase = 0f;
        }

        /// <summary>Cosmetic ADS/spring only when combat event leases are unavailable on the product path.</summary>
        public void TickAim(bool aimHeld, float dtSeconds)
        {
            if (!float.IsFinite(dtSeconds) || dtSeconds < 0) throw new ArgumentOutOfRangeException(nameof(dtSeconds));
            float targetAds = aimHeld ? 1f : 0f;
            ads = MoveTowards(ads, targetAds, dtSeconds / 0.15f);
            IntegrateSpring(ref recoilPitch, ref recoilPitchVel, dtSeconds);
            IntegrateSpring(ref recoilYaw, ref recoilYawVel, dtSeconds);
            AdvanceCycle(dtSeconds);
            AdvanceReload(dtSeconds);
            AdvanceMelee(dtSeconds);
            AdvanceThrow(dtSeconds);
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
            ads = MoveTowards(ads, targetAds, dtSeconds / 0.15f);

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
                        recoilPitchVel += activeSlot == ProductAuthorityActionView.PistolSlot ? 3.2f : 2.5f;
                        recoilYawVel += ((ev.Id.Ordinal & 1) == 0 ? -1f : 1f) *
                            (activeSlot == ProductAuthorityActionView.PistolSlot ? 0.85f : 0.6f);
                        cycleOpen = Math.Max(cycleOpen, FirstPersonWeaponMechanics.CycleImpulse);
                    }
                    else if (ProductAuthorityActionView.IsKnifeSlot(activeSlot))
                    {
                        meleePhase = 0.01f;
                        meleeStab = HasMeleeHit(events, ev.Instigator, ev.Id.Tick);
                    }
                }
                else if (ev.Kind == CombatEventKind.ProjectileSpawned
                    && ProductAuthorityActionView.IsGrenadeSlot(activeSlot))
                {
                    throwPhase = 0.01f;
                    cooking = false;
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
            ApplyGripPose();
        }

        public bool HandsWithinGripTolerance(Float3 weaponLeftSocket, Float3 weaponRightSocket) =>
            Distance(leftGrip, weaponLeftSocket) <= GripToleranceMeters
            && Distance(rightGrip, weaponRightSocket) <= GripToleranceMeters;

        public int ConsumedEventCount => consumedEvents.Count;

        /// <summary>Combined equipment offset for knife/grenade special actions (meters).</summary>
        public Float3 SpecialActionPosition(bool reducedMotion)
        {
            if (ProductAuthorityActionView.IsKnifeSlot(activeSlot) && meleePhase > 0f)
                return FirstPersonSpecialActions.MeleePositionOffset(meleePhase, meleeStab, reducedMotion);
            if (ProductAuthorityActionView.IsGrenadeSlot(activeSlot))
            {
                if (throwPhase > 0f)
                    return FirstPersonSpecialActions.GrenadeThrowPosition(throwPhase, reducedMotion);
                if (cooking)
                    return FirstPersonSpecialActions.GrenadeCookPosition(true, reducedMotion);
            }
            return default;
        }

        public Float3 SpecialActionRotation(bool reducedMotion)
        {
            if (ProductAuthorityActionView.IsKnifeSlot(activeSlot) && meleePhase > 0f)
                return FirstPersonSpecialActions.MeleeRotationDegrees(meleePhase, meleeStab, reducedMotion);
            if (ProductAuthorityActionView.IsGrenadeSlot(activeSlot))
            {
                if (throwPhase > 0f)
                    return FirstPersonSpecialActions.GrenadeThrowRotation(throwPhase, reducedMotion);
                if (cooking)
                    return FirstPersonSpecialActions.GrenadeCookRotation(true, reducedMotion);
            }
            return default;
        }

        /// <summary>True once a throw phase has started or magazine is empty on grenade (hide held prop).</summary>
        public bool HideHeldGrenade(ushort magazine) =>
            ProductAuthorityActionView.IsGrenadeSlot(activeSlot) && (magazine == 0 || throwPhase > 0.35f);

        private void AdvanceReload(float dtSeconds)
        {
            if (reloadPhase <= 0f) return;
            float duration = reloadDurationSeconds > 1e-4f ? reloadDurationSeconds : 2.4f;
            reloadPhase = Math.Min(1f, reloadPhase + dtSeconds / duration);
            if (reloadPhase >= 1f) reloadPhase = 0f;
        }

        private void AdvanceCycle(float dtSeconds)
        {
            if (cycleOpen <= 0f) { cycleOpen = 0f; return; }
            // Bolt/slide closes in ~90 ms after the impulse peak.
            cycleOpen = MoveTowards(cycleOpen, 0f, dtSeconds / 0.09f);
        }

        private void AdvanceMelee(float dtSeconds)
        {
            if (meleePhase <= 0f) return;
            float duration = FirstPersonSpecialActions.MeleeDurationSeconds;
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
            Float3 mag = FirstPersonMagazineTravel.Offset(reloadPhase, magazineEmpty: false, reducedMotion: false);
            if (reloadPhase > 0f)
                leftGrip = new Float3(-0.12f + mag.X, -0.09f + mag.Y, 0.33f + mag.Z);
            else
                leftGrip = new Float3(-0.12f, -0.09f, 0.33f);

            float hipZ = ProductAuthorityActionView.IsRifleSlot(activeSlot) ? 0.36f : 0.28f;
            float hipY = ProductAuthorityActionView.IsRifleSlot(activeSlot) ? -0.12f : -0.09f;
            rightGrip = new Float3(0.085f - adsPull, hipY + ads * 0.025f, hipZ + adsPull);
            leftGrip = new Float3(leftGrip.X - adsPull * 0.5f, leftGrip.Y, leftGrip.Z + adsPull * 0.5f);
        }

        /// <summary>Equipment-relative kick meters from the cosmetic springs (ADS-safe).</summary>
        public Float3 EquipmentKickMeters(bool reducedMotion) =>
            FirstPersonHandling.EquipmentRecoilKick(recoilPitch, recoilYaw, ads, reducedMotion);

        // Exact solution of x'' + 12x' + 40x = 0. Frame rate and pause do not
        // change the spring energy; large frame gaps decay rather than exploding.
        private static void IntegrateSpring(ref float position, ref float velocity, float dt)
        {
            double decay = Math.Exp(-6d * dt);
            double cosine = Math.Cos(2d * dt);
            double sine = Math.Sin(2d * dt);
            float initialPosition = position;
            float initialVelocity = velocity;
            position = (float)(decay * (initialPosition * cosine + (initialVelocity + 6d * initialPosition) * sine / 2d));
            velocity = (float)(decay * (initialVelocity * cosine - (6d * initialVelocity + 40d * initialPosition) * sine / 2d));
        }

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
