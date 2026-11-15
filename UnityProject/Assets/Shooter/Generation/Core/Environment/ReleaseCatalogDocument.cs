#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// ADR-038 persisted release catalog: catalog entries + gameplay sidecars written at publish time.
    /// Shipping players load these bytes only; they must not invoke planners or MapPackageBuilder.
    /// </summary>
    public static class ReleaseCatalogDocument
    {
        public const string Schema = "shooter.release-catalog.v1";
        public const string FileName = "catalog.json";
        public static readonly string[] RequiredMaps = { "training-compound", "depot-yard" };
        public static readonly string[] RequiredProfiles = { "gameplay", "low", "balanced", "high" };

        public static string CatalogPath(string cacheRoot) =>
            Path.Combine(Path.GetFullPath(cacheRoot), FileName);

        /// <summary>Compile-time encode: planners/builders allowed here only.</summary>
        public static byte[] BuildBytes(ContentId toolchainHash, IReadOnlyList<string>? mapIds = null)
        {
            var maps = mapIds == null || mapIds.Count == 0 ? RequiredMaps : mapIds;
            var mapEntries = new List<object?>(maps.Count);
            for (int i = 0; i < maps.Count; i++)
            {
                string mapId = maps[i];
                if (mapId != "training-compound" && mapId != "depot-yard")
                    throw new ArgumentOutOfRangeException(nameof(mapIds), "unknown-map:" + mapId);
                ulong seed = MapRecipe.ApprovedSeed(mapId);
                var gameplay = BuildGameplaySidecar(mapId, seed);
                var roots = new Dictionary<string, ContentId>(StringComparer.Ordinal);
                for (int p = 0; p < RequiredProfiles.Length; p++)
                {
                    var package = MapPackageBuilder.Build(mapId, RequiredProfiles[p], toolchainHash);
                    roots[RequiredProfiles[p]] = package.PackageRootHash;
                }

                mapEntries.Add(new Dictionary<string, object?>
                {
                    ["mapId"] = mapId,
                    ["displayName"] = mapId == "depot-yard" ? "Depot Yard" : "Training Compound",
                    ["description"] = mapId == "depot-yard"
                        ? "Distinct depot grammar for offline TDM."
                        : "Bounded compound for offline TDM.",
                    ["supportedModes"] = new List<object?> { "team-deathmatch" },
                    ["supportedTeamSizes"] = new List<object?> { 5, 6 },
                    ["recipeSeed"] = seed.ToString(CultureInfo.InvariantCulture),
                    ["recipeVersion"] = MapRecipe.GeneratorIdFor(mapId) + "." + MapRecipe.GeneratorVersion,
                    ["gameplayRootSha256"] = roots["gameplay"].ToString(),
                    ["lowVisualRootSha256"] = roots["low"].ToString(),
                    ["balancedVisualRootSha256"] = roots["balanced"].ToString(),
                    ["highVisualRootSha256"] = roots["high"].ToString(),
                    ["gameplay"] = gameplay
                });
            }

            return MapDocumentJson.EncodeObject(new Dictionary<string, object?>
            {
                ["catalogSchema"] = Schema,
                ["toolchainHash"] = toolchainHash.ToString(),
                ["maps"] = mapEntries
            });
        }

        public static void WriteAtomic(string cacheRoot, ContentId toolchainHash, IReadOnlyList<string>? mapIds = null)
        {
            string root = Path.GetFullPath(cacheRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Directory.CreateDirectory(root);
            byte[] bytes = BuildBytes(toolchainHash, mapIds);
            string path = CatalogPath(root);
            string stage = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var stream = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(bytes, 0, bytes.Length);
            if (File.Exists(path)) File.Replace(stage, path, null);
            else File.Move(stage, path);
        }

        public static bool TryOpen(string cacheRoot, ContentId expectedToolchain, out byte[] catalogBytes, out string diagnostic)
        {
            catalogBytes = Array.Empty<byte>();
            diagnostic = "missing";
            string path = CatalogPath(cacheRoot);
            if (!File.Exists(path)) return false;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (!TryValidate(bytes, expectedToolchain, out diagnostic)) return false;
                catalogBytes = bytes;
                diagnostic = "ok";
                return true;
            }
            catch (Exception error)
            {
                diagnostic = Safe(error.Message);
                return false;
            }
        }

        public static bool TryValidate(byte[] bytes, ContentId expectedToolchain, out string diagnostic)
        {
            diagnostic = "invalid";
            if (bytes == null || bytes.Length == 0)
            {
                diagnostic = "empty-catalog";
                return false;
            }
            try
            {
                var root = MapJsonValue.Parse(bytes).Object;
                if (root["catalogSchema"].String != Schema)
                {
                    diagnostic = "catalog-schema-mismatch";
                    return false;
                }
                if (root["toolchainHash"].String != expectedToolchain.ToString())
                {
                    diagnostic = "toolchain-mismatch";
                    return false;
                }
                var maps = root["maps"].Array;
                if (maps.Count < 1)
                {
                    diagnostic = "maps-empty";
                    return false;
                }
                for (int i = 0; i < maps.Count; i++)
                {
                    var map = maps[i].Object;
                    string mapId = map["mapId"].String;
                    if (mapId != "training-compound" && mapId != "depot-yard")
                    {
                        diagnostic = "unknown-map:" + mapId;
                        return false;
                    }
                    _ = map["gameplayRootSha256"].String;
                    _ = map["lowVisualRootSha256"].String;
                    _ = map["balancedVisualRootSha256"].String;
                    _ = map["highVisualRootSha256"].String;
                    _ = map["gameplay"].Object;
                }
                diagnostic = "ok";
                return true;
            }
            catch (Exception error)
            {
                diagnostic = Safe(error.Message);
                return false;
            }
        }

        private static Dictionary<string, object?> BuildGameplaySidecar(string mapId, ulong seed)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            ProductMapPlanner.ValidateOrThrow(mapId, plan);

            var candidates = MapSpawnLayout.BuildBothTeams(plan);
            var spawns = new List<object?>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                spawns.Add(new Dictionary<string, object?>
                {
                    ["slotId"] = checked((int)c.SlotId),
                    ["team"] = (int)c.Team,
                    ["positionMeters"] = Vec(Feet(c.Position)),
                    ["yawRadians"] = c.Yaw
                });
            }

            var patrol = Subsample(plan.RouteCorners, 64);
            if (patrol.Count == 0) patrol.Add(Vec(Feet(plan.PlayerSpawn)));
            var combat = new List<object?>();
            var destinations = ProductMapPlanner.CombatDestinations(plan);
            for (int i = 0; i < destinations.Count; i++) combat.Add(Vec(Feet(destinations[i])));

            return new Dictionary<string, object?>
            {
                ["collisionRevision"] = checked((int)plan.CollisionRevision),
                ["collisionHash"] = plan.CollisionHash.ToString(),
                ["navRevision"] = checked((int)plan.NavRevision),
                ["navHash"] = plan.NavHash.ToString(),
                ["boundsMinMeters"] = new List<object?> { -CompoundPlan.MapHalfExtent, -2f, -CompoundPlan.MapHalfExtent },
                ["boundsMaxMeters"] = new List<object?> { CompoundPlan.MapHalfExtent, 40f, CompoundPlan.MapHalfExtent },
                ["spawnSlots"] = spawns,
                ["patrolDestinationsMeters"] = patrol,
                ["combatDestinationsMeters"] = combat
            };
        }

        private static Float3 Feet(Float3 p) => new Float3(p.X, MathF.Max(0f, p.Y), p.Z);

        private static List<object?> Subsample(IReadOnlyList<Float3> source, int max)
        {
            var list = new List<object?>(Math.Min(max, source.Count));
            if (source.Count == 0) return list;
            if (source.Count <= max)
            {
                for (int i = 0; i < source.Count; i++) list.Add(Vec(Feet(source[i])));
                return list;
            }
            for (int i = 0; i < max; i++)
            {
                int idx = (int)((long)i * (source.Count - 1) / (max - 1));
                list.Add(Vec(Feet(source[idx])));
            }
            return list;
        }

        private static List<object?> Vec(Float3 v) => new List<object?> { v.X, v.Y, v.Z };

        private static string Safe(string message) => message.Length <= 200 ? message : message.Substring(0, 200);
    }
}
