using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Presentation.View;
using Shooter.Simulation.Combat;
using Xunit;

namespace Shooter.Simulation.Tests.View
{
    public sealed class WeaponRecoilPresentationTests
    {
        private const float Deg = (float)Math.PI / 180f;
        private static readonly EntityId Local = new EntityId(1);
        private static readonly EntityId Remote = new EntityId(2);

        private static CombatEvent Shot(EntityId who, uint tick, uint index, string weapon = "weapons/rifle") =>
            new CombatEvent(new EventId(9, tick, who, 0), CombatEventKind.ShotAccepted, who, default,
                new ShotPayload(new DefinitionId(weapon), index, default, new Float3(0, 0, 1), 1, 1));

        [Fact]
        public void EveryProfileCarriesItsWeaponDefinitionRecoil()
        {
            foreach (var entry in WeaponCatalog.All)
            {
                var def = entry.Definition;
                Assert.True(WeaponVisualProfiles.TryGet(def.Id, out var profile), def.Id.ToString());
                Assert.Equal(def.RecoilPitchRadians, profile.RecoilPitchDegrees * Deg, 6);
                Assert.Equal(def.RecoilYawHalfRadians, profile.RecoilYawHalfDegrees * Deg, 6);
            }
        }

        [Theory]
        [InlineData("weapons/rifle")]
        [InlineData("weapons/rifle-carbine")]
        [InlineData("weapons/pistol")]
        public void CameraClimbMirrorsAuthoritativeRecoilTickForTick(string weapon)
        {
            var def = WeaponCatalog.RequireDefinition(new DefinitionId(weapon));
            var camera = new FirstPersonCameraRecoil();
            float yaw = 0f, pitch = 0f;
            uint index = 0;
            // A burst at the weapon's cadence, a pause, then single shots: covers the clamp and the decay.
            var shotTicks = new HashSet<uint>();
            for (uint t = 10; t < 10 + 40 * def.FireIntervalTicks; t += def.FireIntervalTicks) shotTicks.Add(t);
            shotTicks.Add(400); shotTicks.Add(430);
            for (uint tick = 1; tick <= 450; tick++)
            {
                AuthoritativeRecoil.Decay(ref yaw, ref pitch);
                bool shot = shotTicks.Contains(tick);
                if (shot) AuthoritativeRecoil.ApplyImpulse(def, index, 77, ref yaw, ref pitch);
                camera.Step(shot ? new[] { Shot(Local, tick, index++, weapon) } : null, Local, 1f / 60f, false);
                if (shot)
                {
                    // Presentation decayed one frame past the shot; undo that to compare at the shot tick.
                    float atShot = camera.ClimbRadians / (float)Math.Exp(-FirstPersonCameraRecoil.ClimbDecayPerSecond / 60f);
                    Assert.InRange(atShot - pitch, -2e-5f, 2e-5f);
                }
            }
            Assert.True(camera.ClimbRadians >= -FirstPersonCameraRecoil.MaxClimbRadians);
        }

        [Fact]
        public void EachShotPunchesAndSettlesBeforeTheNextAutomaticRound()
        {
            var camera = new FirstPersonCameraRecoil();
            camera.Step(new[] { Shot(Local, 10, 0) }, Local, 0f, false);
            float peakT = DampedSpring.FirstPeakSeconds(FirstPersonCameraRecoil.PunchDamping, FirstPersonCameraRecoil.PunchFrequency);
            Assert.InRange(peakT, 0.025f, 0.06f);
            camera.Step(null, Local, peakT, false);
            float peak = camera.PunchPitchRadians;
            Assert.Equal(0.35f * Deg * FirstPersonCameraRecoil.PunchPeakPerShotClimb, peak, 4);
            Assert.True(camera.PitchRadians < -0.35f * Deg, "camera looks up past one shot of climb at the punch peak");
            camera.Step(null, Local, 0.1f - peakT, false);
            Assert.True(Math.Abs(camera.PunchPitchRadians) < 0.25f * peak, "punch mostly settles within 100 ms");
        }

        [Fact]
        public void ReducedMotionKeepsTheAuthoritativeClimbButDropsThePunch()
        {
            var camera = new FirstPersonCameraRecoil();
            camera.Step(new[] { Shot(Local, 10, 0) }, Local, 0.04f, true);
            Assert.Equal(0f, camera.PunchPitchRadians);
            Assert.Equal(0f, camera.YawRadians);
            Assert.True(camera.ClimbRadians < 0f);
        }

        [Fact]
        public void RemoteDuplicateAndRecoilLessShotsLeaveTheCameraAlone()
        {
            var camera = new FirstPersonCameraRecoil();
            camera.Step(new[] { Shot(Remote, 10, 0), Shot(Local, 11, 0, "weapons/knife") }, Local, 0.03f, false);
            Assert.Equal(0f, camera.PitchRadians);
            var shot = Shot(Local, 12, 0);
            camera.Step(new[] { shot }, Local, 0f, false);
            float once = camera.ClimbRadians;
            camera.Step(new[] { shot }, Local, 0f, false);
            Assert.Equal(once, camera.ClimbRadians);
            camera.Reset();
            Assert.Equal(0f, camera.PitchRadians);
        }

        [Fact]
        public void ViewModelKickScalesWithEachWeaponsRecoil()
        {
            var rifle = ProceduralViewModel.ShotKickImpulse(WeaponVisualProfiles.Require(new DefinitionId("weapons/rifle")));
            var carbine = ProceduralViewModel.ShotKickImpulse(WeaponVisualProfiles.Require(new DefinitionId("weapons/rifle-carbine")));
            var pistol = ProceduralViewModel.ShotKickImpulse(WeaponVisualProfiles.Require(new DefinitionId("weapons/pistol")));
            var knife = ProceduralViewModel.ShotKickImpulse(WeaponVisualProfiles.Require(new DefinitionId("weapons/knife")));
            Assert.True(carbine.pitch > rifle.pitch && pistol.pitch > carbine.pitch);
            Assert.Equal(0f, knife.pitch);
            Assert.Equal(0f, knife.yaw);
        }

        [Fact]
        public void ViewModelKickPulsesPerShotInsteadOfDrifting()
        {
            var view = new ProceduralViewModel();
            view.ConsumeEvents(new[] { Shot(Local, 10, 0) }, Local, false, 0f);
            float peakT = DampedSpring.FirstPeakSeconds(ProceduralViewModel.KickDamping, ProceduralViewModel.KickFrequency);
            view.TickAim(false, peakT);
            float peak = view.Pose.RecoilPitch;
            Assert.Equal(0.35f * ProceduralViewModel.KickPeakRadiansPerRecoilDegree, peak, 4);
            Assert.True(view.EquipmentKickMeters(false).Z < -0.01f, "rifle punches back over a centimetre");
            Assert.True(view.EquipmentKickMeters(false).Z < 0f && FirstPersonHandling.EquipmentRecoilKick(peak, 0f, 1f, false).Z < -0.01f,
                "the rearward punch survives full ADS");
            view.TickAim(false, 0.1f - peakT);
            Assert.True(Math.Abs(view.Pose.RecoilPitch) < 0.3f * peak, "kick returns before the next 600 RPM round");

            // Sustained automatic fire keeps pulsing: each shot's peak clearly exceeds the trough before it.
            var auto = new ProceduralViewModel();
            float trough = 0f, lastPeak = 0f;
            for (uint i = 0; i < 20; i++)
            {
                trough = auto.Pose.RecoilPitch;
                auto.ConsumeEvents(new[] { Shot(Local, 10 + i * 6, i) }, Local, false, 0f);
                auto.TickAim(false, peakT);
                lastPeak = auto.Pose.RecoilPitch;
                auto.TickAim(false, 0.1f - peakT);
            }
            Assert.True(lastPeak - trough > 0.6f * peak);
        }
    }
}
