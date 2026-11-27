#nullable enable
using Shooter.Contracts;
using Shooter.Presentation.Fx;
using Xunit;

namespace Shooter.Simulation.Tests.Fx
{
    /// <summary>ADR-111 B: layered, colourful grenade blast in the pure FX pool.</summary>
    public sealed class GrenadeBlastFxTests
    {
        private static readonly Float3 Up = new Float3(0, 1, 0);
        private static readonly Float3 At = new Float3(3, 0, 4);

        private static int Count(AnalyticFxPool pool, FxKind kind, bool includeDelayed = true)
        {
            int n = 0;
            for (int i = 0; i < AnalyticFxPool.MaxInstances; i++)
            {
                var fx = pool.Get(i);
                if (fx.Active && fx.Kind == kind && (includeDelayed || fx.Age >= 0f)) n++;
            }
            return n;
        }

        [Fact]
        public void Blast_kinds_keep_their_wire_values_and_lifetimes()
        {
            Assert.Equal(10, (int)FxKind.BlastFlash);
            Assert.Equal(11, (int)FxKind.FirePuff);
            Assert.Equal(12, (int)FxKind.Sparks);
            Assert.Equal(13, (int)FxKind.Embers);
            Assert.Equal(14, (int)FxKind.DustRing);
            Assert.Equal(0.09f, AnalyticFxPool.LifetimeFor(FxKind.BlastFlash));
            Assert.Equal(1.6f, AnalyticFxPool.LifetimeFor(FxKind.Fireball));
            Assert.Equal(1.2f, AnalyticFxPool.LifetimeFor(FxKind.Sparks));
            Assert.Equal(2.5f, AnalyticFxPool.LifetimeFor(FxKind.Embers));
            Assert.True(AnalyticFxPool.LifetimeFor(FxKind.Shockwave) < 0.5f, "shockwave got faster");
            Assert.Equal(5.5f, AnalyticFxPool.LifetimeFor(FxKind.SmokeProxy, AnalyticFxPool.BlastVariant));
            Assert.Equal(AnalyticFxPool.SmokeProxyLifetime, AnalyticFxPool.LifetimeFor(FxKind.SmokeProxy, 0));
            Assert.Equal(1.3f, AnalyticFxPool.LifetimeFor(FxKind.Debris));
            Assert.True(AnalyticFxPool.SparkMinLifetime == 0.5f);
        }

        [Theory]
        [InlineData(0, 6, 40, 8)]
        [InlineData(1, 8, 80, 14)]
        [InlineData(2, 10, 120, 14)]
        public void Tier_counts_scale_and_low_stays_cheapest(int tier, int puffs, int sparks, int smoke)
        {
            Assert.Equal(puffs, AnalyticFxPool.FirePuffCount(tier));
            Assert.Equal(sparks, AnalyticFxPool.SparkCount(tier));
            Assert.Equal(smoke, AnalyticFxPool.BlastSmokeParticles(tier));
            Assert.True(AnalyticFxPool.EmberCount(0) <= AnalyticFxPool.EmberCount(tier));
            Assert.True(AnalyticFxPool.DustRingParticles(0) <= AnalyticFxPool.DustRingParticles(tier));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void Full_blast_spawns_every_layer(int tier)
        {
            var pool = new AnalyticFxPool();
            int spawned = pool.SpawnGrenadeBlast(At, Up, reducedFlash: false, qualityTier: tier);
            Assert.Equal(1, Count(pool, FxKind.BlastFlash));
            Assert.Equal(1, Count(pool, FxKind.Fireball));
            Assert.Equal(AnalyticFxPool.FirePuffCount(tier), Count(pool, FxKind.FirePuff));
            Assert.Equal(1, Count(pool, FxKind.Sparks));
            Assert.Equal(1, Count(pool, FxKind.Embers));
            Assert.Equal(1, Count(pool, FxKind.Shockwave));
            Assert.Equal(1, Count(pool, FxKind.DustRing));
            Assert.Equal(2, Count(pool, FxKind.Debris));
            Assert.Equal(1, Count(pool, FxKind.SmokeProxy));
            Assert.Equal(spawned, pool.ActiveCount);
            Assert.Equal(spawned - 3, pool.BlastCount);
            Assert.True(pool.BlastCount <= AnalyticFxPool.MaxBlastProxies);
        }

        [Fact]
        public void Fire_puffs_are_radial_and_staggered_within_120_ms()
        {
            var pool = new AnalyticFxPool();
            pool.SpawnGrenadeBlast(At, Up, false, 2);
            float minDelay = 1f, maxDelay = 0f;
            for (int i = 0; i < AnalyticFxPool.MaxInstances; i++)
            {
                var fx = pool.Get(i);
                if (!fx.Active || fx.Kind != FxKind.FirePuff) continue;
                float delay = -fx.Age;
                Assert.InRange(delay, 0f, AnalyticFxPool.FirePuffMaxDelay + 1e-5f);
                minDelay = System.Math.Min(minDelay, delay);
                maxDelay = System.Math.Max(maxDelay, delay);
                Assert.True(fx.Normal.IsUnit);
                Assert.True(fx.Normal.Y > 0f, "puffs lean away from the ground");
                float dx = fx.Position.X - At.X, dz = fx.Position.Z - At.Z;
                Assert.True(dx * dx + dz * dz > 0.01f, "puffs sit around, not on, the blast centre");
            }
            Assert.Equal(0f, minDelay);
            Assert.True(maxDelay > 0.1f);
            // Delayed layers stay hidden until their age reaches zero, then live their full lifetime.
            int visible = Count(pool, FxKind.FirePuff, includeDelayed: false);
            Assert.True(visible < AnalyticFxPool.FirePuffCount(2));
            pool.Tick(AnalyticFxPool.FirePuffMaxDelay + 0.001f);
            Assert.Equal(AnalyticFxPool.FirePuffCount(2), Count(pool, FxKind.FirePuff, includeDelayed: false));
            pool.Tick(AnalyticFxPool.FirePuffLifetime - 0.01f);
            Assert.True(Count(pool, FxKind.FirePuff) > 0, "late puffs still burning");
        }

        [Fact]
        public void Reduced_flash_drops_bright_layers_but_keeps_dust_debris_and_smoke()
        {
            var pool = new AnalyticFxPool();
            pool.SpawnGrenadeBlast(At, Up, reducedFlash: true, qualityTier: 2);
            foreach (var kind in new[] { FxKind.BlastFlash, FxKind.Fireball, FxKind.FirePuff, FxKind.Sparks, FxKind.Shockwave })
            {
                Assert.Equal(0, Count(pool, kind));
                Assert.True(AnalyticFxPool.IsFlashing(kind));
            }
            Assert.Equal(1, Count(pool, FxKind.DustRing));
            Assert.Equal(2, Count(pool, FxKind.Debris));
            Assert.Equal(1, Count(pool, FxKind.SmokeProxy));
            Assert.False(AnalyticFxPool.IsFlashing(FxKind.SmokeProxy));
            Assert.False(AnalyticFxPool.IsFlashing(FxKind.DustRing));
        }

        [Fact]
        public void Blast_smoke_column_lasts_five_and_a_half_seconds()
        {
            var pool = new AnalyticFxPool();
            pool.SpawnGrenadeBlast(At, Up, false, 0);
            pool.Tick(5.0f);
            Assert.Equal(1, Count(pool, FxKind.SmokeProxy));
            Assert.Equal(0, Count(pool, FxKind.Fireball));
            pool.Tick(0.6f);
            Assert.Equal(0, pool.ActiveCount);
            Assert.Equal(0, pool.BlastCount);
            Assert.Equal(0, pool.SmokeCount);
        }

        [Fact]
        public void Blast_evicts_ambient_proxies_from_a_saturated_pool()
        {
            var pool = new AnalyticFxPool();
            for (int i = 0; i < AnalyticFxPool.MaxSmokeProxies; i++) Assert.True(pool.TrySpawn(FxKind.SmokeProxy, new Float3(i, 0, 0), Up));
            for (int i = 0; i < AnalyticFxPool.MaxShellCasings; i++) Assert.True(pool.TrySpawn(FxKind.ShellCasing, new Float3(i, 1, 0), Up));
            for (int i = 0; i < AnalyticFxPool.MaxDustAndDebris; i++) Assert.True(pool.TrySpawn(FxKind.Dust, new Float3(i, 2, 0), Up));
            while (pool.ActiveCount < AnalyticFxPool.MaxInstances)
                Assert.True(pool.TrySpawn(FxKind.ImpactSpark, new Float3(pool.ActiveCount, 3, 0), Up));
            pool.Tick(0.05f);

            pool.SpawnGrenadeBlast(At, Up, false, 2);
            Assert.Equal(1, Count(pool, FxKind.BlastFlash));
            Assert.Equal(1, Count(pool, FxKind.Fireball));
            Assert.Equal(1, Count(pool, FxKind.Sparks));
            Assert.Equal(1, Count(pool, FxKind.Shockwave));
            Assert.Equal(AnalyticFxPool.FirePuffCount(2), Count(pool, FxKind.FirePuff));
            Assert.True(pool.Evicted > 0);
            Assert.True(pool.ActiveCount <= AnalyticFxPool.MaxInstances);
            int smoke = pool.SmokeCount, shells = pool.ShellCount, dust = pool.DustCount, blast = pool.BlastCount, active = pool.ActiveCount;
            pool.Tick(0f);
            Assert.Equal(smoke, pool.SmokeCount);
            Assert.Equal(shells, pool.ShellCount);
            Assert.Equal(dust, pool.DustCount);
            Assert.Equal(blast, pool.BlastCount);
            Assert.Equal(active, pool.ActiveCount);
        }

        [Fact]
        public void Blast_cap_holds_and_newest_blast_replaces_the_most_faded_layers()
        {
            var pool = new AnalyticFxPool();
            for (int b = 0; b < 4; b++)
            {
                pool.SpawnGrenadeBlast(new Float3(b * 10, 0, 0), Up, false, 2);
                Assert.True(pool.BlastCount <= AnalyticFxPool.MaxBlastProxies);
                pool.Tick(0.02f);
            }
            Assert.Equal(AnalyticFxPool.MaxBlastProxies, pool.BlastCount);
            // The last blast is complete: its flash and fireball were never refused.
            int newestFlashes = 0;
            for (int i = 0; i < AnalyticFxPool.MaxInstances; i++)
            {
                var fx = pool.Get(i);
                if (fx.Active && fx.Kind == FxKind.BlastFlash && fx.Position.X == 30) newestFlashes++;
            }
            Assert.Equal(1, newestFlashes);
            Assert.True(pool.ActiveCount <= AnalyticFxPool.MaxInstances);
        }

        [Fact]
        public void Two_overlapping_blasts_fit_without_eviction()
        {
            var pool = new AnalyticFxPool();
            pool.SpawnGrenadeBlast(At, Up, false, 2);
            pool.SpawnGrenadeBlast(new Float3(-3, 0, 1), Up, false, 2);
            Assert.Equal(0, pool.Evicted);
            Assert.Equal(0, pool.Rejected);
            Assert.Equal(2, Count(pool, FxKind.Fireball));
        }

        [Fact]
        public void Flash_light_peaks_at_sixteen_flickers_and_is_dark_after_450_ms()
        {
            Assert.Equal(22f, AnalyticFxPool.BlastLightRange);
            Assert.Equal(16f, AnalyticFxPool.BlastLightAt(0f), 3);
            Assert.Equal(0f, AnalyticFxPool.BlastLightAt(AnalyticFxPool.BlastLightSeconds));
            Assert.Equal(0f, AnalyticFxPool.BlastLightAt(-0.01f));
            float previousEnvelope = float.MaxValue;
            bool flickers = false;
            float last = AnalyticFxPool.BlastLightAt(0f);
            for (float t = 0.01f; t < AnalyticFxPool.BlastLightSeconds; t += 0.01f)
            {
                float value = AnalyticFxPool.BlastLightAt(t);
                float k = 1f - t / AnalyticFxPool.BlastLightSeconds, envelope = 16f * k * k;
                Assert.InRange(value, 0.78f * envelope - 1e-4f, envelope + 1e-4f);
                Assert.True(envelope <= previousEnvelope);
                previousEnvelope = envelope;
                if (value > last) flickers = true;
                last = value;
            }
            Assert.True(flickers, "light must flicker, not just decay");
        }

        [Fact]
        public void Camera_shake_falls_off_to_zero_at_25_m()
        {
            Assert.Equal(1f, AnalyticFxPool.BlastShakeScale(0f));
            Assert.True(AnalyticFxPool.BlastShakeScale(5f) > AnalyticFxPool.BlastShakeScale(15f));
            Assert.True(AnalyticFxPool.BlastShakeScale(24f) > 0f);
            Assert.Equal(0f, AnalyticFxPool.BlastShakeScale(25f));
            Assert.Equal(0f, AnalyticFxPool.BlastShakeScale(80f));
            Assert.Equal(0f, AnalyticFxPool.BlastShakeScale(float.NaN));
        }

        [Fact]
        public void Pool_caps_were_raised_for_layered_blasts()
        {
            Assert.Equal(104, AnalyticFxPool.MaxInstances); // ADR-127: +8 with the tracer cap (16 -> 24)
            Assert.Equal(24, AnalyticFxPool.MaxTracers);
            Assert.Equal(40, AnalyticFxPool.MaxBlastProxies);
            Assert.True(AnalyticFxPool.MaxBlastProxies + AnalyticFxPool.MaxSmokeProxies + AnalyticFxPool.MaxShellCasings
                + AnalyticFxPool.MaxTracers + AnalyticFxPool.MaxDustAndDebris <= AnalyticFxPool.MaxInstances + 14);
        }
    }
}
