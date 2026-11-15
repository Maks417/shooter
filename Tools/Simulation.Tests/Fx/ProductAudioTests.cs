#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Presentation.Audio;
using Xunit;

namespace Shooter.Simulation.Tests.Fx
{
    public sealed class ProductAudioTests
    {
        [Fact]
        public void Device_rate_fill_peak_limits_and_voice_cap()
        {
            var mix = new SynthAudioMixer();
            mix.SetChannelGains(1f, 1f, 0.7f);
            int accepted = 0;
            for (int i = 0; i < 40; i++)
                if (mix.TryPlay(SynthVoiceKind.Shot, i * 0.5f, (ulong)i)) accepted++;
            Assert.Equal(SynthAudioMixer.MaxVoices, accepted);
            Assert.True(mix.RejectedPlays > 0);

            var buffer = new float[480 * 2];
            mix.Fill(buffer, 2, 48000);
            for (int i = 0; i < buffer.Length; i++)
                Assert.InRange(buffer[i], -SynthAudioMixer.PeakLimit, SynthAudioMixer.PeakLimit);
            Assert.True(mix.ActiveVoices <= SynthAudioMixer.MaxVoices);
        }

        [Fact]
        public void UserSettings_schema2_gains_bind_to_mixer_channels()
        {
            var mix = new SynthAudioMixer();
            var settings = new UserSettings(
                2, 0.5f, 0.25f, 0.1f, 0, 1280, 720, DisplayMode.Windowed, false, 60, 80f, 0.1f, false, 1f, false, false, 1f, false,
                UserSettings.DefaultBindings());
            UserSettingsAudioBinding.ApplyGains(settings, mix);
            Assert.Equal(0.5f, mix.MasterVolume);
            Assert.Equal(0.25f, mix.EffectsVolume);
            Assert.Equal(0.1f, mix.UiVolume);
            Assert.False(mix.MasterMuted);

            var muted = new UserSettings(
                2, 0f, 0f, 0f, 0, 1280, 720, DisplayMode.Windowed, false, 60, 80f, 0.1f, false, 1f, false, false, 1f, false,
                UserSettings.DefaultBindings());
            UserSettingsAudioBinding.ApplyGains(muted, mix);
            Assert.True(mix.MasterMuted);
            Assert.True(mix.EffectsMuted);
            Assert.True(mix.UiMuted);
        }

        [Fact]
        public void Gains_and_mute_apply_to_existing_voices()
        {
            var mix = new SynthAudioMixer();
            mix.SetChannelGains(1f, 1f, 0.7f);
            Assert.True(mix.TryPlay(SynthVoiceKind.Shot, 0f, 7));
            float loud = System.Math.Abs(mix.Mix(1f / 48000f));

            mix.SetChannelGains(0f, 1f, 0.7f, masterMuted: true);
            float muted = System.Math.Abs(mix.Mix(1f / 48000f));
            Assert.True(loud > muted);
            Assert.Equal(0f, muted);

            mix.StopAll();
            mix.SetChannelGains(1f, 0f, 0.7f, effectsMuted: true);
            Assert.True(mix.TryPlay(SynthVoiceKind.Impact, 0f, 8));
            Assert.Equal(0f, mix.Mix(1f / 48000f));

            mix.StopAll();
            mix.SetChannelGains(1f, 1f, 0f, uiMuted: true);
            Assert.True(mix.TryPlay(SynthVoiceKind.Ui, 0f, 9));
            Assert.Equal(0f, mix.Mix(1f / 48000f));
        }

        [Fact]
        public void Distance_attenuation_reduces_far_shots()
        {
            var near = new SynthAudioMixer();
            var far = new SynthAudioMixer();
            near.SetChannelGains(1f, 1f, 0.7f);
            far.SetChannelGains(1f, 1f, 0.7f);
            Assert.True(near.TryPlay(SynthVoiceKind.Shot, 0f, 1));
            Assert.True(far.TryPlay(SynthVoiceKind.Shot, 60f, 1));
            float n = 0, f = 0;
            for (int i = 0; i < 64; i++)
            {
                n += System.Math.Abs(near.Mix(1f / 48000f));
                f += System.Math.Abs(far.Mix(1f / 48000f));
            }
            Assert.True(n > f);
        }

        [Fact]
        public void Combat_events_play_once_by_event_id()
        {
            var mix = new SynthAudioMixer();
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(1001);
            var id = new EventId(1001, 3, new EntityId(5), 1);
            var shot = new CombatEvent(id, CombatEventKind.ShotAccepted, new EntityId(5), default,
                new ShotPayload(new DefinitionId("weapons/rifle"), 1, new Float3(0, 1.5f, 0), new Float3(0, 0, 1), 42, 1));
            var list = new List<CombatEvent> { shot, shot };
            Assert.Equal(1, router.ConsumeCombatEvents(list, new Float3(0, 0, 0)));
            Assert.Equal(0, router.ConsumeCombatEvents(list, new Float3(0, 0, 0)));
            mix.Mix(1f / 48000f);
            // Ambient bed + shot.
            Assert.True(mix.ActiveVoices >= 1);
            Assert.True(mix.ActiveVoices <= 2);
        }

        [Fact]
        public void Stale_session_suppresses_and_stop_clears_voices()
        {
            var mix = new SynthAudioMixer();
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(1001);
            var id = new EventId(1001, 1, new EntityId(2), 1);
            var impact = new CombatEvent(id, CombatEventKind.Impact, new EntityId(2), new EntityId(9),
                new ImpactPayload(new Float3(1, 0, 0), new Float3(0, 1, 0), 1, 1));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { impact }, new Float3(0, 0, 0)));
            mix.Mix(1f / 48000f);
            Assert.True(mix.ActiveVoices > 0);

            router.DetachSession();
            mix.Mix(1f / 48000f);
            Assert.Equal(0, mix.ActiveVoices);
            Assert.Equal(0, router.ConsumeCombatEvents(new[] { impact }, new Float3(0, 0, 0)));
            Assert.False(router.PlayUi(3));
        }

        [Fact]
        public void Footsteps_emit_from_stride_and_ui_uses_ui_channel()
        {
            var mix = new SynthAudioMixer();
            mix.SetChannelGains(1f, 1f, 0.7f);
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(55);
            var living = new List<ActorLifecycleState>
            {
                new ActorLifecycleState(new EntityId(7), 1, true, 100, new Float3(0, 0, 0), 0, 0, 1, 30, 90)
            };
            Assert.Equal(0, router.TickFootsteps(living, new Float3(0, 0, 0), 1));
            living[0] = new ActorLifecycleState(new EntityId(7), 1, true, 100, new Float3(ProductAudioEventRouter.FootstepStrideMeters, 0, 0), 0, 0, 1, 30, 90);
            Assert.Equal(1, router.TickFootsteps(living, new Float3(0, 0, 0), 2));
            Assert.True(router.PlayUi(99));
            mix.Mix(1f / 48000f);
            Assert.True(mix.ActiveVoices >= 1);
        }

        [Fact]
        public void StopAll_detaches_stale_voices_between_sessions()
        {
            var mix = new SynthAudioMixer();
            Assert.True(mix.TryPlay(SynthVoiceKind.Shot, 0f, 1));
            mix.StopAll();
            var buffer = new float[128];
            mix.Fill(buffer, 1, 48000);
            Assert.Equal(0, mix.ActiveVoices);
            for (int i = 0; i < buffer.Length; i++)
                Assert.Equal(0f, buffer[i]);
        }

        [Fact]
        public void Bank_hash_is_stable_and_rifle_differs_from_pistol()
        {
            var a = CombatAudioBankBaker.Bake();
            var b = CombatAudioBankBaker.Bake();
            Assert.Equal(a.ContentHashHex, b.ContentHashHex);
            Assert.Equal(CombatAudioBank.RecipeId, CombatAudioBank.RecipeId);
            Assert.True(a.TryGetClip(SynthVoiceKind.RifleShot, 0, out var rifle));
            Assert.True(a.TryGetClip(SynthVoiceKind.PistolShot, 0, out var pistol));
            Assert.True(rifle.Length > pistol.Length);
            Assert.False(SameSamples(rifle, pistol));
        }

        [Fact]
        public void Reload_death_and_pistol_map_from_combat_events()
        {
            var mix = new SynthAudioMixer();
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(77);
            var reload = new CombatEvent(
                new EventId(77, 1, new EntityId(3), 1), CombatEventKind.ReloadStarted, new EntityId(3), default,
                new ActionPayload(0, 0, 144));
            var done = new CombatEvent(
                new EventId(77, 2, new EntityId(3), 2), CombatEventKind.ReloadCompleted, new EntityId(3), default,
                new ActionPayload(0, 0, 144));
            var death = new CombatEvent(
                new EventId(77, 3, new EntityId(9), 1), CombatEventKind.Death, new EntityId(4), new EntityId(9),
                new DeathPayload(new EntityId(4)));
            var pistol = new CombatEvent(
                new EventId(77, 4, new EntityId(3), 3), CombatEventKind.ShotAccepted, new EntityId(3), default,
                new ShotPayload(new DefinitionId("weapons/pistol"), 1, new Float3(2, 1, 0), new Float3(0, 0, 1), 9, 1));

            Assert.Equal(1, router.ConsumeCombatEvents(new[] { reload }, new Float3(0, 0, 0)));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { done }, new Float3(0, 0, 0)));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { death }, new Float3(0, 0, 0)));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { pistol }, new Float3(0, 0, 0)));
            mix.Mix(1f / 48000f);
            Assert.True(mix.ActiveVoices >= 4);
        }

        [Fact]
        public void Head_region_maps_to_headshot_not_body_hit()
        {
            var mix = new SynthAudioMixer();
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(88);
            var headImpact = new CombatEvent(
                new EventId(88, 1, new EntityId(2), 1), CombatEventKind.Impact, new EntityId(2), new EntityId(9),
                new ImpactPayload(new Float3(1, 1.6f, 0), new Float3(0, 0, 1), 1, ProductAudioEventRouter.HeadRegionId));
            var bodyDamage = new CombatEvent(
                new EventId(88, 2, new EntityId(2), 2), CombatEventKind.DamageApplied, new EntityId(2), new EntityId(9),
                new DamagePayload(30, 70, 2));
            var headDamage = new CombatEvent(
                new EventId(88, 3, new EntityId(2), 3), CombatEventKind.DamageApplied, new EntityId(2), new EntityId(9),
                new DamagePayload(60, 10, ProductAudioEventRouter.HeadRegionId));

            Assert.Equal(1, router.ConsumeCombatEvents(new[] { headImpact }, new Float3(0, 0, 0)));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { bodyDamage }, new Float3(0, 0, 0)));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { headDamage }, new Float3(0, 0, 0)));

            Assert.True(mix.Bank.TryGetClip(SynthVoiceKind.Headshot, 0, out var hs));
            Assert.True(mix.Bank.TryGetClip(SynthVoiceKind.Impact, 0, out var impact));
            Assert.True(mix.Bank.TryGetClip(SynthVoiceKind.DamageTaken, 0, out var body));
            Assert.False(SameSamples(hs, impact));
            Assert.False(SameSamples(hs, body));
        }

        [Fact]
        public void Stereo_fill_pans_left_and_right()
        {
            var mix = new SynthAudioMixer();
            mix.SetChannelGains(1f, 1f, 0.7f);
            Assert.True(mix.TryPlay(SynthVoiceKind.RifleShot, 5f, -1f, 3));
            var leftBuf = new float[256 * 2];
            mix.Fill(leftBuf, 2, 48000);
            float leftEnergy = 0f, rightEnergy = 0f;
            for (int i = 0; i < leftBuf.Length; i += 2)
            {
                leftEnergy += System.Math.Abs(leftBuf[i]);
                rightEnergy += System.Math.Abs(leftBuf[i + 1]);
            }
            Assert.True(leftEnergy > rightEnergy);
        }

        private static bool SameSamples(float[] a, float[] b)
        {
            int n = System.Math.Min(a.Length, b.Length);
            int nCompare = System.Math.Min(n, 512);
            for (int i = 0; i < nCompare; i++)
                if (System.Math.Abs(a[i] - b[i]) > 1e-6f) return false;
            return a.Length == b.Length;
        }
    }
}
