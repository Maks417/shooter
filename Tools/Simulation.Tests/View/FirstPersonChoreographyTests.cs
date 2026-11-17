#nullable enable
using System;
using Xunit;
using Shooter.Contracts;
using Shooter.Presentation.View;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Simulation.Tests.View
{
    /// <summary>ADR-099 first-person choreography: reload, draw, knife, grenade and movement layers.</summary>
    public sealed class FirstPersonChoreographyTests
    {
        private static float Length(Float3 v) => (float)Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        private static float Distance(Float3 a, Float3 b) => Length(MotionCurve.Sub(a, b));

        [Fact]
        public void MotionCurveHitsKeysAndClampsOutsideTheRange()
        {
            var curve = new MotionCurve(new MotionKey(0f, 0, 0, 0), new MotionKey(.5f, 1, 2, 3), new MotionKey(1f, 0, 0, 0));
            Assert.Equal(0f, curve.Sample(-1f).X);
            Assert.Equal(2f, curve.Sample(.5f).Y);
            Assert.Equal(0f, curve.Sample(2f).Z);
            // An extreme key holds still: nothing overshoots past it.
            for (int i = 0; i <= 100; i++) Assert.InRange(curve.Sample(i / 100f).Y, 0f, 2f + 1e-5f);
            Assert.Throws<ArgumentException>(() => new MotionCurve(new MotionKey(.5f, 0, 0, 0), new MotionKey(.5f, 1, 1, 1)));
        }

        [Theory]
        [InlineData(WeaponVisualFamily.PrimaryGun, false)]
        [InlineData(WeaponVisualFamily.PrimaryGun, true)]
        [InlineData(WeaponVisualFamily.Sidearm, false)]
        [InlineData(WeaponVisualFamily.Sidearm, true)]
        public void ReloadIsContinuousAndReturnsToRest(WeaponVisualFamily family, bool empty)
        {
            float seconds = family == WeaponVisualFamily.Sidearm ? 1.8f : 2.4f;
            var previous = FirstPersonReloadChoreography.Evaluate(family, 0.0005f, empty, seconds, false);
            const int Steps = 2000;
            for (int i = 2; i < Steps; i++)
            {
                float t = i / (float)Steps;
                var pose = FirstPersonReloadChoreography.Evaluate(family, t, empty, seconds, false);
                Assert.InRange(Distance(pose.WeaponPosition, previous.WeaponPosition), 0f, 0.004f);
                Assert.InRange(Distance(pose.WeaponRotationDegrees, previous.WeaponRotationDegrees), 0f, 1.5f);
                Assert.InRange(Distance(pose.SupportWristOffset, previous.SupportWristOffset), 0f, 0.006f);
                // Fresh magazine stays in the hand; the seated magazine only appears where the fresh one seats.
                if (pose.FreshMagazineVisible && previous.FreshMagazineVisible)
                    Assert.InRange(Distance(pose.FreshMagazineOffset, previous.FreshMagazineOffset), 0f, 0.006f);
                if (pose.MagazineVisible && !previous.MagazineVisible)
                    Assert.InRange(Length(pose.MagazineOffset), 0f, 0.002f);
                Assert.InRange(pose.SupportOpen01, 0f, 1f);
                previous = pose;
            }
            var rest = FirstPersonReloadChoreography.Evaluate(family, 1f, empty, seconds, false);
            Assert.Equal(default, rest.WeaponPosition);
            Assert.True(rest.MagazineVisible);
            Assert.False(rest.FreshMagazineVisible);
            Assert.True(rest.CycleOverride01 < 0f);
            var near = FirstPersonReloadChoreography.Evaluate(family, 0.9995f, empty, seconds, false);
            Assert.InRange(Length(near.WeaponPosition), 0f, 0.001f);
            Assert.InRange(Length(near.SupportWristOffset), 0f, 0.001f);
        }

        [Theory]
        [InlineData(WeaponVisualFamily.PrimaryGun)]
        [InlineData(WeaponVisualFamily.Sidearm)]
        public void OldMagazineDropsOutOfViewBeforeTheFreshOneSeats(WeaponVisualFamily family)
        {
            float release = FirstPersonReloadChoreography.ReleasePhase(family, false);
            float seat = FirstPersonReloadChoreography.SeatPhase(family);
            Assert.True(release < seat);
            bool sawHidden = false, sawFresh = false;
            for (int i = 1; i < 1000; i++)
            {
                float t = i / 1000f;
                var pose = FirstPersonReloadChoreography.Evaluate(family, t, false, 2f, false);
                Assert.False(pose.MagazineVisible && pose.FreshMagazineVisible, "never two magazines in the well at once");
                if (t > release && t < seat && !pose.MagazineVisible) sawHidden = true;
                if (pose.FreshMagazineVisible) sawFresh = true;
                if (t > release && pose.MagazineVisible && t < seat) Assert.True(pose.MagazineOffset.Y < 0f, "dropping");
                if (t >= seat) Assert.True(pose.MagazineVisible);
            }
            Assert.True(sawHidden);
            Assert.True(sawFresh);
        }

        [Theory]
        [InlineData(WeaponVisualFamily.PrimaryGun)]
        [InlineData(WeaponVisualFamily.Sidearm)]
        public void EmptyReloadLocksTheCycleUntilTheBoltOrSlideRelease(WeaponVisualFamily family)
        {
            float release = FirstPersonReloadChoreography.CycleReleasePhase(family, true);
            Assert.True(release > FirstPersonReloadChoreography.SeatPhase(family));
            Assert.True(FirstPersonReloadChoreography.CycleReleasePhase(family, false) < 0f);
            Assert.Equal(1f, FirstPersonReloadChoreography.Evaluate(family, release - 0.05f, true, 2f, false).CycleOverride01);
            Assert.Equal(0f, FirstPersonReloadChoreography.Evaluate(family, release + 0.05f, true, 2f, false).CycleOverride01);
            Assert.True(FirstPersonReloadChoreography.Evaluate(family, 0.5f, false, 2f, false).CycleOverride01 < 0f);
            // The empty reload's extra beat moves the support hand somewhere the tactical one never goes.
            var emptyHand = FirstPersonReloadChoreography.Evaluate(family, release, true, 2f, false).SupportWristOffset;
            var tacticalHand = FirstPersonReloadChoreography.Evaluate(family, release, false, 2f, false).SupportWristOffset;
            Assert.True(Distance(emptyHand, tacticalHand) > 0.03f);
        }

        [Fact]
        public void ReducedMotionKeepsHandAndMagazinesButHalvesTheTilt()
        {
            var full = FirstPersonReloadChoreography.Evaluate(WeaponVisualFamily.PrimaryGun, .5f, false, 2.4f, false);
            var reduced = FirstPersonReloadChoreography.Evaluate(WeaponVisualFamily.PrimaryGun, .5f, false, 2.4f, true);
            Assert.Equal(full.SupportWristOffset, reduced.SupportWristOffset);
            Assert.Equal(full.WeaponPosition, reduced.WeaponPosition);
            Assert.Equal(full.WeaponRotationDegrees.Z * .5f, reduced.WeaponRotationDegrees.Z, 4);
            Assert.Equal(ReloadPose.Rest.WeaponPosition,
                FirstPersonReloadChoreography.Evaluate(WeaponVisualFamily.Melee, .5f, false, 1f, false).WeaponPosition);
        }

        [Theory]
        [InlineData("weapons/rifle", (byte)1, (byte)0)]
        [InlineData("weapons/pistol", (byte)0, (byte)1)]
        [InlineData("weapons/knife", (byte)0, (byte)2)]
        [InlineData("weapons/grenade", (byte)0, (byte)3)]
        public void SwitchFinishesExactlyWithTheAuthoritativeEquipWindow(string incoming, byte from, byte to)
        {
            var profile = WeaponVisualProfiles.Require(new DefinitionId(incoming));
            float equip = profile.EquipSeconds;
            Assert.True(equip > 0f);
            var handling = new FirstPersonHandling();
            handling.Reset(from);
            const float Dt = 1f / 240f;
            int frames = 0;
            do
            {
                handling.Step(default, to, Dt, false, false, equip);
                frames++;
                Assert.True(frames < 2000);
            } while (handling.EquipBlend < 1f || handling.IsSwitching);
            Assert.Equal(to, handling.VisibleWeaponSlot);
            Assert.InRange(frames * Dt, equip - 2 * Dt, equip + 2 * Dt);
            Assert.True(handling.CurrentHolsterSeconds <= FirstPersonEquipChoreography.MaxHolsterSeconds);
            Assert.Equal(0f, handling.PositionOffset(0f, -1f, true).Y);
        }

        [Theory]
        [InlineData((byte)0)]
        [InlineData((byte)1)]
        [InlineData((byte)2)]
        [InlineData((byte)3)]
        public void DrawStylesStartLowAndSettleWithoutPopping(byte slot)
        {
            var start = FirstPersonEquipChoreography.DrawPosition(slot, 0f);
            Assert.True(start.Y < -0.15f, "incoming weapon starts below the view");
            Assert.Equal(default, FirstPersonEquipChoreography.DrawPosition(slot, 1f));
            Assert.Equal(default, FirstPersonEquipChoreography.DrawRotationDegrees(slot, 1f));
            var previous = FirstPersonEquipChoreography.DrawRotationDegrees(slot, 0f);
            for (int i = 1; i <= 200; i++)
            {
                var next = FirstPersonEquipChoreography.DrawRotationDegrees(slot, i / 200f);
                Assert.InRange(Distance(next, previous), 0f, 3.5f);
                previous = next;
            }
        }

        [Fact]
        public void SwitchingAgainMidDrawOrBackMidTuckNeverJumps()
        {
            var handling = new FirstPersonHandling();
            handling.Reset(0);
            float pistolEquip = WeaponVisualProfiles.Require(new DefinitionId("weapons/pistol")).EquipSeconds;
            // Rifle -> pistol, interrupted half-way up the pistol draw by a switch to the knife.
            for (int i = 0; i < 12; i++) handling.Step(default, 1, 1f / 60f, false, false, pistolEquip);
            Assert.Equal(1, handling.VisibleWeaponSlot);
            Assert.True(handling.Drawing && handling.EquipBlend > 0.2f && handling.EquipBlend < 0.9f);
            Float3 before = handling.PositionOffset(0f, -1f, false), beforeRot = handling.RotationDegrees(0f, -1f, false);
            handling.Step(default, 2, 0f, false, false, 0.2f);
            Assert.InRange(Distance(handling.PositionOffset(0f, -1f, false), before), 0f, 1e-5f);
            Assert.InRange(Distance(handling.RotationDegrees(0f, -1f, false), beforeRot), 0f, 1e-3f);
            // Back to the pistol before the knife swap: the pistol rises again from where it is.
            handling.Step(default, 2, 1f / 120f, false, false, 0.2f);
            before = handling.PositionOffset(0f, -1f, false);
            handling.Step(default, 1, 0f, false, false, pistolEquip);
            handling.Step(default, 1, 1e-4f, false, false, pistolEquip);
            Assert.InRange(Distance(handling.PositionOffset(0f, -1f, false), before), 0f, 0.002f);
            for (int i = 0; i < 60; i++) handling.Step(default, 1, 1f / 60f, false, false, pistolEquip);
            Assert.False(handling.IsSwitching);
            Assert.InRange(Length(handling.PositionOffset(0f, -1f, true)), 0f, 1e-6f);
        }

        [Fact]
        public void HeldThrowDelaysTheSwapThenDrawsInTheRestOfTheWindow()
        {
            var handling = new FirstPersonHandling();
            handling.Reset(3);
            float rifleEquip = WeaponVisualProfiles.Require(new DefinitionId("weapons/rifle")).EquipSeconds;
            for (int i = 0; i < 18; i++)
                handling.Step(default, 0, 1f / 60f, false, false, rifleEquip, holdOutgoing: true);
            Assert.Equal(3, handling.VisibleWeaponSlot);
            Assert.Equal(1f, handling.EquipBlend);
            handling.Step(default, 0, 1f / 60f, false, false, rifleEquip, holdOutgoing: false);
            Assert.Equal(0, handling.VisibleWeaponSlot);
            Assert.True(handling.Drawing);
            Assert.Equal(FirstPersonHandling.MinDrawAfterHoldSeconds, handling.CurrentDrawSeconds, 3);
            for (int i = 0; i < 30; i++)
                handling.Step(default, 0, 1f / 60f, false, false, rifleEquip, holdOutgoing: false);
            Assert.False(handling.IsSwitching);
        }

        private static CombatEvent KnifeSwing(EntityId id, uint tick) => new CombatEvent(
            new EventId(1, tick, id, 1), CombatEventKind.ShotAccepted, id, default,
            new ShotPayload(new DefinitionId("weapons/knife"), tick, default, new Float3(0, 0, 1), 1, 1));

        [Fact]
        public void KnifeSwingsLastTheFireIntervalAndAlternateDirection()
        {
            var id = new EntityId(1);
            var view = new ProceduralViewModel();
            view.ResetForLife(ProductAuthorityActionView.KnifeSlot);
            float interval = WeaponVisualProfiles.Require(new DefinitionId("weapons/knife")).FireIntervalSeconds;
            view.ConsumeEvents(new[] { KnifeSwing(id, 1) }, id, false, 0f);
            Assert.Equal(interval, view.MeleeDurationSeconds, 4);
            int first = view.MeleeDirection;
            view.TickAim(false, interval * 0.3f);
            float firstYaw = view.SpecialActionRotation(false).Y;
            view.TickAim(false, interval * 0.6f);
            Assert.True(view.MeleePhase01 > 0f, "still recovering just before the next swing is allowed");
            view.TickAim(false, interval * 0.1f);
            Assert.Equal(0f, view.MeleePhase01);
            view.ConsumeEvents(new[] { KnifeSwing(id, 30) }, id, false, 0f);
            Assert.Equal(-first, view.MeleeDirection);
            view.TickAim(false, interval * 0.3f);
            Assert.True(Math.Sign(view.SpecialActionRotation(false).Y) == -Math.Sign(firstYaw));
        }

        [Fact]
        public void GrenadePinPullMovesTheSupportHandAndTrembleGrowsWithTheFuse()
        {
            var view = new ProceduralViewModel();
            view.ResetForLife(ProductAuthorityActionView.GrenadeSlot);
            view.SetCooking(true);
            Assert.Equal(default, view.SpecialSupportWrist);
            view.TickAim(false, 0.12f);
            Assert.True(view.SpecialSupportWrist.Y > 0.03f, "hand reaches up to the ring");
            Assert.True(view.SpecialSupportOpen < 0.2f, "fingers closed on the ring");
            view.TickAim(false, 0.4f);
            Assert.True(view.SpecialSupportWrist.Y < -0.05f, "hand lowered away after the pull");

            float fuse = WeaponVisualProfiles.Require(new DefinitionId("weapons/grenade")).FuseSeconds;
            float early = 0f, late = 0f;
            for (int i = 0; i < 60; i++)
            {
                early = Math.Max(early, Math.Abs(FirstPersonSpecialActions.GrenadeCookRotation(0.6f + i * 0.01f, fuse, false).X
                    - FirstPersonSpecialActions.GrenadeCookRotation(1f, 0f, false).X));
                late = Math.Max(late, Math.Abs(FirstPersonSpecialActions.GrenadeCookRotation(fuse - 0.7f + i * 0.01f, fuse, false).X
                    - FirstPersonSpecialActions.GrenadeCookRotation(1f, 0f, false).X));
            }
            Assert.True(late > early * 2f);
            Assert.Equal(FirstPersonSpecialActions.GrenadeCookRotation(fuse - 0.2f, 0f, true).X,
                FirstPersonSpecialActions.GrenadeCookRotation(fuse - 0.2f, fuse, true).X);
        }

        [Fact]
        public void ThrowSurvivesTheAuthoritySwitchBackAndStartsFromTheCookPose()
        {
            var id = new EntityId(1);
            var view = new ProceduralViewModel();
            view.ResetForLife(ProductAuthorityActionView.GrenadeSlot);
            view.SetCooking(true);
            view.TickAim(false, 0.5f);
            // Release tick: authority already reports the rifle slot before the event is consumed.
            view.SetActiveWeaponSlot(ProductAuthorityActionView.RifleSlot);
            var spawn = new CombatEvent(
                new EventId(1, 5, id, 1), CombatEventKind.ProjectileSpawned, id, default,
                new ProjectilePayload(new EntityId(40), new Float3(0, 1, 0), new Float3(0, 2, 10), new DefinitionId("weapons/grenade")));
            view.ConsumeEvents(new[] { spawn }, id, false, 0f);
            Assert.True(view.Throwing);
            var start = view.SpecialActionPosition(false);
            Assert.InRange(Distance(start, FirstPersonSpecialActions.GrenadeCookPosition(true, false)), 0f, 0.02f);
            Assert.False(view.HideHeldGrenade(30));
            view.TickAim(false, FirstPersonSpecialActions.ThrowDurationSeconds * 0.2f);
            Assert.True(view.HideHeldGrenade(30), "grenade left the hand; rifle magazine count is irrelevant");
            view.TickAim(false, FirstPersonSpecialActions.ThrowDurationSeconds);
            Assert.False(view.Throwing);
            Assert.Equal(default, view.SpecialActionPosition(false));
        }

        [Fact]
        public void CycleTimeFollowsTheFireIntervalAndEasesHome()
        {
            var id = new EntityId(1);
            var view = new ProceduralViewModel();
            view.ResetForLife(ProductAuthorityActionView.RifleSlot);
            var shot = new CombatEvent(new EventId(1, 1, id, 1), CombatEventKind.ShotAccepted, id, default,
                new ShotPayload(new DefinitionId("weapons/rifle-carbine"), 1, default, new Float3(0, 0, 1), 1, 1));
            view.ConsumeEvents(new[] { shot }, id, false, 0f);
            float interval = WeaponVisualProfiles.Require(new DefinitionId("weapons/rifle-carbine")).FireIntervalSeconds;
            Assert.True(view.CycleSeconds < interval, "the bolt is home before the next round");
            view.TickAim(false, view.CycleSeconds * 0.5f);
            Assert.True(view.CycleOpen01 > view.CycleLinear01, "lingers at the rear, then slams home");
            view.TickAim(false, view.CycleSeconds);
            Assert.Equal(0f, view.CycleOpen01);
        }

        [Fact]
        public void AdsRaiseUsesTheProfileTime()
        {
            var view = new ProceduralViewModel();
            view.ResetForLife(ProductAuthorityActionView.PistolSlot);
            Assert.Equal(WeaponVisualProfiles.Require(new DefinitionId("weapons/pistol")).AdsSeconds, view.AdsSeconds);
            view.TickAim(true, view.AdsSeconds * 0.5f);
            Assert.Equal(0.5f, view.Pose.AdsBlend, 3);
        }

        private static FirstPersonLocomotionInput Move(bool sprint, bool grounded = true, float vy = 0f, float distance = 0f,
            bool crouch = false) => new FirstPersonLocomotionInput(sprint, grounded, crouch, false, vy, distance, sprint ? 6f : 0f);

        [Fact]
        public void SprintCarryBlendsInAndDropsOnShotOrAds()
        {
            var pose = new FirstPersonLocomotionPose();
            for (int i = 0; i < 30; i++) pose.Step(Move(true, distance: i * 0.1f), 1f / 60f, 0f, false, 0f, 0f);
            Assert.True(pose.SprintBlend > 0.95f);
            Assert.True(pose.PositionOffset(0, 0f, false).Y < -0.03f);
            Assert.True(pose.RotationDegrees(0, 0f, false).Y > 15f);
            for (int i = 0; i < 9; i++) pose.Step(Move(true, distance: 3f), 1f / 60f, 0f, true, 0f, 0f);
            Assert.True(pose.SprintBlend < 0.1f, "a shot drops the carry within ~150 ms");
            Assert.Equal(0f, pose.RotationDegrees(0, 1f, false).Y);
        }

        [Fact]
        public void LandingDipScalesWithFallSpeedAndSettles()
        {
            float Dip(float fall)
            {
                var pose = new FirstPersonLocomotionPose();
                pose.Step(Move(false), 1f / 60f, 0f, false, 0f, 0f);
                pose.Step(Move(false, grounded: false, vy: 4f), 1f / 60f, 0f, false, 0f, 0f);
                for (int i = 0; i < 40; i++) pose.Step(Move(false, grounded: false, vy: -fall), 1f / 60f, 0f, false, 0f, 0f);
                float lowest = 0f;
                for (int i = 0; i < 20; i++)
                {
                    pose.Step(Move(false), 1f / 60f, 0f, false, 0f, 0f);
                    lowest = Math.Min(lowest, pose.VerticalOffset);
                }
                for (int i = 0; i < 120; i++) pose.Step(Move(false), 1f / 60f, 0f, false, 0f, 0f);
                Assert.InRange(Math.Abs(pose.VerticalOffset), 0f, 1e-3f);
                return lowest;
            }
            float soft = Dip(3f), hard = Dip(9f);
            Assert.True(soft < 0f);
            Assert.True(hard < soft * 1.5f, "a harder landing dips further");
        }

        [Fact]
        public void LocomotionLayersAreFrameRateIndependent()
        {
            Float3 Run(int hz)
            {
                var pose = new FirstPersonLocomotionPose();
                float dt = 1f / hz;
                for (int i = 0; i < hz / 2; i++)
                    pose.Step(Move(true, distance: i * dt * 6f), dt, 0f, false, 0.02f * 60f * dt, 0f);
                return pose.PositionOffset(0, 0f, false);
            }
            var a = Run(30); var b = Run(60); var c = Run(144);
            Assert.InRange(Distance(a, b), 0f, 0.004f);
            Assert.InRange(Distance(b, c), 0f, 0.004f);
        }

        [Fact]
        public void LookInertiaTrailsATurnAndIsBoundedAndAdsSafe()
        {
            var pose = new FirstPersonLocomotionPose();
            pose.Step(Move(false), 1f / 60f, 0f, false, 0.5f, 0f);
            for (int i = 0; i < 3; i++) pose.Step(Move(false), 1f / 60f, 0f, false, 0f, 0f);
            Assert.True(pose.LookYawLag < 0f, "turning right leaves the gun trailing left");
            Assert.InRange(Math.Abs(pose.LookYawLag), 0f, FirstPersonLocomotionPose.MaxLookLagRadians);
            Assert.Equal(0f, pose.RotationDegrees(0, 1f, false).Y);
            Assert.Equal(default, pose.RotationDegrees(0, 0f, true));
            for (int i = 0; i < 120; i++) pose.Step(Move(false), 1f / 60f, 0f, false, 0f, 0f);
            Assert.InRange(Math.Abs(pose.LookYawLag), 0f, 1e-4f);
        }

        [Fact]
        public void CrouchTucksAndResetClearsEveryLayer()
        {
            var pose = new FirstPersonLocomotionPose();
            for (int i = 0; i < 40; i++) pose.Step(Move(false, crouch: true), 1f / 60f, 0f, false, 0f, 0f);
            Assert.True(pose.CrouchBlend > 0.95f);
            Assert.True(pose.PositionOffset(0, 0f, false).Y < -0.01f);
            pose.Reset();
            Assert.Equal(default, pose.PositionOffset(0, 0f, false));
            Assert.Equal(default, pose.RotationDegrees(0, 0f, false));
        }
    }
}
