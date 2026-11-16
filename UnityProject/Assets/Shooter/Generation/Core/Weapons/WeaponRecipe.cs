#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>
    /// Resolved numeric rifle parts (ADR-082). geometry.weapon 1.0.5 recipes map their <c>silhouette</c> onto the two
    /// frozen presets; 1.1.0 recipes state every part explicitly in millimetres. Pistols have no parts.
    /// </summary>
    public sealed class WeaponParts
    {
        public int HandguardFrontMm { get; }
        public int BarrelFrontMm { get; }
        public int TopRailTeeth { get; }
        public int GasBlockZMm { get; }
        public int GasTubeZMm { get; }
        public int GasTubeLengthMm { get; }
        public int FrontSightZMm { get; }
        public bool Stock { get; }
        public bool Optic { get; }

        public WeaponParts(int handguardFrontMm, int barrelFrontMm, int topRailTeeth, int gasBlockZMm, int gasTubeZMm,
            int gasTubeLengthMm, int frontSightZMm, bool stock, bool optic)
        {
            HandguardFrontMm = handguardFrontMm; BarrelFrontMm = barrelFrontMm; TopRailTeeth = topRailTeeth;
            GasBlockZMm = gasBlockZMm; GasTubeZMm = gasTubeZMm; GasTubeLengthMm = gasTubeLengthMm;
            FrontSightZMm = frontSightZMm; Stock = stock; Optic = optic;
            Validate();
        }

        /// <summary>1.0.5 <c>standard</c> rifle.</summary>
        public static WeaponParts Standard { get; } = new WeaponParts(485, 640, 15, 480, 400, 120, 530, true, true);
        /// <summary>1.0.5 <c>carbine</c> rifle.</summary>
        public static WeaponParts Carbine { get; } = new WeaponParts(380, 520, 10, 360, 300, 80, 400, true, true);

        internal float HandguardFront => HandguardFrontMm / 1000f;
        internal float BarrelFront => BarrelFrontMm / 1000f;
        internal float GasBlockZ => GasBlockZMm / 1000f;
        internal float GasTubeZ => GasTubeZMm / 1000f;
        internal float GasTubeLength => GasTubeLengthMm / 1000f;
        internal float FrontSightZ => FrontSightZMm / 1000f;
        internal int TopRailTeethLod1 => (TopRailTeeth + 1) / 2;

        /// <summary>Bounds keep every part inside the frozen rifle construction envelope and in front-to-back order.</summary>
        private void Validate()
        {
            static void Range(int value, int min, int max, string name)
            {
                if (value < min || value > max)
                    throw new FormatException("Weapon part " + name + " must be within " + min + ".." + max + ".");
            }
            Range(HandguardFrontMm, 300, 485, "handguardFrontMm");
            Range(BarrelFrontMm, HandguardFrontMm + 40, 640, "barrelFrontMm");
            Range(TopRailTeeth, 4, 15, "topRailTeeth");
            if (20 + (TopRailTeeth - 1) * 28 > HandguardFrontMm) throw new FormatException("Top rail must end before the handguard front.");
            Range(GasBlockZMm, 300, BarrelFrontMm - 100, "gasBlockZMm");
            Range(GasTubeLengthMm, 40, 160, "gasTubeLengthMm");
            Range(GasTubeZMm, 225 + GasTubeLengthMm / 2, GasBlockZMm - GasTubeLengthMm / 2, "gasTubeZMm");
            Range(FrontSightZMm, HandguardFrontMm - 100, BarrelFrontMm - 50, "frontSightZMm");
        }

        /// <summary>Canonical (ordinal key order) JSON of the parts object.</summary>
        public string ToCanonicalJson() =>
            "{\"barrelFrontMm\":" + I(BarrelFrontMm) + ",\"frontSightZMm\":" + I(FrontSightZMm) + ",\"gasBlockZMm\":" + I(GasBlockZMm) +
            ",\"gasTubeLengthMm\":" + I(GasTubeLengthMm) + ",\"gasTubeZMm\":" + I(GasTubeZMm) + ",\"handguardFrontMm\":" + I(HandguardFrontMm) +
            ",\"optic\":\"" + (Optic ? "compact" : "none") + "\",\"stock\":\"" + (Stock ? "fixed" : "none") + "\",\"topRailTeeth\":" + I(TopRailTeeth) + "}";

        private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    }

    public sealed class WeaponRecipe
    {
        public const string Schema = "shooter.recipe.weapon.v1";
        public const string GeneratorId = "geometry.weapon";
        /// <summary>Frozen silhouette generator (ADR-072). Shipped recipes stay on it.</summary>
        public const string GeneratorVersion = "1.0.5";
        /// <summary>Parametric part generator (ADR-082).</summary>
        public const string PartsVersion = "1.1.0";
        public static bool IsSupportedVersion(string? version) => version == GeneratorVersion || version == PartsVersion;

        public RecipeDocument Document { get; }
        public string Kind { get; }
        /// <summary>1.0.5: <c>standard</c> or <c>carbine</c>. 1.1.0: always <c>parts</c>.</summary>
        public string Silhouette { get; }
        /// <summary>Resolved rifle parts; null for pistols.</summary>
        public WeaponParts? Parts { get; }
        public string Version => Document.GeneratorVersion;
        private readonly byte[] source;
        public byte[] CanonicalSource => (byte[])source.Clone();
        public ContentId SourceHash { get; }
        private WeaponRecipe(RecipeDocument document, string kind, string silhouette, WeaponParts? parts, byte[] canonical)
        { Document = document; Kind = kind; Silhouette = silhouette; Parts = parts; source = canonical; SourceHash = ContentIdentity.SourceHash(Schema, canonical); }

        public static WeaponRecipe Parse(ReadOnlySpan<byte> source)
        {
            byte[] canonical = CanonicalJson.Encode(source);
            var root = JsonValue.Parse(canonical).Object;
            Fields(root, "schemaId", "recipeId", "generatorId", "generatorVersion", "seed", "parameters", "dependencies");
            string version = root["generatorVersion"].String;
            if (root["schemaId"].String != Schema || root["generatorId"].String != GeneratorId || !IsSupportedVersion(version))
                throw new FormatException("Unsupported weapon schema/generator version.");
            var parsed = Parameters(version, root["parameters"]);
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
                var document = new RecipeDocument(new DefinitionId(root["recipeId"].String), Schema, GeneratorId, version, seed,
                    ParameterBytes(version, parsed.kind, parsed.silhouette, parsed.parts), dependencies);
                return new WeaponRecipe(document, parsed.kind, parsed.silhouette, parsed.parts, canonical);
            }
            catch (ArgumentException error) { throw new FormatException("Invalid weapon identity/seed/dependency.", error); }
        }

        internal static (string kind, string silhouette, WeaponParts? parts) Validate(RecipeDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (!document.RecipeId.IsValid || document.SchemaId != Schema || document.GeneratorId != GeneratorId || !IsSupportedVersion(document.GeneratorVersion))
                throw new FormatException("Unsupported weapon identity/schema/generator.");
            ContentIdentity.ParseSeed(document.Seed);
            var parsed = Parameters(document.GeneratorVersion, JsonValue.Parse(document.Parameters));
            if (!document.Parameters.SequenceEqual(ParameterBytes(document.GeneratorVersion, parsed.kind, parsed.silhouette, parsed.parts)))
                throw new FormatException("Weapon parameters must be canonical.");
            return parsed;
        }

        private static (string kind, string silhouette, WeaponParts? parts) Parameters(string version, JsonValue value)
        {
            if (version == GeneratorVersion)
            {
                Fields(value.Object, "kind", "silhouette");
                string kind = value.Object["kind"].String;
                string silhouette = value.Object["silhouette"].String;
                if (kind != "rifle" && kind != "pistol") throw new FormatException("Weapon kind must be rifle or pistol.");
                if (silhouette != "standard" && silhouette != "carbine") throw new FormatException("Weapon silhouette must be standard or carbine.");
                if (silhouette == "carbine" && kind != "rifle") throw new FormatException("Carbine silhouette requires kind rifle.");
                return (kind, silhouette, kind == "rifle" ? (silhouette == "carbine" ? WeaponParts.Carbine : WeaponParts.Standard) : null);
            }
            Fields(value.Object, "kind", "parts");
            string partKind = value.Object["kind"].String;
            var parts = value.Object["parts"].Object;
            if (partKind == "pistol")
            {
                if (parts.Count != 0) throw new FormatException("Pistol recipes have no parts.");
                return (partKind, "parts", null);
            }
            if (partKind != "rifle") throw new FormatException("Weapon kind must be rifle or pistol.");
            Fields(parts, "handguardFrontMm", "barrelFrontMm", "topRailTeeth", "gasBlockZMm", "gasTubeZMm", "gasTubeLengthMm",
                "frontSightZMm", "stock", "optic");
            string stock = parts["stock"].String, optic = parts["optic"].String;
            if (stock != "fixed" && stock != "none") throw new FormatException("Weapon part stock must be fixed or none.");
            if (optic != "compact" && optic != "none") throw new FormatException("Weapon part optic must be compact or none.");
            var resolved = new WeaponParts(Int(parts, "handguardFrontMm"), Int(parts, "barrelFrontMm"), Int(parts, "topRailTeeth"),
                Int(parts, "gasBlockZMm"), Int(parts, "gasTubeZMm"), Int(parts, "gasTubeLengthMm"), Int(parts, "frontSightZMm"),
                stock == "fixed", optic == "compact");
            return (partKind, "parts", resolved);
        }

        private static int Int(IReadOnlyDictionary<string, JsonValue> values, string name) => values[name].Number;

        private static byte[] ParameterBytes(string version, string kind, string silhouette, WeaponParts? parts) =>
            Encoding.UTF8.GetBytes(version == GeneratorVersion
                ? "{\"kind\":\"" + kind + "\",\"silhouette\":\"" + silhouette + "\"}"
                : "{\"kind\":\"" + kind + "\",\"parts\":" + (parts == null ? "{}" : parts.ToCanonicalJson()) + "}");

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
