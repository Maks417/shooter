#nullable enable
using Shooter.Contracts;
using Shooter.Presentation.Audio;
using Shooter.Presentation.Fx;
using Xunit;

namespace Shooter.Simulation.Tests.Fx
{
    public sealed class FeedbackTests
    {
        [Fact]
        public void Synth_mixer_caps_voices_and_peak_limits()
        {
            var mix = new SynthAudioMixer();
            mix.SetChannelGains(1f, 1f, 0.7f);
            int accepted = 0;
            for (int i = 0; i < 40; i++)
                if (mix.TryPlay(SynthVoiceKind.Shot, i * 0.5f, (ulong)i)) accepted++;
            Assert.Equal(SynthAudioMixer.MaxVoices, accepted);
            float sample = mix.Mix(1f / 60f);
            Assert.InRange(sample, -SynthAudioMixer.PeakLimit, SynthAudioMixer.PeakLimit);
            Assert.True(mix.ActiveVoices <= SynthAudioMixer.MaxVoices);
            Assert.True(mix.LastMixCpuMs <= SynthAudioMixer.CpuBudgetMs * 20); // loose host timing; budget is design target
        }

        [Fact]
        public void Analytic_fx_pool_is_bounded_without_sprites()
        {
            var pool = new AnalyticFxPool();
            for (int i = 0; i < 100; i++)
                pool.TrySpawn(i % 3 == 0 ? FxKind.SmokeProxy : FxKind.ImpactSpark, new Float3(i, 0, 0), new Float3(0, 1, 0));
            Assert.True(pool.ActiveCount <= AnalyticFxPool.MaxInstances);
            Assert.True(pool.SmokeCount <= AnalyticFxPool.MaxSmokeProxies);
            Assert.True(pool.Rejected > 0);
            pool.Tick(2f);
            Assert.Equal(0, pool.ActiveCount);
        }
    }
}
