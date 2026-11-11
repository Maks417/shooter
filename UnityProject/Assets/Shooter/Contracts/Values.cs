#nullable enable
using System;
using System.Globalization;

namespace Shooter.Contracts
{
    public enum QueryStatus { NoHit = 0, Hit = 1, BufferOverflow = 2, InvalidInput = 3 }
    public enum Stance { Standing = 0, Crouching = 1, Dead = 2 }
    public enum WeaponAction { Ready = 0, FiringRecovery = 1, Reloading = 2, Equipping = 3, Disabled = 4 }
    public enum ContentKind { Mesh = 1, Material = 2, Skeleton = 3, Collision = 4, Navigation = 5 }
    public enum PathStatus { Pending = 0, Complete = 1, Partial = 2, Failed = 3, Stale = 4, Cancelled = 5 }
    public enum MatchState { Loading = 0, Ready = 1, Playing = 2, Won = 3, Lost = 4, Stopped = 5 }
    public enum CombatEventKind { ShotAccepted = 1, ProjectileSpawned = 2, Impact = 3, DamageApplied = 4, ReloadStarted = 5, ReloadCompleted = 6, Death = 7, MatchChanged = 8 }
    public enum ErrorCode { None = 0, InvalidInput = 1, UnsupportedVersion = 2, MissingContent = 3, CorruptContent = 4, LimitExceeded = 5, Cancelled = 6, StaleRevision = 7, Busy = 8, InternalError = 9 }
    [Flags]
    public enum InputButtons : ushort { None = 0, FireHeld = 1, FirePressed = 2, ReloadPressed = 4, SprintHeld = 8, CrouchHeld = 16, JumpPressed = 32, AimHeld = 64 }
    public enum Delivery { UnreliableSequenced = 0, ReliableOrdered = 1 }

    public readonly struct Float2
    {
        public float X { get; }
        public float Y { get; }
        public Float2(float x, float y) { X = Require.Finite(x, nameof(x)); Y = Require.Finite(y, nameof(y)); }
    }
    public readonly struct Float3
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public Float3(float x, float y, float z) { X = Require.Finite(x, nameof(x)); Y = Require.Finite(y, nameof(y)); Z = Require.Finite(z, nameof(z)); }
        public double LengthSquared => (double)X * X + (double)Y * Y + (double)Z * Z;
        public bool IsUnit => Math.Abs(Math.Sqrt(LengthSquared) - 1) <= 1e-4;
    }
    public readonly struct Float4
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float W { get; }
        public Float4(float x, float y, float z, float w) { X = Require.Finite(x, nameof(x)); Y = Require.Finite(y, nameof(y)); Z = Require.Finite(z, nameof(z)); W = Require.Finite(w, nameof(w)); }
    }
    public readonly struct Quat
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float W { get; }
        public static Quat Identity => new Quat(0, 0, 0, 1);
        public bool IsValid => Math.Abs(Math.Sqrt((double)X * X + (double)Y * Y + (double)Z * Z + (double)W * W) - 1) <= 1e-4;
        public Quat(float x, float y, float z, float w)
        {
            X = Require.Finite(x, nameof(x)); Y = Require.Finite(y, nameof(y)); Z = Require.Finite(z, nameof(z)); W = Require.Finite(w, nameof(w));
            Require.That(IsValid, "quaternion normalization");
        }
    }
    public readonly struct UInt16x4
    {
        public ushort X { get; }
        public ushort Y { get; }
        public ushort Z { get; }
        public ushort W { get; }
        public UInt16x4(ushort x, ushort y, ushort z, ushort w) { X = x; Y = y; Z = z; W = w; }
    }
    public readonly struct Aabb
    {
        public Float3 Min { get; }
        public Float3 Max { get; }
        public Aabb(Float3 min, Float3 max)
        {
            Require.That(min.X <= max.X && min.Y <= max.Y && min.Z <= max.Z, "bounds");
            Min = min; Max = max;
        }
    }

    public readonly struct ContentId : IEquatable<ContentId>, IComparable<ContentId>
    {
        private readonly ulong a, b, c, d;
        public ContentId(string hex)
        {
            Require.That(hex != null && hex.Length == 64, nameof(hex));
            foreach (char ch in hex) Require.That(ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f', nameof(hex));
            a = ulong.Parse(hex.Substring(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            b = ulong.Parse(hex.Substring(16, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            c = ulong.Parse(hex.Substring(32, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            d = ulong.Parse(hex.Substring(48, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        public ContentId(ReadOnlySpan<byte> bytes) : this(ToHex(bytes)) { }
        private static string ToHex(ReadOnlySpan<byte> bytes)
        {
            Require.That(bytes.Length == 32, nameof(bytes));
            var chars = new char[64]; const string hex = "0123456789abcdef";
            for (int i = 0; i < 32; i++) { chars[2 * i] = hex[bytes[i] >> 4]; chars[2 * i + 1] = hex[bytes[i] & 15]; }
            return new string(chars);
        }
        public byte[] ToBytes()
        {
            var bytes = new byte[32]; string hex = ToString();
            for (int i = 0; i < 32; i++) bytes[i] = byte.Parse(hex.Substring(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return bytes;
        }
        public override string ToString() => a.ToString("x16") + b.ToString("x16") + c.ToString("x16") + d.ToString("x16");
        public bool Equals(ContentId other) => a == other.a && b == other.b && c == other.c && d == other.d;
        public override bool Equals(object? other) => other is ContentId id && Equals(id);
        public override int GetHashCode() => a.GetHashCode() ^ b.GetHashCode() ^ c.GetHashCode() ^ d.GetHashCode();
        public int CompareTo(ContentId other) => string.CompareOrdinal(ToString(), other.ToString());
        public static bool operator ==(ContentId left, ContentId right) => left.Equals(right);
        public static bool operator !=(ContentId left, ContentId right) => !left.Equals(right);
    }
    public readonly struct DefinitionId : IEquatable<DefinitionId>
    {
        private readonly string? value;
        public bool IsValid => value != null;
        public DefinitionId(string value)
        {
            Require.Text(value, nameof(value));
            Require.That(value[0] != '/' && value[value.Length - 1] != '/' && !value.Contains("//"), nameof(value));
            foreach (char ch in value) Require.That(ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9' || ch == '/' || ch == '-' || ch == '_' || ch == '.', nameof(value));
            foreach (string part in value.Split('/')) Require.That(part != "." && part != "..", nameof(value));
            this.value = value;
        }
        public override string ToString() => value ?? "";
        public bool Equals(DefinitionId other) => value == other.value;
        public override bool Equals(object? other) => other is DefinitionId id && Equals(id);
        public override int GetHashCode() => value?.GetHashCode() ?? 0;
    }
    public readonly struct EventId : IEquatable<EventId>
    {
        public ulong AuthoritySessionId { get; }
        public uint Tick { get; }
        public EntityId EntityId { get; }
        public ushort Ordinal { get; }
        public bool IsEmpty => AuthoritySessionId == 0 && Tick == 0 && !EntityId.IsValid && Ordinal == 0;
        public EventId(ulong authoritySessionId, uint tick, EntityId entityId, ushort ordinal)
        {
            Require.That(authoritySessionId != 0 && entityId.IsValid, "event identity");
            AuthoritySessionId = authoritySessionId; Tick = tick; EntityId = entityId; Ordinal = ordinal;
        }
        public EventId NextOrdinal()
        {
            if (Ordinal == ushort.MaxValue) throw new OverflowException("Event ordinal exhausted");
            return new EventId(AuthoritySessionId, Tick, EntityId, (ushort)(Ordinal + 1));
        }
        public bool Equals(EventId other) => AuthoritySessionId == other.AuthoritySessionId && Tick == other.Tick && EntityId.Equals(other.EntityId) && Ordinal == other.Ordinal;
        public override bool Equals(object? other) => other is EventId id && Equals(id);
        public override int GetHashCode() => AuthoritySessionId.GetHashCode() ^ Tick.GetHashCode() ^ EntityId.GetHashCode() ^ Ordinal;
    }
}
