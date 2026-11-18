#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Simulation.Combat
{
    /// <summary>What pressing the pickup key on a dropped weapon does (ADR-105).</summary>
    public enum WeaponPickupKind : byte
    {
        None = 0,
        /// <summary>A different primary: it replaces the holder's primary, which drops in its place.</summary>
        Swap = 1,
        /// <summary>The same primary: its magazine + reserve tops up the holder's reserve, capped at capacity.</summary>
        Ammo = 2
    }

    /// <summary>One primary weapon lying on the floor (ADR-105).</summary>
    public readonly struct DroppedWeapon
    {
        public int Id { get; }
        public DefinitionId DefinitionId { get; }
        public ushort Magazine { get; }
        public ushort Reserve { get; }
        public Float3 Position { get; }
        public uint DroppedActiveTick { get; }
        public uint ExpiresActiveTick { get; }
        /// <summary>ADR-107: the sight mounted on the dropped gun; a pickup keeps it.</summary>
        public DefinitionId SightId { get; }

        public DroppedWeapon(int id, DefinitionId definitionId, ushort magazine, ushort reserve, Float3 position, uint droppedActiveTick, uint expiresActiveTick,
            DefinitionId sightId = default)
        {
            Id = id; DefinitionId = definitionId; Magazine = magazine; Reserve = reserve; Position = position;
            DroppedActiveTick = droppedActiveTick; ExpiresActiveTick = expiresActiveTick; SightId = sightId;
        }

        public bool IsValid => DefinitionId.IsValid;
    }

    /// <summary>
    /// ADR-105: bounded authority-side list of dropped primary weapons. Lifetime is counted in active match
    /// ticks so pause and the pre-match countdown never run the clock. Pure and deterministic; the session owns
    /// when entries are added, picked up and expired. Presentation reads entries and uses the same
    /// <see cref="TryFindPickup"/> query so the HUD prompt always matches what the pickup key will do.
    /// </summary>
    public sealed class DroppedWeaponField
    {
        public const int Capacity = 32;
        public const uint LifetimeActiveTicks = 15 * 60;
        public const float PickupHorizontalRadius = 1.75f;
        public const float PickupVerticalReach = 1.5f;

        private readonly DroppedWeapon[] entries = new DroppedWeapon[Capacity];
        private int count;
        private int nextId = 1;

        public int Count => count;

        public bool TryGet(int index, out DroppedWeapon weapon)
        {
            if ((uint)index >= (uint)count) { weapon = default; return false; }
            weapon = entries[index];
            return true;
        }

        public void Clear()
        {
            Array.Clear(entries, 0, entries.Length);
            count = 0;
        }

        /// <summary>
        /// Drops a primary at <paramref name="position"/>. Only primaries are accepted; empty guns are not dropped.
        /// When the field is full the entry closest to expiry is replaced. Returns false when nothing was dropped.
        /// </summary>
        public bool Drop(DefinitionId definitionId, ushort magazine, ushort reserve, Float3 position, uint activeTick,
            DefinitionId sightId = default)
        {
            if (!definitionId.IsValid) return false;
            if (!WeaponCatalog.TryGet(definitionId, out var entry) || entry.Family != WeaponFamily.PrimaryGun) return false;
            if (magazine == 0 && reserve == 0) return false;
            var weapon = new DroppedWeapon(nextId++, definitionId, magazine, reserve, position, activeTick,
                unchecked(activeTick + LifetimeActiveTicks), SightCatalog.Resolve(definitionId, sightId));
            if (count < Capacity)
            {
                entries[count++] = weapon;
                return true;
            }
            int oldest = 0;
            for (int i = 1; i < count; i++)
                if (entries[i].ExpiresActiveTick < entries[oldest].ExpiresActiveTick) oldest = i;
            RemoveAt(oldest);
            entries[count++] = weapon;
            return true;
        }

        /// <summary>Removes every entry whose lifetime has run out at <paramref name="activeTick"/>.</summary>
        public int Expire(uint activeTick)
        {
            int removed = 0;
            for (int i = count - 1; i >= 0; i--)
            {
                if (activeTick < entries[i].ExpiresActiveTick) continue;
                RemoveAt(i);
                removed++;
            }
            return removed;
        }

        public bool RemoveById(int id)
        {
            for (int i = 0; i < count; i++)
            {
                if (entries[i].Id != id) continue;
                RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Nearest dropped weapon within reach of <paramref name="feet"/> that the holder of
        /// <paramref name="heldPrimary"/> can take. A same-id gun is an ammo top-up and is skipped when the
        /// held reserve is already full; any other primary is a swap.
        /// </summary>
        public bool TryFindPickup(Float3 feet, WeaponState heldPrimary, out DroppedWeapon weapon, out WeaponPickupKind kind)
        {
            weapon = default;
            kind = WeaponPickupKind.None;
            double best = double.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var candidate = entries[i];
                double dx = candidate.Position.X - feet.X;
                double dz = candidate.Position.Z - feet.Z;
                double dy = candidate.Position.Y - feet.Y;
                double horizontal = dx * dx + dz * dz;
                if (horizontal > PickupHorizontalRadius * PickupHorizontalRadius) continue;
                if (Math.Abs(dy) > PickupVerticalReach) continue;
                var candidateKind = KindFor(heldPrimary, candidate);
                if (candidateKind == WeaponPickupKind.None) continue;
                if (horizontal >= best) continue;
                best = horizontal;
                weapon = candidate;
                kind = candidateKind;
            }
            return kind != WeaponPickupKind.None;
        }

        public static WeaponPickupKind KindFor(WeaponState heldPrimary, DroppedWeapon dropped)
        {
            if (!dropped.IsValid) return WeaponPickupKind.None;
            if (!heldPrimary.IsValid || !heldPrimary.DefinitionId.Equals(dropped.DefinitionId))
                return WeaponPickupKind.Swap;
            return ToppedUpReserve(heldPrimary, dropped) > heldPrimary.Reserve ? WeaponPickupKind.Ammo : WeaponPickupKind.None;
        }

        /// <summary>Held reserve plus the dropped magazine and reserve, capped at the weapon's reserve capacity.</summary>
        public static ushort ToppedUpReserve(WeaponState heldPrimary, DroppedWeapon dropped)
        {
            var def = WeaponDefinition.FromId(heldPrimary.DefinitionId);
            int total = heldPrimary.Reserve + dropped.Magazine + dropped.Reserve;
            int cap = Math.Max(def.ReserveCapacity, (int)heldPrimary.Reserve);
            return (ushort)Math.Min(total, cap);
        }

        private void RemoveAt(int index)
        {
            for (int i = index; i < count - 1; i++) entries[i] = entries[i + 1];
            entries[--count] = default;
        }
    }
}
