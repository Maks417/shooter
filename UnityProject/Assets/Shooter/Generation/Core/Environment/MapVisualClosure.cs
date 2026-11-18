#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// Verified visual dependency paths and load/extract helpers for shooter.map-package.v1 non-gameplay packages (T067/B04).
    /// Presentation uploads these payloads only; shipping path must not regenerate soldier/weapon/map geometry.
    /// </summary>
    public static class MapVisualClosure
    {
        public const string MapSolidsMeshPath = "mesh-map-solids.bin";
        public const string UnitBoxMeshPath = "mesh-unit-box.bin";
        public static readonly string[] FirstPersonHandPaths =
        {
            "mesh-fp-rifle-trigger-hand.bin", "mesh-fp-rifle-support-hand.bin",
            "mesh-fp-pistol-trigger-hand.bin", "mesh-fp-pistol-support-hand.bin",
            "mesh-fp-knife-trigger-hand.bin", "mesh-fp-knife-support-hand.bin",
            "mesh-fp-grenade-trigger-hand.bin", "mesh-fp-grenade-support-hand.bin"
        };
        public static string FirstPersonHandSkeletonPath(int index) => FirstPersonHandPaths[index].Replace("mesh-", "skeleton-");
        public const string SoldierSkeletonPath = "skeleton-soldier.bin";
        public const string ShaderPathPrefix = "shader-";
        public const string FontPath = "font-exception.json";

        public static readonly string[] SoldierLodPaths =
        {
            "mesh-soldier-lod0.bin", "mesh-soldier-lod1.bin", "mesh-soldier-lod2.bin"
        };

        // Weapon meshes moved to the shared weapon package (ADR-078); paths come from WeaponMeshCatalog.
        public static string[] LodPathsForMeshKey(string meshAssetKey) => WeaponMeshCatalog.LodPaths(meshAssetKey);

        public static string? MagazinePathForMeshKey(string meshAssetKey) =>
            WeaponMeshCatalog.TryGet(meshAssetKey, out _) ? WeaponMeshCatalog.MagazinePath(meshAssetKey) : null;

        public static string? CyclePathForMeshKey(string meshAssetKey) =>
            WeaponMeshCatalog.TryGet(meshAssetKey, out _) ? WeaponMeshCatalog.CyclePath(meshAssetKey) : null;

        public static readonly string[] MapMaterialPaths =
        {
            "material-map-concrete.bin",
            "material-map-cover.bin",
            "material-map-ground.bin",
            "material-map-asphalt.bin"
        };

        public static readonly string[] WeaponMaterialPaths =
        {
            "material-weapon-metal.bin",
            "material-weapon-polymer.bin"
        };

        public static readonly string[] FirstPersonSurfacePaths =
        {
            "material-weapon-metal.bin", "material-weapon-polymer.bin",
            "material-fp-sleeve.bin", "material-fp-glove.bin",
            "material-weapon-steel.bin", "material-weapon-recess.bin", "material-weapon-enamel.bin",
            // ADR-111 knife and grenade finishes (FirstPersonSurfaceBaker families 7-9).
            "material-weapon-blade.bin", "material-weapon-blade-edge.bin", "material-weapon-grenade-paint.bin"
        };

        public static readonly string[] SoldierMaterialPaths =
        {
            "material-soldier-team1-uniform.bin",
            "material-soldier-team1-gear.bin",
            "material-soldier-team1-skin.bin",
            "material-soldier-team1-goggles.bin",
            "material-soldier-team2-uniform.bin",
            "material-soldier-team2-gear.bin",
            "material-soldier-team2-skin.bin",
            "material-soldier-team2-goggles.bin"
        };

        public static string SoldierBandPath(byte team)=>"material-soldier-team"+team+"-armband.bin";
        public static string SoldierPatchPath(byte team)=>"material-soldier-team"+team+"-patch.bin";
        public static string MaterialCompanionPath(string albedoPath, string plane)
        {
            if (plane != "normal" && plane != "mask") throw new ArgumentOutOfRangeException(nameof(plane));
            if (string.IsNullOrEmpty(albedoPath) || !albedoPath.EndsWith(".bin", StringComparison.Ordinal))
                throw new ArgumentException("A packaged material .bin path is required.", nameof(albedoPath));
            return albedoPath.Substring(0, albedoPath.Length - 4) + "-" + plane + ".bin";
        }

        public static ContentId MapFingerprint(string mapId) =>
            GeneratorFingerprint.Compute(MapRecipe.GeneratorIdFor(mapId), MapRecipe.GeneratorVersion);

        public static ContentId SoldierFingerprint =>
            GeneratorFingerprint.Compute(SoldierRecipe.GeneratorId, SoldierRecipe.ProfileVersion);

        public static string ShaderPath(string profile) => ShaderPathPrefix + profile + ".json";

        public static int SoldierLodIndex(string profile) =>
            profile == "high" ? 0 : profile == "balanced" ? 1 : 2;

        public static int WeaponLodIndex(string profile) =>
            profile == "high" ? 0 : profile == "balanced" ? 1 : 3;

        public static IReadOnlyList<string> RequiredPaths(string profile)
        {
            var list = new List<string>(48)
            {
                MapSolidsMeshPath,
                UnitBoxMeshPath,
                SoldierSkeletonPath,
                ShaderPath(profile),
                FontPath
            };
            list.AddRange(SoldierLodPaths);
            list.AddRange(FirstPersonHandPaths);
            for (int hand = 0; hand < FirstPersonHandPaths.Length; hand++) list.Add(FirstPersonHandSkeletonPath(hand));
            foreach (string path in MapMaterialPaths)
            {
                list.Add(path);
                list.Add(MaterialCompanionPath(path, "normal"));
                list.Add(MaterialCompanionPath(path, "mask"));
            }
            foreach (string path in FirstPersonSurfacePaths)
            {
                list.Add(path);
                list.Add(MaterialCompanionPath(path, "normal"));
                list.Add(MaterialCompanionPath(path, "mask"));
            }
            list.AddRange(SoldierMaterialPaths);
            foreach(byte team in new byte[]{1,2}){list.Add(SoldierBandPath(team));list.Add(SoldierPatchPath(team));}
            foreach(int i in new[]{0,4}){list.Add(MaterialCompanionPath(SoldierMaterialPaths[i],"normal"));list.Add(MaterialCompanionPath(SoldierMaterialPaths[i],"mask"));}
            list.Add(MapAmbientOcclusionVolume.Path);
            foreach(Shooter.Generation.Fx.FlipbookKind kind in Enum.GetValues(typeof(Shooter.Generation.Fx.FlipbookKind)))list.Add(Shooter.Generation.Fx.FxFlipbook.Path(kind));
            list.Add(ProceduralDecals.AtlasPath);
            list.Add(ProceduralDecals.PlacementsPath);
            return list;
        }

        public static bool TryExtract(
            IReadOnlyList<MapPackageDependency> dependencies,
            string mapId,
            string profile,
            ContentId toolchainHash,
            out PackagedVisualPayloads payloads,
            out string diagnostic)
        {
            payloads = default!;
            diagnostic = "missing";
            if (dependencies == null)
            {
                diagnostic = "dependencies-null";
                return false;
            }
            var byPath = new Dictionary<string, MapPackageDependency>(StringComparer.Ordinal);
            for (int i = 0; i < dependencies.Count; i++)
            {
                var dep = dependencies[i];
                if (byPath.ContainsKey(dep.Path))
                {
                    diagnostic = "duplicate-path:" + dep.Path;
                    return false;
                }
                byPath[dep.Path] = dep;
            }

            foreach (string path in RequiredPaths(profile))
            {
                if (!byPath.ContainsKey(path))
                {
                    diagnostic = "missing-visual-dep:" + path;
                    return false;
                }
            }

            try
            {
                payloads = PackagedVisualPayloads.FromDependencies(byPath, mapId, profile, toolchainHash);
                diagnostic = "ok";
                return true;
            }
            catch (Exception error)
            {
                diagnostic = Trim(error.Message);
                payloads = default!;
                return false;
            }
        }

        public static bool TryLoadFromPackageDirectory(
            string packageDirectory,
            ContentId expectedRootHash,
            ContentId toolchainHash,
            out PackagedVisualPayloads payloads,
            out string diagnostic)
        {
            payloads = default!;
            diagnostic = "missing";
            if (string.IsNullOrWhiteSpace(packageDirectory) || !Directory.Exists(packageDirectory))
                return false;
            try
            {
                byte[] rootBytes = File.ReadAllBytes(Path.Combine(packageDirectory, "package.json"));
                if (ContentIdentity.Hash(rootBytes) != expectedRootHash)
                {
                    diagnostic = "package-root-hash-mismatch";
                    return false;
                }
                var root = JsonValue.Parse(rootBytes).Object;
                if (root["toolchainHash"].String != toolchainHash.ToString())
                {
                    diagnostic = "toolchain-mismatch";
                    return false;
                }
                string profile = root["visualProfile"].String;
                if (profile == "gameplay")
                {
                    diagnostic = "gameplay-has-no-visual-closure";
                    return false;
                }
                var deps = root["dependencies"].Array;
                var byPath = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                var hashes = new Dictionary<string, ContentId>(StringComparer.Ordinal);
                for (int i = 0; i < deps.Count; i++)
                {
                    var row = deps[i].Object;
                    string path = row["path"].String;
                    var sha = new ContentId(row["sha256"].String);
                    byte[] bytes = File.ReadAllBytes(Path.Combine(packageDirectory, path));
                    if (ContentIdentity.Hash(bytes) != sha)
                    {
                        diagnostic = "dependency-hash-mismatch:" + path;
                        return false;
                    }
                    byPath[path] = bytes;
                    hashes[path] = sha;
                }
                foreach (string path in RequiredPaths(profile))
                {
                    if (!byPath.ContainsKey(path))
                    {
                        diagnostic = "missing-visual-dep:" + path;
                        return false;
                    }
                }
                payloads = PackagedVisualPayloads.FromBytes(byPath, hashes, profile, root["mapId"].String, toolchainHash);
                diagnostic = "ok";
                return true;
            }
            catch (Exception error)
            {
                diagnostic = Trim(error.Message);
                return false;
            }
        }

        public static MeshData DecodeMapMesh(PackagedVisualPayloads payloads, string path)
        {
            byte[] body = ArtifactCodec.Decode(payloads.Bytes(path), ContentKind.Mesh, MapFingerprint(payloads.MapId), payloads.ToolchainHash);
            return GeometryCodec.DecodeMesh(body);
        }

        public static MeshData DecodeSoldierLod(PackagedVisualPayloads payloads, int lod)
        {
            if (lod < 0 || lod >= SoldierLodPaths.Length) throw new ArgumentOutOfRangeException(nameof(lod));
            byte[] body = ArtifactCodec.Decode(payloads.Bytes(SoldierLodPaths[lod]), ContentKind.Mesh, SoldierFingerprint, payloads.ToolchainHash);
            return GeometryCodec.DecodeMesh(body);
        }

        /// <summary>Weapon LOD from the attached weapon package (ADR-078).</summary>
        public static MeshData DecodeWeaponLod(PackagedVisualPayloads payloads, string meshAssetKey, int lod)
        {
            var source = WeaponMeshCatalog.Require(meshAssetKey);
            return DecodeWeaponMesh(payloads, WeaponMeshCatalog.LodPath(meshAssetKey, lod), source.Fingerprint);
        }

        public static MeshData? DecodeWeaponMagazine(PackagedVisualPayloads payloads, string meshAssetKey)
        {
            string? path = MagazinePathForMeshKey(meshAssetKey);
            return path == null ? null : DecodeWeaponMesh(payloads, path, WeaponMeshCatalog.Require(meshAssetKey).Fingerprint);
        }

        public static MeshData? DecodeWeaponCycle(PackagedVisualPayloads payloads, string meshAssetKey)
        {
            string? path = CyclePathForMeshKey(meshAssetKey);
            return path == null ? null : DecodeWeaponMesh(payloads, path, WeaponMeshCatalog.Require(meshAssetKey).Fingerprint);
        }

        private static MeshData DecodeWeaponMesh(PackagedVisualPayloads payloads, string path, ContentId fingerprint)
        {
            if (!payloads.HasWeapons)
                throw new InvalidOperationException("Weapon package not attached to visual payloads: " + path);
            return GeometryCodec.DecodeMesh(ArtifactCodec.Decode(payloads.Bytes(path), ContentKind.Mesh, fingerprint, payloads.ToolchainHash));
        }

        public static MeshData DecodeFirstPersonHand(PackagedVisualPayloads payloads, byte slot, bool support)
        {
            if (slot > 3) throw new ArgumentOutOfRangeException(nameof(slot));
            int index = slot * 2 + (support ? 1 : 0);
            return GeometryCodec.DecodeMesh(ArtifactCodec.Decode(payloads.Bytes(FirstPersonHandPaths[index]),
                ContentKind.Mesh, MapFingerprint(payloads.MapId), payloads.ToolchainHash));
        }

        public static SkeletonData DecodeFirstPersonHandSkeleton(PackagedVisualPayloads payloads, byte slot, bool support)
        {
            if (slot > 3) throw new ArgumentOutOfRangeException(nameof(slot));
            return GeometryCodec.DecodeSkeleton(ArtifactCodec.Decode(payloads.Bytes(FirstPersonHandSkeletonPath(slot*2+(support?1:0))),
                ContentKind.Skeleton, MapFingerprint(payloads.MapId), payloads.ToolchainHash));
        }

        public static SkeletonData DecodeSoldierSkeleton(PackagedVisualPayloads payloads)
        {
            byte[] body = ArtifactCodec.Decode(payloads.Bytes(SoldierSkeletonPath), ContentKind.Skeleton, SoldierFingerprint, payloads.ToolchainHash);
            return GeometryCodec.DecodeSkeleton(body);
        }

        public static MaterialData DecodeMaterial(PackagedVisualPayloads payloads, string path, ContentId generatorFingerprint)
        {
            byte[] body = ArtifactCodec.Decode(payloads.Bytes(path), ContentKind.Material, generatorFingerprint, payloads.ToolchainHash);
            return GeometryCodec.DecodeMaterial(body);
        }

        public static MaterialData DecodeMapMaterial(PackagedVisualPayloads payloads, string path) =>
            DecodeMaterial(payloads, path, path.StartsWith("material-map-", StringComparison.Ordinal) && path != MapAmbientOcclusionVolume.Path
                ? GeneratorFingerprint.Compute(NoiseMaterialRecipe.Generator, NoiseMaterialRecipe.StructuredVersion) // ADR-106 map surfaces
                : path.Contains("-uniform") ? GeneratorFingerprint.Compute(NoiseMaterialRecipe.Generator, NoiseMaterialRecipe.Version) : MapFingerprint(payloads.MapId));

        /// <summary>ADR-081 baked ambient-occlusion volume (Texture3D layout, see <see cref="MapAmbientOcclusionVolume.Index"/>).</summary>
        public static byte[] DecodeAmbientOcclusionVolume(PackagedVisualPayloads payloads) =>
            MapAmbientOcclusionVolume.FromAtlas(DecodeMapMaterial(payloads, MapAmbientOcclusionVolume.Path));

        private static string Trim(string message) => message.Length <= 200 ? message : message.Substring(0, 200);
    }

    /// <summary>Verified visual payload bag extracted from a published non-gameplay map package.</summary>
    public sealed class PackagedVisualPayloads
    {
        private readonly Dictionary<string, byte[]> bytes;
        private readonly Dictionary<string, ContentId> hashes;

        private PackagedVisualPayloads(
            Dictionary<string, byte[]> bytes,
            Dictionary<string, ContentId> hashes,
            string mapId,
            string profile,
            ContentId toolchainHash)
        {
            this.bytes = bytes;
            this.hashes = hashes;
            MapId = mapId;
            Profile = profile;
            ToolchainHash = toolchainHash;
        }

        public string MapId { get; }
        public string Profile { get; }
        public ContentId ToolchainHash { get; }

        /// <summary>Root hash of the attached weapon package, or default when none is attached (ADR-078).</summary>
        public ContentId WeaponPackageRootHash { get; private set; }
        public bool HasWeapons { get; private set; }

        /// <summary>Returns a bag that also serves the verified weapon package meshes.</summary>
        public PackagedVisualPayloads WithWeapons(WeaponVisualPayloads weapons)
        {
            if (weapons == null) throw new ArgumentNullException(nameof(weapons));
            if (weapons.ToolchainHash != ToolchainHash)
                throw new InvalidOperationException("Weapon package toolchain differs from the map package.");
            var mergedBytes = new Dictionary<string, byte[]>(bytes, StringComparer.Ordinal);
            var mergedHashes = new Dictionary<string, ContentId>(hashes, StringComparer.Ordinal);
            foreach (var pair in weapons.Bytes)
            {
                if (mergedBytes.ContainsKey(pair.Key))
                    throw new InvalidOperationException("Weapon package path collides with map payload: " + pair.Key);
                mergedBytes.Add(pair.Key, pair.Value);
                mergedHashes.Add(pair.Key, ContentIdentity.Hash(pair.Value));
            }
            return new PackagedVisualPayloads(mergedBytes, mergedHashes, MapId, Profile, ToolchainHash)
            {
                WeaponPackageRootHash = weapons.PackageRootHash,
                HasWeapons = true
            };
        }

        public byte[] Bytes(string path)
        {
            if (!bytes.TryGetValue(path, out var payload))
                throw new InvalidOperationException("Required visual payload missing: " + path);
            return payload;
        }

        public ContentId Hash(string path)
        {
            if (!hashes.TryGetValue(path, out var hash))
                throw new InvalidOperationException("Required visual payload hash missing: " + path);
            return hash;
        }

        public bool Has(string path) => bytes.ContainsKey(path);

        internal static PackagedVisualPayloads FromDependencies(
            Dictionary<string, MapPackageDependency> byPath,
            string mapId,
            string profile,
            ContentId toolchainHash)
        {
            var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var hashes = new Dictionary<string, ContentId>(StringComparer.Ordinal);
            foreach (string path in MapVisualClosure.RequiredPaths(profile))
            {
                var dep = byPath[path];
                bytes[path] = dep.Bytes;
                hashes[path] = dep.Sha256;
            }
            return new PackagedVisualPayloads(bytes, hashes, mapId, profile, toolchainHash);
        }

        public static PackagedVisualPayloads FromBuild(MapPackageBuild build)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            if (build.VisualProfile == "gameplay")
                throw new InvalidOperationException("Gameplay packages have no visual closure.");
            if (!MapVisualClosure.TryExtract(build.Dependencies, build.MapId, build.VisualProfile, build.ToolchainHash, out var payloads, out string diagnostic))
                throw new InvalidOperationException("Visual closure incomplete: " + diagnostic);
            // Compile-time/editor path: attach the in-memory weapon package build (ADR-078).
            return payloads.WithWeapons(WeaponPackage.Build(build.ToolchainHash).ToPayloads());
        }

        internal static PackagedVisualPayloads FromBytes(
            Dictionary<string, byte[]> byPath,
            Dictionary<string, ContentId> hashes,
            string profile,
            string mapId,
            ContentId toolchainHash)
        {
            var selected = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var selectedHashes = new Dictionary<string, ContentId>(StringComparer.Ordinal);
            foreach (string path in MapVisualClosure.RequiredPaths(profile))
            {
                selected[path] = byPath[path];
                selectedHashes[path] = hashes[path];
            }
            return new PackagedVisualPayloads(selected, selectedHashes, mapId, profile, toolchainHash);
        }
    }
}
