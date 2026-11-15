#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Combat;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    public sealed class KnifeGrenadeTests
    {
        private static void Step(
            WeaponSimulator sim, WeaponState[] slots, ref byte active, ref byte desired,
            uint tick, InputButtons buttons, byte slot,
            List<AcceptedShot> shots, List<AcceptedGrenade> grenades, List<CombatEvent> events, ref EventId next)
        {
            sim.Step(tick, buttons, slot, true, false, slots, ref active, ref desired, shots, grenades, events, ref next, new EntityId(1));
        }

        [Fact]
        public void KnifeNeverConsumesAmmoAndFiresOnPress()
        {
            var sim = new WeaponSimulator();
            var slots = WeaponSimulator.CreateDefaultLoadout();
            byte active = 2, desired = 2;
            var shots = new List<AcceptedShot>();
            var grenades = new List<AcceptedGrenade>();
            var events = new List<CombatEvent>();
            var next = new EventId(1, 0, new EntityId(1), 0);
            // Equip knife (12 ticks).
            for (uint t = 0; t < 12; t++)
                Step(sim, slots, ref active, ref desired, t, InputButtons.None, 2, shots, grenades, events, ref next);
            Assert.Equal(WeaponAction.Ready, slots[2].Action);
            Step(sim, slots, ref active, ref desired, 12, InputButtons.FirePressed | InputButtons.FireHeld, 2, shots, grenades, events, ref next);
            Assert.Single(shots);
            Assert.Equal(FireMode.Melee, shots[0].Definition.FireMode);
            Assert.Equal((ushort)1, slots[2].AmmoInMagazine);
        }

        [Fact]
        public void GrenadeCooksThrowsOnceAndThenIsDry()
        {
            var sim = new WeaponSimulator();
            var slots = WeaponSimulator.CreateDefaultLoadout();
            byte active = 3, desired = 3;
            var shots = new List<AcceptedShot>();
            var grenades = new List<AcceptedGrenade>();
            var events = new List<CombatEvent>();
            var next = new EventId(1, 0, new EntityId(1), 0);
            for (uint t = 0; t < 18; t++)
                Step(sim, slots, ref active, ref desired, t, InputButtons.None, 3, shots, grenades, events, ref next);
            Step(sim, slots, ref active, ref desired, 18, InputButtons.FirePressed | InputButtons.FireHeld, 3, shots, grenades, events, ref next);
            Assert.Equal(WeaponAction.Cooking, slots[3].Action);
            Assert.Equal((ushort)1, slots[3].AmmoInMagazine);
            Step(sim, slots, ref active, ref desired, 30, InputButtons.None, 3, shots, grenades, events, ref next);
            Assert.Single(grenades);
            Assert.False(grenades[0].DetonateInHand);
            Assert.Equal(108u, grenades[0].RemainingFuseTicks); // 120 - (30-18)
            Assert.Equal((ushort)0, slots[3].AmmoInMagazine);
            grenades.Clear();
            Step(sim, slots, ref active, ref desired, 31, InputButtons.FirePressed | InputButtons.FireHeld, 3, shots, grenades, events, ref next);
            Assert.Empty(grenades);
            Assert.Equal(WeaponAction.Ready, slots[3].Action);
        }

        [Fact]
        public void GrenadeHeldThroughFuseDetonatesInHand()
        {
            var sim = new WeaponSimulator();
            var slots = WeaponSimulator.CreateDefaultLoadout();
            byte active = 3, desired = 3;
            var shots = new List<AcceptedShot>();
            var grenades = new List<AcceptedGrenade>();
            var events = new List<CombatEvent>();
            var next = new EventId(1, 0, new EntityId(1), 0);
            for (uint t = 0; t < 18; t++)
                Step(sim, slots, ref active, ref desired, t, InputButtons.None, 3, shots, grenades, events, ref next);
            Step(sim, slots, ref active, ref desired, 18, InputButtons.FirePressed | InputButtons.FireHeld, 3, shots, grenades, events, ref next);
            for (uint t = 19; t < 138; t++)
                Step(sim, slots, ref active, ref desired, t, InputButtons.FireHeld, 3, shots, grenades, events, ref next);
            Assert.Empty(grenades);
            Step(sim, slots, ref active, ref desired, 138, InputButtons.FireHeld, 3, shots, grenades, events, ref next);
            Assert.Single(grenades);
            Assert.True(grenades[0].DetonateInHand);
            Assert.Equal((ushort)0, slots[3].AmmoInMagazine);
        }

        [Fact]
        public void BlastDamageFallsOffAndIsZeroOutsideRadius()
        {
            var def = WeaponDefinition.Grenade;
            Assert.Equal((ushort)100, GrenadeSimulator.BlastDamage(def, 0f));
            Assert.Equal((ushort)50, GrenadeSimulator.BlastDamage(def, 2.5f));
            Assert.Equal((ushort)0, GrenadeSimulator.BlastDamage(def, 5f));
            Assert.Equal((ushort)0, GrenadeSimulator.BlastDamage(def, 6f));
        }
    }
}
