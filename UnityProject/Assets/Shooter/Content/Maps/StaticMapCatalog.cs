#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;

namespace Shooter.Content.Maps
{
    public sealed class StaticMapCatalog : IMapCatalog
    {
        private readonly Dictionary<string, MapCatalogEntry> entries = new Dictionary<string, MapCatalogEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, MapGameplayDefinition> gameplay = new Dictionary<string, MapGameplayDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, ContentId> gameplayRoots = new Dictionary<string, ContentId>(StringComparer.Ordinal);
        private readonly Dictionary<string, ContentId> lowVisualRoots = new Dictionary<string, ContentId>(StringComparer.Ordinal);
        private readonly Dictionary<string, ContentId> balancedVisualRoots = new Dictionary<string, ContentId>(StringComparer.Ordinal);
        private readonly Dictionary<string, ContentId> highVisualRoots = new Dictionary<string, ContentId>(StringComparer.Ordinal);

        public StaticMapCatalog(IReadOnlyList<MapCatalogEntry> catalog, IReadOnlyList<MapGameplayDefinition> maps)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (maps == null) throw new ArgumentNullException(nameof(maps));
            foreach (var entry in catalog)
            {
                entries[entry.MapId.Value] = entry;
                gameplayRoots[entry.MapId.Value] = entry.GameplayRootHash;
                lowVisualRoots[entry.MapId.Value] = entry.LowVisualRootHash;
                balancedVisualRoots[entry.MapId.Value] = entry.BalancedVisualRootHash;
                highVisualRoots[entry.MapId.Value] = entry.HighVisualRootHash;
            }
            foreach (var map in maps) gameplay[map.MapId.Value] = map;
        }

        public int Count => entries.Count;
        public IReadOnlyDictionary<string, ContentId> GameplayRootHashes => gameplayRoots;
        public IReadOnlyDictionary<string, ContentId> LowVisualRootHashes => lowVisualRoots;
        public IReadOnlyDictionary<string, ContentId> BalancedVisualRootHashes => balancedVisualRoots;
        public IReadOnlyDictionary<string, ContentId> HighVisualRootHashes => highVisualRoots;

        public bool TryGet(MapId mapId, out MapCatalogEntry entry) => entries.TryGetValue(mapId.Value, out entry!);

        public bool TryGetGameplay(MapId mapId, out MapGameplayDefinition definition) => gameplay.TryGetValue(mapId.Value, out definition!);

        public GameOperationResult RejectUnknown(MapId mapId, ModeId modeId, byte teamSize)
        {
            if (!TryGet(mapId, out var entry))
                return new GameOperationResult(GameErrorCode.InvalidRequest, "unknown-map", "Unknown map.");
            bool modeOk = false;
            for (int i = 0; i < entry.SupportedModes.Count; i++) if (entry.SupportedModes[i] == modeId) { modeOk = true; break; }
            if (!modeOk) return new GameOperationResult(GameErrorCode.InvalidRequest, "unknown-mode", "Unsupported mode.");
            bool sizeOk = false;
            for (int i = 0; i < entry.SupportedTeamSizes.Count; i++) if (entry.SupportedTeamSizes[i] == teamSize) { sizeOk = true; break; }
            if (!sizeOk) return new GameOperationResult(GameErrorCode.InvalidRequest, "bad-team-size", "Unsupported team size.");
            return GameOperationResult.Ok;
        }

        /// <summary>SHA256 of labeled bytes — retained only for negative tests; release catalog must use package roots.</summary>
        public static ContentId Hash(string label)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return new ContentId(sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes(label)));
        }

        private static readonly object CatalogGate = new object();
        private static ContentId? CachedToolchain;
        private static StaticMapCatalog? CachedCatalog;

        /// <summary>
        /// Shipping path: load verified <c>catalog.json</c> bytes only.
        /// Never invokes planners or <see cref="MapPackageBuilder"/>.
        /// </summary>
        public static bool TryLoadPublished(string cacheRoot, ContentId toolchainHash, out StaticMapCatalog catalog, out string diagnostic)
        {
            catalog = null!;
            if (string.IsNullOrWhiteSpace(cacheRoot))
            {
                diagnostic = "cache-root-required";
                return false;
            }
            if (!ReleaseCatalogDocument.TryOpen(cacheRoot, toolchainHash, out byte[] bytes, out diagnostic))
                return false;
            if (!TryParse(bytes, toolchainHash, out catalog, out diagnostic))
                return false;
            diagnostic = "ok";
            return true;
        }

        /// <summary>
        /// Compile/test helper: build catalog via planners/builders then parse the same bytes shipping loads.
        /// Must not be called from the release player composition path.
        /// </summary>
        public static StaticMapCatalog CompileSeed(ContentId? toolchainHash = null)
        {
            var toolchain = toolchainHash ?? ResolveToolchainHashForTests();
            lock (CatalogGate)
            {
                if (CachedCatalog != null && CachedToolchain is ContentId cached && cached == toolchain)
                    return CachedCatalog;
            }

            byte[] bytes = ReleaseCatalogDocument.BuildBytes(toolchain, ReleaseCatalogDocument.RequiredMaps);
            if (!TryParse(bytes, toolchain, out var created, out string diagnostic))
                throw new InvalidOperationException("Compiled release catalog failed to parse: " + diagnostic);

            lock (CatalogGate)
            {
                CachedToolchain = toolchain;
                CachedCatalog = created;
            }
            return created;
        }

        /// <summary>Obsolete shipping name — use <see cref="TryLoadPublished"/> in players; tests use <see cref="CompileSeed"/>.</summary>
        public static StaticMapCatalog CreateReleaseSeed(ContentId? toolchainHash = null) => CompileSeed(toolchainHash);

        public static bool TryParse(byte[] catalogBytes, ContentId expectedToolchain, out StaticMapCatalog catalog, out string diagnostic)
        {
            catalog = null!;
            if (!ReleaseCatalogDocument.TryValidate(catalogBytes, expectedToolchain, out diagnostic))
                return false;
            try
            {
                var root = MapJsonValue.Parse(catalogBytes).Object;
                var mapsJson = root["maps"].Array;
                var catalogEntries = new List<MapCatalogEntry>(mapsJson.Count);
                var gameplayDefs = new List<MapGameplayDefinition>(mapsJson.Count);
                for (int i = 0; i < mapsJson.Count; i++)
                {
                    var map = mapsJson[i].Object;
                    var mapId = new MapId(map["mapId"].String);
                    var modesJson = map["supportedModes"].Array;
                    var modes = new ModeId[modesJson.Count];
                    for (int m = 0; m < modesJson.Count; m++) modes[m] = new ModeId(modesJson[m].String);
                    var sizesJson = map["supportedTeamSizes"].Array;
                    var sizes = new byte[sizesJson.Count];
                    for (int s = 0; s < sizesJson.Count; s++) sizes[s] = checked((byte)sizesJson[s].Int32);
                    ulong seed = ulong.Parse(map["recipeSeed"].String, CultureInfo.InvariantCulture);
                    catalogEntries.Add(new MapCatalogEntry(
                        mapId,
                        map["displayName"].String,
                        map["description"].String,
                        modes,
                        sizes,
                        seed,
                        map["recipeVersion"].String,
                        new ContentId(map["gameplayRootSha256"].String),
                        new ContentId(map["lowVisualRootSha256"].String),
                        new ContentId(map["balancedVisualRootSha256"].String),
                        new ContentId(map["highVisualRootSha256"].String)));
                    gameplayDefs.Add(ParseGameplay(mapId, map["gameplay"].Object));
                }
                catalog = new StaticMapCatalog(catalogEntries, gameplayDefs);
                diagnostic = "ok";
                return true;
            }
            catch (Exception error)
            {
                diagnostic = error.Message.Length <= 200 ? error.Message : error.Message.Substring(0, 200);
                catalog = null!;
                return false;
            }
        }

        private static MapGameplayDefinition ParseGameplay(MapId mapId, IReadOnlyDictionary<string, MapJsonValue> gameplay)
        {
            var min = ReadVec3(gameplay["boundsMinMeters"].Array);
            var max = ReadVec3(gameplay["boundsMaxMeters"].Array);
            var spawnJson = gameplay["spawnSlots"].Array;
            var spawns = new List<SpawnSlot>(spawnJson.Count);
            for (int i = 0; i < spawnJson.Count; i++)
            {
                var slot = spawnJson[i].Object;
                spawns.Add(new SpawnSlot(
                    checked((uint)slot["slotId"].Int32),
                    checked((byte)slot["team"].Int32),
                    ReadVec3(slot["positionMeters"].Array),
                    (float)slot["yawRadians"].Number));
            }
            var patrol = ReadVec3List(gameplay["patrolDestinationsMeters"].Array);
            var combat = ReadVec3List(gameplay["combatDestinationsMeters"].Array);
            return new MapGameplayDefinition(
                mapId,
                checked((uint)gameplay["collisionRevision"].Int32),
                new ContentId(gameplay["collisionHash"].String),
                checked((uint)gameplay["navRevision"].Int32),
                new ContentId(gameplay["navHash"].String),
                new Aabb(min, max),
                spawns,
                patrol,
                combat);
        }

        private static Float3 ReadVec3(IReadOnlyList<MapJsonValue> values)
        {
            if (values.Count != 3) throw new FormatException("expected xyz vector");
            return new Float3((float)values[0].Number, (float)values[1].Number, (float)values[2].Number);
        }

        private static List<Float3> ReadVec3List(IReadOnlyList<MapJsonValue> values)
        {
            var list = new List<Float3>(values.Count);
            for (int i = 0; i < values.Count; i++) list.Add(ReadVec3(values[i].Array));
            return list;
        }

        /// <summary>Pure-test toolchain probe only. Shipping Bootstrap always hashes the packaged lock file.</summary>
        public static ContentId ResolveToolchainHashForTests()
        {
            string? probe = Environment.GetEnvironmentVariable("SHOOTER_TOOLCHAIN_LOCK");
            if (!string.IsNullOrWhiteSpace(probe) && File.Exists(probe))
                return ContentIdentity.Hash(File.ReadAllBytes(probe));
            return ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("shooter.toolchain.placeholder.v1"));
        }

        /// <summary>Retained name for older tests; prefer <see cref="ResolveToolchainHashForTests"/>.</summary>
        public static ContentId ResolveToolchainHash() => ResolveToolchainHashForTests();
    }
}
