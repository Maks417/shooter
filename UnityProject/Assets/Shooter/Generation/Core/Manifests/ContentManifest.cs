#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation
{
    public sealed class ManifestArtifact
    {
        public ContentKind Kind { get; }
        public string Variant { get; }
        public string Path { get; }
        public ContentId Sha256 { get; }
        public int Bytes { get; }
        public uint FormatVersion => 1;
        public ManifestArtifact(ContentKind kind, string variant, string path, ContentId sha256, int bytes)
        {
            if (!Enum.IsDefined(typeof(ContentKind), kind)) throw new FormatException("Unknown artifact kind.");
            ValidateVariant(variant);
            if (path != kind.ToString().ToLowerInvariant() + "-" + variant + ".bin") throw new FormatException("Noncanonical artifact path.");
            if (bytes < 116 || bytes > ContractVersion.MaxArtifactBytes) throw new FormatException("Artifact byte limit.");
            Kind = kind; Variant = variant; Path = path; Sha256 = sha256; Bytes = bytes;
        }
        private static void ValidateVariant(string value)
        {
            if (value == null || value.Length == 0 || value.Length > 64) throw new FormatException("Invalid variant.");
            foreach (char c in value) if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_')) throw new FormatException("Invalid variant.");
        }
    }

    /// <summary>Production manifest v1, separate from the frozen nonshipping C0 example.</summary>
    public sealed class ContentManifest
    {
        public const string Schema = "shooter.content.manifest.v1";
        public ContentId ContentId { get; }
        public ContentId SourceHash { get; }
        public string RecipeId { get; }
        public string GeneratorId { get; }
        public string GeneratorVersion { get; }
        public ulong Seed { get; }
        public ContentId ToolchainHash { get; }
        public string BuildPlatform { get; }
        public string VisualProfile { get; }
        public FrozenList<ContentId> Dependencies { get; }
        public FrozenList<ManifestArtifact> Artifacts { get; }
        public Aabb Bounds { get; }
        public int VertexCount { get; }
        public int TriangleCount { get; }
        public int SubmeshCount { get; }
        public int BoneCount { get; }
        private readonly int[] minMm, maxMm;
        public ContentId GeneratorHash => GeneratorFingerprint.Compute(GeneratorId, GeneratorVersion);

        private ContentManifest(ContentId contentId, ContentId sourceHash, string recipeId, string generatorId, string generatorVersion,
            ulong seed, ContentId toolchainHash, string platform, string profile, IReadOnlyList<ContentId> dependencies,
            IReadOnlyList<ManifestArtifact> artifacts, int[] min, int[] max, int vertices, int triangles, int submeshes, int bones)
        {
            _ = new DefinitionId(recipeId); Text(generatorId); Text(generatorVersion); Text(platform); Text(profile);
            if (dependencies.Count > 256 || artifacts.Count == 0 || artifacts.Count > 256) throw new FormatException("Manifest count limits.");
            for (int i = 1; i < dependencies.Count; i++) if (dependencies[i - 1].CompareTo(dependencies[i]) >= 0) throw new FormatException("Dependencies must be sorted and unique.");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
            bool gameplay = false, visual = false;
            for (int i = 0; i < artifacts.Count; i++)
            {
                var a = artifacts[i]; total += a.Bytes;
                if (!paths.Add(a.Path)) throw new FormatException("Duplicate artifact path.");
                if (i > 0 && Compare(artifacts[i - 1], a) >= 0) throw new FormatException("Artifacts must be sorted and unique.");
                if (a.Kind == ContentKind.Collision || a.Kind == ContentKind.Navigation) gameplay = true; else visual = true;
            }
            if (total > 256L * 1024 * 1024 || gameplay && (visual || profile != "gameplay") || !gameplay && profile == "gameplay")
                throw new FormatException("Content size or gameplay/visual key separation violated.");
            if (vertices < 0 || triangles < 0 || submeshes < 0 || bones < 0) throw new FormatException("Negative content counts.");
            ContentId computed = ContentIdentity.ContentKey(sourceHash, generatorId, generatorVersion, seed, dependencies, platform, profile, 1, toolchainHash);
            if (computed != contentId) throw new FormatException("Manifest request identity mismatch.");
            minMm = (int[])min.Clone(); maxMm = (int[])max.Clone();
            Bounds = new Aabb(new Float3(min[0] / 1000f, min[1] / 1000f, min[2] / 1000f), new Float3(max[0] / 1000f, max[1] / 1000f, max[2] / 1000f));
            ContentId = contentId; SourceHash = sourceHash; RecipeId = recipeId; GeneratorId = generatorId; GeneratorVersion = generatorVersion;
            Seed = seed; ToolchainHash = toolchainHash; BuildPlatform = platform; VisualProfile = profile;
            Dependencies = new FrozenList<ContentId>(dependencies, 256); Artifacts = new FrozenList<ManifestArtifact>(artifacts, 256);
            VertexCount = vertices; TriangleCount = triangles; SubmeshCount = submeshes; BoneCount = bones;
        }

        public static ContentManifest Create(GenerationRequest request, ContentId sourceHash, GenerationResult result)
        {
            if (result.Error != ErrorCode.None) throw new ArgumentException("Failed generation cannot publish.");
            var artifacts = new List<ManifestArtifact>(); int vertices = 0, triangles = 0, submeshes = 0, bones = 0;
            ContentId generator = GeneratorFingerprint.Compute(request.Recipe.GeneratorId, request.Recipe.GeneratorVersion);
            foreach (var a in result.Artifacts)
            {
                if (a.FormatVersion != 1) throw new FormatException("Unsupported artifact format.");
                byte[] body = ArtifactCodec.Decode(a.Payload, a.Kind, generator, request.ToolchainHash);
                ArtifactCodec.ValidateTyped(a.Kind, body);
                if (a.Kind == ContentKind.Mesh)
                {
                    var mesh = GeometryCodec.DecodeMesh(body);
                    vertices = checked(vertices + mesh.Positions.Count); triangles = checked(triangles + mesh.Indices.Count / 3); submeshes = checked(submeshes + mesh.Submeshes.Count);
                }
                if (a.Kind == ContentKind.Skeleton) bones = checked(bones + GeometryCodec.DecodeSkeleton(body).Joints.Count);
                string path = a.Kind.ToString().ToLowerInvariant() + "-" + a.Variant + ".bin";
                artifacts.Add(new ManifestArtifact(a.Kind, a.Variant, path, a.Sha256, a.Payload.Length));
            }
            artifacts.Sort(Compare);
            int[] Min(Float3 v) => new[] { Quantize(v.X, false), Quantize(v.Y, false), Quantize(v.Z, false) };
            int[] Max(Float3 v) => new[] { Quantize(v.X, true), Quantize(v.Y, true), Quantize(v.Z, true) };
            var recipe = request.Recipe;
            var id = ContentIdentity.ContentKey(sourceHash, recipe.GeneratorId, recipe.GeneratorVersion, recipe.SeedValue, recipe.Dependencies, request.OutputPlatform, request.VisualProfile, 1, request.ToolchainHash);
            return new ContentManifest(id, sourceHash, recipe.RecipeId.ToString(), recipe.GeneratorId, recipe.GeneratorVersion, recipe.SeedValue,
                request.ToolchainHash, request.OutputPlatform, request.VisualProfile, recipe.Dependencies, artifacts, Min(result.Bounds.Min), Max(result.Bounds.Max), vertices, triangles, submeshes, bones);
        }

        public byte[] Encode()
        {
            string Ints(int[] v) => "[" + string.Join(",", v.Select(n => n.ToString(CultureInfo.InvariantCulture))) + "]";
            string deps = string.Join(",", Dependencies.Select(d => Q(d.ToString())));
            string artifacts = string.Join(",", Artifacts.Select(a => "{\"kind\":" + Q(a.Kind.ToString().ToLowerInvariant()) + ",\"variant\":" + Q(a.Variant) + ",\"path\":" + Q(a.Path) + ",\"sha256\":" + Q(a.Sha256.ToString()) + ",\"compressedBytes\":" + a.Bytes.ToString(CultureInfo.InvariantCulture) + ",\"uncompressedBytes\":" + a.Bytes.ToString(CultureInfo.InvariantCulture) + ",\"formatVersion\":1,\"compression\":\"none\"}"));
            string json = "{\"manifestSchema\":" + Q(Schema) + ",\"contentId\":" + Q(ContentId.ToString()) + ",\"sourceHash\":" + Q(SourceHash.ToString()) +
                ",\"recipeId\":" + Q(RecipeId) + ",\"generatorId\":" + Q(GeneratorId) + ",\"generatorVersion\":" + Q(GeneratorVersion) +
                ",\"seed\":" + Q(Seed.ToString(CultureInfo.InvariantCulture)) + ",\"toolchainHash\":" + Q(ToolchainHash.ToString()) + ",\"buildPlatform\":" + Q(BuildPlatform) +
                ",\"visualProfile\":" + Q(VisualProfile) + ",\"dependencies\":[" + deps + "],\"artifacts\":[" + artifacts + "],\"bounds\":{\"minMm\":" + Ints(minMm) + ",\"maxMm\":" + Ints(maxMm) +
                "},\"counts\":{\"vertices\":" + VertexCount.ToString(CultureInfo.InvariantCulture) + ",\"triangles\":" + TriangleCount.ToString(CultureInfo.InvariantCulture) + ",\"submeshes\":" + SubmeshCount.ToString(CultureInfo.InvariantCulture) + ",\"bones\":" + BoneCount.ToString(CultureInfo.InvariantCulture) +
                "},\"materialLayerIds\":[],\"provenance\":{\"sourceKind\":\"numeric-recipes\",\"policy\":\"procedural-v1\"},\"validationSummary\":\"validated-v1\"}";
            return CanonicalJson.Encode(Encoding.UTF8.GetBytes(json));
        }

        public static ContentManifest Decode(ReadOnlySpan<byte> bytes)
        {
            var root = JsonValue.Parse(bytes).Object;
            Keys(root, "manifestSchema", "contentId", "sourceHash", "recipeId", "generatorId", "generatorVersion", "seed", "toolchainHash", "buildPlatform", "visualProfile", "dependencies", "artifacts", "bounds", "counts", "materialLayerIds", "provenance", "validationSummary");
            if (root["manifestSchema"].String != Schema || root["validationSummary"].String != "validated-v1") throw new FormatException("Unsupported manifest version.");
            var provenance = root["provenance"].Object; Keys(provenance, "sourceKind", "policy");
            if (provenance["sourceKind"].String != "numeric-recipes" || provenance["policy"].String != "procedural-v1" || root["materialLayerIds"].Array.Count != 0) throw new FormatException("Unsupported provenance/material registry.");
            var dependencies = root["dependencies"].Array.Select(d => new ContentId(d.String)).ToArray();
            var artifacts = new List<ManifestArtifact>();
            foreach (var item in root["artifacts"].Array)
            {
                var a = item.Object; Keys(a, "kind", "variant", "path", "sha256", "compressedBytes", "uncompressedBytes", "formatVersion", "compression");
                if (a["formatVersion"].Number != 1 || a["compression"].String != "none" || a["compressedBytes"].Number != a["uncompressedBytes"].Number) throw new FormatException("Unsupported artifact format/compression.");
                ContentKind kind = ParseKind(a["kind"].String);
                artifacts.Add(new ManifestArtifact(kind, a["variant"].String, a["path"].String, new ContentId(a["sha256"].String), a["uncompressedBytes"].Number));
            }
            var bounds = root["bounds"].Object; Keys(bounds, "minMm", "maxMm");
            int[] Vec(JsonValue value) { if (value.Array.Count != 3) throw new FormatException("Invalid bounds."); return value.Array.Select(n => n.Number).ToArray(); }
            var counts = root["counts"].Object; Keys(counts, "vertices", "triangles", "submeshes", "bones");
            return new ContentManifest(new ContentId(root["contentId"].String), new ContentId(root["sourceHash"].String), root["recipeId"].String,
                root["generatorId"].String, root["generatorVersion"].String, ContentIdentity.ParseSeed(root["seed"].String), new ContentId(root["toolchainHash"].String),
                root["buildPlatform"].String, root["visualProfile"].String, dependencies, artifacts, Vec(bounds["minMm"]), Vec(bounds["maxMm"]), counts["vertices"].Number, counts["triangles"].Number, counts["submeshes"].Number, counts["bones"].Number);
        }

        private static ContentKind ParseKind(string value)
        {
            foreach (ContentKind kind in Enum.GetValues(typeof(ContentKind))) if (kind.ToString().ToLowerInvariant() == value) return kind;
            throw new FormatException("Unknown artifact kind.");
        }
        private static int Compare(ManifestArtifact a, ManifestArtifact b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : string.CompareOrdinal(a.Variant, b.Variant);
        private static int Quantize(float value, bool upper)
        {
            double mm = value * 1000.0; double result = upper ? Math.Ceiling(mm) : Math.Floor(mm);
            if (result < int.MinValue || result > int.MaxValue) throw new FormatException("Bounds exceed millimeter encoding.");
            return (int)result;
        }
        private static void Text(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128) throw new FormatException("Invalid manifest identifier.");
            foreach (char c in value) if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '.' || c == '_' || c == '-' || c == '/')) throw new FormatException("Invalid manifest identifier.");
        }
        private static string Q(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        private static void Keys(IReadOnlyDictionary<string, JsonValue> value, params string[] keys)
        {
            if (value.Count != keys.Length || keys.Any(k => !value.ContainsKey(k))) throw new FormatException("Missing or unknown manifest fields.");
        }
    }
}
