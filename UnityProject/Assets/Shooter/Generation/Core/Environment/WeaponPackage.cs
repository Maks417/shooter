#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>Compiled shared weapon package: every weapon mesh, independent of map and graphics profile (ADR-078).</summary>
    public sealed class WeaponPackageBuild
    {
        internal WeaponPackageBuild(ContentId toolchainHash, byte[] rootBytes, IReadOnlyList<MapPackageDependency> dependencies)
        {
            ToolchainHash = toolchainHash;
            PackageRootBytes = rootBytes;
            PackageRootHash = ContentIdentity.Hash(rootBytes);
            Dependencies = dependencies;
        }

        public ContentId ToolchainHash { get; }
        public ContentId PackageRootHash { get; }
        public byte[] PackageRootBytes { get; }
        public IReadOnlyList<MapPackageDependency> Dependencies { get; }

        public WeaponVisualPayloads ToPayloads()
        {
            var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var dep in Dependencies) bytes.Add(dep.Path, dep.Bytes);
            return new WeaponVisualPayloads(PackageRootHash, ToolchainHash, bytes);
        }
    }

    /// <summary>Verified weapon mesh bytes, attached to a map's <see cref="PackagedVisualPayloads"/> at load (ADR-078).</summary>
    public sealed class WeaponVisualPayloads
    {
        private readonly Dictionary<string, byte[]> bytes;

        internal WeaponVisualPayloads(ContentId packageRootHash, ContentId toolchainHash, Dictionary<string, byte[]> bytes)
        {
            PackageRootHash = packageRootHash;
            ToolchainHash = toolchainHash;
            this.bytes = bytes;
        }

        public ContentId PackageRootHash { get; }
        public ContentId ToolchainHash { get; }
        internal IReadOnlyDictionary<string, byte[]> Bytes => bytes;
    }

    /// <summary>
    /// Builds, publishes and opens <c>shooter.weapon-package.v1</c>. The package lives in the map package cache under its
    /// content-addressed directory; <c>weapons.json</c> (<c>shooter.weapon-package-index.v1</c>) names its root hash.
    /// A new weapon republishes this package only; map packages no longer carry weapon meshes.
    /// </summary>
    public static class WeaponPackage
    {
        public const string Schema = "shooter.weapon-package.v1";
        public const string IndexSchema = "shooter.weapon-package-index.v1";
        public const string IndexFileName = "weapons.json";

        private static readonly object BuildLock = new object();
        private static readonly Dictionary<ContentId, WeaponPackageBuild> Built = new Dictionary<ContentId, WeaponPackageBuild>();

        /// <summary>Compile-time build (cached per toolchain). Shipping players use <see cref="TryLoad"/> instead.</summary>
        public static WeaponPackageBuild Build(ContentId toolchainHash)
        {
            lock (BuildLock)
            {
                if (Built.TryGetValue(toolchainHash, out var cached)) return cached;
                var build = BuildUncached(toolchainHash);
                Built[toolchainHash] = build;
                return build;
            }
        }

        private static WeaponPackageBuild BuildUncached(ContentId toolchainHash)
        {
            var dependencies = new List<MapPackageDependency>();
            var keys = new List<object?>();
            foreach (var source in WeaponMeshCatalog.All)
            {
                keys.Add(source.MeshKey);
                if (source.IsProp)
                {
                    for (int lod = 0; lod < WeaponMeshCatalog.LodCount; lod++)
                        Add(dependencies, WeaponMeshCatalog.LodPath(source.MeshKey, lod), source.BuildPropLod(lod), source.Fingerprint, toolchainHash);
                    continue;
                }
                var gun = WeaponGeometry.Build(source.Recipe!);
                for (int lod = 0; lod < WeaponMeshCatalog.LodCount; lod++)
                    Add(dependencies, WeaponMeshCatalog.LodPath(source.MeshKey, lod), gun.Lods[lod], source.Fingerprint, toolchainHash);
                Add(dependencies, WeaponMeshCatalog.MagazinePath(source.MeshKey)!, gun.Magazine, source.Fingerprint, toolchainHash);
                Add(dependencies, WeaponMeshCatalog.CyclePath(source.MeshKey)!, gun.CyclePart, source.Fingerprint, toolchainHash);
            }

            var rows = new List<object?>(dependencies.Count);
            foreach (var dep in dependencies)
                rows.Add(new Dictionary<string, object?> { ["path"] = dep.Path, ["role"] = dep.Role, ["sha256"] = dep.Sha256.ToString() });
            byte[] root = MapDocumentJson.EncodeObject(new Dictionary<string, object?>
            {
                ["packageSchema"] = Schema,
                ["toolchainHash"] = toolchainHash.ToString(),
                ["gunGenerator"] = WeaponRecipe.GeneratorId + "." + WeaponRecipe.GeneratorVersion,
                ["propGenerator"] = WeaponMeshCatalog.PropGeneratorId + "." + WeaponMeshCatalog.PropGeneratorVersion,
                ["meshKeys"] = keys,
                ["dependencies"] = rows
            });
            return new WeaponPackageBuild(toolchainHash, root, dependencies);
        }

        private static void Add(List<MapPackageDependency> dependencies, string path, MeshData mesh, ContentId fingerprint, ContentId toolchainHash)
        {
            byte[] sgaf = ArtifactCodec.Encode(ContentKind.Mesh, GeometryCodec.EncodeMesh(mesh), fingerprint, toolchainHash);
            dependencies.Add(new MapPackageDependency("mesh", ContentIdentity.Hash(sgaf), path, sgaf));
        }

        public static string IndexPath(string cacheRoot) => Path.Combine(Path.GetFullPath(cacheRoot), IndexFileName);

        /// <summary>Publish the package (atomic staging, reuse when identical bytes exist) and rewrite the index.</summary>
        public static bool TryPublish(string cacheRoot, ContentId toolchainHash, out WeaponPackageBuild build, out string diagnostic)
        {
            build = Build(toolchainHash);
            string root = Path.GetFullPath(cacheRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string packageDir = MapPackagePublisher.PackageDirectory(root, build.PackageRootHash);
            try
            {
                if (Directory.Exists(packageDir))
                {
                    if (!TryOpenDirectory(packageDir, build.PackageRootHash, toolchainHash, out _, out diagnostic))
                    {
                        diagnostic = "corrupt-retained:" + diagnostic;
                        return false;
                    }
                }
                else
                {
                    string stage = Path.Combine(root, build.PackageRootHash.ToString().Substring(0, 2),
                        "." + build.PackageRootHash + "." + Guid.NewGuid().ToString("N") + ".tmp");
                    Directory.CreateDirectory(stage);
                    File.WriteAllBytes(Path.Combine(stage, "package.json"), build.PackageRootBytes);
                    foreach (var dep in build.Dependencies) File.WriteAllBytes(Path.Combine(stage, dep.Path), dep.Bytes);
                    if (!TryOpenDirectory(stage, build.PackageRootHash, toolchainHash, out _, out diagnostic))
                        return false;
                    Directory.Move(stage, packageDir);
                }

                byte[] index = MapDocumentJson.EncodeObject(new Dictionary<string, object?>
                {
                    ["indexSchema"] = IndexSchema,
                    ["toolchainHash"] = toolchainHash.ToString(),
                    ["packageRootSha256"] = build.PackageRootHash.ToString()
                });
                string path = IndexPath(root);
                string staged = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllBytes(staged, index);
                MapPackagePublisher.CommitStagedFile(staged, path);
                diagnostic = "ok";
                return true;
            }
            catch (Exception error)
            {
                diagnostic = "publish-failed:" + Trim(error.Message);
                return false;
            }
        }

        /// <summary>Load the published package named by <c>weapons.json</c>. Verifies every hash; never regenerates.</summary>
        public static bool TryLoad(string cacheRoot, ContentId toolchainHash, out WeaponVisualPayloads payloads, out string diagnostic)
        {
            payloads = null!;
            string indexPath = IndexPath(cacheRoot);
            if (!File.Exists(indexPath))
            {
                diagnostic = "weapon-index-missing";
                return false;
            }
            try
            {
                var index = JsonValue.Parse(File.ReadAllBytes(indexPath)).Object;
                if (index["indexSchema"].String != IndexSchema)
                {
                    diagnostic = "weapon-index-schema-mismatch";
                    return false;
                }
                if (index["toolchainHash"].String != toolchainHash.ToString())
                {
                    diagnostic = "weapon-index-toolchain-mismatch";
                    return false;
                }
                var rootHash = new ContentId(index["packageRootSha256"].String);
                string dir = MapPackagePublisher.PackageDirectory(Path.GetFullPath(cacheRoot), rootHash);
                return TryOpenDirectory(dir, rootHash, toolchainHash, out payloads, out diagnostic);
            }
            catch (Exception error)
            {
                diagnostic = "weapon-index-invalid:" + Trim(error.Message);
                return false;
            }
        }

        public static bool TryOpenDirectory(string packageDirectory, ContentId expectedRootHash, ContentId toolchainHash,
            out WeaponVisualPayloads payloads, out string diagnostic)
        {
            payloads = null!;
            if (!Directory.Exists(packageDirectory))
            {
                diagnostic = "weapon-package-missing";
                return false;
            }
            try
            {
                byte[] rootBytes = File.ReadAllBytes(Path.Combine(packageDirectory, "package.json"));
                if (ContentIdentity.Hash(rootBytes) != expectedRootHash)
                {
                    diagnostic = "weapon-package-root-hash-mismatch";
                    return false;
                }
                var root = JsonValue.Parse(rootBytes).Object;
                if (root["packageSchema"].String != Schema)
                {
                    diagnostic = "weapon-package-schema-mismatch";
                    return false;
                }
                if (root["toolchainHash"].String != toolchainHash.ToString())
                {
                    diagnostic = "weapon-package-toolchain-mismatch";
                    return false;
                }
                var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                var deps = root["dependencies"].Array;
                for (int i = 0; i < deps.Count; i++)
                {
                    var row = deps[i].Object;
                    string path = row["path"].String;
                    if (path.IndexOfAny(new[] { '/', '\\' }) >= 0 || path.StartsWith(".", StringComparison.Ordinal) || bytes.ContainsKey(path))
                    {
                        diagnostic = "weapon-package-invalid-path:" + path;
                        return false;
                    }
                    byte[] payload = File.ReadAllBytes(Path.Combine(packageDirectory, path));
                    if (ContentIdentity.Hash(payload) != new ContentId(row["sha256"].String))
                    {
                        diagnostic = "weapon-dependency-hash-mismatch:" + path;
                        return false;
                    }
                    bytes.Add(path, payload);
                }
                foreach (string path in WeaponMeshCatalog.RequiredPaths())
                {
                    if (!bytes.ContainsKey(path))
                    {
                        diagnostic = "missing-weapon-dep:" + path;
                        return false;
                    }
                }
                payloads = new WeaponVisualPayloads(expectedRootHash, toolchainHash, bytes);
                diagnostic = "ok";
                return true;
            }
            catch (Exception error)
            {
                diagnostic = "weapon-package-invalid:" + Trim(error.Message);
                return false;
            }
        }

        private static string Trim(string message) => message.Length <= 200 ? message : message.Substring(0, 200);
    }
}
