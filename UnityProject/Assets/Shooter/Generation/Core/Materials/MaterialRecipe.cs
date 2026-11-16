#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation
{
    public sealed class MaterialRecipe
    {
        public const string Schema = "shooter.recipe.material.v1";
        public const string Generator = "material.surface";
        /// <summary>Default generator: hand-rolled periodic value noise. Frozen; every shipped recipe uses it.</summary>
        public const string Version = "1.0.1";
        /// <summary>ADR-075/076: same families, sampled through <see cref="Noise.NoiseField"/> (FastNoiseLite). Opt-in per recipe.</summary>
        public const string NoiseVersion = "1.1.0";
        public static bool IsSupportedVersion(string? version) => version == Version || version == NoiseVersion;
        private readonly byte[] source;
        public RecipeDocument Document { get; }
        public byte[] CanonicalSource => (byte[])source.Clone();
        public ContentId SourceHash { get; }
        public string Family { get; }
        public int Resolution { get; }
        public int TileMm { get; }
        public float MetersPerTile => TileMm / 1000f;
        public string GeneratorVersion => Document.GeneratorVersion;

        private MaterialRecipe(RecipeDocument document, byte[] canonicalSource, string family, int resolution, int tileMm)
        {
            Document = document; source = canonicalSource; Family = family; Resolution = resolution; TileMm = tileMm;
            SourceHash = ContentIdentity.SourceHash(Schema, canonicalSource);
        }

        public static MaterialRecipe Parse(ReadOnlySpan<byte> source)
        {
            byte[] canonical = CanonicalJson.Encode(source);
            var root = JsonValue.Parse(canonical).Object;
            Exact(root, "schemaId", "recipeId", "generatorId", "generatorVersion", "seed", "parameters", "dependencies");
            string version = root["generatorVersion"].String;
            if (root["schemaId"].String != Schema || root["generatorId"].String != Generator || !IsSupportedVersion(version))
                throw new FormatException("Unsupported material recipe schema/generator/version.");
            if (root["dependencies"].Array.Count != 0) throw new FormatException("Material recipe has no dependency artifacts.");
            string seed = root["seed"].String;
            try { ContentIdentity.ParseSeed(seed); }
            catch (ArgumentException error) { throw new FormatException("Expected canonical uint64 seed.", error); }
            ReadParameters(root["parameters"], out string family, out int resolution, out int tileMm);
            try
            {
                var document = new RecipeDocument(new DefinitionId(root["recipeId"].String), Schema, Generator, version, seed,
                    Parameters(family, resolution, tileMm), Array.Empty<ContentId>());
                return new MaterialRecipe(document, canonical, family, resolution, tileMm);
            }
            catch (ArgumentException error) { throw new FormatException("Invalid material recipe identity.", error); }
        }

        internal static MaterialRecipe FromDocument(RecipeDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.SchemaId != Schema || document.GeneratorId != Generator || !IsSupportedVersion(document.GeneratorVersion))
                throw new FormatException("Unsupported material recipe schema/generator/version.");
            if (!document.RecipeId.IsValid || document.Dependencies.Count != 0) throw new FormatException("Invalid material recipe identity/dependencies.");
            ContentIdentity.ParseSeed(document.Seed);
            ReadParameters(JsonValue.Parse(document.Parameters), out string family, out int resolution, out int tileMm);
            byte[] parameters = Parameters(family, resolution, tileMm);
            if (!document.Parameters.SequenceEqual(parameters)) throw new FormatException("Expected canonical material parameters.");
            string source = "{\"schemaId\":\"" + Schema + "\",\"recipeId\":\"" + document.RecipeId + "\",\"generatorId\":\"" + Generator +
                "\",\"generatorVersion\":\"" + document.GeneratorVersion + "\",\"seed\":\"" + document.Seed + "\",\"parameters\":" + Encoding.UTF8.GetString(parameters) + ",\"dependencies\":[]}";
            return new MaterialRecipe(document, CanonicalJson.Encode(Encoding.UTF8.GetBytes(source)), family, resolution, tileMm);
        }

        private static void ReadParameters(JsonValue parameters, out string family, out int resolution, out int tileMm)
        {
            var values = parameters.Object; Exact(values, "family", "resolution", "tileMm");
            family = values["family"].String; _ = MaterialFamilyDefinition.For(family);
            resolution = values["resolution"].Number;
            if (resolution != 32 && resolution != 64 && resolution != 128 && resolution != 256 && resolution != 512) throw new FormatException("Material resolution must be 32,64,128,256 or512.");
            tileMm = values["tileMm"].Number;
            if (tileMm < 100 || tileMm > 8000) throw new FormatException("tileMm must be 100..8000.");
        }
        private static byte[] Parameters(string family, int resolution, int tileMm) => Encoding.UTF8.GetBytes("{\"family\":\"" + family + "\",\"resolution\":" + resolution.ToString(CultureInfo.InvariantCulture) + ",\"tileMm\":" + tileMm.ToString(CultureInfo.InvariantCulture) + "}");
        private static void Exact(IReadOnlyDictionary<string, JsonValue> value, params string[] keys)
        {
            if (value.Count != keys.Length) throw new FormatException("Unknown or missing material recipe field.");
            foreach (string key in keys) if (!value.ContainsKey(key)) throw new FormatException("Missing material recipe field: " + key);
        }
    }

    public sealed class MaterialRecipeValidator : IRecipeValidator
    {
        public ValidationReport Validate(RecipeDocument recipe)
        {
            try { MaterialRecipe.FromDocument(recipe); return new ValidationReport(true, Array.Empty<Diagnostic>()); }
            catch (Exception error) when (error is ArgumentException || error is FormatException)
            {
                string message = error.Message.Length <= 512 ? error.Message : error.Message.Substring(0, 512);
                return new ValidationReport(false, new[] { new Diagnostic(ErrorCode.InvalidInput, "recipe", message) });
            }
        }
    }

    /// <summary>Linear reflectance, physical relief and default tiling, not baked illumination.</summary>
    public sealed class MaterialFamilyDefinition
    {
        public string Family { get; }
        public Float3 LinearAlbedo { get; }
        public float Roughness { get; }
        public float HeightMeters { get; }
        public int RecommendedTileMm { get; }
        private MaterialFamilyDefinition(string family, Float3 albedo, float roughness, float heightMeters, int tileMm)
        { Family = family; LinearAlbedo = albedo; Roughness = roughness; HeightMeters = heightMeters; RecommendedTileMm = tileMm; }
        public static MaterialFamilyDefinition For(string family)
        {
            switch (family)
            {
                case "metal": return new MaterialFamilyDefinition(family, new Float3(.065f, .072f, .060f), .48f, .0002f, 1000);
                case "polymer": return new MaterialFamilyDefinition(family, new Float3(.045f, .050f, .040f), .62f, .00025f, 500);
                case "concrete": return new MaterialFamilyDefinition(family, new Float3(.28f, .29f, .27f), .82f, .0016f, 2000);
                case "plaster": return new MaterialFamilyDefinition(family, new Float3(.58f, .56f, .51f), .88f, .0007f, 2000);
                case "asphalt": return new MaterialFamilyDefinition(family, new Float3(.018f, .021f, .024f), .90f, .0018f, 2000);
                case "soil": return new MaterialFamilyDefinition(family, new Float3(.095f, .055f, .027f), .93f, .0025f, 2000);
                case "fabric": return new MaterialFamilyDefinition(family, new Float3(.070f, .085f, .040f), .86f, .00018f, 250);
                default: throw new FormatException("Unknown procedural material family.");
            }
        }
    }
}
