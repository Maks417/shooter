using Shooter.Contracts;
using Shooter.Simulation.Combat;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    /// <summary>ADR-105: dropped primary lifetime, reach, swap/ammo selection and capacity.</summary>
    public sealed class DroppedWeaponFieldTests
    {
        private static readonly DefinitionId Rifle = WeaponLoadoutPolicy.DefaultPrimaryId;
        private static readonly DefinitionId Carbine = WeaponLoadoutPolicy.CarbinePrimaryId;

        private static WeaponState Held(DefinitionId id, ushort mag, ushort reserve) =>
            new WeaponState(id, WeaponAction.Ready, mag, reserve, 0, 0, 0, 0, false);

        [Fact]
        public void LifetimeIsFifteenSecondsOfActiveTicks()
        {
            Assert.Equal(900u, DroppedWeaponField.LifetimeActiveTicks);
            var field = new DroppedWeaponField();
            Assert.True(field.Drop(Rifle, 12, 60, new Float3(0, 0, 0), 100));
            Assert.Equal(0, field.Expire(999));
            Assert.Equal(1, field.Count);
            Assert.Equal(1, field.Expire(1000));
            Assert.Equal(0, field.Count);
        }

        [Fact]
        public void OnlyPrimariesWithAmmoDrop()
        {
            var field = new DroppedWeaponField();
            Assert.False(field.Drop(WeaponLoadoutPolicy.PistolId, 10, 10, default, 0));
            Assert.False(field.Drop(WeaponLoadoutPolicy.KnifeId, 1, 0, default, 0));
            Assert.False(field.Drop(WeaponLoadoutPolicy.GrenadeId, 1, 0, default, 0));
            Assert.False(field.Drop(Rifle, 0, 0, default, 0));
            Assert.Equal(0, field.Count);
            Assert.True(field.Drop(Rifle, 0, 1, default, 0));
        }

        [Fact]
        public void DifferentGunIsSwapSameGunIsAmmoAndNearestWins()
        {
            var field = new DroppedWeaponField();
            field.Drop(Rifle, 10, 20, new Float3(1.2f, 0, 0), 0);
            field.Drop(Carbine, 5, 5, new Float3(0.5f, 0, 0), 0);
            var feet = new Float3(0, 0, 0);

            Assert.True(field.TryFindPickup(feet, Held(Rifle, 30, 0), out var nearest, out var kind));
            Assert.Equal(Carbine, nearest.DefinitionId);
            Assert.Equal(WeaponPickupKind.Swap, kind);

            Assert.True(field.TryFindPickup(new Float3(1.4f, 0, 0), Held(Rifle, 30, 0), out var same, out kind));
            Assert.Equal(Rifle, same.DefinitionId);
            Assert.Equal(WeaponPickupKind.Ammo, kind);
        }

        [Fact]
        public void OutOfReachHorizontallyOrVerticallyFindsNothing()
        {
            var field = new DroppedWeaponField();
            field.Drop(Carbine, 5, 5, new Float3(0, 0, 0), 0);
            Assert.False(field.TryFindPickup(new Float3(1.8f, 0, 0), Held(Rifle, 30, 0), out _, out _));
            Assert.False(field.TryFindPickup(new Float3(0, 1.6f, 0), Held(Rifle, 30, 0), out _, out _));
            Assert.True(field.TryFindPickup(new Float3(1.7f, 1.4f, 0), Held(Rifle, 30, 0), out _, out _));
        }

        [Fact]
        public void AmmoTopUpIsCappedAndSkippedWhenReserveIsFull()
        {
            var def = WeaponDefinition.FromId(Rifle);
            var field = new DroppedWeaponField();
            field.Drop(Rifle, 20, 50, default, 0);
            Assert.True(field.TryGet(0, out var dropped));

            var low = Held(Rifle, 30, 10);
            Assert.Equal((ushort)System.Math.Min((int)def.ReserveCapacity, 10 + 20 + 50), DroppedWeaponField.ToppedUpReserve(low, dropped));

            var nearlyFull = Held(Rifle, 30, (ushort)(def.ReserveCapacity - 5));
            Assert.Equal(def.ReserveCapacity, DroppedWeaponField.ToppedUpReserve(nearlyFull, dropped));

            var full = Held(Rifle, 30, def.ReserveCapacity);
            Assert.Equal(WeaponPickupKind.None, DroppedWeaponField.KindFor(full, dropped));
            Assert.False(field.TryFindPickup(default, full, out _, out _));
        }

        [Fact]
        public void FullFieldReplacesTheEntryClosestToExpiry()
        {
            var field = new DroppedWeaponField();
            for (int i = 0; i < DroppedWeaponField.Capacity; i++)
                Assert.True(field.Drop(Rifle, 1, 0, new Float3(i, 0, 0), (uint)(10 + i)));
            Assert.True(field.Drop(Carbine, 1, 0, new Float3(-5, 0, 0), 500));
            Assert.Equal(DroppedWeaponField.Capacity, field.Count);
            for (int i = 0; i < field.Count; i++)
            {
                Assert.True(field.TryGet(i, out var w));
                Assert.NotEqual(10u + DroppedWeaponField.LifetimeActiveTicks, w.ExpiresActiveTick);
            }
        }

        [Fact]
        public void RemoveByIdAndClear()
        {
            var field = new DroppedWeaponField();
            field.Drop(Rifle, 1, 0, default, 0);
            field.Drop(Carbine, 1, 0, default, 0);
            Assert.True(field.TryGet(0, out var first));
            Assert.True(field.RemoveById(first.Id));
            Assert.False(field.RemoveById(first.Id));
            Assert.Equal(1, field.Count);
            field.Clear();
            Assert.Equal(0, field.Count);
        }
    }
}
