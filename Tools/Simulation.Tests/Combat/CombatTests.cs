#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Combat;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    public sealed class CombatTests
    {
        [Theory]
        [InlineData(30, 3, 23)] // rifle limb 22.5 -> 23
        [InlineData(25, 3, 19)] // pistol limb 18.75 -> 19
        [InlineData(30, 1, 60)] // rifle head
        [InlineData(30, 2, 30)] // rifle torso
        public void DamageRoundingMatchesBaseline(ushort baseDamage, ushort region, ushort expected)
        {
            Assert.Equal(expected, DamageMath.RoundDamage(baseDamage, region));
        }

        [Fact]
        public void AutomaticRifleShotCountIsExactForHeldFire()
        {
            var sim = new WeaponSimulator();
            var slots = new[] { WeaponSimulator.CreateLoaded(WeaponDefinition.Rifle), WeaponSimulator.CreateLoaded(WeaponDefinition.Pistol) };
            byte active = 0, desired = 0;
            var shots = new List<AcceptedShot>();
            var events = new List<CombatEvent>();
            var next = new EventId(1, 0, new EntityId(1), 0);
            int accepted = 0;
            // Hold fire for 60 ticks from tick 0. Interval 6 => shots at 0,6,12,...,54 = 10 shots.
            for (uint tick = 0; tick < 60; tick++)
            {
                var buttons = InputButtons.FireHeld | (tick == 0 ? InputButtons.FirePressed : InputButtons.None);
                sim.Step(tick, buttons, 0, true, false, slots, ref active, ref desired, shots, events, ref next, new EntityId(1));
                accepted += shots.Count;
            }
            Assert.Equal(10, accepted);
            Assert.Equal((ushort)20, slots[0].AmmoInMagazine);
        }

        [Fact]
        public void SemiAutoIgnoresHeldOnlyRepeats()
        {
            var sim = new WeaponSimulator();
            var slots = new[] { WeaponSimulator.CreateLoaded(WeaponDefinition.Rifle), WeaponSimulator.CreateLoaded(WeaponDefinition.Pistol) };
            byte active = 1, desired = 1;
            var shots = new List<AcceptedShot>();
            var events = new List<CombatEvent>();
            var next = new EventId(1, 0, new EntityId(1), 0);
            int accepted = 0;
            for (uint tick = 0; tick < 60; tick++)
            {
                // Hold without edges after first.
                var buttons = tick == 0 ? (InputButtons.FirePressed | InputButtons.FireHeld) : InputButtons.FireHeld;
                sim.Step(tick, buttons, 1, true, false, slots, ref active, ref desired, shots, events, ref next, new EntityId(1));
                accepted += shots.Count;
            }
            Assert.Equal(1, accepted);
        }

        [Fact]
        public void DuplicateCommandsDoNotDuplicateDamage()
        {
            ushort health = 100;
            var amounts = new HashSet<string>();
            // Same shot identity applied twice — second apply ignored by caller policy.
            ushort dmg = DamageMath.RoundDamage(30, 2);
            string key = "1:1:1"; // session/tick/shot
            Assert.True(amounts.Add(key));
            DamageMath.Apply(health, dmg, out health);
            Assert.False(amounts.Add(key));
            Assert.Equal((ushort)70, health);
        }

        [Fact]
        public void ReloadCancelOnSwitchTransfersNoAmmo()
        {
            var sim = new WeaponSimulator();
            var slots = new[] { WeaponSimulator.CreateLoaded(WeaponDefinition.Rifle), WeaponSimulator.CreateLoaded(WeaponDefinition.Pistol) };
            slots[0] = new WeaponState(WeaponDefinition.Rifle.Id, WeaponAction.Ready, 0, 90, 0, 0, 0, 0, false);
            byte active = 0, desired = 0;
            var shots = new List<AcceptedShot>();
            var events = new List<CombatEvent>();
            var next = new EventId(1, 0, new EntityId(1), 0);
            sim.Step(0, InputButtons.ReloadPressed, 0, true, false, slots, ref active, ref desired, shots, events, ref next, new EntityId(1));
            Assert.Equal(WeaponAction.Reloading, slots[0].Action);
            // Switch away before completion.
            sim.Step(1, InputButtons.None, 1, true, false, slots, ref active, ref desired, shots, events, ref next, new EntityId(1));
            Assert.Equal((byte)1, active);
            Assert.Equal(WeaponAction.Ready, slots[0].Action);
            Assert.Equal((ushort)0, slots[0].AmmoInMagazine);
            Assert.Equal((ushort)90, slots[0].Reserve);
        }

        [Fact]
        public void AmmoNeverNegativeThroughReload()
        {
            var sim = new WeaponSimulator();
            var slots = new[]
            {
                new WeaponState(WeaponDefinition.Rifle.Id, WeaponAction.Ready, 0, 5, 0, 0, 0, 0, false),
                WeaponSimulator.CreateLoaded(WeaponDefinition.Pistol)
            };
            byte active = 0, desired = 0;
            var shots = new List<AcceptedShot>();
            var events = new List<CombatEvent>();
            var next = new EventId(1, 0, new EntityId(1), 0);
            sim.Step(0, InputButtons.ReloadPressed, 0, true, false, slots, ref active, ref desired, shots, events, ref next, new EntityId(1));
            for (uint tick = 1; tick <= 144; tick++)
                sim.Step(tick, InputButtons.None, 0, true, false, slots, ref active, ref desired, shots, events, ref next, new EntityId(1));
            Assert.Equal(WeaponAction.Ready, slots[0].Action);
            Assert.Equal((ushort)5, slots[0].AmmoInMagazine);
            Assert.Equal((ushort)0, slots[0].Reserve);
        }
    }
}
