#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// Two-stage native navigation publication seam. The locked Unity editor bakes ONLY navigation from the
    /// authoritative gameplay collision published by the pure .NET publisher and writes one stage entry per map
    /// (raw NVB1 bytes + a small JSON descriptor). The pure .NET publisher then publishes the final packages with
    /// these externally supplied bakes, so every non-navigation payload is produced by .NET exactly as in the software
    /// path (Mono float rounding never touches content bytes). Stage entries are bound to the exact collision input
    /// by <see cref="InputKey"/>; a bake for a different map or collision revision can never be injected.
    /// </summary>
    public static class NavBakeStage
    {
        public const string Schema = "shooter.nav-bake-stage.v1";
        public const string DescriptorSuffix = ".nav-stage.json";
        public const string BakeSuffix = ".nvb1";

        public sealed class Entry
        {
            public string MapId { get; }
            public string InputKey { get; }
            public ContentId CollisionSha256 { get; }
            public string Provenance { get; }
            public string ToolIdentity { get; }
            public bool RepeatIdentical { get; }
            public byte[] BakeBytes { get; }
            public UnityNavMeshBakeCodec.Bake Bake { get; }

            public Entry(string mapId, string inputKey, ContentId collisionSha256, string provenance, string toolIdentity, bool repeatIdentical, byte[] bakeBytes)
            {
                if (!MapRecipe.IsApprovedMapId(mapId)) throw new FormatException("stage-unknown-map");
                if (!IsHex64(inputKey)) throw new FormatException("stage-input-key-invalid");
                if (!NavBakePublication.IsNativeProvenance(provenance)) throw new FormatException("stage-provenance-not-native");
                if (string.IsNullOrWhiteSpace(toolIdentity)) throw new FormatException("stage-tool-identity-missing");
                if (!repeatIdentical) throw new FormatException("stage-bake-not-reproducible");
                if (bakeBytes == null || bakeBytes.Length == 0) throw new FormatException("stage-bake-empty");
                UnityNavMeshBakeCodec.Bake decoded;
                try { decoded = UnityNavMeshBakeCodec.Decode(bakeBytes); }
                catch (Exception error) when (error is FormatException || error is OverflowException || error is EndOfStreamException || error is ArgumentException)
                { throw new FormatException("stage-bake-malformed:" + error.Message); }
                if (decoded.Polygons.Count == 0) throw new FormatException("stage-bake-no-polygons");
                // Canonical bytes only: re-encoding the decoded bake must reproduce the staged bytes exactly.
                if (!BytesEqual(UnityNavMeshBakeCodec.Encode(decoded), bakeBytes)) throw new FormatException("stage-bake-not-canonical");
                MapId = mapId; InputKey = inputKey; CollisionSha256 = collisionSha256; Provenance = provenance;
                ToolIdentity = toolIdentity; RepeatIdentical = repeatIdentical; BakeBytes = (byte[])bakeBytes.Clone(); Bake = decoded;
            }
        }

        /// <summary>
        /// Stable key over the exact gameplay collision input and nav parameters (raw float bits, culture/runtime free).
        /// Identical on Mono (editor) and .NET/IL2CPP because only IEEE bit patterns are hashed.
        /// </summary>
        public static string InputKey(IReadOnlyList<CollisionBox> boxes, float halfExtent, float agentRadius, float agentHeight, float maxStep, float maxSlopeDegrees, float clearanceMargin)
        {
            var stream = new MemoryStream();
            var w = new BinaryWriter(stream);
            w.Write(new byte[] { (byte)'N', (byte)'B', (byte)'S', (byte)'1' });
            WriteBits(w, halfExtent); WriteBits(w, agentRadius); WriteBits(w, agentHeight);
            WriteBits(w, maxStep); WriteBits(w, maxSlopeDegrees); WriteBits(w, clearanceMargin);
            int gameplay = 0;
            for (int i = 0; i < boxes.Count; i++) if (boxes[i].Gameplay) gameplay++;
            w.Write(gameplay);
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (!b.Gameplay) continue;
                WriteBits(w, b.Center.X); WriteBits(w, b.Center.Y); WriteBits(w, b.Center.Z);
                WriteBits(w, b.Size.X); WriteBits(w, b.Size.Y); WriteBits(w, b.Size.Z);
            }
            w.Flush();
            return ContentIdentity.Hash(stream.ToArray()).ToString();
        }

        /// <summary>Gameplay boxes recovered from published (authoritative) collision data, in published order.</summary>
        public static IReadOnlyList<CollisionBox> BoxesFromCollision(CollisionData collision)
        {
            if (collision == null) throw new ArgumentNullException(nameof(collision));
            var boxes = new List<CollisionBox>(collision.Boxes.Count);
            for (int i = 0; i < collision.Boxes.Count; i++)
            {
                var tagged = collision.Boxes[i];
                // Navigation consumes only center/size of gameplay boxes; ids are placeholders (not hashed).
                boxes.Add(new CollisionBox((ulong)(i + 1), 1, tagged.SurfaceTag == 0 ? (ushort)1 : tagged.SurfaceTag, tagged.Shape.Center, tagged.Shape.Size, true));
            }
            return boxes;
        }

        public static byte[] EncodeDescriptor(Entry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            return MapDocumentJson.EncodeObject(new Dictionary<string, object?>
            {
                ["stageSchema"] = Schema,
                ["mapId"] = entry.MapId,
                ["inputKey"] = entry.InputKey,
                ["collisionSha256"] = entry.CollisionSha256.ToString(),
                ["navBakeProvenance"] = entry.Provenance,
                ["nativeToolIdentity"] = entry.ToolIdentity,
                ["repeatIdentical"] = entry.RepeatIdentical,
                ["bakeSha256"] = ContentIdentity.Hash(entry.BakeBytes).ToString(),
                ["bakeBytes"] = entry.BakeBytes.Length
            });
        }

        public static Entry Parse(byte[] descriptorBytes, byte[] bakeBytes)
        {
            if (descriptorBytes == null || bakeBytes == null) throw new ArgumentNullException("stage bytes");
            MapJsonValue root;
            try { root = MapJsonValue.Parse(descriptorBytes); }
            catch (Exception error) { throw new FormatException("stage-descriptor-malformed:" + error.Message); }
            try
            {
                var obj = root.Object;
                if (obj["stageSchema"].String != Schema) throw new FormatException("stage-schema-mismatch");
                var expectedBake = new ContentId(obj["bakeSha256"].String);
                if (ContentIdentity.Hash(bakeBytes) != expectedBake || obj["bakeBytes"].Int32 != bakeBytes.Length)
                    throw new FormatException("stage-bake-hash-mismatch");
                return new Entry(obj["mapId"].String, obj["inputKey"].String, new ContentId(obj["collisionSha256"].String),
                    obj["navBakeProvenance"].String, obj["nativeToolIdentity"].String, obj["repeatIdentical"].Boolean, bakeBytes);
            }
            catch (FormatException) { throw; }
            catch (Exception error) when (error is KeyNotFoundException || error is InvalidCastException || error is ArgumentException)
            { throw new FormatException("stage-descriptor-malformed:" + error.Message); }
        }

        public static void Write(string stageDirectory, Entry entry)
        {
            Directory.CreateDirectory(stageDirectory);
            File.WriteAllBytes(Path.Combine(stageDirectory, entry.MapId + BakeSuffix), entry.BakeBytes);
            File.WriteAllBytes(Path.Combine(stageDirectory, entry.MapId + DescriptorSuffix), EncodeDescriptor(entry));
        }

        public static IReadOnlyList<Entry> Load(string stageDirectory, IReadOnlyList<string> mapIds)
        {
            if (!Directory.Exists(stageDirectory)) throw new FormatException("stage-directory-missing");
            var entries = new List<Entry>(mapIds.Count);
            foreach (string mapId in mapIds)
            {
                string descriptor = Path.Combine(stageDirectory, mapId + DescriptorSuffix);
                string bake = Path.Combine(stageDirectory, mapId + BakeSuffix);
                if (!File.Exists(descriptor) || !File.Exists(bake)) throw new FormatException("stage-entry-missing:" + mapId);
                var entry = Parse(File.ReadAllBytes(descriptor), File.ReadAllBytes(bake));
                if (entry.MapId != mapId) throw new FormatException("stage-entry-map-mismatch:" + mapId);
                entries.Add(entry);
            }
            return entries;
        }

        /// <summary>
        /// Builds a <see cref="NavBakePublication.NativeProvider"/> that serves only staged bakes whose input key equals the
        /// collision handed to the builder. Any other input throws (never falls back to the software grid).
        /// </summary>
        public static Func<IReadOnlyList<CollisionBox>, float, float, float, float, float, float, NavBakePublication.NativeBake?> CreateProvider(IReadOnlyList<Entry> entries)
        {
            if (entries == null || entries.Count == 0) throw new ArgumentException("stage entries required", nameof(entries));
            var byKey = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (byKey.ContainsKey(entry.InputKey)) throw new ArgumentException("duplicate stage input key", nameof(entries));
                byKey.Add(entry.InputKey, entry);
            }
            return (boxes, half, radius, height, step, slope, margin) =>
            {
                string key = InputKey(boxes, half, radius, height, step, slope, margin);
                if (!byKey.TryGetValue(key, out var entry))
                    throw new InvalidOperationException("native-stage-has-no-bake-for-collision-input:" + key.Substring(0, 16));
                return new NavBakePublication.NativeBake(entry.Bake, entry.Provenance);
            };
        }

        private static void WriteBits(BinaryWriter w, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            w.Write(BitConverter.SingleToInt32Bits(value == 0f ? 0f : value));
        }

        private static bool IsHex64(string? s)
        {
            if (s == null || s.Length != 64) return false;
            foreach (char c in s) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
            return true;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
