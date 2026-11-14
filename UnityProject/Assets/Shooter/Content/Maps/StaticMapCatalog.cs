#nullable enable
using System;
using System.Collections.Generic;
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

        public StaticMapCatalog(IReadOnlyList<MapCatalogEntry> catalog, IReadOnlyList<MapGameplayDefinition> maps)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (maps == null) throw new ArgumentNullException(nameof(maps));
            foreach (var entry in catalog)
            {
                entries[entry.MapId.Value] = entry;
                gameplayRoots[entry.MapId.Value] = entry.GameplayRootHash;
            }
            foreach (var map in maps) gameplay[map.MapId.Value] = map;
        }

        public int Count => entries.Count;
        public IReadOnlyDictionary<string, ContentId> GameplayRootHashes => gameplayRoots;

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

        public static StaticMapCatalog CreateReleaseSeed(ContentId? toolchainHash = null)
        {
            var toolchain = toolchainHash ?? ResolveToolchainHash();
            lock (CatalogGate)
            {
                if (CachedCatalog != null && CachedToolchain is ContentId cached && cached == toolchain)
                    return CachedCatalog;
            }

            var modes = new[] { WellKnownModes.TeamDeathmatch };
            var sizes = new byte[] { 5, 6 };
            var compoundGameplay = CompoundCatalogFactory.BuildGameplay(WellKnownMaps.TrainingCompound, 460072030UL);
            var depotGameplay = CompoundCatalogFactory.BuildGameplay(WellKnownMaps.DepotYard, 460072031UL);

            var compoundGameplayPkg = MapPackageBuilder.Build("training-compound", "gameplay", toolchain);
            var depotGameplayPkg = MapPackageBuilder.Build("depot-yard", "gameplay", toolchain);
            var compoundLow = MapPackageBuilder.Build("training-compound", "low", toolchain);
            var compoundBalanced = MapPackageBuilder.Build("training-compound", "balanced", toolchain);
            var compoundHigh = MapPackageBuilder.Build("training-compound", "high", toolchain);
            var depotLow = MapPackageBuilder.Build("depot-yard", "low", toolchain);
            var depotBalanced = MapPackageBuilder.Build("depot-yard", "balanced", toolchain);
            var depotHigh = MapPackageBuilder.Build("depot-yard", "high", toolchain);

            var catalog = new[]
            {
                new MapCatalogEntry(WellKnownMaps.TrainingCompound, "Training Compound", "Bounded compound for offline TDM.", modes, sizes, 460072030UL, "map.compound.1.0.0",
                    compoundGameplayPkg.PackageRootHash, compoundLow.PackageRootHash, compoundBalanced.PackageRootHash, compoundHigh.PackageRootHash),
                new MapCatalogEntry(WellKnownMaps.DepotYard, "Depot Yard", "Distinct depot grammar for offline TDM.", modes, sizes, 460072031UL, "map.depot.1.0.0",
                    depotGameplayPkg.PackageRootHash, depotLow.PackageRootHash, depotBalanced.PackageRootHash, depotHigh.PackageRootHash)
            };
            var created = new StaticMapCatalog(catalog, new[] { compoundGameplay, depotGameplay });
            lock (CatalogGate)
            {
                CachedToolchain = toolchain;
                CachedCatalog = created;
            }
            return created;
        }

        public static ContentId ResolveToolchainHash()
        {
            string? probe = Environment.GetEnvironmentVariable("SHOOTER_TOOLCHAIN_LOCK");
            if (!string.IsNullOrWhiteSpace(probe) && File.Exists(probe))
                return ContentIdentity.Hash(File.ReadAllBytes(probe));
            // Deterministic fallback for pure tests without a repo path; publishers always pass the lock hash.
            return ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("shooter.toolchain.placeholder.v1"));
        }
    }
}
