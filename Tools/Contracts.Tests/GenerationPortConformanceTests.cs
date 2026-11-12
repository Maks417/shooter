using System;
using System.Text;
using System.Threading;
using Shooter.Bootstrap;
using Shooter.Contracts;
using Xunit;

namespace Shooter.Contracts.Tests
{
    // Fixture-only port doubles. These bytes are never compiler output or shipping content.
    internal sealed class FixtureRecipeValidator : IRecipeValidator, IFakeAdapter
    {
        public ValidationReport Validate(RecipeDocument recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            return recipe.SchemaId == "fixture-only.v1" && Encoding.UTF8.GetString(recipe.Parameters) == "{}"
                ? new ValidationReport(true, Array.Empty<Diagnostic>())
                : new ValidationReport(false, new[] { new Diagnostic(ErrorCode.UnsupportedVersion, "$.schemaId", "Unsupported test fixture") });
        }
    }
    internal sealed class FixtureGenerator : IAssetGenerator, IFakeAdapter
    {
        public GenerationResult Generate(GenerationRequest request, CancellationToken cancel)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (cancel.IsCancellationRequested) return Failure(ErrorCode.Cancelled, "$", "Cancelled");
            var validation = new FixtureRecipeValidator().Validate(request.Recipe);
            if (!validation.Valid) return new GenerationResult(ErrorCode.InvalidInput, Array.Empty<GeneratedArtifact>(), default, validation.Issues);
            var artifact = new GeneratedArtifact(ContentKind.Mesh, "test-only-not-a-mesh", 1, Encoding.ASCII.GetBytes("abc"),
                new ContentId("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
            return new GenerationResult(ErrorCode.None, new[] { artifact }, default, Array.Empty<Diagnostic>());
        }
        private static GenerationResult Failure(ErrorCode error, string path, string message) =>
            new GenerationResult(error, Array.Empty<GeneratedArtifact>(), default, new[] { new Diagnostic(error, path, message) });
    }
    public sealed class GenerationPortConformanceTests
    {
        private static GenerationRequest Request(string schema = "fixture-only.v1") => new GenerationRequest(
            new RecipeDocument(new DefinitionId("fixtures/port"), schema, "fixture-only", "1", "460072030", Encoding.UTF8.GetBytes("{}"), Array.Empty<ContentId>()),
            "test-only", "Low", default);
        [Fact]
        public void GeneratorPortPublishesOwnedVerifiedBytesAndRepeatableIdentity()
        {
            IRecipeValidator validator = new FixtureRecipeValidator(); IAssetGenerator generator = new FixtureGenerator();
            Assert.True(validator.Validate(Request().Recipe).Valid);
            var first = generator.Generate(Request(), CancellationToken.None);
            var second = generator.Generate(Request(), CancellationToken.None);
            Assert.Equal(ErrorCode.None, first.Error); Assert.Single(first.Artifacts);
            Assert.Equal(first.Artifacts[0].Sha256, second.Artifacts[0].Sha256);
            Assert.Equal("abc", Encoding.ASCII.GetString(first.Artifacts[0].Payload));
            Assert.Throws<InvalidOperationException>(() => CompositionPolicy.RequireReal(validator, generator));
        }
        [Fact]
        public void InvalidAndCancelledGenerationNeverPublishesPartialArtifacts()
        {
            IAssetGenerator generator = new FixtureGenerator();
            var invalid = generator.Generate(Request("unknown.v999"), CancellationToken.None);
            Assert.Equal(ErrorCode.InvalidInput, invalid.Error); Assert.Empty(invalid.Artifacts); Assert.Single(invalid.Diagnostics);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            var cancelled = generator.Generate(Request(), cancellation.Token);
            Assert.Equal(ErrorCode.Cancelled, cancelled.Error); Assert.Empty(cancelled.Artifacts);
        }
    }
}
