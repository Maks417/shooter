#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Shooter.Contracts;

namespace Shooter.Generation
{
    public sealed class WeaponAssetGenerator : IAssetGenerator
    {
        public GenerationResult Generate(GenerationRequest request, CancellationToken cancel)
        {
            if (cancel.IsCancellationRequested) return Fail(ErrorCode.Cancelled, "Weapon generation cancelled.");
            if (request == null) return Fail(ErrorCode.InvalidInput, "Weapon generation request is required.");
            if (request.Recipe.SchemaId != WeaponRecipe.Schema || request.Recipe.GeneratorId != WeaponRecipe.GeneratorId || request.Recipe.GeneratorVersion != WeaponRecipe.GeneratorVersion)
                return Fail(ErrorCode.UnsupportedVersion, "Unsupported weapon schema/generator version.");
            if (request.VisualProfile != "gameplay" && request.VisualProfile != "Low" && request.VisualProfile != "Balanced" && request.VisualProfile != "High")
                return Fail(ErrorCode.InvalidInput, "Unknown weapon visual profile.");
            if (request.Recipe.Dependencies.Count != 0) return Fail(ErrorCode.InvalidInput, "Initial numeric weapon generator has no dependency inputs.");
            string kind;
            string silhouette;
            try
            {
                var parsed = WeaponRecipe.Validate(request.Recipe);
                kind = parsed.kind;
                silhouette = parsed.silhouette;
            }
            catch (Exception error) when (error is FormatException || error is ArgumentException) { return Fail(ErrorCode.InvalidInput, error.Message); }
            var artifacts = new List<GeneratedArtifact>();
            ContentId generator = GeneratorFingerprint.Compute(WeaponRecipe.GeneratorId, WeaponRecipe.GeneratorVersion);
            void Add(ContentKind contentKind, string variant, byte[] body)
            {
                byte[] payload = ArtifactCodec.Encode(contentKind, body, generator, request.ToolchainHash);
                artifacts.Add(new GeneratedArtifact(contentKind, variant, 1, payload, ContentIdentity.Hash(payload)));
            }
            Aabb bounds = WeaponGeometry.BoundsFor(kind);
            if (request.VisualProfile == "gameplay") Add(ContentKind.Collision, "main", GeometryCodec.EncodeCollision(WeaponGeometry.BuildCollision(kind)));
            else
            {
                string source = "{\"schemaId\":\"" + WeaponRecipe.Schema + "\",\"recipeId\":\"" + request.Recipe.RecipeId + "\",\"generatorId\":\"" + WeaponRecipe.GeneratorId + "\",\"generatorVersion\":\"" + WeaponRecipe.GeneratorVersion + "\",\"seed\":\"" + request.Recipe.Seed + "\",\"parameters\":{\"kind\":\"" + kind + "\",\"silhouette\":\"" + silhouette + "\"},\"dependencies\":[]}";
                var build = WeaponGeometry.Build(WeaponRecipe.Parse(System.Text.Encoding.UTF8.GetBytes(source)));
                for (int i = 0; i < build.Lods.Count; i++)
                {
                    if (cancel.IsCancellationRequested) return Fail(ErrorCode.Cancelled, "Weapon generation cancelled.");
                    Add(ContentKind.Mesh, "lod" + i, GeometryCodec.EncodeMesh(build.Lods[i]));
                }
                if (cancel.IsCancellationRequested) return Fail(ErrorCode.Cancelled, "Weapon generation cancelled.");
                Add(ContentKind.Mesh, "magazine", GeometryCodec.EncodeMesh(build.Magazine));
                Add(ContentKind.Mesh, "cycle", GeometryCodec.EncodeMesh(build.CyclePart));
            }
            if (cancel.IsCancellationRequested) return Fail(ErrorCode.Cancelled, "Weapon generation cancelled.");
            return new GenerationResult(ErrorCode.None, artifacts, bounds, Array.Empty<Diagnostic>());
        }
        private static GenerationResult Fail(ErrorCode error, string message) => new GenerationResult(error, Array.Empty<GeneratedArtifact>(),
            new Aabb(new Float3(0, 0, 0), new Float3(0, 0, 0)), new[] { new Diagnostic(error, "recipe", message.Length <= 512 ? message : message.Substring(0, 512)) });
    }
}
