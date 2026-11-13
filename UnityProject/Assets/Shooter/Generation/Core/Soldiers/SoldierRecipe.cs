#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>The fixed anatomy spike deliberately rejects unsupported variation instead of ignoring parameters.</summary>
    public sealed class SoldierRecipe
    {
        public const string Schema = "shooter.soldier.v1";
        public const string GeneratorId = "soldier.loft";
        public const string GeneratorVersion = "1.0.2";
        public const string ParametersJson = "{\"heightMm\":1800,\"hipWidthMm\":280,\"nearLimbSegments\":12,\"nearTorsoSegments\":16,\"runCycleMm\":2200,\"runDutyPpm\":350000,\"shoulderWidthMm\":420,\"uniformOffsetMm\":6,\"walkCycleMm\":1400,\"walkDutyPpm\":500000}";
        public RecipeDocument Document { get; }
        public ContentId SourceHash { get; }
        private readonly byte[] source;
        public byte[] CanonicalSource => (byte[])source.Clone();
        private SoldierRecipe(RecipeDocument document, byte[] canonical)
        { Document = document; source = canonical; SourceHash = ContentIdentity.SourceHash(Schema, canonical); }
        public static SoldierRecipe Parse(ReadOnlySpan<byte> source)
        {
            byte[] canonical = CanonicalJson.Encode(source); var root = JsonValue.Parse(canonical).Object;
            string[] fields = {"schemaId","recipeId","generatorId","generatorVersion","seed","parameters","dependencies"};
            if (root.Count != fields.Length) throw new FormatException("Missing/unknown soldier recipe field.");
            foreach (var field in fields) if (!root.ContainsKey(field)) throw new FormatException("Missing soldier field: " + field);
            if (root["schemaId"].String != Schema || root["generatorId"].String != GeneratorId || root["generatorVersion"].String != GeneratorVersion)
                throw new FormatException("Unsupported soldier schema/generator.");
            var parameters = root["parameters"].Object; var fixedParameters = JsonValue.Parse(Encoding.UTF8.GetBytes(ParametersJson)).Object;
            if (parameters.Count != fixedParameters.Count) throw new FormatException("Soldier spike uses fixed baseline parameters.");
            foreach (var item in fixedParameters)
                if (!parameters.TryGetValue(item.Key, out var actual) || actual.Number != item.Value.Number) throw new FormatException("Unsupported soldier parameter: " + item.Key);
            if (root["dependencies"].Array.Count != 0) throw new FormatException("Soldier spike has no dependency inputs.");
            try
            {
                ContentIdentity.ParseSeed(root["seed"].String);
                var document = new RecipeDocument(new DefinitionId(root["recipeId"].String), Schema, GeneratorId, GeneratorVersion,
                    root["seed"].String, Encoding.UTF8.GetBytes(ParametersJson), Array.Empty<ContentId>());
                return new SoldierRecipe(document, canonical);
            }
            catch (ArgumentException error) { throw new FormatException("Invalid soldier identity/seed.", error); }
        }
        internal static void Validate(RecipeDocument recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (!recipe.RecipeId.IsValid || recipe.SchemaId != Schema || recipe.GeneratorId != GeneratorId || recipe.GeneratorVersion != GeneratorVersion)
                throw new FormatException("Unsupported soldier identity/schema/generator.");
            ContentIdentity.ParseSeed(recipe.Seed);
            if (!recipe.Parameters.SequenceEqual(Encoding.UTF8.GetBytes(ParametersJson)) || recipe.Dependencies.Count != 0)
                throw new FormatException("Soldier spike requires canonical fixed parameters and no dependencies.");
        }
    }
    public sealed class SoldierRecipeValidator : IRecipeValidator
    {
        public ValidationReport Validate(RecipeDocument recipe)
        {
            try { SoldierRecipe.Validate(recipe); return new ValidationReport(true, Array.Empty<Diagnostic>()); }
            catch (Exception error) when (error is FormatException || error is ArgumentException)
            { return new ValidationReport(false, new[] {new Diagnostic(ErrorCode.InvalidInput, "recipe", error.Message)}); }
        }
    }
}
