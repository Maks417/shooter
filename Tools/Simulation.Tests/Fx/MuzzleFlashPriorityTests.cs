#nullable enable
using Shooter.Contracts;
using Shooter.Presentation.Fx;
using Shooter.Presentation.View;
using Xunit;

namespace Shooter.Simulation.Tests.Fx
{
    /// <summary>0.3.20 playtest: busy firefights showed smoke without the muzzle flash.</summary>
    public sealed class MuzzleFlashPriorityTests
    {
        private static readonly Float3 Up = new Float3(0, 1, 0);

        private static AnalyticFxPool SaturatedWithAmbient()
        {
            var pool = new AnalyticFxPool();
            for (int i = 0; i < AnalyticFxPool.MaxSmokeProxies; i++) Assert.True(pool.TrySpawn(FxKind.SmokeProxy, new Float3(i, 0, 0), Up));
            for (int i = 0; i < AnalyticFxPool.MaxShellCasings; i++) Assert.True(pool.TrySpawn(FxKind.ShellCasing, new Float3(i, 1, 0), Up));
            for (int i = 0; i < AnalyticFxPool.MaxDustAndDebris; i++) Assert.True(pool.TrySpawn(i % 2 == 0 ? FxKind.Dust : FxKind.Debris, new Float3(i, 2, 0), Up));
            while (pool.ActiveCount < AnalyticFxPool.MaxInstances)
                Assert.True(pool.TrySpawn(FxKind.ImpactSpark, new Float3(pool.ActiveCount, 3, 0), Up));
            return pool;
        }

        [Fact]
        public void Flash_evicts_the_most_faded_ambient_proxy_when_the_pool_is_full()
        {
            var pool = SaturatedWithAmbient();
            pool.Tick(0.05f);
            Assert.Equal(AnalyticFxPool.MaxInstances, pool.ActiveCount);
            Assert.False(pool.TrySpawn(FxKind.ImpactSpark, new Float3(0, 9, 0), Up));

            int spawned = pool.SpawnGunshotFeedback(new Float3(0, 1, 0), new Float3(0, 0, 1), false, 100);
            Assert.True(spawned >= 2);
            int flashes = 0;
            for (int i = 0; i < AnalyticFxPool.MaxInstances; i++)
                if (pool.Get(i).Active && pool.Get(i).Kind == FxKind.MuzzleFlash) flashes++;
            Assert.Equal(2, flashes);
            Assert.True(pool.Evicted >= 2);
            Assert.True(pool.ActiveCount <= AnalyticFxPool.MaxInstances);
        }

        [Fact]
        public void Counts_stay_consistent_after_eviction()
        {
            var pool = SaturatedWithAmbient();
            for (int i = 0; i < 20; i++) pool.TrySpawn(FxKind.MuzzleFlash, new Float3(i, 5, 0), new Float3(0, 0, 1));
            int smoke = pool.SmokeCount, shells = pool.ShellCount, dust = pool.DustCount, active = pool.ActiveCount;
            pool.Tick(0f);
            Assert.Equal(smoke, pool.SmokeCount);
            Assert.Equal(shells, pool.ShellCount);
            Assert.Equal(dust, pool.DustCount);
            Assert.Equal(active, pool.ActiveCount);
        }

        [Fact]
        public void Dust_and_debris_share_a_cap()
        {
            var pool = new AnalyticFxPool();
            int accepted = 0;
            for (int i = 0; i < AnalyticFxPool.MaxDustAndDebris + 10; i++)
                if (pool.TrySpawn(i % 2 == 0 ? FxKind.Dust : FxKind.Debris, new Float3(i, 0, 0), Up)) accepted++;
            Assert.Equal(AnalyticFxPool.MaxDustAndDebris, accepted);
            Assert.Equal(AnalyticFxPool.MaxDustAndDebris, pool.DustCount);
        }

        [Fact]
        public void Reload_lowers_the_sights_even_without_the_reload_event()
        {
            Assert.True(FirstPersonHandling.ViewAimTarget(aimHeld: true, reloadEventActive: false, authorityReloading: false));
            Assert.False(FirstPersonHandling.ViewAimTarget(true, reloadEventActive: true, authorityReloading: false));
            // Missed ReloadStarted edge: authority alone must still drop ADS so the reload is visible.
            Assert.False(FirstPersonHandling.ViewAimTarget(true, reloadEventActive: false, authorityReloading: true));
            Assert.False(FirstPersonHandling.ViewAimTarget(false, false, false));
        }
    }
}
