#nullable enable
using System;
using System.IO;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation
{
    internal ref struct BodyReader
    {
        private ReadOnlySpan<byte> bytes;
        private int position;
        public int Remaining => bytes.Length - position;
        public BodyReader(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length > ArtifactCodec.MaxBodyBytes) throw new FormatException("Artifact body exceeds limit.");
            this.bytes = bytes; position = 0;
        }
        public void Need(long count)
        { if (count < 0 || count > Remaining) throw new FormatException("Truncated or oversized artifact field."); }
        public ReadOnlySpan<byte> Bytes(int count) { Need(count); var value = bytes.Slice(position, count); position += count; return value; }
        public ushort U16() { var b = Bytes(2); return (ushort)(b[0] | b[1] << 8); }
        public uint U32() { var b = Bytes(4); return (uint)b[0] | (uint)b[1] << 8 | (uint)b[2] << 16 | (uint)b[3] << 24; }
        public int Count(int max, bool nonzero = false)
        { uint n = U32(); if (n > max || nonzero && n == 0) throw new FormatException("Artifact count outside bounds."); return (int)n; }
        public float F32()
        {
            float value = BitConverter.Int32BitsToSingle(unchecked((int)U32()));
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new FormatException("Nonfinite artifact scalar.");
            return value;
        }
        public Float2 V2() => new Float2(F32(), F32());
        public Float3 V3() => new Float3(F32(), F32(), F32());
        public Float4 V4() => new Float4(F32(), F32(), F32(), F32());
        public Quat Q() => new Quat(F32(), F32(), F32(), F32());
        public string Text()
        {
            int count = Count(4096, true); var b = Bytes(count); var chars = new char[count];
            for (int i = 0; i < count; i++) { if (b[i] < 32 || b[i] > 126) throw new FormatException("Non-ASCII artifact text."); chars[i] = (char)b[i]; }
            return new string(chars);
        }
        public void End() { if (Remaining != 0) throw new FormatException("Trailing artifact data."); }
    }
    internal static class BodyWriting
    {
        public static void V2(BinaryWriter w, Float2 v) { w.Write(v.X); w.Write(v.Y); }
        public static void V3(BinaryWriter w, Float3 v) { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); }
        public static void V4(BinaryWriter w, Float4 v) { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); w.Write(v.W); }
        public static void Q(BinaryWriter w, Quat v) { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); w.Write(v.W); }
        public static void Text(BinaryWriter w, string value)
        {
            if (value == null || value.Length < 1 || value.Length > 4096) throw new ArgumentException("Artifact text length.");
            foreach (char c in value) if (c < 32 || c > 126) throw new ArgumentException("Artifact text must be printable ASCII.");
            byte[] b = Encoding.ASCII.GetBytes(value); w.Write((uint)b.Length); w.Write(b);
        }
        public static void Size(long bytes)
        { if (bytes < 0 || bytes > ArtifactCodec.MaxBodyBytes) throw new ArgumentException("Artifact body exceeds limit."); }
        public static byte[] Finish(MemoryStream stream) { Size(stream.Length); return stream.ToArray(); }
        public static void Valid(bool valid, string diagnostic) { if (!valid) throw new FormatException(diagnostic); }
    }
}
