#nullable enable
using Shooter.Presentation.Audio;
using Shooter.Presentation.Hud;
using Shooter.Presentation.View;
using Xunit;

namespace Shooter.Simulation.Tests.Hud
{
    /// <summary>T195: per-gun kill-feed and report tables come from Content/Weapons through the generator.</summary>
    public sealed class GeneratedWeaponTablesTests
    {
        [Fact]
        public void Every_profile_glyph_has_its_mesh_icon_and_every_gun_report_is_a_shot_voice()
        {
            foreach (var profile in WeaponVisualProfiles.All)
            {
                Assert.StartsWith("weapon.", KillFeedGlyphs.IconName(profile.KillFeedGlyph));
                bool gun = profile.Family == WeaponVisualFamily.PrimaryGun || profile.Family == WeaponVisualFamily.Sidearm;
                Assert.Equal(gun, WeaponShotVoices.IsWeaponShot(profile.ShotVoice));
                Assert.Equal(gun, SynthAudioMixer.IsGunShotKind(profile.ShotVoice));
            }
        }

        [Fact]
        public void Named_reports_keep_their_interim_derivation()
        {
            Assert.Equal((SynthVoiceKind.RifleShot, 0.66f, 1.00f), CombatAudioBankBaker.DerivedFrom(SynthVoiceKind.M870Shot));
            Assert.Equal((SynthVoiceKind.PistolShot, 0.74f, 1.00f), CombatAudioBankBaker.DerivedFrom(SynthVoiceKind.EagleShot));
            // Foley derivations are still owned by the baker.
            Assert.Equal((SynthVoiceKind.ReloadComplete, 0.80f, 1.00f), CombatAudioBankBaker.DerivedFrom(SynthVoiceKind.PumpCycle));
            Assert.Equal(default(SynthVoiceKind), CombatAudioBankBaker.DerivedFrom(SynthVoiceKind.RifleShot).source);
            Assert.True(SynthAudioMixer.IsGunShotKind(SynthVoiceKind.Shot));
        }

        [Fact]
        public void Carbines_use_the_short_fallback_silhouette()
        {
            Assert.Equal(KillFeedSilhouette.Carbine, KillFeedGlyphs.Silhouette(KillFeedWeapon.Carbine));
            Assert.Equal(KillFeedSilhouette.Carbine, KillFeedGlyphs.Silhouette(KillFeedWeapon.M4a1));
            Assert.Equal(KillFeedSilhouette.Rifle, KillFeedGlyphs.Silhouette(KillFeedWeapon.M870));
            Assert.Equal(KillFeedSilhouette.Grenade, KillFeedGlyphs.Silhouette(KillFeedWeapon.Grenade));
            Assert.Equal(13, KillFeedGlyphs.Gallery.Length);
        }
    }
}
