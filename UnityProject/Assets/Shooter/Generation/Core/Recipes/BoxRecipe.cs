#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>Schema-validated numeric box source. No geometry or cache loading happens here.</summary>
    public sealed class BoxRecipe
    {
        public const string GeometrySchema = "shooter.recipe.box.v1";
        public const string CollisionSchema = "shooter.recipe.box-collision.v1";
        public RecipeDocument Document { get; }
        private readonly byte[] source;
        public byte[] CanonicalSource => (byte[])source.Clone();
        public ContentId SourceHash { get; }
        public int WidthMm { get; }
        public int HeightMm { get; }
        public int DepthMm { get; }
        public int BevelMm { get; }

        private BoxRecipe(RecipeDocument document, byte[] canonicalSource, Dimensions dimensions)
        {
            Document = document; source = canonicalSource;
            WidthMm = dimensions.Width; HeightMm = dimensions.Height; DepthMm = dimensions.Depth; BevelMm = dimensions.Bevel;
            SourceHash = ContentIdentity.SourceHash(document.SchemaId, canonicalSource);
        }

        public static BoxRecipe Parse(ReadOnlySpan<byte> source)
        {
            byte[] canonical = CanonicalJson.Encode(source);
            var root = JsonValue.Parse(canonical).Object;
            ExactFields(root, "recipe", "schemaId", "recipeId", "generatorId", "generatorVersion", "seed", "parameters", "dependencies");
            string schema = root["schemaId"].String;
            string generator = root["generatorId"].String;
            string version = root["generatorVersion"].String;
            ValidateIdentity(schema, generator, version);
            string seed = root["seed"].String;
            try { ContentIdentity.ParseSeed(seed); }
            catch (ArgumentException error) { throw new FormatException("seed: expected canonical uint64 decimal string.", error); }
            var dimensions = ReadDimensions(root["parameters"]);
            var dependencyValues = root["dependencies"].Array;
            if (dependencyValues.Count > 256) throw new FormatException("dependencies: maximum count is 256.");
            var dependencies = new ContentId[dependencyValues.Count];
            try
            {
                for (int i = 0; i < dependencies.Length; i++)
                {
                    dependencies[i] = new ContentId(dependencyValues[i].String);
                    if (i > 0 && dependencies[i - 1].CompareTo(dependencies[i]) >= 0)
                        throw new FormatException("dependencies: hashes must be sorted and unique.");
                }
                var document = new RecipeDocument(new DefinitionId(root["recipeId"].String), schema, generator, version,
                    seed, dimensions.CanonicalBytes(), dependencies);
                return new BoxRecipe(document, canonical, dimensions);
            }
            catch (ArgumentException error) { throw new FormatException("Invalid recipe identity or dependency hash.", error); }
        }

        internal static Dimensions ValidateDocument(RecipeDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            ValidateIdentity(document.SchemaId, document.GeneratorId, document.GeneratorVersion);
            if (!document.RecipeId.IsValid) throw new FormatException("recipeId: invalid definition identifier.");
            ContentIdentity.ParseSeed(document.Seed);
            var dimensions = ReadDimensions(JsonValue.Parse(document.Parameters));
            if (!document.Parameters.SequenceEqual(dimensions.CanonicalBytes()))
                throw new FormatException("parameters: expected canonical JSON bytes.");
            return dimensions;
        }

        private static void ValidateIdentity(string schema, string generator, string version)
        {
            if (schema != GeometrySchema && schema != CollisionSchema) throw new FormatException("schemaId: unsupported box recipe schema.");
            string expected = schema == GeometrySchema ? "geometry.box" : "collision.box";
            if (generator != expected) throw new FormatException("generatorId: does not match box schema.");
            if (version != "1.0.0") throw new FormatException("generatorVersion: unsupported box generator version.");
        }

        private static Dimensions ReadDimensions(JsonValue parameters)
        {
            var values = parameters.Object;
            ExactFields(values, "parameters", "widthMm", "heightMm", "depthMm", "bevelMm");
            int width = Dimension(values["widthMm"], "widthMm", 1);
            int height = Dimension(values["heightMm"], "heightMm", 1);
            int depth = Dimension(values["depthMm"], "depthMm", 1);
            int bevel = Dimension(values["bevelMm"], "bevelMm", 0);
            return new Dimensions(width, height, depth, bevel);
        }

        private static int Dimension(JsonValue value, string name, int minimum)
        {
            int number = value.Number;
            if (number < minimum || number > 100000) throw new FormatException("parameters." + name + ": outside allowed millimeter range.");
            return number;
        }

        private static void ExactFields(IReadOnlyDictionary<string, JsonValue> values, string path, params string[] expected)
        {
            if (values.Count != expected.Length) throw new FormatException(path + ": missing or unknown field.");
            foreach (string key in expected)
                if (!values.ContainsKey(key)) throw new FormatException(path + "." + key + ": required field missing.");
        }

        internal readonly struct Dimensions
        {
            public readonly int Width, Height, Depth, Bevel;
            public Dimensions(int width, int height, int depth, int bevel) { Width = width; Height = height; Depth = depth; Bevel = bevel; }
            public byte[] CanonicalBytes() => Encoding.UTF8.GetBytes("{\"bevelMm\":" + Bevel.ToString(CultureInfo.InvariantCulture) +
                ",\"depthMm\":" + Depth.ToString(CultureInfo.InvariantCulture) + ",\"heightMm\":" + Height.ToString(CultureInfo.InvariantCulture) +
                ",\"widthMm\":" + Width.ToString(CultureInfo.InvariantCulture) + "}");
        }
    }

    public sealed class BoxRecipeValidator : IRecipeValidator
    {
        public ValidationReport Validate(RecipeDocument recipe)
        {
            try
            {
                BoxRecipe.ValidateDocument(recipe);
                return new ValidationReport(true, System.Array.Empty<Diagnostic>());
            }
            catch (Exception error) when (error is FormatException || error is ArgumentException)
            {
                return new ValidationReport(false, new[] { new Diagnostic(ErrorCode.InvalidInput, "recipe", error.Message.Length <= 512 ? error.Message : error.Message.Substring(0, 512)) });
            }
        }
    }
}
