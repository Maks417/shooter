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
    /// <summary>Release-player map package locator. Loads verified published bytes only; never regenerates recipes.</summary>
    public sealed class VerifiedMapPackageStore
    {
        private readonly string cacheRoot;
        private readonly ContentId toolchainHash;
        private readonly Dictionary<string, ContentId> gameplayRoots = new Dictionary<string, ContentId>(StringComparer.Ordinal);
        private readonly Dictionary<string, ContentId> lowVisualRoots = new Dictionary<string, ContentId>(StringComparer.Ordinal);
        private readonly Dictionary<string, ContentId> balancedVisualRoots = new Dictionary<string, ContentId>(StringComparer.Ordinal);
        private readonly Dictionary<string, ContentId> highVisualRoots = new Dictionary<string, ContentId>(StringComparer.Ordinal);

        public VerifiedMapPackageStore(string cacheRoot, ContentId toolchainHash, IReadOnlyDictionary<string, ContentId> gameplayRootHashes)
            : this(cacheRoot, toolchainHash, gameplayRootHashes, null, null, null)
        {
        }

        public VerifiedMapPackageStore(
            string cacheRoot,
            ContentId toolchainHash,
            IReadOnlyDictionary<string, ContentId> gameplayRootHashes,
            IReadOnlyDictionary<string, ContentId>? lowVisualRootHashes,
            IReadOnlyDictionary<string, ContentId>? balancedVisualRootHashes,
            IReadOnlyDictionary<string, ContentId>? highVisualRootHashes)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot)) throw new ArgumentException("cache root required", nameof(cacheRoot));
            this.cacheRoot = Path.GetFullPath(cacheRoot);
            this.toolchainHash = toolchainHash;
            if (gameplayRootHashes == null) throw new ArgumentNullException(nameof(gameplayRootHashes));
            foreach (var pair in gameplayRootHashes) gameplayRoots[pair.Key] = pair.Value;
            CopyOptional(lowVisualRootHashes, lowVisualRoots);
            CopyOptional(balancedVisualRootHashes, balancedVisualRoots);
            CopyOptional(highVisualRootHashes, highVisualRoots);
        }

        public string CacheRoot => cacheRoot;
        public ContentId ToolchainHash => toolchainHash;

        public bool TryLoadGameplay(MapId mapId, ContentId expectedRootHash, out PublishedMapPackage package, out string diagnostic)
        {
            package = default;
            if (!gameplayRoots.TryGetValue(mapId.Value, out var catalogHash) || catalogHash != expectedRootHash)
            {
                diagnostic = "catalog-root-mismatch";
                return false;
            }
            string dir = MapPackagePublisher.PackageDirectory(cacheRoot, expectedRootHash);
            if (!MapPackagePublisher.TryOpenPublished(dir, expectedRootHash, out package, out diagnostic))
                return false;
            if (package.MapId != mapId.Value || package.VisualProfile != "gameplay")
            {
                diagnostic = "package-identity-mismatch";
                return false;
            }
            try
            {
                var root = JsonValue.Parse(package.PackageRootBytes).Object;
                if (root["toolchainHash"].String != toolchainHash.ToString())
                {
                    diagnostic = "toolchain-mismatch";
                    return false;
                }
            }
            catch (Exception error)
            {
                diagnostic = error.Message;
                return false;
            }
            diagnostic = "ok";
            return true;
        }

        public bool TryLoadVisual(MapId mapId, string visualProfile, ContentId expectedRootHash, out PackagedVisualPayloads visuals, out string diagnostic)
        {
            visuals = default!;
            if (visualProfile != "low" && visualProfile != "balanced" && visualProfile != "high")
            {
                diagnostic = "invalid-visual-profile";
                return false;
            }
            var roots = visualProfile == "low" ? lowVisualRoots
                : visualProfile == "balanced" ? balancedVisualRoots
                : highVisualRoots;
            if (roots.Count == 0)
            {
                // Catalog visual roots not registered on this store instance.
                diagnostic = "visual-roots-unregistered";
                return false;
            }
            if (!roots.TryGetValue(mapId.Value, out var catalogHash) || catalogHash != expectedRootHash)
            {
                diagnostic = "catalog-visual-root-mismatch";
                return false;
            }
            string dir = MapPackagePublisher.PackageDirectory(cacheRoot, expectedRootHash);
            if (!MapVisualClosure.TryLoadFromPackageDirectory(dir, expectedRootHash, toolchainHash, out visuals, out diagnostic))
                return false;
            if (visuals.MapId != mapId.Value || visuals.Profile != visualProfile)
            {
                diagnostic = "visual-package-identity-mismatch";
                return false;
            }
            diagnostic = "ok";
            return true;
        }

        private static void CopyOptional(IReadOnlyDictionary<string, ContentId>? source, Dictionary<string, ContentId> destination)
        {
            if (source == null) return;
            foreach (var pair in source) destination[pair.Key] = pair.Value;
        }
    }
}
