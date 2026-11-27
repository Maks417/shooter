#nullable enable
using System.Collections.Generic;
using System.Linq;
using Shooter.Contracts;
using Shooter.Simulation.Combat;
using Shooter.Simulation.Tests.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    /// <summary>ADR-130: the G17 kills a fresh, armored soldier in 5 torso hits up close and the Golden Eagle in 3.</summary>
    public sealed class SidearmLethalityTests
    {
        private static readonly DefinitionId M16 = new DefinitionId("weapons/m16");
        private static readonly DefinitionId G17 = new DefinitionId("weapons/g17");
        private static readonly DefinitionId Eagle = new DefinitionId("weapons/golden-eagle");

        [Theory]
        [InlineData("weapons/g17", 5f, 40)]
        [InlineData("weapons/g17", 10f, 40)]
        [InlineData("weapons/g17", 20f, 35)]
        [InlineData("weapons/g17", 30f, 30)]
        [InlineData("weapons/g17", 45f, 30)]
        [InlineData("weapons/golden-eagle", 15f, 80)]
        [InlineData("weapons/golden-eagle", 25f, 74)]
        [InlineData("weapons/golden-eagle", 40f, 64)]
        [InlineData("weapons/golden-eagle", 60f, 64)]
        public void Falloff_damage_per_shot(string gun, float meters, int expected)
        {
            var def = WeaponDefinition.FromId(new DefinitionId(gun));
            Assert.Equal(expected, DamageMath.FalloffDamage(def.BaseDamage, def.FalloffScale(meters)));
        }

        [Theory]
        [InlineData("weapons/g17", 10f, 2, 5)]
        [InlineData("weapons/g17", 20f, 2, 6)]
        [InlineData("weapons/g17", 30f, 2, 7)]
        [InlineData("weapons/g17", 10f, 3, 10)]
        [InlineData("weapons/golden-eagle", 15f, 2, 3)]
        [InlineData("weapons/golden-eagle", 35f, 2, 3)]
        [InlineData("weapons/golden-eagle", 40f, 2, 4)]
        [InlineData("weapons/golden-eagle", 15f, 3, 5)]
        public void Hits_to_kill_a_fresh_armored_soldier(string gun, float meters, ushort region, int expected)
        {
            var def = WeaponDefinition.FromId(new DefinitionId(gun));
            ushort amount = DamageMath.RoundDamage(DamageMath.FalloffDamage(def.BaseDamage, def.FalloffScale(meters)), region);
            ushort health = DamageMath.MaxHealth, armor = DamageMath.MaxArmor;
            int hits = 0;
            while (health > 0)
            {
                ushort toHealth = DamageMath.SplitArmor(amount, armor, out ushort armorTaken);
                armor -= armorTaken;
                DamageMath.Apply(health, toHealth, out health);
                hits++;
            }
            Assert.Equal(expected, hits);
        }

        [Theory]
        [InlineData("weapons/g17", 5)]
        [InlineData("weapons/golden-eagle", 3)]
        public void Aimed_torso_shots_kill_an_armored_soldier_at_six_meters(string gun, int expectedHits)
        {
            var sim = new CombatHarnessSimulation(new StaticPlaneWorld());
            var shooter = sim.SpawnActor(default, 0, 1);
            var target = sim.SpawnActor(new Float3(0, 0, 6f), 3.1f, 2);
            Assert.True(sim.PinLoadout(shooter, PlayerLoadout.Create(M16, SightCatalog.Irons, new DefinitionId(gun))));
            var damage = new List<DamagePayload>();
            int deaths = 0;
            // Sidearm selected and sights held throughout; one press every 30 ticks from tick 60 lets recoil recover.
            for (uint t = 0; t < 60 + 30 * 8; t++)
            {
                bool press = t >= 60 && (t - 60) % 30 == 0;
                var buttons = InputButtons.AimHeld | (press ? InputButtons.FireHeld | InputButtons.FirePressed : InputButtons.None);
                sim.Step(t, new[]
                {
                    new InputCommand(shooter, t, t + 1, 0, 0, 0, 0.075f, buttons, 1),
                    new InputCommand(target, t, t + 1, 0, 0, 3.1f, 0, InputButtons.None, 0)
                });
                using var ev = sim.AcquireEvents();
                for (int i = 0; i < ev.Events.Count; i++)
                {
                    if (ev.Events[i].Kind == CombatEventKind.DamageApplied && ev.Events[i].Target == target)
                        damage.Add((DamagePayload)ev.Events[i].Payload);
                    if (ev.Events[i].Kind == CombatEventKind.Death && ev.Events[i].Target == target) deaths++;
                }
                if (deaths > 0) break;
            }
            Assert.Equal(1, deaths);
            Assert.All(damage, d => Assert.Equal((ushort)2, d.RegionId));
            Assert.Equal(expectedHits, damage.Count);
            Assert.Equal((ushort)0, damage.Last().RemainingHealth);
        }

        [Fact]
        public void Only_the_g17_and_golden_eagle_are_drawn_as_sidearms()
        {
            var drawn = WeaponCatalog.All
                .Where(e => e.Family == WeaponFamily.Sidearm && e.Definition.SpawnWeight > 0)
                .Select(e => e.Definition.Id).ToHashSet();
            Assert.Equal(new HashSet<DefinitionId> { G17, Eagle }, drawn);
        }
    }
}
