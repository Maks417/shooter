#nullable enable
using System;
using System.Threading;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>Real CPU-only collision diagnostic. It deliberately accepts no render-geometry schema.</summary>
    public sealed class CpuBoxCollisionGenerator : IAssetGenerator
    {
        public GenerationResult Generate(GenerationRequest request, CancellationToken cancel)
        {
            if (cancel.IsCancellationRequested) return Failure(ErrorCode.Cancelled, "Generation cancelled.");
            if (request == null) return Failure(ErrorCode.InvalidInput, "Generation request is required.");
            var recipe = request.Recipe;
            if (recipe.SchemaId != BoxRecipe.CollisionSchema || recipe.GeneratorId != "collision.box" || recipe.GeneratorVersion != "1.0.0")
                return Failure(ErrorCode.UnsupportedVersion, "Expected shooter.recipe.box-collision.v1 / collision.box 1.0.0.");
            if (request.VisualProfile != "gameplay" || recipe.Dependencies.Count != 0)
                return Failure(ErrorCode.InvalidInput, "Collision diagnostic requires gameplay profile and no dependencies.");
            BoxRecipe.Dimensions dimensions;
            try { dimensions = BoxRecipe.ValidateDocument(recipe); }
            catch (Exception error) when (error is ArgumentException || error is FormatException)
            { return Failure(ErrorCode.InvalidInput, error.Message); }
            if (dimensions.Bevel != 0) return Failure(ErrorCode.InvalidInput, "Collision diagnostic supports bevelMm=0 only.");
            float width = dimensions.Width / 1000f, height = dimensions.Height / 1000f, depth = dimensions.Depth / 1000f;
            var collision = new CollisionData(Array.Empty<TaggedCapsule>(),
                new[] { new TaggedBox(new Box(new Float3(0, 0, 0), new Float3(width, height, depth), Quat.Identity), 0) },
                Array.Empty<TaggedHeightGrid>());
            byte[] body = GeometryCodec.EncodeCollision(collision);
            byte[] payload = ArtifactCodec.Encode(ContentKind.Collision, body,
                GeneratorFingerprint.Compute(recipe.GeneratorId, recipe.GeneratorVersion), request.ToolchainHash);
            var bounds = new Aabb(new Float3(-width / 2, -height / 2, -depth / 2), new Float3(width / 2, height / 2, depth / 2));
            if (cancel.IsCancellationRequested) return Failure(ErrorCode.Cancelled, "Generation cancelled.");
            return new GenerationResult(ErrorCode.None,
                new[] { new GeneratedArtifact(ContentKind.Collision, "main", 1, payload, ContentIdentity.Hash(payload)) },
                bounds, Array.Empty<Diagnostic>());
        }

        private static GenerationResult Failure(ErrorCode code, string message)
        {
            if (message.Length > 512) message = message.Substring(0, 512);
            return new GenerationResult(code, Array.Empty<GeneratedArtifact>(),
                new Aabb(new Float3(0, 0, 0), new Float3(0, 0, 0)), new[] { new Diagnostic(code, "request", message) });
        }
    }
}
