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

        [Fact]
        public void Analytic_fx_lifetimes_and_scales_are_kind_specific()
        {
            Assert.Equal(AnalyticFxPool.MuzzleFlashLifetime, AnalyticFxPool.LifetimeFor(FxKind.MuzzleFlash));
            Assert.Equal(AnalyticFxPool.ImpactSparkLifetime, AnalyticFxPool.LifetimeFor(FxKind.ImpactSpark));
            Assert.Equal(AnalyticFxPool.SmokeProxyLifetime, AnalyticFxPool.LifetimeFor(FxKind.SmokeProxy));

            float muzzleStart = AnalyticFxPool.EvaluateScale(FxKind.MuzzleFlash, 0f, AnalyticFxPool.MuzzleFlashLifetime);
            float muzzleEnd = AnalyticFxPool.EvaluateScale(FxKind.MuzzleFlash, AnalyticFxPool.MuzzleFlashLifetime, AnalyticFxPool.MuzzleFlashLifetime);
            Assert.True(muzzleStart > muzzleEnd);

            float smokeStart = AnalyticFxPool.EvaluateScale(FxKind.SmokeProxy, 0f, AnalyticFxPool.SmokeProxyLifetime);
            float smokeEnd = AnalyticFxPool.EvaluateScale(FxKind.SmokeProxy, AnalyticFxPool.SmokeProxyLifetime, AnalyticFxPool.SmokeProxyLifetime);
            Assert.True(smokeEnd > smokeStart);

            Assert.True(AnalyticFxPool.EvaluateOpacity(FxKind.SmokeProxy, 0f, AnalyticFxPool.SmokeProxyLifetime) < 0.5f);
            Assert.Equal(0f, AnalyticFxPool.EvaluateOpacity(FxKind.ImpactSpark, AnalyticFxPool.ImpactSparkLifetime, AnalyticFxPool.ImpactSparkLifetime));
        }

        [Fact]
        public void Analytic_fx_pool_expires_by_kind_lifetime_and_clear_resets()
        {
            var pool = new AnalyticFxPool();
            Assert.True(pool.TrySpawn(FxKind.MuzzleFlash, new Float3(0, 1, 0), new Float3(0, 0, 1)));
            Assert.True(pool.TrySpawn(FxKind.ImpactSpark, new Float3(1, 0, 0), new Float3(0, 1, 0)));
            Assert.True(pool.TrySpawn(FxKind.SmokeProxy, new Float3(2, 0, 0), new Float3(0, 1, 0)));
            Assert.Equal(3, pool.ActiveCount);
            Assert.Equal(1, pool.SmokeCount);

            pool.Tick(AnalyticFxPool.MuzzleFlashLifetime);
            Assert.Equal(2, pool.ActiveCount);

            pool.Tick(AnalyticFxPool.ImpactSparkLifetime);
            Assert.Equal(1, pool.ActiveCount);
            Assert.Equal(1, pool.SmokeCount);

            pool.Clear();
            Assert.Equal(0, pool.ActiveCount);
            Assert.Equal(0, pool.SmokeCount);
            for (int i = 0; i < AnalyticFxPool.MaxInstances; i++)
                Assert.False(pool.Get(i).Active);
        }

        [Fact]
        public void Analytic_fx_smoke_cap_independent_of_flash_slots()
        {
            var pool = new AnalyticFxPool();
            int smokeAccepted = 0;
            for (int i = 0; i < AnalyticFxPool.MaxSmokeProxies + 8; i++)
            {
                if (pool.TrySpawn(FxKind.SmokeProxy, new Float3(i, 0, 0), new Float3(0, 1, 0)))
                    smokeAccepted++;
            }
            Assert.Equal(AnalyticFxPool.MaxSmokeProxies, smokeAccepted);
            Assert.Equal(AnalyticFxPool.MaxSmokeProxies, pool.SmokeCount);
            Assert.True(pool.Rejected >= 8);

            int flashes = 0;
            for (int i = 0; i < 40; i++)
            {
                if (pool.TrySpawn(FxKind.MuzzleFlash, new Float3(i, 1, 0), new Float3(1, 0, 0)))
                    flashes++;
            }
            Assert.True(flashes > 0);
            Assert.True(pool.ActiveCount <= AnalyticFxPool.MaxInstances);
            Assert.Equal(AnalyticFxPool.MaxSmokeProxies, pool.SmokeCount);
        }

        [Fact]
        public void Analytic_fx_muzzle_is_shorter_than_impact_and_smoke()
        {
            // Readable combat flash: brief muzzle, short spark, longer translucent smoke.
            Assert.True(AnalyticFxPool.MuzzleFlashLifetime < AnalyticFxPool.ImpactSparkLifetime);
            Assert.True(AnalyticFxPool.ImpactSparkLifetime < AnalyticFxPool.SmokeProxyLifetime);

            var pool = new AnalyticFxPool();
            Assert.True(pool.TrySpawn(FxKind.MuzzleFlash, new Float3(0, 0, 0), new Float3(0, 0, 1)));
            var muzzle = pool.Get(0);
            Assert.Equal(FxKind.MuzzleFlash, muzzle.Kind);
            Assert.Equal(AnalyticFxPool.MuzzleFlashLifetime, muzzle.Lifetime);
            Assert.True(muzzle.Normal.IsUnit);
        }

        [Fact]
        public void Analytic_fx_rejects_negative_tick_and_preserves_counts()
        {
            var pool = new AnalyticFxPool();
            Assert.True(pool.TrySpawn(FxKind.ImpactSpark, new Float3(0, 0, 0), new Float3(0, 1, 0)));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => pool.Tick(-0.01f));
            Assert.Equal(1, pool.ActiveCount);
            pool.Tick(0f);
            Assert.Equal(1, pool.ActiveCount);
            Assert.True(pool.Get(0).Active);
        }
    }
}
