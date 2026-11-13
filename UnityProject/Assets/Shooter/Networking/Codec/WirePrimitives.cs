#nullable enable
using System;
using System.Buffers.Binary;
using System.Text;
using Shooter.Contracts;
using Shooter.Networking.Transport;

namespace Shooter.Networking.Codec
{
    public enum WireMessageKind : byte
    {
        Handshake = 1,
        HandshakeReject = 2,
        CommandBatch = 3,
        Snapshot = 4,
        SnapshotAck = 5,
        PredictionState = 6,
        ResyncRequest = 7,
        Ready = 8
    }

    public sealed class WireWriter
    {
        private readonly byte[] buffer;
        private int offset;
        public WireWriter(int capacity = 1200)
        {
            if (capacity <= 0 || capacity > 1200) throw new ArgumentOutOfRangeException(nameof(capacity));
            buffer = new byte[capacity];
        }
        public int Length => offset;
        public ReadOnlyMemory<byte> Memory => buffer.AsMemory(0, offset);
        public void Reset() => offset = 0;
        public void WriteByte(byte value) { Ensure(1); buffer[offset++] = value; }
        public void WriteUInt16(ushort value) { Ensure(2); BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), value); offset += 2; }
        public void WriteUInt32(uint value) { Ensure(4); BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), value); offset += 4; }
        public void WriteUInt64(ulong value) { Ensure(8); BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(offset), value); offset += 8; }
        public void WriteInt32(int value) { Ensure(4); BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), value); offset += 4; }
        public void WriteFloat(float value)
        {
            Ensure(4);
            int bits = BitConverter.SingleToInt32Bits(value);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), bits);
            offset += 4;
        }
        public void WriteFloat3(Float3 value) { WriteFloat(value.X); WriteFloat(value.Y); WriteFloat(value.Z); }
        public void WriteBytes(ReadOnlySpan<byte> data)
        {
            Ensure(data.Length);
            data.CopyTo(buffer.AsSpan(offset));
            offset += data.Length;
        }
        public void WriteString(string value, int maxChars = 64)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length > maxChars) throw new ArgumentOutOfRangeException(nameof(value));
            var utf8 = Encoding.UTF8.GetBytes(value);
            if (utf8.Length > 255) throw new ArgumentOutOfRangeException(nameof(value));
            WriteByte((byte)utf8.Length);
            WriteBytes(utf8);
        }
        public void WriteContentId(ContentId id) => WriteBytes(id.ToBytes());
        public void WriteHeader(WireMessageKind kind)
        {
            WriteString(NetworkTransportSelection.WireProtocolVersion, 16);
            WriteByte((byte)kind);
        }
        private void Ensure(int needed)
        {
            if (offset + needed > buffer.Length) throw new InvalidOperationException("Wire payload exceeds 1200-byte bound");
        }
    }

    public ref struct WireReader
    {
        private ReadOnlySpan<byte> span;
        private int offset;
        public WireReader(ReadOnlySpan<byte> span) { this.span = span; offset = 0; }
        public int Remaining => span.Length - offset;
        public byte ReadByte() { Ensure(1); return span[offset++]; }
        public ushort ReadUInt16() { Ensure(2); ushort v = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset)); offset += 2; return v; }
        public uint ReadUInt32() { Ensure(4); uint v = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset)); offset += 4; return v; }
        public ulong ReadUInt64() { Ensure(8); ulong v = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset)); offset += 8; return v; }
        public int ReadInt32() { Ensure(4); int v = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset)); offset += 4; return v; }
        public float ReadFloat()
        {
            Ensure(4);
            int bits = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
            offset += 4;
            float v = BitConverter.Int32BitsToSingle(bits);
            if (float.IsNaN(v) || float.IsInfinity(v)) throw new ArgumentException("nonfinite wire float");
            return v;
        }
        public Float3 ReadFloat3() => new Float3(ReadFloat(), ReadFloat(), ReadFloat());
        public string ReadString(int maxChars = 64)
        {
            int len = ReadByte();
            Ensure(len);
            var s = Encoding.UTF8.GetString(span.Slice(offset, len));
            offset += len;
            if (s.Length > maxChars) throw new ArgumentException("wire string");
            return s;
        }
        public ContentId ReadContentId()
        {
            Ensure(32);
            var id = new ContentId(span.Slice(offset, 32));
            offset += 32;
            return id;
        }
        public WireMessageKind ReadHeader()
        {
            string version = ReadString(16);
            if (version != NetworkTransportSelection.WireProtocolVersion)
                throw new ArgumentException("UnsupportedVersion");
            return (WireMessageKind)ReadByte();
        }
        private void Ensure(int needed)
        {
            if (needed < 0 || offset + needed > span.Length) throw new ArgumentException("truncated wire payload");
        }
    }
}
