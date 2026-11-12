using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;

public sealed class RecipeTests
{
    private static byte[] Bytes(string source) => Encoding.UTF8.GetBytes(source);
    private static string Source => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/box-recipe.json"));
    private static string DiagnosticSource => Source.Replace("shooter.recipe.box.v1", BoxRecipe.CollisionSchema).Replace("geometry.box", "collision.box");

    [Fact]
    public void JsonTreeUsesImmutableTypedViewsAndCanonicalEscapeDecoding()
    {
        var tree = JsonValue.Parse(Bytes("{\"string\":\"a\\n\\u0001\\\"\\\\\\/\",\"number\":-2147483648,\"values\":[true,false,null,{},[]]}"));
        Assert.Equal("a\n\u0001\"\\/", tree.Object["string"].String);
        Assert.Equal(int.MinValue, tree.Object["number"].Number);
        var array = tree.Object["values"].Array;
        Assert.True(array[0].Boolean); Assert.False(array[1].Boolean); Assert.True(array[2].IsNull);
        Assert.Empty(array[3].Object); Assert.Empty(array[4].Array);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, JsonValue>)tree.Object).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<JsonValue>)array).Clear());
        Assert.Throws<FormatException>(() => tree.String);
        Assert.Throws<FormatException>(() => tree.Number);
        Assert.Throws<FormatException>(() => tree.Boolean);
        Assert.Throws<FormatException>(() => tree.Array);
        Assert.Throws<FormatException>(() => array[0].Object);
    }

    [Theory]
    [InlineData("{\"a\":1,\"\\u0061\":2}")]
    [InlineData("[1,]")]
    [InlineData("1.0")]
    [InlineData("2147483648")]
    public void JsonTreeRetainsStrictCanonicalGuards(string source) => Assert.Throws<FormatException>(() => JsonValue.Parse(Bytes(source)));

    [Fact]
    public void FixtureRecipeReproducesIndependentSourceHashAndCanonicalParameters()
    {
        BoxRecipe recipe = BoxRecipe.Parse(Bytes(Source));
        Assert.Equal("b42ba515ac2e46f825cef3ca2cbc0f79ca05563068f76a806af270660b6a1570", recipe.SourceHash.ToString());
        Assert.Equal(1000, recipe.WidthMm); Assert.Equal(1000, recipe.HeightMm); Assert.Equal(1000, recipe.DepthMm); Assert.Equal(0, recipe.BevelMm);
        Assert.Equal("{\"bevelMm\":0,\"depthMm\":1000,\"heightMm\":1000,\"widthMm\":1000}", Encoding.UTF8.GetString(recipe.Document.Parameters));
        Assert.True(new BoxRecipeValidator().Validate(recipe.Document).Valid);
        byte[] copy = recipe.CanonicalSource; copy[0] = 0;
        Assert.Equal((byte)'{', recipe.CanonicalSource[0]);
    }

    [Fact]
    public void RejectsIndependentNegativeDimensionFixture()
    {
        byte[] invalid = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/invalid-box-recipe.json"));
        var failure = Assert.Throws<FormatException>(() => BoxRecipe.Parse(invalid));
        Assert.Contains("parameters.widthMm", failure.Message);
    }

    [Theory]
    [InlineData("\"widthMm\": 1000", "\"widthMm\": 0")]
    [InlineData("\"heightMm\": 1000", "\"heightMm\": 100001")]
    [InlineData("\"depthMm\": 1000", "\"depthMm\": true")]
    [InlineData("\"bevelMm\": 0", "\"bevelMm\": -1")]
    [InlineData("460072030", "18446744073709551616")]
    [InlineData("460072030", "01")]
    [InlineData("fixtures/box", "Fixtures/Box")]
    [InlineData("shooter.recipe.box.v1", "shooter.recipe.unknown.v1")]
    [InlineData("geometry.box", "collision.box")]
    [InlineData("1.0.0", "2.0.0")]
    public void RejectsInvalidSchemaIdentityAndNumericBounds(string oldValue, string newValue)
    {
        string source = Source;
        Assert.Contains(oldValue, source);
        Assert.Throws<FormatException>(() => BoxRecipe.Parse(Bytes(source.Replace(oldValue, newValue))));
    }

    [Fact]
    public void RejectsUnknownRootAndParameterFieldsAndWrongRoot()
    {
        Assert.Throws<FormatException>(() => BoxRecipe.Parse(Bytes(Source.Replace("\"seed\"", "\"unknown\""))));
        Assert.Throws<FormatException>(() => BoxRecipe.Parse(Bytes(Source.Replace("\"bevelMm\"", "\"unknown\""))));
        Assert.Throws<FormatException>(() => BoxRecipe.Parse(Bytes("[]")));
    }

    [Fact]
    public void DependencyHashesMustBeSortedUniqueBoundedAndLowercase()
    {
        string first = new string('1', 64), second = new string('2', 64);
        string Make(string deps) => Source.Replace("\"dependencies\": []", "\"dependencies\": " + deps);
        var recipe = BoxRecipe.Parse(Bytes(Make("[\"" + first + "\",\"" + second + "\"]")));
        Assert.Equal(2, recipe.Document.Dependencies.Count);
        Assert.Throws<FormatException>(() => BoxRecipe.Parse(Bytes(Make("[\"" + second + "\",\"" + first + "\"]"))));
        Assert.Throws<FormatException>(() => BoxRecipe.Parse(Bytes(Make("[\"" + first + "\",\"" + first + "\"]"))));
        Assert.Throws<FormatException>(() => BoxRecipe.Parse(Bytes(Make("[\"" + new string('A', 64) + "\"]"))));
        Assert.Throws<FormatException>(() => BoxRecipe.Parse(Bytes(Make("[\"short\"]"))));
        var entries = new string[257]; for (int i = 0; i < entries.Length; i++) entries[i] = "\"" + i.ToString("x64") + "\"";
        Assert.Throws<FormatException>(() => BoxRecipe.Parse(Bytes(Make("[" + string.Join(",", entries) + "]"))));
    }

    [Fact]
    public void DiagnosticSchemaAndBoundarySeedsValidateWithoutCultureDependence()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(ulong.MaxValue, BoxRecipe.Parse(Bytes(DiagnosticSource.Replace("460072030", ulong.MaxValue.ToString(CultureInfo.InvariantCulture)))).Document.SeedValue);
            Assert.Equal(0UL, BoxRecipe.Parse(Bytes(DiagnosticSource.Replace("460072030", "0"))).Document.SeedValue);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void DocumentValidatorRejectsSchemaAndUnvalidatedPhysicalAndCanonicalParameters()
    {
        var valid = BoxRecipe.Parse(Bytes(Source)).Document;
        RecipeDocument Replace(string schema, string parameters) => new RecipeDocument(valid.RecipeId, schema, valid.GeneratorId, valid.GeneratorVersion, valid.Seed, Bytes(parameters), valid.Dependencies);
        var validator = new BoxRecipeValidator();
        var dimensions = Encoding.UTF8.GetString(valid.Parameters);
        Assert.False(validator.Validate(Replace("unknown", dimensions)).Valid);
        Assert.False(validator.Validate(Replace(valid.SchemaId, dimensions.Replace("1000", "-1"))).Valid);
        Assert.False(validator.Validate(Replace(valid.SchemaId, dimensions + " ")).Valid);
        Assert.False(validator.Validate(null!).Valid);
    }

    [Fact]
    public void FingerprintUsesLengthPrefixedIdentityAndRejectsAmbiguousStrings()
    {
        Assert.NotEqual(GeneratorFingerprint.Compute("ab", "c"), GeneratorFingerprint.Compute("a", "bc"));
        // Independent Python hashlib/struct.pack('<I') result, not computed by production encoding.
        Assert.Equal("c4cb26f4cfecdb45517090a00d0a812d65dfbc108b0c51fba7e60e1bbe664447", GeneratorFingerprint.Compute("collision.box", "1.0.0").ToString());
        Assert.Equal(GeneratorFingerprint.Compute("collision.box", "1.0.0"), GeneratorFingerprint.Compute("collision.box", "1.0.0"));
        Assert.Throws<ArgumentException>(() => GeneratorFingerprint.Compute("", "1"));
        Assert.Throws<ArgumentException>(() => GeneratorFingerprint.Compute("bad\n", "1"));
        Assert.Throws<ArgumentException>(() => GeneratorFingerprint.Compute(new string('a', 4097), "1"));
    }

    [Fact]
    public void CpuCollisionRegeneratesVerifiedEnvelopeAndConservativeBounds()
    {
        BoxRecipe recipe = BoxRecipe.Parse(Bytes(DiagnosticSource));
        ContentId toolchain = new ContentId(new string('a', 64));
        var request = new GenerationRequest(recipe.Document, "windows64", "gameplay", toolchain);
        var generator = new CpuBoxCollisionGenerator();
        var first = generator.Generate(request, CancellationToken.None);
        var second = generator.Generate(request, CancellationToken.None);
        Assert.Equal(ErrorCode.None, first.Error); Assert.Empty(first.Diagnostics);
        var artifact = Assert.Single(first.Artifacts);
        Assert.Equal(ContentKind.Collision, artifact.Kind); Assert.Equal("main", artifact.Variant); Assert.Equal(1U, artifact.FormatVersion);
        Assert.Equal(artifact.Payload.ToArray(), Assert.Single(second.Artifacts).Payload.ToArray());
        Assert.Equal(artifact.Sha256, ContentIdentity.Hash(artifact.Payload));
        byte[] body = ArtifactCodec.Decode(artifact.Payload, ContentKind.Collision,
            GeneratorFingerprint.Compute("collision.box", "1.0.0"), toolchain);
        CollisionData data = GeometryCodec.DecodeCollision(body);
        Assert.Empty(data.Capsules); Assert.Empty(data.HeightGrids);
        var box = Assert.Single(data.Boxes).Shape;
        Assert.Equal(1f, box.Size.X); Assert.Equal(1f, box.Size.Y); Assert.Equal(1f, box.Size.Z);
        Assert.Equal(-.5f, first.Bounds.Min.X); Assert.Equal(.5f, first.Bounds.Max.Y);
        Assert.Equal(0f, box.Center.Y);
    }

    [Fact]
    public void CpuCollisionRejectsUnsupportedSchemasProfilesDependenciesAndBevels()
    {
        var generator = new CpuBoxCollisionGenerator();
        var diagnostic = BoxRecipe.Parse(Bytes(DiagnosticSource));
        GenerationRequest Request(BoxRecipe recipe, string profile = "gameplay") => new GenerationRequest(recipe.Document, "windows64", profile, default);
        Assert.Equal(ErrorCode.UnsupportedVersion, generator.Generate(Request(BoxRecipe.Parse(Bytes(Source))), CancellationToken.None).Error);
        Assert.Equal(ErrorCode.InvalidInput, generator.Generate(Request(diagnostic, "Low"), CancellationToken.None).Error);
        Assert.Equal(ErrorCode.InvalidInput, generator.Generate(Request(BoxRecipe.Parse(Bytes(DiagnosticSource.Replace("\"bevelMm\": 0", "\"bevelMm\": 1")))), CancellationToken.None).Error);
        string dependency = new string('a', 64);
        var withDependency = BoxRecipe.Parse(Bytes(DiagnosticSource.Replace("\"dependencies\": []", "\"dependencies\": [\"" + dependency + "\"]")));
        Assert.Equal(ErrorCode.InvalidInput, generator.Generate(Request(withDependency), CancellationToken.None).Error);
        Assert.Equal(ErrorCode.InvalidInput, generator.Generate(null!, CancellationToken.None).Error);
        var invalidParameters = new RecipeDocument(diagnostic.Document.RecipeId, BoxRecipe.CollisionSchema, "collision.box", "1.0.0", "0",
            Bytes("{\"bevelMm\":0,\"depthMm\":1000,\"heightMm\":1000,\"widthMm\":-1}"), Array.Empty<ContentId>());
        Assert.Equal(ErrorCode.InvalidInput, generator.Generate(new GenerationRequest(invalidParameters, "windows64", "gameplay", default), CancellationToken.None).Error);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var cancelled = generator.Generate(Request(diagnostic), cancel.Token);
        Assert.Equal(ErrorCode.Cancelled, cancelled.Error); Assert.Empty(cancelled.Artifacts);
        Assert.Single(cancelled.Diagnostics);
    }
}
