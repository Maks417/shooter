#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>Deterministic-format v1 identities. Schema validation precedes source hashing.</summary>
    public static class ContentIdentity
    {
        public static ulong ParseSeed(string value)
        {
            if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed) ||
                seed.ToString(CultureInfo.InvariantCulture) != value)
                throw new ArgumentException("Expected canonical uint64 decimal seed.", nameof(value));
            return seed;
        }

        /// <summary>Normalizes the JSON subset; caller must first validate the recipe's schema and units.</summary>
        public static ContentId SourceHash(string schemaId, ReadOnlySpan<byte> schemaValidJson)
        {
            byte[] canonical = CanonicalJson.Encode(schemaValidJson);
            using var bytes = new MemoryStream();
            using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
            WriteText(writer, schemaId);
            WriteBytes(writer, canonical);
            return Hash(bytes.ToArray());
        }

        public static ContentId ContentKey(ContentId sourceHash, string generatorId, string generatorVersion,
            ulong seed, IReadOnlyList<ContentId> dependencies, string targetPlatform, string visualProfile,
            uint formatVersion, ContentId toolchainHash)
        {
            if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));
            if (dependencies.Count > 256 || formatVersion == 0) throw new ArgumentException("Invalid content key limits.");
            var sorted = new ContentId[dependencies.Count];
            for (int i = 0; i < sorted.Length; i++) sorted[i] = dependencies[i];
            Array.Sort(sorted);
            for (int i = 1; i < sorted.Length; i++)
                if (sorted[i] == sorted[i - 1]) throw new ArgumentException("Duplicate dependency.", nameof(dependencies));
            using var bytes = new MemoryStream();
            using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
            WriteText(writer, "shooter.asset.v1");
            WriteBytes(writer, sourceHash.ToBytes());
            WriteText(writer, generatorId);
            WriteText(writer, generatorVersion);
            writer.Write(seed);
            writer.Write((uint)sorted.Length);
            foreach (var dependency in sorted) WriteBytes(writer, dependency.ToBytes());
            WriteText(writer, targetPlatform);
            WriteText(writer, visualProfile);
            writer.Write(formatVersion);
            WriteBytes(writer, toolchainHash.ToBytes());
            return Hash(bytes.ToArray());
        }

        public static ContentId GameplayKey(ContentId sourceHash, string generatorId, string generatorVersion,
            ulong seed, IReadOnlyList<ContentId> dependencies, string targetPlatform, uint formatVersion, ContentId toolchainHash)
            => ContentKey(sourceHash, generatorId, generatorVersion, seed, dependencies, targetPlatform, "gameplay", formatVersion, toolchainHash);

        public static string UnityGuid(string logicalAssetId)
        {
            using var bytes = new MemoryStream();
            using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
            WriteText(writer, "shooter.unity-guid.v1");
            WriteText(writer, logicalAssetId);
            return Hash(bytes.ToArray()).ToString().Substring(0, 32);
        }

        public static ContentId Hash(ReadOnlySpan<byte> payload)
        {
            using var sha = SHA256.Create();
            return new ContentId(sha.ComputeHash(payload.ToArray()));
        }

        private static void WriteText(BinaryWriter writer, string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length == 0 || value.Length > 4096) throw new ArgumentException("Identity text must contain 1..4096 ASCII characters.");
            foreach (char ch in value)
                if (ch < 0x20 || ch > 0x7e) throw new ArgumentException("Identity text must be printable ASCII.");
            WriteBytes(writer, Encoding.UTF8.GetBytes(value));
        }

        private static void WriteBytes(BinaryWriter writer, byte[] value)
        {
            writer.Write((uint)value.Length);
            writer.Write(value);
        }
    }
}
