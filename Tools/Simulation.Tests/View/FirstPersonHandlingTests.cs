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
            handling.Step(default, 1, 0f, false);
            Assert.True(handling.PositionOffset(0, -1, true).Y < -.1f);
            handling.Step(default, 1, 0f, false);
            Assert.Equal(0f, handling.EquipBlend);
            handling.Step(default, 1, .22f, false);
            Assert.Equal(0f, handling.PositionOffset(0, -1, true).Y);
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
    }
}
