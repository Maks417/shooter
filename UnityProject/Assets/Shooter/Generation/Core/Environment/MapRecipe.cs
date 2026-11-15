#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    public sealed class MapRecipe
    {
        public const string CompoundSchema = "shooter.recipe.map-compound.v1";
        public const string DepotSchema = "shooter.recipe.map-depot.v1";
        public const string OilSchema = "shooter.recipe.map-oil.v1";
        public const string CompoundGenerator = "map.compound";
        public const string DepotGenerator = "map.depot";
        public const string OilGenerator = "map.oil";
        public const string PackageSchema = "shooter.map-package.v1";
        public const string MetadataSchema = "shooter.map-metadata.v1";
        public const string GeneratorVersion = "1.0.1";

        public RecipeDocument Document { get; }
        public byte[] CanonicalSource { get; }
        public ContentId SourceHash { get; }
        public string MapId { get; }

        private MapRecipe(RecipeDocument document, byte[] canonical, string mapId)
        {
            Document = document;
            CanonicalSource = canonical;
            MapId = mapId;
            SourceHash = ContentIdentity.SourceHash(document.SchemaId, canonical);
        }

        public static MapRecipe Parse(ReadOnlySpan<byte> source)
        {
            byte[] canonical = CanonicalJson.Encode(source);
            var root = JsonValue.Parse(canonical).Object;
            ExactFields(root, "recipe", "schemaId", "recipeId", "generatorId", "generatorVersion", "seed", "parameters", "dependencies");
            string schema = root["schemaId"].String;
            string generator = root["generatorId"].String;
            string version = root["generatorVersion"].String;
            ValidateIdentity(schema, generator, version);
            string seed = root["seed"].String;
            ContentIdentity.ParseSeed(seed);
            var parameters = root["parameters"].Object;
            ExactFields(parameters, "parameters", "mapId", "halfExtentMeters", "chunkSizeMeters", "navAdapterFormat", "navAdapterVersion");
            string mapId = parameters["mapId"].String;
            if (parameters["halfExtentMeters"].Number != 128) throw new FormatException("halfExtentMeters must be 128.");
            if (parameters["chunkSizeMeters"].Number != 32) throw new FormatException("chunkSizeMeters must be 32.");
            if (parameters["navAdapterFormat"].String != UnityNavMeshBakeCodec.AdapterFormat)
                throw new FormatException("navAdapterFormat must be unity.navmesh.bake.v1.");
            if (parameters["navAdapterVersion"].Number != 1) throw new FormatException("navAdapterVersion must be 1.");
            if (generator == CompoundGenerator && mapId != "training-compound") throw new FormatException("map.compound requires training-compound.");
            if (generator == DepotGenerator && mapId != "depot-yard") throw new FormatException("map.depot requires depot-yard.");
            if (generator == OilGenerator && mapId != "oil-depot") throw new FormatException("map.oil requires oil-depot.");
            var dependencyValues = root["dependencies"].Array;
            if (dependencyValues.Count > 256) throw new FormatException("dependencies: maximum count is 256.");
            var dependencies = new ContentId[dependencyValues.Count];
            for (int i = 0; i < dependencies.Length; i++)
            {
                dependencies[i] = new ContentId(dependencyValues[i].String);
                if (i > 0 && dependencies[i - 1].CompareTo(dependencies[i]) >= 0)
                    throw new FormatException("dependencies: hashes must be sorted and unique.");
            }
            byte[] parameterBytes = Encoding.UTF8.GetBytes(
                "{\"chunkSizeMeters\":32,\"halfExtentMeters\":128,\"mapId\":\"" + mapId +
                "\",\"navAdapterFormat\":\"unity.navmesh.bake.v1\",\"navAdapterVersion\":1}");
            var document = new RecipeDocument(new DefinitionId(root["recipeId"].String), schema, generator, version, seed, parameterBytes, dependencies);
            return new MapRecipe(document, canonical, mapId);
        }

        public static byte[] CreateSource(string mapId)
        {
            string schema = SchemaFor(mapId);
            string generator = GeneratorIdFor(mapId);
            string seed = ApprovedSeed(mapId).ToString(CultureInfo.InvariantCulture);
            string recipeId = mapId;
            string json = "{\"dependencies\":[],\"generatorId\":\"" + generator + "\",\"generatorVersion\":\"" + GeneratorVersion + "\",\"parameters\":{\"chunkSizeMeters\":32,\"halfExtentMeters\":128,\"mapId\":\"" +
                mapId + "\",\"navAdapterFormat\":\"unity.navmesh.bake.v1\",\"navAdapterVersion\":1},\"recipeId\":\"" + recipeId +
                "\",\"schemaId\":\"" + schema + "\",\"seed\":\"" + seed + "\"}";
            return CanonicalJson.Encode(Encoding.UTF8.GetBytes(json));
        }

        public static ulong ApprovedSeed(string mapId) => mapId switch
        {
            "training-compound" => 460072030UL,
            "depot-yard" => 460072031UL,
            "oil-depot" => 460072032UL,
            _ => throw new ArgumentOutOfRangeException(nameof(mapId))
        };

        public static string GeneratorIdFor(string mapId) => mapId switch
        {
            "training-compound" => CompoundGenerator,
            "depot-yard" => DepotGenerator,
            "oil-depot" => OilGenerator,
            _ => throw new ArgumentOutOfRangeException(nameof(mapId))
        };

        public static string SchemaFor(string mapId) => mapId switch
        {
            "training-compound" => CompoundSchema,
            "depot-yard" => DepotSchema,
            "oil-depot" => OilSchema,
            _ => throw new ArgumentOutOfRangeException(nameof(mapId))
        };

        public static string DisplayName(string mapId) => mapId switch
        {
            "training-compound" => "Training Compound",
            "depot-yard" => "Depot Yard",
            "oil-depot" => "Oil Depot",
            _ => throw new ArgumentOutOfRangeException(nameof(mapId))
        };

        public static string Description(string mapId) => mapId switch
        {
            "training-compound" => "Tight courtyards and training lanes for local TDM.",
            "depot-yard" => "Steel yards, cargo lanes, and hard cover for local TDM.",
            "oil-depot" => "Petroleum tanks, pump house, and elevated catwalks for local TDM.",
            _ => throw new ArgumentOutOfRangeException(nameof(mapId))
        };

        public static bool IsApprovedMapId(string mapId)
            => mapId == "training-compound" || mapId == "depot-yard" || mapId == "oil-depot";

        private static void ValidateIdentity(string schema, string generator, string version)
        {
            if (schema == CompoundSchema)
            {
                if (generator != CompoundGenerator) throw new FormatException("generatorId mismatch for compound schema.");
            }
            else if (schema == DepotSchema)
            {
                if (generator != DepotGenerator) throw new FormatException("generatorId mismatch for depot schema.");
            }
            else if (schema == OilSchema)
            {
                if (generator != OilGenerator) throw new FormatException("generatorId mismatch for oil schema.");
            }
            else throw new FormatException("Unsupported map recipe schema.");
            if (version != GeneratorVersion) throw new FormatException("Unsupported map generator version.");
        }

        private static void ExactFields(IReadOnlyDictionary<string, JsonValue> values, string path, params string[] expected)
        {
            if (values.Count != expected.Length) throw new FormatException(path + ": missing or unknown field.");
            foreach (string key in expected)
                if (!values.ContainsKey(key)) throw new FormatException(path + "." + key + ": required field missing.");
        }
    }

    public sealed class MapRecipeValidator : IRecipeValidator
    {
        public ValidationReport Validate(RecipeDocument recipe)
        {
            try
            {
                string Q(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                string reconstructed = "{\"schemaId\":" + Q(recipe.SchemaId) + ",\"recipeId\":" + Q(recipe.RecipeId.ToString()) +
                    ",\"generatorId\":" + Q(recipe.GeneratorId) + ",\"generatorVersion\":" + Q(recipe.GeneratorVersion) + ",\"seed\":" + Q(recipe.Seed) +
                    ",\"parameters\":" + Encoding.UTF8.GetString(recipe.Parameters) + ",\"dependencies\":[]}";
                MapRecipe.Parse(Encoding.UTF8.GetBytes(reconstructed));
                return new ValidationReport(true, Array.Empty<Diagnostic>());
            }
            catch (Exception error) when (error is FormatException || error is ArgumentException)
            {
                string message = error.Message.Length <= 512 ? error.Message : error.Message.Substring(0, 512);
                return new ValidationReport(false, new[] { new Diagnostic(ErrorCode.InvalidInput, "recipe", message) });
            }
        }
    }
}
