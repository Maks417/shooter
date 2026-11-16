#nullable enable
using System;
using System.Threading;
using Shooter.Contracts;

namespace Shooter.Generation
{
    public sealed class MaterialAssetGenerator : IAssetGenerator
    {
        public GenerationResult Generate(GenerationRequest request, CancellationToken cancel)
        {
            if (cancel.IsCancellationRequested) return Failure(ErrorCode.Cancelled, "Material generation cancelled.");
            if (request == null) return Failure(ErrorCode.InvalidInput, "Material generation requires a request.");
            if (request.Recipe.SchemaId != MaterialRecipe.Schema || request.Recipe.GeneratorId != MaterialRecipe.Generator || !MaterialRecipe.IsSupportedVersion(request.Recipe.GeneratorVersion))
                return Failure(ErrorCode.UnsupportedVersion, "Unsupported material schema/generator/version.");
            if (request.VisualProfile != "Low" && request.VisualProfile != "Balanced" && request.VisualProfile != "High") return Failure(ErrorCode.InvalidInput, "Materials require a supported visual profile.");
            try
            {
                MaterialRecipe recipe = MaterialRecipe.FromDocument(request.Recipe);
                MaterialBake bake = MaterialBaker.Bake(recipe, cancel);
                var generator = GeneratorFingerprint.Compute(MaterialRecipe.Generator, recipe.GeneratorVersion);
                GeneratedArtifact Plane(MaterialData data, string variant)
                {
                    cancel.ThrowIfCancellationRequested();
                    byte[] body = GeometryCodec.EncodeMaterial(data);
                    byte[] payload = ArtifactCodec.Encode(ContentKind.Material, body, generator, request.ToolchainHash);
                    return new GeneratedArtifact(ContentKind.Material, variant, 1, payload, ContentIdentity.Hash(payload));
                }
                var output = new[] { Plane(bake.Albedo, "albedo"), Plane(bake.Normal, "normal"), Plane(bake.Mask, "mask") };
                cancel.ThrowIfCancellationRequested();
                return new GenerationResult(ErrorCode.None, output, new Aabb(new Float3(0, 0, 0), new Float3(0, 0, 0)), Array.Empty<Diagnostic>());
            }
            catch (OperationCanceledException) { return Failure(ErrorCode.Cancelled, "Material generation cancelled."); }
            catch (Exception error) when (error is FormatException || error is ArgumentException)
            { return Failure(ErrorCode.InvalidInput, error.Message); }
        }
        private static GenerationResult Failure(ErrorCode code, string message) => new GenerationResult(code, Array.Empty<GeneratedArtifact>(),
            new Aabb(new Float3(0, 0, 0), new Float3(0, 0, 0)), new[] { new Diagnostic(code, "request", message.Length <= 512 ? message : message.Substring(0, 512)) });
    }
}
