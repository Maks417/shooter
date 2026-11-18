#nullable enable
using System.Collections.Generic;
using System.Linq;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Simulation.Combat;
using Shooter.Simulation.Session;
using Shooter.Simulation.Tests.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    /// <summary>ADR-107: rail-mounted sights carried by the authority, Armory loadouts pinned to the human.</summary>
    public sealed class SightLoadoutTests
    {
        private static readonly DefinitionId Ak47 = new DefinitionId("weapons/ak47");
        private static readonly DefinitionId M16 = new DefinitionId("weapons/m16");
        private static readonly DefinitionId Mk14 = new DefinitionId("weapons/mk14");
        private static readonly DefinitionId Mtac = new DefinitionId("weapons/mtac338");
        private static readonly DefinitionId G17 = new DefinitionId("weapons/g17");
        private static readonly DefinitionId Eagle = new DefinitionId("weapons/golden-eagle");

        [Fact]
        public void Every_primary_takes_irons_collimator_and_the_m16_prism_and_only_its_own_magnified_scope()
        {
            var shared = new[] { SightCatalog.Irons, SightCatalog.Collimator, SightCatalog.Prism2x };
            foreach (var gun in new[] { Ak47, new DefinitionId("weapons/ak102"), M16, Mk14, Mtac })
                foreach (var sight in shared)
                    Assert.True(SightCatalog.IsAllowed(gun, sight), gun + " " + sight);
            Assert.True(SightCatalog.IsAllowed(Mk14, SightCatalog.Scope4x));
            Assert.True(SightCatalog.IsAllowed(Mtac, SightCatalog.Scope8x));
            Assert.False(SightCatalog.IsAllowed(M16, SightCatalog.Scope4x));
            Assert.False(SightCatalog.IsAllowed(Ak47, SightCatalog.Scope8x));
            Assert.False(SightCatalog.IsAllowed(Mk14, SightCatalog.Scope8x));
            // Factory sights are the 0.3.x look: AK irons, M16 prism, Mk 14 4x, M-TAC 8x.
            Assert.Equal(SightCatalog.Irons, SightCatalog.DefaultFor(Ak47));
            Assert.Equal(SightCatalog.Prism2x, SightCatalog.DefaultFor(M16));
            Assert.Equal(SightCatalog.Scope4x, SightCatalog.DefaultFor(Mk14));
            Assert.Equal(SightCatalog.Scope8x, SightCatalog.DefaultFor(Mtac));
        }

        [Fact]
        public void Sidearms_and_legacy_guns_take_no_mounted_sight()
        {
            Assert.False(SightCatalog.TryGetOptions(G17, out _));
            Assert.False(SightCatalog.TryGetOptions(Eagle, out _));
            Assert.False(SightCatalog.TryGetOptions(WeaponLoadoutPolicy.DefaultPrimaryId, out _));
            Assert.False(SightCatalog.DefaultFor(G17).IsValid);
            Assert.False(SightCatalog.Resolve(G17, SightCatalog.Collimator).IsValid);
        }

        [Fact]
        public void Player_loadout_falls_back_per_field()
        {
            var good = PlayerLoadout.Create(Ak47, SightCatalog.Collimator, Eagle);
            Assert.Equal(Ak47, good.Primary);
            Assert.Equal(SightCatalog.Collimator, good.PrimarySight);
            Assert.Equal(Eagle, good.Sidearm);
            // A 4x scope is not offered on the AK: it carries its factory irons instead.
            Assert.Equal(SightCatalog.Irons, PlayerLoadout.Create(Ak47, SightCatalog.Scope4x, G17).PrimarySight);
            // A sidearm in the primary slot, a legacy test rifle and a primary in the sidearm slot all fall back.
            var swapped = PlayerLoadout.Create(G17, default, M16);
            Assert.Equal(PlayerLoadout.DefaultPrimary, swapped.Primary);
            Assert.Equal(PlayerLoadout.DefaultSidearm, swapped.Sidearm);
            Assert.Equal(PlayerLoadout.DefaultPrimary, PlayerLoadout.Create(WeaponLoadoutPolicy.DefaultPrimaryId, default, G17).Primary);
            Assert.Equal(SightCatalog.Prism2x, PlayerLoadout.Default.PrimarySight);
        }

        [Fact]
        public void Random_sight_is_deterministic_allowed_and_varied()
        {
            var seen = new HashSet<DefinitionId>();
            for (ulong actor = 1; actor <= 200; actor++)
            {
                var sight = WeaponLoadoutPolicy.RandomSight(460072030UL, new EntityId(actor), 7, M16);
                Assert.Equal(sight, WeaponLoadoutPolicy.RandomSight(460072030UL, new EntityId(actor), 7, M16));
                Assert.True(SightCatalog.IsAllowed(M16, sight));
                seen.Add(sight);
            }
            Assert.Equal(3, seen.Count);
            Assert.False(WeaponLoadoutPolicy.RandomSight(1, new EntityId(1), 0, G17).IsValid);
        }

        [Fact]
        public void Random_loadout_harness_issues_an_allowed_sight_with_every_primary()
        {
            var sim = new CombatHarnessSimulation(new StaticPlaneWorld()) { RandomLoadouts = true };
            for (int i = 0; i < 24; i++)
            {
                var id = sim.SpawnActor(new Float3(i * 2f, 0, 0), 0, (byte)(1 + i % 2));
                Assert.True(sim.TryGetWeapon(id, 0, out var primary));
                Assert.True(sim.TryGetPrimarySight(id, out var sight));
                Assert.True(SightCatalog.IsAllowed(primary.DefinitionId, sight), primary.DefinitionId + " " + sight);
            }
        }

        [Fact]
        public void Default_loadout_harness_keeps_the_legacy_rifle_without_a_sight()
        {
            var sim = new CombatHarnessSimulation(new StaticPlaneWorld());
            var id = sim.SpawnActor(default, 0, 1);
            Assert.True(sim.TryGetPrimarySight(id, out var sight));
            Assert.False(sight.IsValid);
        }

        [Fact]
        public void Pinned_armory_loadout_is_issued_on_spawn_and_every_reset()
        {
            var sim = new CombatHarnessSimulation(new StaticPlaneWorld()) { RandomLoadouts = true };
            var lifecycle = new HarnessActorLifecycle(sim);
            var human = new ParticipantId(1);
            var loadout = PlayerLoadout.Create(Ak47, SightCatalog.Collimator, Eagle);
            lifecycle.PinLoadout(human, loadout);
            var entity = lifecycle.SpawnLife(human, new LifeId(1), default, 0, 1);
            AssertCarries(sim, entity, loadout);
            Assert.True(lifecycle.TryGetActorState(entity, out var state));
            Assert.Equal(Ak47, state.ActiveWeaponDefinition);
            lifecycle.ResetLife(entity, new Float3(3, 0, 0), 0);
            AssertCarries(sim, entity, loadout);
            // A new life (respawn) of the same participant carries the same loadout.
            lifecycle.DespawnLife(entity);
            var next = lifecycle.SpawnLife(human, new LifeId(2), default, 0, 1);
            AssertCarries(sim, next, loadout);
            // Bots (other participants) keep drawing from the random pool.
            var bot = lifecycle.SpawnLife(new ParticipantId(2), new LifeId(1), new Float3(5, 0, 0), 0, 2);
            Assert.True(sim.TryGetWeapon(bot, 0, out var botPrimary) && botPrimary.IsValid);
            Assert.True(sim.TryGetPrimarySight(bot, out var botSight) && SightCatalog.IsAllowed(botPrimary.DefinitionId, botSight));
        }

        [Fact]
        public void Picked_up_primary_keeps_its_sight_and_drops_keep_theirs()
        {
            var sim = new CombatHarnessSimulation(new StaticPlaneWorld());
            var id = sim.SpawnActor(default, 0, 1);
            Assert.True(sim.ReplacePrimary(id, Mk14, 10, 20, SightCatalog.Collimator));
            Assert.True(sim.TryGetPrimarySight(id, out var sight));
            Assert.Equal(SightCatalog.Collimator, sight);
            // A disallowed sight resolves to the gun's factory sight; the legacy overload uses it too.
            Assert.True(sim.ReplacePrimary(id, Mk14, 10, 20, SightCatalog.Scope8x));
            Assert.True(sim.TryGetPrimarySight(id, out sight));
            Assert.Equal(SightCatalog.Scope4x, sight);
            Assert.True(sim.ReplacePrimary(id, M16, 10, 20));
            Assert.True(sim.TryGetPrimarySight(id, out sight));
            Assert.Equal(SightCatalog.Prism2x, sight);

            var field = new DroppedWeaponField();
            Assert.True(field.Drop(Ak47, 30, 60, default, 0, SightCatalog.Prism2x));
            Assert.True(field.Drop(M16, 30, 60, default, 0));
            Assert.True(field.TryGet(0, out var ak));
            Assert.Equal(SightCatalog.Prism2x, ak.SightId);
            Assert.True(field.TryGet(1, out var m16));
            Assert.Equal(SightCatalog.Prism2x, m16.SightId);
        }

        [Fact]
        public void Sight_catalog_rows_cover_only_spawning_primaries()
        {
            var primaries = WeaponCatalog.All.Where(e => e.Family == WeaponFamily.PrimaryGun && e.Definition.SpawnWeight > 0)
                .Select(e => e.Definition.Id).ToArray();
            Assert.Equal(primaries.OrderBy(x => x.ToString()), SightCatalog.All.Select(o => o.WeaponId).OrderBy(x => x.ToString()));
        }

        private static void AssertCarries(CombatHarnessSimulation sim, EntityId entity, PlayerLoadout loadout)
        {
            Assert.True(sim.TryGetWeapon(entity, 0, out var primary));
            Assert.True(sim.TryGetWeapon(entity, 1, out var sidearm));
            Assert.True(sim.TryGetPrimarySight(entity, out var sight));
            Assert.Equal(loadout.Primary, primary.DefinitionId);
            Assert.Equal(loadout.Sidearm, sidearm.DefinitionId);
            Assert.Equal(loadout.PrimarySight, sight);
        }
    }
}
