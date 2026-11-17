#nullable enable
using System.Collections.Generic;
using System.Linq;
using Shooter.Contracts;
using Shooter.Simulation.Combat;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    /// <summary>ADR-102 handling: range falloff and seeded random spawn loadouts.</summary>
    public sealed class ArsenalHandlingTests
    {
        private static WeaponDefinition Falloff(float start, float end, float min) =>
            new WeaponDefinition(new DefinitionId("weapons/test"), FireMode.Automatic, 6, 100, 20, 30, 90, 40,
                0.01f, 0.001f, 0.001f, 0.001f, falloffStartMeters: start, falloffEndMeters: end, falloffMinScale: min);

        [Fact]
        public void Falloff_is_full_then_linear_then_clamped()
        {
            var def = Falloff(20f, 60f, 0.5f);
            Assert.Equal(1f, def.FalloffScale(5f));
            Assert.Equal(1f, def.FalloffScale(20f));
            Assert.Equal(0.75f, def.FalloffScale(40f), 4);
            Assert.Equal(0.5f, def.FalloffScale(60f));
            Assert.Equal(0.5f, def.FalloffScale(250f));
            Assert.Equal((ushort)30, DamageMath.FalloffDamage(40, def.FalloffScale(40f)));
            Assert.Equal((ushort)1, DamageMath.FalloffDamage(1, 0.1f));
        }

        [Fact]
        public void No_falloff_when_start_is_zero()
        {
            var def = Falloff(0f, 0f, 0.5f);
            Assert.Equal(1f, def.FalloffScale(300f));
        }

        [Fact]
        public void Random_loadout_is_deterministic_per_seed_actor_and_tick()
        {
            var a = WeaponLoadoutPolicy.CreateRandomLoadout(42UL, new EntityId(7), 1200);
            var b = WeaponLoadoutPolicy.CreateRandomLoadout(42UL, new EntityId(7), 1200);
            Assert.Equal(a.Select(w => w.DefinitionId.ToString()), b.Select(w => w.DefinitionId.ToString()));
            Assert.Equal(4, a.Length);
            Assert.Equal(WeaponFamily.PrimaryGun, WeaponCatalog.Require(a[0].DefinitionId).Family);
            Assert.Equal(WeaponFamily.Sidearm, WeaponCatalog.Require(a[1].DefinitionId).Family);
            Assert.Equal(WeaponLoadoutPolicy.KnifeId, a[2].DefinitionId);
            Assert.Equal(WeaponLoadoutPolicy.GrenadeId, a[3].DefinitionId);
        }

        [Fact]
        public void Random_loadouts_cover_every_weighted_primary()
        {
            var seen = new HashSet<string>();
            for (uint tick = 0; tick < 2000; tick += 7)
                seen.Add(WeaponLoadoutPolicy.CreateRandomLoadout(460072030UL, new EntityId(3), tick)[0].DefinitionId.ToString());
            var expected = WeaponCatalog.Primaries().Where(e => e.Definition.SpawnWeight > 0).Select(e => e.Definition.Id.ToString());
            Assert.Equal(expected.OrderBy(x => x), seen.OrderBy(x => x));
        }

        [Fact]
        public void Weighted_pick_respects_weights_in_catalog_order()
        {
            var primaries = WeaponCatalog.Primaries().Where(e => e.Definition.SpawnWeight > 0).ToArray();
            ulong total = (ulong)primaries.Sum(e => e.Definition.SpawnWeight);
            Assert.Equal(primaries[0].Definition.Id, WeaponLoadoutPolicy.Pick(WeaponFamily.PrimaryGun, 0, default));
            Assert.Equal(primaries[primaries.Length - 1].Definition.Id, WeaponLoadoutPolicy.Pick(WeaponFamily.PrimaryGun, total - 1, default));
        }
    }
}
