using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shooter.Contracts;
using Xunit;

namespace Shooter.Contracts.Tests
{
    // Independent C# specification reproduction. Production generators are T005, after M0.
    internal sealed class ReferencePcg32
    {
        private ulong state;
        private readonly ulong increment;
        public ReferencePcg32(ulong initial, ulong sequence)
        { increment = unchecked((sequence << 1) | 1); Next(); state = unchecked(state + initial); Next(); }
        public uint Next()
        {
            ulong old = state; state = unchecked(old * 6364136223846793005UL + increment);
            uint x = (uint)(((old >> 18) ^ old) >> 27); int rotation = (int)(old >> 59);
            return (x >> rotation) | (x << ((-rotation) & 31));
        }
        public float Unit() => (Next() >> 8) * (1f / 16777216f);
        public uint Bounded(uint bound)
        {
            if (bound == 0) throw new ArgumentOutOfRangeException(nameof(bound)); uint threshold = unchecked(0u - bound) % bound;
            while (true) { uint r = Next(); if (r >= threshold) return r % bound; }
        }
    }
    internal static class ReferenceEncoding
    {
        public static byte[] Lp(byte[] value) { var result = new byte[4 + value.Length]; BinaryPrimitives.WriteUInt32LittleEndian(result, (uint)value.Length); value.CopyTo(result, 4); return result; }
        public static byte[] Lp(string value) => Lp(Encoding.UTF8.GetBytes(value));
        public static byte[] U64(ulong value) { var b = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(b, value); return b; }
        public static byte[] U32(uint value) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, value); return b; }
        public static byte[] Join(params byte[][] values) => values.SelectMany(v => v).ToArray();
        public static ContentId Hash(byte[] value) => new ContentId(SHA256.HashData(value));
        public static byte[] Canonical(JsonElement value) => Encoding.UTF8.GetBytes(CanonicalText(value));
        private static string String(string value)
        {
            var b = new StringBuilder("\"");
            foreach (char c in value)
            {
                if (c > 127) throw new ArgumentException("Non-ASCII fixture identifier");
                b.Append(c switch { '"' => "\\\"", '\\' => "\\\\", '\b' => "\\b", '\f' => "\\f", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t", _ when c < 32 => "\\u" + ((int)c).ToString("x4"), _ => c.ToString() });
            }
            return b.Append('"').ToString();
        }
        private static string CanonicalText(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    var properties = value.EnumerateObject().ToArray(); var keys = new HashSet<string>();
                    foreach (var p in properties) if (!keys.Add(p.Name)) throw new ArgumentException("Duplicate key");
                    return "{" + string.Join(",", properties.OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => String(p.Name) + ":" + CanonicalText(p.Value))) + "}";
                case JsonValueKind.Array: return "[" + string.Join(",", value.EnumerateArray().Select(CanonicalText)) + "]";
                case JsonValueKind.String: return String(value.GetString()!);
                case JsonValueKind.Number:
                    if (!value.TryGetInt32(out int integer)) throw new ArgumentException("Only int32 source numbers");
                    return integer.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case JsonValueKind.True: return "true";
                case JsonValueKind.False: return "false";
                case JsonValueKind.Null: return "null";
                default: throw new ArgumentException("Unsupported JSON value");
            }
        }
    }
    public sealed class DeterministicFixtureTests
    {
        [Fact]
        public void CSharpPcgReproducesIndependentPublishedBits()
        {
            using var fixture = FixtureJson.Load("golden-vectors.json"); var p = fixture.RootElement.GetProperty("pcg");
            var rng = new ReferencePcg32(ulong.Parse(p.GetProperty("initstate").GetString()!), ulong.Parse(p.GetProperty("initseq").GetString()!));
            foreach (var expected in p.GetProperty("outputsHex").EnumerateArray()) Assert.Equal(expected.GetString(), rng.Next().ToString("x8"));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.Bounded(0));
            for (int i = 0; i < 1000; i++) { float value = rng.Unit(); Assert.True(value >= 0 && value < 1); Assert.InRange(rng.Bounded(17), 0u, 16u); }
        }
        [Fact]
        public void CSharpCanonicalAndSourceHashesMatchUnmodifiedFixtures()
        {
            using var fixture = FixtureJson.Load("golden-vectors.json"); using var recipe = FixtureJson.Load("box-recipe.json"); var g = fixture.RootElement;
            byte[] canonical = ReferenceEncoding.Canonical(recipe.RootElement);
            Assert.Equal(g.GetProperty("canonicalUtf8").GetString(), Encoding.UTF8.GetString(canonical));
            Assert.Equal(g.GetProperty("canonicalSha256").GetString(), ReferenceEncoding.Hash(canonical).ToString());
            Assert.Equal(g.GetProperty("sourceHash").GetString(), ReferenceEncoding.Hash(ReferenceEncoding.Join(ReferenceEncoding.Lp(recipe.RootElement.GetProperty("schemaId").GetString()!), ReferenceEncoding.Lp(canonical))).ToString());
        }
        [Theory]
        [InlineData("{\"a\":1,\"a\":2}")] [InlineData("{\"a\":0.1}")] [InlineData("{\"a\":2147483648}")]
        public void CanonicalSourceRejectsAmbiguousOrOutOfRangeNumbers(string json)
        { using var doc = JsonDocument.Parse(json); Assert.Throws<ArgumentException>(() => ReferenceEncoding.Canonical(doc.RootElement)); }
        [Fact]
        public void NamedStreamAndLogicalGuidMatchIndependentBytes()
        {
            using var fixture = FixtureJson.Load("golden-vectors.json"); var g = fixture.RootElement; var s = g.GetProperty("namedStream");
            byte[] bytes = ReferenceEncoding.Join(ReferenceEncoding.Lp(s.GetProperty("domain").GetString()!), ReferenceEncoding.U64(ulong.Parse(s.GetProperty("rootSeed").GetString()!)),
                ReferenceEncoding.Lp(s.GetProperty("recipeId").GetString()!), ReferenceEncoding.Lp(s.GetProperty("featureKey").GetString()!));
            Assert.Equal(s.GetProperty("bytesHex").GetString(), Convert.ToHexString(bytes).ToLowerInvariant());
            byte[] digest = SHA256.HashData(bytes); Assert.Equal(s.GetProperty("sha256").GetString(), new ContentId(digest).ToString());
            Assert.Equal(s.GetProperty("initstate").GetString(), BinaryPrimitives.ReadUInt64LittleEndian(digest.AsSpan(0, 8)).ToString());
            Assert.Equal(s.GetProperty("initseq").GetString(), BinaryPrimitives.ReadUInt64LittleEndian(digest.AsSpan(8, 8)).ToString());
            string guid = ReferenceEncoding.Hash(ReferenceEncoding.Join(ReferenceEncoding.Lp("shooter.unity-guid.v1"), ReferenceEncoding.Lp(g.GetProperty("logicalAssetId").GetString()!))).ToString().Substring(0, 32);
            Assert.Equal(g.GetProperty("unityGuid").GetString(), guid);
        }
        [Fact]
        public void ContentRequestKeyAndActualFixturePayloadHashAreSeparate()
        {
            using var fixture = FixtureJson.Load("golden-vectors.json"); var g = fixture.RootElement; var k = g.GetProperty("contentKeyInputs");
            byte[] request = ReferenceEncoding.Join(ReferenceEncoding.Lp("shooter.asset.v1"), ReferenceEncoding.Lp(Convert.FromHexString(k.GetProperty("sourceHash").GetString()!)),
                ReferenceEncoding.Lp(k.GetProperty("generatorId").GetString()!), ReferenceEncoding.Lp(k.GetProperty("generatorVersion").GetString()!), ReferenceEncoding.U64(ulong.Parse(k.GetProperty("seed").GetString()!)),
                ReferenceEncoding.U32((uint)k.GetProperty("dependencies").GetArrayLength()),
                k.GetProperty("dependencies").EnumerateArray().Select(d => d.GetString()!).OrderBy(d => d, StringComparer.Ordinal).SelectMany(d => ReferenceEncoding.Lp(Convert.FromHexString(d))).ToArray(),
                ReferenceEncoding.Lp(k.GetProperty("targetPlatform").GetString()!), ReferenceEncoding.Lp(k.GetProperty("visualProfile").GetString()!), ReferenceEncoding.U32(k.GetProperty("formatVersion").GetUInt32()),
                ReferenceEncoding.Lp(Convert.FromHexString(k.GetProperty("toolchainHash").GetString()!)));
            Assert.Equal(g.GetProperty("contentId").GetString(), ReferenceEncoding.Hash(request).ToString());
            using var manifest = FixtureJson.Load("manifest.json"); var artifact = manifest.RootElement.GetProperty("artifacts")[0];
            byte[] payload = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", artifact.GetProperty("path").GetString()!));
            Assert.Equal(artifact.GetProperty("uncompressedBytes").GetInt32(), payload.Length); Assert.Equal(artifact.GetProperty("sha256").GetString(), ReferenceEncoding.Hash(payload).ToString());
        }
    }
}
