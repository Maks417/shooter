#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation
{
    public sealed class WeaponRecipe
    {
        public const string Schema = "shooter.recipe.weapon.v1";
        public const string GeneratorId = "geometry.weapon";
        public const string GeneratorVersion = "1.0.5";
        public RecipeDocument Document { get; }
        public string Kind { get; }
        /// <summary>Visual silhouette within kind (ADR-072). <c>standard</c> or <c>carbine</c> (rifle only).</summary>
        public string Silhouette { get; }
        private readonly byte[] source;
        public byte[] CanonicalSource => (byte[])source.Clone();
        public ContentId SourceHash { get; }
        private WeaponRecipe(RecipeDocument document, string kind, string silhouette, byte[] canonical)
        { Document = document; Kind = kind; Silhouette = silhouette; source = canonical; SourceHash = ContentIdentity.SourceHash(Schema, canonical); }

        public static WeaponRecipe Parse(ReadOnlySpan<byte> source)
        {
            byte[] canonical = CanonicalJson.Encode(source);
            var root = JsonValue.Parse(canonical).Object;
            Fields(root, "schemaId", "recipeId", "generatorId", "generatorVersion", "seed", "parameters", "dependencies");
            if (root["schemaId"].String != Schema || root["generatorId"].String != GeneratorId || root["generatorVersion"].String != GeneratorVersion)
                throw new FormatException("Unsupported weapon schema/generator version.");
            var parsed = Parameters(root["parameters"]);
            string seed = root["seed"].String;
            try
            {
                ContentIdentity.ParseSeed(seed);
                var values = root["dependencies"].Array;
                if (values.Count > 256) throw new FormatException("Weapon dependency count exceeds 256.");
                var dependencies = new ContentId[values.Count];
                for (int i = 0; i < dependencies.Length; i++)
                {
                    dependencies[i] = new ContentId(values[i].String);
                    if (i > 0 && dependencies[i - 1].CompareTo(dependencies[i]) >= 0) throw new FormatException("Dependencies must be sorted and unique.");
                }
                var document = new RecipeDocument(new DefinitionId(root["recipeId"].String), Schema, GeneratorId, GeneratorVersion, seed, ParameterBytes(parsed.kind, parsed.silhouette), dependencies);
                return new WeaponRecipe(document, parsed.kind, parsed.silhouette, canonical);
            }
            catch (ArgumentException error) { throw new FormatException("Invalid weapon identity/seed/dependency.", error); }
        }
        internal static (string kind, string silhouette) Validate(RecipeDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (!document.RecipeId.IsValid || document.SchemaId != Schema || document.GeneratorId != GeneratorId || document.GeneratorVersion != GeneratorVersion)
                throw new FormatException("Unsupported weapon identity/schema/generator.");
            ContentIdentity.ParseSeed(document.Seed);
            var parsed = Parameters(JsonValue.Parse(document.Parameters));
            if (!document.Parameters.SequenceEqual(ParameterBytes(parsed.kind, parsed.silhouette))) throw new FormatException("Weapon parameters must be canonical.");
            return parsed;
        }
        private static (string kind, string silhouette) Parameters(JsonValue value)
        {
            Fields(value.Object, "kind", "silhouette");
            string kind = value.Object["kind"].String;
            string silhouette = value.Object["silhouette"].String;
            if (kind != "rifle" && kind != "pistol") throw new FormatException("Weapon kind must be rifle or pistol.");
            if (silhouette != "standard" && silhouette != "carbine") throw new FormatException("Weapon silhouette must be standard or carbine.");
            if (silhouette == "carbine" && kind != "rifle") throw new FormatException("Carbine silhouette requires kind rifle.");
            return (kind, silhouette);
        }
        private static byte[] ParameterBytes(string kind, string silhouette) =>
            Encoding.UTF8.GetBytes("{\"kind\":\"" + kind + "\",\"silhouette\":\"" + silhouette + "\"}");
        private static void Fields(IReadOnlyDictionary<string, JsonValue> values, params string[] names)
        {
            if (values.Count != names.Length) throw new FormatException("Missing or unknown weapon recipe field.");
            foreach (string name in names) if (!values.ContainsKey(name)) throw new FormatException("Missing weapon recipe field: " + name);
        }
    }
    public sealed class WeaponRecipeValidator : IRecipeValidator
    {
        public ValidationReport Validate(RecipeDocument recipe)
        {
            try { WeaponRecipe.Validate(recipe); return new ValidationReport(true, Array.Empty<Diagnostic>()); }
            catch (Exception error) when (error is FormatException || error is ArgumentException)
            { return new ValidationReport(false, new[] { new Diagnostic(ErrorCode.InvalidInput, "recipe", error.Message.Length <= 512 ? error.Message : error.Message.Substring(0, 512)) }); }
        }
    }
}
