#nullable enable
using System;

namespace Shooter.Game.Contracts
{
    public readonly struct ParticipantId : IEquatable<ParticipantId>, IComparable<ParticipantId>
    {
        public uint Value { get; }
        public bool IsValid => Value != 0;
        public ParticipantId(uint value)
        {
            GameRequire.That(value != 0, nameof(value));
            Value = value;
        }
        public bool Equals(ParticipantId other) => Value == other.Value;
        public override bool Equals(object? other) => other is ParticipantId id && Equals(id);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(ParticipantId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(ParticipantId left, ParticipantId right) => left.Equals(right);
        public static bool operator !=(ParticipantId left, ParticipantId right) => !left.Equals(right);
    }

    public readonly struct LifeId : IEquatable<LifeId>, IComparable<LifeId>
    {
        public uint Value { get; }
        public bool IsValid => Value != 0;
        public LifeId(uint value)
        {
            GameRequire.That(value != 0, nameof(value));
            Value = value;
        }
        public bool Equals(LifeId other) => Value == other.Value;
        public override bool Equals(object? other) => other is LifeId id && Equals(id);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(LifeId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(LifeId left, LifeId right) => left.Equals(right);
        public static bool operator !=(LifeId left, LifeId right) => !left.Equals(right);
    }

    public readonly struct MapId : IEquatable<MapId>
    {
        public string Value { get; }
        public MapId(string value) { Value = GameRequire.AsciiId(value, nameof(value), GameContractVersion.MaxMapId); }
        public bool Equals(MapId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? other) => other is MapId id && Equals(id);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(MapId left, MapId right) => left.Equals(right);
        public static bool operator !=(MapId left, MapId right) => !left.Equals(right);
    }

    public readonly struct ModeId : IEquatable<ModeId>
    {
        public string Value { get; }
        public ModeId(string value) { Value = GameRequire.AsciiId(value, nameof(value), GameContractVersion.MaxMapId); }
        public bool Equals(ModeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? other) => other is ModeId id && Equals(id);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(ModeId left, ModeId right) => left.Equals(right);
        public static bool operator !=(ModeId left, ModeId right) => !left.Equals(right);
    }

    public static class WellKnownMaps
    {
        public static MapId TrainingCompound { get; } = new MapId("training-compound");
        public static MapId DepotYard { get; } = new MapId("depot-yard");
        public static MapId OilDepot { get; } = new MapId("oil-depot");
    }

    public static class WellKnownModes
    {
        public static ModeId TeamDeathmatch { get; } = new ModeId("team-deathmatch");
    }
}
