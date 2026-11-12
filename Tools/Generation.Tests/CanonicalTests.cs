#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shooter.Generation;
using Xunit;

namespace Shooter.Generation.Tests
{
    public sealed class CanonicalTests
    {
        private static byte[] Encode(string source) => CanonicalJson.Encode(Encoding.UTF8.GetBytes(source));
        private static string Canonical(string source) => Encoding.UTF8.GetString(Encode(source));

        [Fact]
        public void RecipeMatchesIndependentGoldenBytesAndDigest()
        {
            using var golden = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/golden-vectors.json")));
            byte[] source = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/box-recipe.json"));
            byte[] result = CanonicalJson.Encode(source);
            Assert.Equal(golden.RootElement.GetProperty("canonicalUtf8").GetString(), Encoding.UTF8.GetString(result));
            Assert.Equal(golden.RootElement.GetProperty("canonicalSha256").GetString(), Convert.ToHexString(SHA256.HashData(result)).ToLowerInvariant());
            Assert.Equal(result, CanonicalJson.Encode(result));
        }

        [Fact]
        public void SortsDecodedKeysOrdinallyAndPreservesArrays()
        {
            Assert.Equal("{\"A\":null,\"a\":[false,true,-2147483648,2147483647],\"z\":{\"a\":0,\"b\":2}}",
                Canonical(" { \"z\": {\"b\":2,\"a\":-0}, \"a\":[false,true,-2147483648,2147483647],\"\\u0041\":null } \r\n"));
        }

        [Fact]
        public void CanonicalEscapesDecodeAliasesAndKeepSlashLiteral()
        {
            Assert.Equal("\"\\\"\\\\/\\b\\f\\n\\r\\t\\u0000\\u001f\"", Canonical("\"\\u0022\\u005c\\/\\u0008\\f\\n\\r\\t\\u0000\\u001F\""));
            Assert.Equal("\"abc\"", Canonical("\"\\u0061bc\""));
            Assert.Equal("\"\u007f\"", Canonical("\"\\u007f\""));
        }

        [Theory]
        [InlineData("{\"a\":1,\"a\":2}")]
        [InlineData("{\"a\":1,\"\\u0061\":2}")]
        [InlineData("{\"outer\":{\"x\":null,\"x\":null}}")]
        [InlineData("{\"\\n\":1,\"\\u000a\":2}")]
        public void RejectsDuplicateDecodedKeys(string source) => Assert.Throws<FormatException>(() => Encode(source));

        [Theory]
        [InlineData("1.0")]
        [InlineData("1e0")]
        [InlineData("1E+0")]
        [InlineData("2147483648")]
        [InlineData("-2147483649")]
        [InlineData("18446744073709551615")]
        [InlineData("01")]
        [InlineData("-01")]
        [InlineData("+1")]
        [InlineData("-")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        public void RejectsNonInt32Numbers(string source) => Assert.Throws<FormatException>(() => Encode(source));

        [Theory]
        [InlineData("")]
        [InlineData("\ufeff{}")]
        [InlineData("[1,]")]
        [InlineData("{\"a\":1,}")]
        [InlineData("[1 2]")]
        [InlineData("{}[]")]
        [InlineData("trueX")]
        [InlineData("\"\\q\"")]
        [InlineData("\"\\u000\"")]
        [InlineData("\"\\u00xz\"")]
        [InlineData("\"open")]
        [InlineData("\"a\nb\"")]
        [InlineData("\"é\"")]
        [InlineData("{\"é\":0}")]
        [InlineData("\"\\u00e9\"")]
        [InlineData("\"\\ud800\"")]
        [InlineData("\"\\ud83d\\ude00\"")]
        public void RejectsMalformedAndNonAsciiSubset(string source) => Assert.Throws<FormatException>(() => Encode(source));

        [Theory]
        [InlineData(new byte[] { 0x22, 0xc0, 0xaf, 0x22 })]
        [InlineData(new byte[] { 0x22, 0xff, 0x22 })]
        [InlineData(new byte[] { 0x22, 0xed, 0xa0, 0x80, 0x22 })]
        public void RejectsMalformedUtf8(byte[] source) => Assert.Throws<FormatException>(() => CanonicalJson.Encode(source));

        [Fact]
        public void EnforcesInputDepthAndNodeBounds()
        {
            Assert.Throws<ArgumentException>(() => CanonicalJson.Encode(new byte[CanonicalJson.MaxBytes + 1]));
            string boundary = new string('[', CanonicalJson.MaxDepth) + "0" + new string(']', CanonicalJson.MaxDepth);
            Assert.Equal(boundary, Canonical(boundary));
            Assert.Throws<FormatException>(() => Encode("[" + boundary + "]"));
            string nodes = "[" + string.Join(",", Enumerable.Repeat("0", CanonicalJson.MaxNodes - 1)) + "]";
            Assert.Equal(nodes, Canonical(nodes));
            Assert.Throws<FormatException>(() => Encode(nodes.Insert(nodes.Length - 1, ",0")));
            string maxString = "\"" + new string('a', CanonicalJson.MaxBytes - 2) + "\"";
            Assert.Equal(maxString, Canonical(maxString));
        }

        [Fact]
        public void CultureDoesNotChangeNumbersOrOrdering()
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Assert.Equal("{\"I\":-12,\"i\":34}", Canonical("{\"i\":34,\"I\":-12}"));
            }
            finally { CultureInfo.CurrentCulture = original; }
        }

        [Fact]
        public void Uint64AndSchemaValidationRemainExplicitCallerResponsibilities()
        {
            Assert.Equal("\"18446744073709551615\"", Canonical("\"18446744073709551615\""));
            // Encoding cannot infer whether a string is a seed, an ID or arbitrary schema data.
            Assert.Equal("{\"seed\":\"01\",\"widthMm\":-1}", Canonical("{\"widthMm\":-1,\"seed\":\"01\"}"));
        }
    }
}
