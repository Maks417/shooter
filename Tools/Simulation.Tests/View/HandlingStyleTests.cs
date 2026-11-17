#nullable enable
using Shooter.Contracts;
using Shooter.Presentation.View;
using Xunit;

namespace Shooter.Simulation.Tests.View
{
    /// <summary>ADR-102 first-person handling styles: AK rock-in and charging pull, bolt-action cycling.</summary>
    public sealed class HandlingStyleTests
    {
        [Theory]
        [InlineData(WeaponHandlingStyle.Rifle)]
        [InlineData(WeaponHandlingStyle.Ak)]
        [InlineData(WeaponHandlingStyle.Bolt)]
        [InlineData(WeaponHandlingStyle.Pistol)]
        public void Every_style_rests_at_both_ends_and_seats_the_magazine(WeaponHandlingStyle style)
        {
            foreach (bool empty in new[] { false, true })
            {
                var rest = FirstPersonReloadChoreography.Evaluate(style, 0f, empty, 2.4f, false);
                Assert.Equal(0f, rest.SupportWristOffset.Z);
                float seat = FirstPersonReloadChoreography.SeatPhase(style, empty);
                Assert.InRange(seat, 0.4f, 0.8f);
                var seated = FirstPersonReloadChoreography.Evaluate(style, seat + 0.01f, empty, 2.4f, false);
                Assert.Equal(default, seated.MagazineOffset);
                var late = FirstPersonReloadChoreography.Evaluate(style, 0.999f, empty, 2.4f, false);
                Assert.True(System.Math.Abs(late.SupportWristOffset.Z) < 0.01f);
            }
        }

        [Fact]
        public void Ak_empty_reload_keeps_the_bolt_closed_until_the_charging_pull()
        {
            float pull = FirstPersonReloadChoreography.CyclePullPhase(WeaponHandlingStyle.Ak, true);
            float release = FirstPersonReloadChoreography.CycleReleasePhase(WeaponHandlingStyle.Ak, true);
            Assert.True(pull > 0f && release > pull);
            Assert.Equal(0f, FirstPersonReloadChoreography.Evaluate(WeaponHandlingStyle.Ak, 0.5f, true, 2.5f, false).CycleOverride01);
            Assert.True(FirstPersonReloadChoreography.Evaluate(WeaponHandlingStyle.Ak, (pull + release) / 2 + 0.01f, true, 2.5f, false).CycleOverride01 > 0.9f);
            Assert.Equal(0f, FirstPersonReloadChoreography.Evaluate(WeaponHandlingStyle.Ak, release + 0.05f, true, 2.5f, false).CycleOverride01);
            // A tactical AK reload never touches the charging handle; the rifle's empty reload is locked back from the start.
            Assert.True(FirstPersonReloadChoreography.Evaluate(WeaponHandlingStyle.Ak, 0.8f, false, 2.5f, false).CycleOverride01 < 0f);
            Assert.True(FirstPersonReloadChoreography.Evaluate(WeaponHandlingStyle.Rifle, 0.5f, true, 2.4f, false).CycleOverride01 > 0.9f);
        }

        [Fact]
        public void Bolt_reload_works_the_bolt_even_when_the_magazine_was_not_empty()
        {
            float pull = FirstPersonReloadChoreography.CyclePullPhase(WeaponHandlingStyle.Bolt, false);
            Assert.True(pull > 0f);
            Assert.True(FirstPersonReloadChoreography.Evaluate(WeaponHandlingStyle.Bolt, pull + 0.06f, false, 3.5f, false).CycleOverride01 > 0.9f);
        }

        [Fact]
        public void Bolt_cycle_opens_after_the_reach_and_closes_before_the_next_shot()
        {
            Assert.Equal(0f, ProceduralViewModel.BoltOpen(0.2f));
            Assert.True(ProceduralViewModel.BoltOpen(0.56f) > 0.99f);
            Assert.Equal(0f, ProceduralViewModel.BoltOpen(0.8f));
            var atKnob = ProceduralViewModel.BoltHandReach(0.56f);
            Assert.True(atKnob.X > 0.04f && atKnob.Z < -0.45f);
            Assert.Equal(default, ProceduralViewModel.BoltHandReach(0f));
        }

        [Fact]
        public void Profiles_resolve_auto_style_from_family()
        {
            var rifle = new WeaponVisualProfile(new DefinitionId("weapons/t-rifle"), WeaponVisualFamily.PrimaryGun, "t",
                WeaponOpticMode.Irons, true, Shooter.Presentation.Audio.SynthVoiceKind.RifleShot, Shooter.Presentation.Hud.KillFeedWeapon.Rifle,
                1f, default, default, .05f, "T");
            var pistol = new WeaponVisualProfile(new DefinitionId("weapons/t-pistol"), WeaponVisualFamily.Sidearm, "t",
                WeaponOpticMode.Irons, true, Shooter.Presentation.Audio.SynthVoiceKind.PistolShot, Shooter.Presentation.Hud.KillFeedWeapon.Pistol,
                1f, default, default, .03f, "T", "pistol");
            Assert.Equal(WeaponHandlingStyle.Rifle, rifle.HandlingStyle);
            Assert.Equal(WeaponHandlingStyle.Pistol, pistol.HandlingStyle);
            Assert.Equal((byte)100, rifle.MuzzleFlashVariant);
            Assert.Equal((byte)65, pistol.MuzzleFlashVariant);
        }

        [Fact]
        public void Magnified_optic_fov_overrides_the_mode_default()
        {
            var scoped = new WeaponVisualProfile(new DefinitionId("weapons/t-sniper"), WeaponVisualFamily.PrimaryGun, "t",
                WeaponOpticMode.ScopedPip, true, Shooter.Presentation.Audio.SynthVoiceKind.RifleShot, Shooter.Presentation.Hud.KillFeedWeapon.Rifle,
                1f, default, default, .05f, "T", adsFovDegrees: 12f);
            Assert.Equal(12f, AdsOpticVisual.AdsFieldOfViewDegrees(80f, scoped), 3);
            Assert.Equal(6f, AdsOpticVisual.AdsFieldOfViewDegrees(40f, scoped), 3);
        }
    }
}

namespace Shooter.Simulation.Tests.View
{
    /// <summary>ADR-102 bank v6 voices and routing.</summary>
    public sealed class ArsenalAudioTests
    {
        [Fact]
        public void New_voices_append_after_v5_slots_and_count_as_gunfire()
        {
            Assert.Equal(29, Shooter.Presentation.Audio.CombatAudioBank.KindIndex(Shooter.Presentation.Audio.SynthVoiceKind.Ak47Shot));
            Assert.Equal(0, Shooter.Presentation.Audio.CombatAudioBank.KindIndex(Shooter.Presentation.Audio.SynthVoiceKind.RifleShot));
            Assert.True(Shooter.Presentation.Audio.SynthAudioMixer.IsGunShotKind(Shooter.Presentation.Audio.SynthVoiceKind.Tac338Shot));
            Assert.False(Shooter.Presentation.Audio.SynthAudioMixer.IsGunShotKind(Shooter.Presentation.Audio.SynthVoiceKind.BoltCycle));
            Assert.Equal(Shooter.Presentation.Audio.CombatAudioDistance.MaxAudibleMeters(Shooter.Presentation.Audio.SynthVoiceKind.ReloadStart),
                Shooter.Presentation.Audio.CombatAudioDistance.MaxAudibleMeters(Shooter.Presentation.Audio.SynthVoiceKind.ReloadAk));
        }

        [Theory]
        [InlineData(WeaponHandlingStyle.Rifle, Shooter.Presentation.Audio.SynthVoiceKind.ReloadStart)]
        [InlineData(WeaponHandlingStyle.Ak, Shooter.Presentation.Audio.SynthVoiceKind.ReloadAk)]
        [InlineData(WeaponHandlingStyle.Bolt, Shooter.Presentation.Audio.SynthVoiceKind.ReloadBolt)]
        [InlineData(WeaponHandlingStyle.Pistol, Shooter.Presentation.Audio.SynthVoiceKind.ReloadPistol)]
        public void Reload_foley_follows_the_handling_style(WeaponHandlingStyle style, Shooter.Presentation.Audio.SynthVoiceKind voice) =>
            Assert.Equal(voice, Shooter.Presentation.Audio.ProductAudioEventRouter.ReloadVoiceForStyle(style));

        [Fact]
        public void Derived_takes_slow_down_for_heavier_guns()
        {
            var clip = new float[2000];
            for (int i = 0; i < clip.Length; i++) clip[i] = (float)System.Math.Sin(i * 0.1);
            var slower = Shooter.Presentation.Audio.CombatAudioBankBaker.Reshape(clip, 0.5f, 1f);
            Assert.Equal(clip.Length * 2, slower.Length);
        }
    }
}
