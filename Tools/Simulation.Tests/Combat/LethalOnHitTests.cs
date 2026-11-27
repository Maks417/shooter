#nullable enable
using System.Collections.Generic;
using System.Linq;
using Shooter.Contracts;
using Shooter.Simulation.Combat;
using Shooter.Simulation.Tests.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    /// <summary>ADR-125: the bolt-action snipers kill with any hit, whatever the region, range or armor.</summary>
    public sealed class LethalOnHitTests
    {
        private static readonly DefinitionId M24 = new DefinitionId("weapons/m24");
        private static readonly DefinitionId Mtac = new DefinitionId("weapons/mtac338");

        [Fact]
        public void Only_the_bolt_action_snipers_are_lethal_on_hit()
        {
            var lethal = WeaponCatalog.All.Where(e => e.Definition.LethalOnHit).Select(e => e.Definition.Id).ToHashSet();
            Assert.Equal(new HashSet<DefinitionId> { M24, Mtac }, lethal);
        }

        [Fact]
        public void Modifiers_keep_the_flag()
        {
            var perks = new WeaponModifiers(850, 850, 850, 850, 1, 850, 850, 850, 1050);
            Assert.True(perks.Apply(WeaponDefinition.FromId(M24)).LethalOnHit);
            Assert.True(perks.Apply(WeaponDefinition.FromId(Mtac)).LethalOnHit);
            Assert.False(perks.Apply(WeaponDefinition.Rifle).LethalOnHit);
        }

        [Theory]
        [InlineData("weapons/m24", 4f)]
        [InlineData("weapons/m24", 200f)]
        [InlineData("weapons/mtac338", 4f)]
        [InlineData("weapons/mtac338", 200f)]
        public void Any_hit_kills_an_armored_target_at_any_range(string gun, float meters)
        {
            // Sweep the aim from head to feet; every hit, torso and limbs included, takes all 100 health and no armor.
            var regions = new HashSet<ushort>();
            float eyeDrop = 1.9f / meters;
            for (float pitch = 0f; pitch <= eyeDrop; pitch += eyeDrop / 24f)
            foreach (float yaw in new[] { 0f, 0.1f / meters, -0.1f / meters })
            {
                var sim = new CombatHarnessSimulation(new StaticPlaneWorld());
                var shooter = sim.SpawnActor(default, 0, 1);
                var target = sim.SpawnActor(new Float3(0, 0, meters), 3.1f, 2);
                Assert.True(sim.ReplacePrimary(shooter, new DefinitionId(gun), 5, 25));
                var damage = new List<DamagePayload>();
                bool died = false;
                for (uint t = 0; t < 61; t++)
                {
                    var buttons = InputButtons.AimHeld | (t == 60 ? InputButtons.FireHeld | InputButtons.FirePressed : InputButtons.None);
                    sim.Step(t, new[]
                    {
                        new InputCommand(shooter, t, t + 1, 0, 0, yaw, pitch, buttons, 0),
                        new InputCommand(target, t, t + 1, 0, 0, 3.1f, 0, InputButtons.None, 0)
                    });
                    using var ev = sim.AcquireEvents();
                    for (int i = 0; i < ev.Events.Count; i++)
                    {
                        if (ev.Events[i].Kind == CombatEventKind.DamageApplied) damage.Add((DamagePayload)ev.Events[i].Payload);
                        if (ev.Events[i].Kind == CombatEventKind.Death && ev.Events[i].Target == target) died = true;
                    }
                }
                if (damage.Count == 0) continue;
                var hit = Assert.Single(damage);
                Assert.Equal(DamageMath.MaxHealth, hit.Amount);
                Assert.Equal((ushort)0, hit.RemainingHealth);
                Assert.True(died);
                Assert.True(sim.TryGetArmor(target, out ushort armor));
                Assert.Equal(DamageMath.MaxArmor, armor);
                regions.Add(hit.RegionId);
            }
            Assert.Contains((ushort)2, regions);
            Assert.Contains(regions, r => r >= 3 && r <= 6);
        }
    }
}
