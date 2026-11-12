#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>Immutable stream identity; creating/consuming another feature never advances this stream.</summary>
    public sealed class NamedRandomStream
    {
        public ContentId StreamId { get; }
        public ulong InitState { get; }
        public ulong InitSequence { get; }

        private NamedRandomStream(byte[] digest)
        {
            StreamId = new ContentId(digest);
            InitState = ReadUInt64(digest, 0);
            InitSequence = ReadUInt64(digest, 8);
        }

        public Pcg32 CreateGenerator() => new Pcg32(InitState, InitSequence);

        public static NamedRandomStream Derive(ulong rootSeed, string recipeId, string featureKey)
        {
            ValidateKey(recipeId, nameof(recipeId));
            ValidateKey(featureKey, nameof(featureKey));
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
            {
                WriteString(writer, "shooter.stream.v1");
                writer.Write(rootSeed); // BinaryWriter integers are little-endian.
                WriteString(writer, recipeId);
                WriteString(writer, featureKey);
            }
            using var sha = SHA256.Create();
            return new NamedRandomStream(sha.ComputeHash(bytes.ToArray()));
        }

        private static void ValidateKey(string key, string parameter)
        {
            if (key == null) throw new ArgumentNullException(parameter);
            if (key.Length == 0 || key.Length > 4096) throw new ArgumentException("Stream keys must contain 1..4096 ASCII bytes.", parameter);
            foreach (char c in key)
                if (c < 32 || c > 126) throw new ArgumentException("Stream keys must be printable ASCII.", parameter);
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            writer.Write((uint)utf8.Length);
            writer.Write(utf8);
        }

        private static ulong ReadUInt64(byte[] bytes, int offset)
        {
            ulong value = 0;
            for (int i = 0; i < 8; i++) value |= (ulong)bytes[offset + i] << (i * 8);
            return value;
        }
    }
}
