#nullable enable
using System;
using Xunit;
using Shooter.Contracts;
using Shooter.Presentation.View;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Simulation.Tests.View
{
    public sealed class FirstPersonHandlingTests
    {
        [Fact]
        public void HipCompositionFadesToIdentityWithoutChangingAdsOrWeaponScale()
        {
            for (byte slot = 0; slot < 2; slot++)
            {
                var hip = FirstPersonHandling.HipRotationDegrees(slot, 0);
                var halfway = FirstPersonHandling.HipRotationDegrees(slot, .5f);
                var ads = FirstPersonHandling.HipRotationDegrees(slot, 1);
                Assert.Equal(hip.Y * .5f, halfway.Y);
                Assert.Equal(0f, ads.X);
                Assert.Equal(0f, ads.Y);
                Assert.Equal(0f, ads.Z);
                Assert.True(hip.Y < 0 && hip.Z < 0);
            }
        }

        [Fact]
        public void ReloadTravelAndSupportHandStayContinuousAndReturnToGrip()
        {
            foreach (bool reduced in new[] { false, true })
            {
                var before = FirstPersonMagazineTravel.Offset(FirstPersonMagazineTravel.RemoveEnd - .00001f, false, reduced);
                var after = FirstPersonMagazineTravel.Offset(FirstPersonMagazineTravel.RemoveEnd + .00001f, false, reduced);
                Assert.InRange(Math.Abs(before.X - after.X) + Math.Abs(before.Y - after.Y) + Math.Abs(before.Z - after.Z), 0, .0001f);
                for (byte slot = 0; slot < 2; slot++)
                {
                    Assert.Equal(0f, FirstPersonHandling.SupportHandOffset(slot, 0, reduced).Y);
                    Assert.InRange(Math.Abs(FirstPersonHandling.SupportHandOffset(slot, 1, reduced).Y), 0, .00001f);
                    var middle = FirstPersonHandling.SupportHandOffset(slot, .5f, reduced);
                    Assert.True(middle.Y < 0);
                    Assert.True(middle.Z < 0);
                    if (reduced) Assert.True(Math.Abs(middle.Z) < Math.Abs(FirstPersonHandling.SupportHandOffset(slot, .5f, false).Z));
                }
            }
        }
        private static CombatEvent Shot(EntityId entity) => new CombatEvent(
            new EventId(1, 1, entity, 1), CombatEventKind.ShotAccepted, entity, default,
            new ShotPayload(new DefinitionId("weapons/rifle"), 1, default, new Float3(0, 0, 1), 1, 1));

        [Fact]
        public void RecoilDecayIsFrameRateIndependentAndPauseDoesNotChangeVelocity()
        {
            var fine = new ProceduralViewModel();
            var coarse = new ProceduralViewModel();
            var id = new EntityId(1);
            fine.ConsumeEvents(new[] { Shot(id) }, id, false, 0f);
            coarse.ConsumeEvents(new[] { Shot(id) }, id, false, 0f);
            for (int i = 0; i < 40; i++) fine.TickAim(false, 0f);
            for (int i = 0; i < 60; i++) fine.TickAim(false, 1f / 60f);
            coarse.TickAim(false, 1f);
            Assert.InRange(Math.Abs(fine.Pose.RecoilPitch - coarse.Pose.RecoilPitch), 0f, 1e-6f);
            coarse.TickAim(false, 30f);
            Assert.True(Math.Abs(coarse.Pose.RecoilPitch) < 1e-5f);
        }

        [Fact]
        public void ReloadAndEquipReturnToRestAndReducedMotionSuppressesGait()
        {
            var handling = new FirstPersonHandling();
            handling.Reset(0);
            handling.Step(default, 0, 1f / 60f, false);
            for (int i = 1; i < 10; i++) handling.Step(new Float3(i * .05f, 0, 0), 0, 1f / 60f, false);
            Assert.NotEqual(0f, handling.PositionOffset(0, -1, false).X);
            Assert.Equal(0f, handling.PositionOffset(0, -1, true).X);
            Assert.Equal(0f, handling.PositionOffset(1, -1, false).X);
            Assert.True(handling.PositionOffset(0, .5f, true).Y < -.05f);
            Assert.InRange(Math.Abs(handling.PositionOffset(0, 1, true).Y), 0f, 1e-7f);
            // Slot change starts a holster of the visible rifle — mesh must not swap yet.
            handling.Step(default, 1, 0f, false);
            Assert.Equal(0, handling.VisibleWeaponSlot);
            Assert.Equal(1, handling.DesiredWeaponSlot);
            Assert.True(handling.IsSwitching);
            handling.Step(default, 1, FirstPersonHandling.HolsterSeconds, false);
            Assert.Equal(1, handling.VisibleWeaponSlot);
            Assert.Equal(0f, handling.EquipBlend);
            Assert.True(handling.PositionOffset(0, -1, false).Y < -.1f);
            handling.Step(default, 1, FirstPersonHandling.DrawSeconds + 0.02f, false);
            Assert.Equal(1f, handling.EquipBlend);
            Assert.Equal(0f, handling.PositionOffset(0, -1, true).Y);
            Assert.False(handling.IsSwitching);
        }

        [Fact]
        public void WeaponSwitchHolstersThenDrawsAndGatesAdsBlend()
        {
            var handling = new FirstPersonHandling();
            handling.Reset(0);
            Assert.Equal(1f, FirstPersonHandling.AdsPresentationBlend(1f, 1f));
            Assert.Equal(0f, FirstPersonHandling.AdsPresentationBlend(1f, 0f));
            handling.Step(default, 1, FirstPersonHandling.HolsterSeconds * 0.5f, false);
            Assert.Equal(0, handling.VisibleWeaponSlot);
            Assert.True(handling.EquipBlend > 0f && handling.EquipBlend < 1f);
            Assert.True(handling.RotationDegrees(0, -1, false).X > 5f);
            handling.Step(default, 1, FirstPersonHandling.HolsterSeconds, false);
            Assert.Equal(1, handling.VisibleWeaponSlot);
            // Partial draw so the next switch must holster a raised pistol before swapping.
            handling.Step(default, 1, FirstPersonHandling.DrawSeconds * 0.55f, false);
            Assert.True(handling.EquipBlend > 0.4f);
            handling.Step(default, 2, FirstPersonHandling.HolsterSeconds * 0.4f, false);
            Assert.Equal(1, handling.VisibleWeaponSlot);
            Assert.Equal(2, handling.DesiredWeaponSlot);
            handling.Step(default, 2, FirstPersonHandling.HolsterSeconds + FirstPersonHandling.DrawSeconds, false);
            Assert.Equal(2, handling.VisibleWeaponSlot);
            Assert.Equal(1f, handling.EquipBlend);
        }

        [Fact]
        public void TeleportAndSlideAreNotFootstepsAndResetRemovesMotionHistory()
        {
            var handling = new FirstPersonHandling();
            handling.Reset(0);
            handling.Step(default, 0, .016f, false);
            handling.Step(new Float3(20, 0, 0), 0, .016f, false);
            // Teleport must not create gait; tiny idle sway may remain until Reset.
            Assert.InRange(Math.Abs(handling.PositionOffset(0, -1, false).X), 0f, 0.0025f);
            handling.Step(new Float3(20.1f, 0, 0), 0, .016f, true);
            Assert.InRange(Math.Abs(handling.PositionOffset(0, -1, false).X), 0f, 0.0025f);
            handling.Step(new Float3(20.2f, 0, 0), 0, .016f, false);
            handling.Reset(0);
            Assert.Equal(0f, handling.PositionOffset(0, -1, false).Y);
            Assert.Equal(0f, handling.SwayBlend);
        }

        [Fact]
        public void SwitchCancelsOldReloadAndResetRemovesShotResidue()
        {
            var view = new ProceduralViewModel();
            var id = new EntityId(1);
            view.ConsumeEvents(new[] { new CombatEvent(new EventId(1, 0, id, 0),
                CombatEventKind.ReloadStarted, id, default, new ActionPayload(0, 0, 144)) }, id, false, .5f);
            view.SetActiveWeaponSlot(1);
            Assert.Equal(0f, view.ReloadPhase);
            view.ConsumeEvents(new[] { Shot(id) }, id, false, .016f);
            view.ResetForLife(1);
            Assert.Equal(0f, view.Pose.RecoilPitch);
            Assert.Equal(0, view.ConsumedEventCount);
            Assert.Equal(0f, view.EquipmentKickMeters(false).Z);
        }

        [Fact]
        public void MagazineTravelRemoveIsDeeperWhenEmptyAndSettlesAtEnd()
        {
            Float3 nonempty = FirstPersonMagazineTravel.Offset(FirstPersonMagazineTravel.RemoveEnd, false, false);
            Float3 empty = FirstPersonMagazineTravel.Offset(FirstPersonMagazineTravel.RemoveEnd, true, false);
            Assert.True(empty.Y < nonempty.Y);
            Assert.True(nonempty.Y < -0.08f);
            Assert.InRange(Math.Abs(FirstPersonMagazineTravel.Offset(1f, true, false).Y), 0f, 1e-6f);
            Assert.True(Math.Abs(FirstPersonMagazineTravel.PeakRemoveDepth(true, true)) <
                        Math.Abs(FirstPersonMagazineTravel.PeakRemoveDepth(true, false)));
        }

        [Fact]
        public void EquipmentRecoilKickIsAdsSafeAndReducedMotionZerosKick()
        {
            Float3 hip = FirstPersonHandling.EquipmentRecoilKick(0.2f, 0.1f, 0f, false);
            Float3 ads = FirstPersonHandling.EquipmentRecoilKick(0.2f, 0.1f, 1f, false);
            Assert.True(hip.Z < 0f);
            Assert.NotEqual(0f, hip.X);
            Assert.Equal(0f, ads.X);
            Assert.True(ads.Z < 0f);
            Assert.Equal(0f, FirstPersonHandling.RecoilRotationScale(1f, false));
            Assert.Equal(1f, FirstPersonHandling.RecoilRotationScale(0f, false));
            Assert.Equal(0f, FirstPersonHandling.EquipmentRecoilKick(0.2f, 0.1f, 0f, true).Z);
        }

        [Fact]
        public void IdleSwayAppearsAtRestAndIsKilledByReducedMotionOrAdsOrLifeReset()
        {
            var handling = new FirstPersonHandling();
            handling.Reset(0);
            for (int i = 0; i < 90; i++)
                handling.Step(new Float3(0, 0, 0), 0, 1f / 60f, false);
            Assert.True(handling.SwayBlend > 0.5f);
            Float3 sway = handling.PositionOffset(0f, -1f, false);
            Assert.True(Math.Abs(sway.X) + Math.Abs(sway.Y) + Math.Abs(sway.Z) > 0f);
            Assert.Equal(0f, handling.PositionOffset(0f, -1f, true).X);
            Assert.Equal(0f, handling.PositionOffset(1f, -1f, false).X);
            handling.Reset(0);
            Assert.Equal(0f, handling.SwayBlend);
            Assert.Equal(0f, handling.PositionOffset(0f, -1f, false).X);
        }

        [Fact]
        public void NearFieldPresenceConstantsFavorReadableRifleAndPistolMass()
        {
            Assert.True(FirstPersonHandling.RifleNearFieldScale > 1f);
            Assert.True(FirstPersonHandling.PistolNearFieldScale > FirstPersonHandling.RifleNearFieldScale);
            Float3 rifle = FirstPersonHandling.NearFieldHipBias(0);
            Float3 pistol = FirstPersonHandling.NearFieldHipBias(1);
            Assert.True(rifle.Z > pistol.Z);
            Assert.True(pistol.Y < 0f && rifle.Y < 0f);
        }

        [Fact]
        public void CameraBobAppearsWhenMovingAndIsKilledByReducedMotionAdsDeathOrReset()
        {
            var bob = new FirstPersonCameraBob();
            bob.Step(default, 1f / 60f, false);
            for (int i = 1; i < 12; i++)
                bob.Step(new Float3(i * 0.05f, 0, 0), 1f / 60f, false);
            Assert.True(bob.MovementBlend > 0.2f);
            Float3 offset = bob.LocalOffsetMeters(0f, false, true);
            Assert.True(Math.Abs(offset.X) + Math.Abs(offset.Y) > 0f);
            Assert.True(Math.Abs(bob.RollDegrees(0f, false, true)) > 0f);
            Assert.Equal(0f, bob.LocalOffsetMeters(0f, true, true).X);
            Assert.Equal(0f, bob.LocalOffsetMeters(1f, false, true).Y);
            Assert.Equal(0f, bob.RollDegrees(0f, false, false));
            bob.Reset();
            Assert.Equal(0f, bob.MovementBlend);
            Assert.Equal(0f, bob.LocalOffsetMeters(0f, false, true).Y);
        }

        [Fact]
        public void CameraBobIgnoresTeleportAndSlide()
        {
            var bob = new FirstPersonCameraBob();
            bob.Step(default, 0.016f, false);
            bob.Step(new Float3(20, 0, 0), 0.016f, false);
            Assert.InRange(bob.MovementBlend, 0f, 0.05f);
            bob.Step(new Float3(20.1f, 0, 0), 0.016f, true);
            Assert.InRange(bob.MovementBlend, 0f, 0.05f);
        }

        [Fact]
        public void WeaponMechanicsDriveMagazineAndCycleOffsets()
        {
            Assert.Equal(0f, FirstPersonWeaponMechanics.MagazineOffset(0f, false, false).Y);
            Assert.True(FirstPersonWeaponMechanics.MagazineOffset(FirstPersonMagazineTravel.RemoveEnd, false, false).Y < -0.05f);
            Assert.True(FirstPersonWeaponMechanics.MagazineOffset(FirstPersonMagazineTravel.RemoveEnd, true, false).Y <
                        FirstPersonWeaponMechanics.MagazineOffset(FirstPersonMagazineTravel.RemoveEnd, false, false).Y);
            Assert.Equal(0f, FirstPersonWeaponMechanics.CycleOffset(ProductAuthorityActionView.RifleSlot, 0f, false).Z);
            Assert.True(FirstPersonWeaponMechanics.CycleOffset(ProductAuthorityActionView.RifleSlot, 1f, false).Z < -0.04f);
            Assert.True(FirstPersonWeaponMechanics.CycleOffset(ProductAuthorityActionView.PistolSlot, 1f, false).Z < -0.02f);
            Assert.True(Math.Abs(FirstPersonWeaponMechanics.CycleOffset(ProductAuthorityActionView.RifleSlot, 1f, true).Z) <
                        Math.Abs(FirstPersonWeaponMechanics.CycleOffset(ProductAuthorityActionView.RifleSlot, 1f, false).Z));
            Assert.False(FirstPersonWeaponMechanics.HasCyclePart(ProductAuthorityActionView.KnifeSlot));
        }

        [Fact]
        public void HitFeedbackClassifiesAuthorityDamageAndFadesMarkers()
        {
            var local = new EntityId(7);
            var other = new EntityId(9);
            Assert.True(FirstPersonHitFeedback.IsDamageTaken(CombatEventKind.DamageApplied, local, local));
            Assert.False(FirstPersonHitFeedback.IsDamageTaken(CombatEventKind.DamageApplied, other, local));
            Assert.True(FirstPersonHitFeedback.IsDamageDealt(CombatEventKind.DamageApplied, local, local));
            Assert.False(FirstPersonHitFeedback.IsDamageDealt(CombatEventKind.Impact, local, local));
            Assert.Equal(1f, FirstPersonHitFeedback.HitMarkerAlpha(FirstPersonHitFeedback.HitMarkerSeconds));
            Assert.Equal(0.5f, FirstPersonHitFeedback.HitMarkerAlpha(FirstPersonHitFeedback.HitMarkerFadeSeconds * 0.5f));
            Assert.Equal(0f, FirstPersonHitFeedback.HitMarkerAlpha(0f));
            Assert.True(FirstPersonHitFeedback.DamageFlashStrength(FirstPersonHitFeedback.DamageFlashSeconds, false) >
                        FirstPersonHitFeedback.DamageFlashStrength(FirstPersonHitFeedback.DamageFlashSeconds, true));
            Assert.Equal(0f, FirstPersonHitFeedback.DamageShakeMeters(0.2f, reducedMotion: true, reducedFlash: false));
            Assert.True(FirstPersonHitFeedback.DamageShakeMeters(0.2f, false, false) > 0f);
        }

        [Fact]
        public void AdsOpticScopesRifleHidesHipReticleAndBlendsFov()
        {
            Assert.True(AdsOpticVisual.UsesScopedOptic(ProductAuthorityActionView.RifleSlot));
            Assert.True(AdsOpticVisual.UsesIronSightOptic(ProductAuthorityActionView.PistolSlot));
            Assert.False(AdsOpticVisual.UsesScopedOptic(ProductAuthorityActionView.PistolSlot));
            Assert.False(AdsOpticVisual.HideHipReticle(0f));
            Assert.True(AdsOpticVisual.HideHipReticle(1f));
            Assert.Equal(0f, AdsOpticVisual.ScopeMaskOpacity(0f));
            Assert.True(AdsOpticVisual.ScopeMaskOpacity(1f) > 0.95f);
            Assert.True(AdsOpticVisual.HideRifleViewModel(ProductAuthorityActionView.RifleSlot, 1f));
            Assert.False(AdsOpticVisual.HideRifleViewModel(ProductAuthorityActionView.PistolSlot, 1f));
            Assert.True(AdsOpticVisual.AdsFieldOfViewDegrees(80f, ProductAuthorityActionView.RifleSlot) <
                        AdsOpticVisual.AdsFieldOfViewDegrees(80f, ProductAuthorityActionView.PistolSlot));
            Assert.Equal(80f, AdsOpticVisual.BlendedFieldOfViewDegrees(80f, 42f, 0f));
            Assert.Equal(42f, AdsOpticVisual.BlendedFieldOfViewDegrees(80f, 42f, 1f));
            Assert.InRange(AdsOpticVisual.LensRadiusPixels(1280f, 720f), 200f, 400f);
            Assert.True(AdsOpticVisual.MilDotCount(300f) >= 2);
        }

        [Fact]
        public void KnifeShotStartsSlashAndUpgradesToStabOnHit()
        {
            var id = new EntityId(1);
            var slash = new ProceduralViewModel();
            slash.ResetForLife(ProductAuthorityActionView.KnifeSlot);
            var miss = new CombatEvent(
                new EventId(1, 1, id, 1), CombatEventKind.ShotAccepted, id, default,
                new ShotPayload(new DefinitionId("weapons/knife"), 1, default, new Float3(0, 0, 1), 1, 1));
            slash.ConsumeEvents(new[] { miss }, id, false, 0f);
            Assert.True(slash.MeleePhase01 > 0f);
            Assert.False(slash.MeleeIsStab);
            // Advance to mid-swing so envelopes are readable.
            slash.TickAim(false, FirstPersonSpecialActions.MeleeDurationSeconds * 0.5f);
            Assert.False(slash.MeleeIsStab);
            Assert.True(Math.Abs(slash.SpecialActionRotation(false).Z) > 10f);

            var stab = new ProceduralViewModel();
            stab.ResetForLife(ProductAuthorityActionView.KnifeSlot);
            var hit = new CombatEvent(
                new EventId(1, 2, id, 1), CombatEventKind.ShotAccepted, id, default,
                new ShotPayload(new DefinitionId("weapons/knife"), 2, default, new Float3(0, 0, 1), 1, 1));
            var dmg = new CombatEvent(
                new EventId(1, 2, id, 2), CombatEventKind.DamageApplied, id, new EntityId(9),
                new DamagePayload(55, 45, 2));
            stab.ConsumeEvents(new[] { hit, dmg }, id, false, 0f);
            Assert.True(stab.MeleeIsStab);
            stab.TickAim(false, FirstPersonSpecialActions.MeleeDurationSeconds * 0.5f);
            Assert.True(stab.SpecialActionPosition(false).Z > slash.SpecialActionPosition(false).Z);
        }

        [Fact]
        public void GrenadeCookAndThrowDriveDistinctPosesAndHideHeldProp()
        {
            var id = new EntityId(1);
            var view = new ProceduralViewModel();
            view.ResetForLife(ProductAuthorityActionView.GrenadeSlot);
            view.SetCooking(true);
            Assert.True(view.Cooking);
            var cookPos = view.SpecialActionPosition(false);
            Assert.True(cookPos.Y > 0f);

            var spawn = new CombatEvent(
                new EventId(1, 5, id, 1), CombatEventKind.ProjectileSpawned, id, default,
                new ProjectilePayload(new EntityId(40), new Float3(0, 1, 0), new Float3(0, 2, 10), new DefinitionId("weapons/grenade")));
            view.ConsumeEvents(new[] { spawn }, id, false, 0f);
            Assert.False(view.Cooking);
            Assert.True(view.ThrowPhase01 > 0f);
            Assert.True(view.HideHeldGrenade(0));
            // Advance past release threshold so hide stays true even if mag were non-zero mid-anim.
            for (int i = 0; i < 20; i++) view.TickAim(false, 0.02f);
            Assert.True(view.HideHeldGrenade(0));
        }
    }
}
