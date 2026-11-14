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
            Assert.Equal(1, mix.ActiveVoices);
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
    }
}
