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

        public VerifiedMapPackageStore(string cacheRoot, ContentId toolchainHash, IReadOnlyDictionary<string, ContentId> gameplayRootHashes)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot)) throw new ArgumentException("cache root required", nameof(cacheRoot));
            this.cacheRoot = Path.GetFullPath(cacheRoot);
            this.toolchainHash = toolchainHash;
            if (gameplayRootHashes == null) throw new ArgumentNullException(nameof(gameplayRootHashes));
            foreach (var pair in gameplayRootHashes) gameplayRoots[pair.Key] = pair.Value;
        }

        public string CacheRoot => cacheRoot;

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
    }
}
