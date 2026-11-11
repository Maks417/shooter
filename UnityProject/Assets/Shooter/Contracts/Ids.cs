#nullable enable
using System;

namespace Shooter.Contracts
{
    public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        public ulong Value { get; }
        public bool IsValid => Value != 0;
        public EntityId(ulong value)
        {
            Require.That(value != 0, nameof(value));
            Value = value;
        }
        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object? other) => other is EntityId id && Equals(id);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(EntityId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);
        public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
    }
    public readonly struct SurfaceId : IEquatable<SurfaceId>, IComparable<SurfaceId>
    {
        public ulong Value { get; }
        public bool IsValid => Value != 0;
        public SurfaceId(ulong value)
        {
            Require.That(value != 0, nameof(value));
            Value = value;
        }
        public bool Equals(SurfaceId other) => Value == other.Value;
        public override bool Equals(object? other) => other is SurfaceId id && Equals(id);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(SurfaceId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(SurfaceId left, SurfaceId right) => left.Equals(right);
        public static bool operator !=(SurfaceId left, SurfaceId right) => !left.Equals(right);
    }
    public readonly struct PeerId : IEquatable<PeerId>, IComparable<PeerId>
    {
        public ulong Value { get; }
        public bool IsValid => Value != 0;
        public PeerId(ulong value)
        {
            Require.That(value != 0, nameof(value));
            Value = value;
        }
        public bool Equals(PeerId other) => Value == other.Value;
        public override bool Equals(object? other) => other is PeerId id && Equals(id);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(PeerId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(PeerId left, PeerId right) => left.Equals(right);
        public static bool operator !=(PeerId left, PeerId right) => !left.Equals(right);
    }
    public readonly struct NavTicketId : IEquatable<NavTicketId>, IComparable<NavTicketId>
    {
        public ulong Value { get; }
        public bool IsValid => Value != 0;
        public NavTicketId(ulong value)
        {
            Require.That(value != 0, nameof(value));
            Value = value;
        }
        public bool Equals(NavTicketId other) => Value == other.Value;
        public override bool Equals(object? other) => other is NavTicketId id && Equals(id);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(NavTicketId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(NavTicketId left, NavTicketId right) => left.Equals(right);
        public static bool operator !=(NavTicketId left, NavTicketId right) => !left.Equals(right);
    }
}
