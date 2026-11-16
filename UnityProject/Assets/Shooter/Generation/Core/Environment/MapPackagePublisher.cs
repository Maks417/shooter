#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    public sealed class MapPackagePublishResult
    {
        public ErrorCode Error { get; }
        public string Diagnostic { get; }
        public IReadOnlyList<MapPackageBuild> Packages { get; }
        public bool Reused { get; }
        /// <summary>Superseded package directories removed after a successful publish (ADR-076 addendum).</summary>
        public int PrunedPackages { get; internal set; }
        public MapPackagePublishResult(ErrorCode error, string diagnostic, IReadOnlyList<MapPackageBuild> packages, bool reused)
        {
            Error = error;
            Diagnostic = diagnostic.Length <= 512 ? diagnostic : diagnostic.Substring(0, 512);
            Packages = packages ?? Array.Empty<MapPackageBuild>();
            Reused = reused;
        }
        public static MapPackagePublishResult Ok(IReadOnlyList<MapPackageBuild> packages, bool reused) =>
            new MapPackagePublishResult(ErrorCode.None, "", packages, reused);
        public static MapPackagePublishResult Fail(ErrorCode error, string diagnostic) =>
            new MapPackagePublishResult(error, diagnostic, Array.Empty<MapPackageBuild>(), false);
    }

    /// <summary>Atomic ADR-018 staging publish for map package sets under a cache root.</summary>
    public static class MapPackagePublisher
    {
        public static MapPackagePublishResult Publish(
            string cacheRoot,
            ContentId toolchainHash,
            IReadOnlyList<string> mapIds,
            IReadOnlyList<string> profiles,
            bool force,
            CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot)) return MapPackagePublishResult.Fail(ErrorCode.InvalidInput, "cache root required");
            if (mapIds == null || mapIds.Count == 0) return MapPackagePublishResult.Fail(ErrorCode.InvalidInput, "map selection required");
            if (profiles == null || profiles.Count == 0) return MapPackagePublishResult.Fail(ErrorCode.InvalidInput, "profile selection required");
            string root = Path.GetFullPath(cacheRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            try
            {
                cancel.ThrowIfCancellationRequested();
                var built = new List<MapPackageBuild>();
                bool allReused = true;
                foreach (string mapId in mapIds)
                {
                    if (!MapRecipe.IsApprovedMapId(mapId))
                        return MapPackagePublishResult.Fail(ErrorCode.InvalidInput, "unknown-map:" + mapId);
                    foreach (string profile in profiles)
                    {
                        cancel.ThrowIfCancellationRequested();
                        var package = MapPackageBuilder.Build(mapId, profile, toolchainHash);
                        string packageDir = PackageDirectory(root, package.PackageRootHash);
                        bool reused = TryVerifyExisting(packageDir, package, out string verifyDiagnostic);
                        if (reused && !force)
                        {
                            built.Add(package);
                            continue;
                        }
                        allReused = false;
                        if (reused && force)
                        {
                            // Force still requires identical bytes; never overwrite divergent content.
                            built.Add(package);
                            continue;
                        }
                        if (Directory.Exists(packageDir))
                        {
                            if (!string.IsNullOrEmpty(verifyDiagnostic))
                                return MapPackagePublishResult.Fail(ErrorCode.CorruptContent, "corrupt-retained:" + verifyDiagnostic);
                            // Same key already present and verified.
                            built.Add(package);
                            continue;
                        }
                        string stage = Path.Combine(root, package.PackageRootHash.ToString().Substring(0, 2),
                            "." + package.PackageRootHash + "." + Guid.NewGuid().ToString("N") + ".tmp");
                        try
                        {
                            Directory.CreateDirectory(stage);
                            WriteAll(Path.Combine(stage, "package.json"), package.PackageRootBytes);
                            for (int i = 0; i < package.Dependencies.Count; i++)
                            {
                                var dep = package.Dependencies[i];
                                WriteAll(Path.Combine(stage, dep.Path), dep.Bytes);
                            }
                            Recheck(stage, package);
                            cancel.ThrowIfCancellationRequested();
                            string parent = Path.GetDirectoryName(packageDir)!;
                            Directory.CreateDirectory(parent);
                            Directory.Move(stage, packageDir);
                            built.Add(package);
                        }
                        catch (OperationCanceledException)
                        {
                            TryDeleteDirectory(stage);
                            return MapPackagePublishResult.Fail(ErrorCode.Cancelled, "publish-cancelled");
                        }
                        catch (Exception error)
                        {
                            // Retain corrupt/partial staging for repair; do not delete published peers.
                            return MapPackagePublishResult.Fail(ErrorCode.InternalError, "publish-failed:" + Safe(error.Message));
                        }
                    }
                }
                WriteIndex(root, built);
                // ADR-078: the shared weapon package is published beside the map packages whenever visuals are.
                bool anyVisual = false;
                foreach (string profile in profiles) anyVisual |= profile != "gameplay";
                WeaponPackageBuild? weapons = null;
                if (anyVisual && !WeaponPackage.TryPublish(root, toolchainHash, out weapons, out string weaponDiagnostic))
                    return MapPackagePublishResult.Fail(ErrorCode.InternalError, "weapon-package:" + weaponDiagnostic);
                // ADR-038: persist release catalog/gameplay sidecars beside packages (compile-time only).
                var catalogMaps = DistinctMapIds(built);
                ReleaseCatalogDocument.WriteAtomic(root, toolchainHash, catalogMaps);
                // T067: provenance sidecar (outside frozen package bytes) distinguishes software grid from native bake.
                NavBakeProvenanceDocument.WriteAtomic(root, built, NavBakePublication.NativeToolIdentity);
                var result = MapPackagePublishResult.Ok(built, allReused && built.Count > 0);
                result.PrunedPackages = PruneSuperseded(root, built, mapIds, profiles, weapons);
                return result;
            }
            catch (OperationCanceledException)
            {
                return MapPackagePublishResult.Fail(ErrorCode.Cancelled, "publish-cancelled");
            }
            catch (Exception error)
            {
                return MapPackagePublishResult.Fail(ErrorCode.InternalError, Safe(error.Message));
            }
        }

        public static string PackageDirectory(string cacheRoot, ContentId packageRootHash)
        {
            string hex = packageRootHash.ToString();
            return Path.Combine(cacheRoot, hex.Substring(0, 2), hex);
        }

        public static bool TryLoad(string cacheRoot, ContentId packageRootHash, out MapPackageBuild? package, out string diagnostic)
        {
            package = null;
            diagnostic = "missing";
            string dir = PackageDirectory(cacheRoot, packageRootHash);
            if (!Directory.Exists(dir)) return false;
            try
            {
                byte[] rootBytes = File.ReadAllBytes(Path.Combine(dir, "package.json"));
                if (ContentIdentity.Hash(rootBytes) != packageRootHash)
                {
                    diagnostic = "package-root-hash-mismatch";
                    return false;
                }
                var root = JsonValue.Parse(rootBytes).Object;
                string mapId = root["mapId"].String;
                string profile = root["visualProfile"].String;
                var toolchain = new ContentId(root["toolchainHash"].String);
                package = MapPackageBuilder.Build(mapId, profile, toolchain);
                if (package.PackageRootHash != packageRootHash || !BytesEqual(package.PackageRootBytes, rootBytes))
                {
                    diagnostic = "rebuilt-package-mismatch";
                    package = null;
                    return false;
                }
                if (!TryVerifyExisting(dir, package, out diagnostic))
                {
                    package = null;
                    return false;
                }
                diagnostic = "ok";
                return true;
            }
            catch (Exception error)
            {
                diagnostic = Safe(error.Message);
                package = null;
                return false;
            }
        }

        /// <summary>Load published bytes without regenerating; verifies hashes only.</summary>
        public static bool TryOpenPublished(string packageDirectory, ContentId expectedRootHash, out PublishedMapPackage published, out string diagnostic)
        {
            published = default!;
            diagnostic = "missing";
            if (!Directory.Exists(packageDirectory)) return false;
            try
            {
                byte[] rootBytes = File.ReadAllBytes(Path.Combine(packageDirectory, "package.json"));
                if (ContentIdentity.Hash(rootBytes) != expectedRootHash)
                {
                    diagnostic = "package-root-hash-mismatch";
                    return false;
                }
                var root = JsonValue.Parse(rootBytes).Object;
                string mapId = root["mapId"].String;
                string profile = root["visualProfile"].String;
                var collisionHash = new ContentId(root["collisionArtifactSha256"].String);
                var navigationHash = new ContentId(root["navigationArtifactSha256"].String);
                var metadataHash = new ContentId(root["metadataSha256"].String);
                byte[] collision = File.ReadAllBytes(Path.Combine(packageDirectory, "collision-gameplay.bin"));
                byte[] navigation = File.ReadAllBytes(Path.Combine(packageDirectory, "navigation-gameplay.bin"));
                byte[] metadata = File.ReadAllBytes(Path.Combine(packageDirectory, "map-metadata.json"));
                if (ContentIdentity.Hash(collision) != collisionHash ||
                    ContentIdentity.Hash(navigation) != navigationHash ||
                    ContentIdentity.Hash(metadata) != metadataHash)
                {
                    diagnostic = "dependency-hash-mismatch";
                    return false;
                }
                published = new PublishedMapPackage(mapId, profile, expectedRootHash, rootBytes, collision, navigation, metadata);
                diagnostic = "ok";
                return true;
            }
            catch (Exception error)
            {
                diagnostic = Safe(error.Message);
                return false;
            }
        }

        private static bool TryVerifyExisting(string packageDir, MapPackageBuild package, out string diagnostic)
        {
            diagnostic = "missing";
            if (!Directory.Exists(packageDir)) return false;
            try
            {
                byte[] root = File.ReadAllBytes(Path.Combine(packageDir, "package.json"));
                if (!BytesEqual(root, package.PackageRootBytes))
                {
                    diagnostic = "package-root-bytes-differ";
                    return false;
                }
                for (int i = 0; i < package.Dependencies.Count; i++)
                {
                    var dep = package.Dependencies[i];
                    string path = Path.Combine(packageDir, dep.Path);
                    if (!File.Exists(path))
                    {
                        diagnostic = "missing-dep:" + dep.Path;
                        return false;
                    }
                    byte[] bytes = File.ReadAllBytes(path);
                    if (!BytesEqual(bytes, dep.Bytes))
                    {
                        diagnostic = "dep-bytes-differ:" + dep.Path;
                        return false;
                    }
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

        private static void Recheck(string stage, MapPackageBuild package)
        {
            byte[] root = File.ReadAllBytes(Path.Combine(stage, "package.json"));
            if (!BytesEqual(root, package.PackageRootBytes)) throw new IOException("staged package root mismatch");
            for (int i = 0; i < package.Dependencies.Count; i++)
            {
                var dep = package.Dependencies[i];
                byte[] bytes = File.ReadAllBytes(Path.Combine(stage, dep.Path));
                if (!BytesEqual(bytes, dep.Bytes)) throw new IOException("staged dependency mismatch: " + dep.Path);
            }
        }

        private static void WriteIndex(string root, List<MapPackageBuild> packages)
        {
            var entries = new List<object?>();
            for (int i = 0; i < packages.Count; i++)
            {
                var p = packages[i];
                entries.Add(new Dictionary<string, object?>
                {
                    ["mapId"] = p.MapId,
                    ["visualProfile"] = p.VisualProfile,
                    ["packageRootSha256"] = p.PackageRootHash.ToString(),
                    ["seed"] = p.Seed.ToString(CultureInfo.InvariantCulture),
                    ["serverGameplayOnly"] = p.VisualProfile == "gameplay"
                });
            }
            byte[] bytes = MapDocumentJson.EncodeObject(new Dictionary<string, object?>
            {
                ["indexSchema"] = "shooter.map-package-index.v1",
                ["packages"] = entries
            });
            string path = Path.Combine(root, "index.json");
            string stage = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            WriteAll(stage, bytes);
            CommitStagedFile(stage, path);
        }

        /// <summary>
        /// Atomically moves a fully written stage file over <paramref name="path"/>. Windows can briefly refuse to
        /// remove the replaced file while another process (e.g. an on-access scanner) holds it without delete sharing;
        /// that IOException is retried with bounded backoff (~3 s total). The stage is deleted if every attempt fails.
        /// </summary>
        internal static void CommitStagedFile(string stage, string path)
        {
            const int attempts = 8;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (File.Exists(path)) File.Replace(stage, path, null);
                    else File.Move(stage, path);
                    return;
                }
                catch (IOException) when (attempt < attempts)
                {
                    Thread.Sleep(50 * attempt * attempt);
                }
                catch
                {
                    try { if (File.Exists(stage)) File.Delete(stage); } catch { /* best effort */ }
                    throw;
                }
            }
        }

        private static List<string> DistinctMapIds(List<MapPackageBuild> packages)
        {
            var maps = new List<string>();
            for (int i = 0; i < packages.Count; i++)
            {
                string mapId = packages[i].MapId;
                bool seen = false;
                for (int j = 0; j < maps.Count; j++)
                {
                    if (maps[j] == mapId) { seen = true; break; }
                }
                if (!seen) maps.Add(mapId);
            }
            return maps;
        }

        private static void WriteAll(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// ADR-076 addendum: delete content-addressed package directories this publish superseded, so repeated
        /// republishing does not accumulate stale packages. Only an unreferenced map package whose map id and profile
        /// were both part of this publish is removed (other maps and profiles keep their packages), plus older weapon
        /// packages when a weapon package was published. Staging (<c>.tmp</c>) directories, unreadable roots and
        /// anything still referenced are retained. Deletion failures are ignored, never fatal.
        /// </summary>
        internal static int PruneSuperseded(string root, IReadOnlyList<MapPackageBuild> built, IReadOnlyList<string> mapIds,
            IReadOnlyList<string> profiles, WeaponPackageBuild? weapons)
        {
            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (var package in built) keep.Add(package.PackageRootHash.ToString());
            if (weapons != null) keep.Add(weapons.PackageRootHash.ToString());
            var maps = new HashSet<string>(mapIds, StringComparer.Ordinal);
            var publishedProfiles = new HashSet<string>(profiles, StringComparer.Ordinal);
            int pruned = 0;
            foreach (string shard in Directory.GetDirectories(root))
            {
                string shardName = Path.GetFileName(shard);
                if (shardName.Length != 2 || !IsHex(shardName)) continue;
                foreach (string dir in Directory.GetDirectories(shard))
                {
                    string name = Path.GetFileName(dir);
                    if (name.Length != 64 || !IsHex(name) || keep.Contains(name)) continue;
                    try
                    {
                        var package = MapJsonValue.Parse(File.ReadAllBytes(Path.Combine(dir, "package.json"))).Object;
                        string schema = package["packageSchema"].String;
                        bool superseded = schema == MapRecipe.PackageSchema
                            ? maps.Contains(package["mapId"].String) && publishedProfiles.Contains(package["visualProfile"].String)
                            : schema == WeaponPackage.Schema && weapons != null;
                        if (!superseded) continue;
                        Directory.Delete(dir, true);
                        pruned++;
                    }
                    catch (Exception)
                    {
                        // Unreadable or locked: retain for inspection/repair.
                    }
                }
                try { if (Directory.GetFileSystemEntries(shard).Length == 0) Directory.Delete(shard); } catch (Exception) { }
            }
            return pruned;
        }

        private static bool IsHex(string value)
        {
            foreach (char c in value)
                if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
            return true;
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { /* retain for repair */ }
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static string Safe(string message) => message.Length <= 200 ? message : message.Substring(0, 200);
    }

    public readonly struct PublishedMapPackage
    {
        public string MapId { get; }
        public string VisualProfile { get; }
        public ContentId PackageRootHash { get; }
        public byte[] PackageRootBytes { get; }
        public byte[] CollisionSgaf { get; }
        public byte[] NavigationSgaf { get; }
        public byte[] MetadataBytes { get; }
        public PublishedMapPackage(string mapId, string visualProfile, ContentId packageRootHash, byte[] packageRootBytes, byte[] collisionSgaf, byte[] navigationSgaf, byte[] metadataBytes)
        {
            MapId = mapId; VisualProfile = visualProfile; PackageRootHash = packageRootHash;
            PackageRootBytes = packageRootBytes; CollisionSgaf = collisionSgaf; NavigationSgaf = navigationSgaf; MetadataBytes = metadataBytes;
        }
    }
}
