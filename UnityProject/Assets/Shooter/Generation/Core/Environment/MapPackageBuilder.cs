#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    public sealed class MapPackageDependency
    {
        public string Role { get; }
        public ContentId Sha256 { get; }
        public string Path { get; }
        public byte[] Bytes { get; }
        public MapPackageDependency(string role, ContentId sha256, string path, byte[] bytes)
        {
            Role = role ?? throw new ArgumentNullException(nameof(role));
            Sha256 = sha256;
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
            if (ContentIdentity.Hash(bytes) != sha256) throw new ArgumentException("Dependency bytes do not match sha256.");
        }
    }

    public sealed class MapPackageBuild
    {
        public string MapId { get; }
        public string VisualProfile { get; }
        public ulong Seed { get; }
        public ContentId ToolchainHash { get; }
        public ContentId PackageRootHash { get; }
        public byte[] PackageRootBytes { get; }
        public byte[] MetadataBytes { get; }
        public ContentId MetadataHash { get; }
        public byte[] CollisionSgaf { get; }
        public ContentId CollisionHash { get; }
        public byte[] NavigationSgaf { get; }
        public ContentId NavigationHash { get; }
        public ContentId NavSettingsHash { get; }
        public IReadOnlyList<MapPackageDependency> Dependencies { get; }
        public CompoundPlan Plan { get; }
        public string NavBakeProvenance { get; }

        public MapPackageBuild(
            string mapId,
            string visualProfile,
            ulong seed,
            ContentId toolchainHash,
            ContentId packageRootHash,
            byte[] packageRootBytes,
            byte[] metadataBytes,
            ContentId metadataHash,
            byte[] collisionSgaf,
            ContentId collisionHash,
            byte[] navigationSgaf,
            ContentId navigationHash,
            ContentId navSettingsHash,
            IReadOnlyList<MapPackageDependency> dependencies,
            CompoundPlan plan,
            string navBakeProvenance)
        {
            MapId = mapId; VisualProfile = visualProfile; Seed = seed; ToolchainHash = toolchainHash;
            PackageRootHash = packageRootHash; PackageRootBytes = packageRootBytes;
            MetadataBytes = metadataBytes; MetadataHash = metadataHash;
            CollisionSgaf = collisionSgaf; CollisionHash = collisionHash;
            NavigationSgaf = navigationSgaf; NavigationHash = navigationHash;
            NavSettingsHash = navSettingsHash; Dependencies = dependencies; Plan = plan;
            NavBakeProvenance = navBakeProvenance;
        }
    }

    /// <summary>Builds verified shooter.map-package.v1 closures from numeric planners (ADR-035).</summary>
    public static class MapPackageBuilder
    {
        public static readonly string[] VisualProfiles = { "gameplay", "low", "balanced", "high" };

        public static MapPackageBuild Build(string mapId, string visualProfile, ContentId toolchainHash)
        {
            if (visualProfile != "gameplay" && visualProfile != "low" && visualProfile != "balanced" && visualProfile != "high")
                throw new ArgumentOutOfRangeException(nameof(visualProfile));
            ulong seed = MapRecipe.ApprovedSeed(mapId);
            var plan = mapId == "depot-yard" ? DepotPlanner.Generate(seed) : CompoundPlanner.Generate(seed);
            if (mapId != "depot-yard")
            {
                var report = CompoundValidation.Validate(plan);
                if (!report.Passed) throw new InvalidOperationException(report.Diagnostic);
            }

            var collision = ToCollisionData(plan);
            byte[] collisionBody = GeometryCodec.EncodeCollision(collision);
            var generatorId = MapRecipe.GeneratorIdFor(mapId);
            var fingerprint = GeneratorFingerprint.Compute(generatorId, MapRecipe.GeneratorVersion);
            byte[] collisionSgaf = ArtifactCodec.Encode(ContentKind.Collision, collisionBody, fingerprint, toolchainHash);
            var collisionHash = ContentIdentity.Hash(collisionSgaf);

            // Locked shooter.nav-settings.v1 SliceDefault (must match World.Navigation.NavSettings).
            const float agentRadius = 0.32f, clearanceMargin = 0.05f;
            float requiredClearance = agentRadius + clearanceMargin;
            var settingsHash = HashNavSettings(agentRadius, 1.8f, 0.25f, 45f, clearanceMargin, 1);
            var bake = UnityNavMeshBakeCodec.BuildWalkableGrid(plan.CollisionBoxes, CompoundPlan.MapHalfExtent, 8f, requiredClearance);
            byte[] bakeBlob = UnityNavMeshBakeCodec.Encode(bake);
            var navigation = new NavigationData(UnityNavMeshBakeCodec.AdapterFormat, UnityNavMeshBakeCodec.AdapterVersion, settingsHash, bakeBlob);
            byte[] navigationBody = GeometryCodec.EncodeNavigation(navigation);
            byte[] navigationSgaf = ArtifactCodec.Encode(ContentKind.Navigation, navigationBody, fingerprint, toolchainHash);
            var navigationHash = ContentIdentity.Hash(navigationSgaf);
            const string navProvenance = "software-walkable-grid;unity-native-bake-pending";

            byte[] metadataBytes = EncodeMetadata(mapId, plan, collision, collisionHash);
            var metadataHash = ContentIdentity.Hash(metadataBytes);

            var dependencies = new List<MapPackageDependency>
            {
                new MapPackageDependency("collision", collisionHash, "collision-gameplay.bin", collisionSgaf),
                new MapPackageDependency("navigation", navigationHash, "navigation-gameplay.bin", navigationSgaf),
                new MapPackageDependency("metadata", metadataHash, "map-metadata.json", metadataBytes)
            };

            if (visualProfile != "gameplay")
                AddVisualClosure(dependencies, plan, fingerprint, toolchainHash, visualProfile);

            byte[] packageRoot = EncodePackageRoot(mapId, visualProfile, seed, toolchainHash, collisionHash, navigationHash, settingsHash, metadataHash, dependencies);
            var packageHash = ContentIdentity.Hash(packageRoot);
            return new MapPackageBuild(mapId, visualProfile, seed, toolchainHash, packageHash, packageRoot, metadataBytes, metadataHash,
                collisionSgaf, collisionHash, navigationSgaf, navigationHash, settingsHash, dependencies, plan, navProvenance);
        }

        public static CollisionData ToCollisionData(CompoundPlan plan)
        {
            var boxes = new List<TaggedBox>(plan.CollisionBoxes.Count);
            for (int i = 0; i < plan.CollisionBoxes.Count; i++)
            {
                var b = plan.CollisionBoxes[i];
                if (!b.Gameplay) continue;
                boxes.Add(new TaggedBox(new Box(b.Center, b.Size, Quat.Identity), b.SurfaceTag));
            }
            return new CollisionData(Array.Empty<TaggedCapsule>(), boxes, Array.Empty<TaggedHeightGrid>());
        }

        private static void AddVisualClosure(List<MapPackageDependency> dependencies, CompoundPlan plan, ContentId fingerprint, ContentId toolchainHash, string profile)
        {
            // Combined axis-aligned solid mesh from gameplay boxes (procedural; no authored art).
            var mesh = BuildSolidsMesh(plan);
            byte[] meshBody = GeometryCodec.EncodeMesh(mesh);
            byte[] meshSgaf = ArtifactCodec.Encode(ContentKind.Mesh, meshBody, fingerprint, toolchainHash);
            dependencies.Add(new MapPackageDependency("mesh", ContentIdentity.Hash(meshSgaf), "mesh-" + profile + ".bin", meshSgaf));

            var material = new MaterialData(1, 1, "rgba8-srgb", new[] { new byte[] { 140, 140, 140, 255 } });
            byte[] materialBody = GeometryCodec.EncodeMaterial(material);
            byte[] materialSgaf = ArtifactCodec.Encode(ContentKind.Material, materialBody, fingerprint, toolchainHash);
            dependencies.Add(new MapPackageDependency("material", ContentIdentity.Hash(materialSgaf), "material-" + profile + ".bin", materialSgaf));

            var skeleton = new SkeletonData(new[]
            {
                new JointDefinition(0, 65535, new Float3(0, 0, 0), Quat.Identity, new Float3(-1, -1, -1), new Float3(1, 1, 1))
            });
            byte[] skeletonBody = GeometryCodec.EncodeSkeleton(skeleton);
            byte[] skeletonSgaf = ArtifactCodec.Encode(ContentKind.Skeleton, skeletonBody, fingerprint, toolchainHash);
            dependencies.Add(new MapPackageDependency("skeleton", ContentIdentity.Hash(skeletonSgaf), "skeleton-" + profile + ".bin", skeletonSgaf));

            byte[] shader = MapDocumentJson.EncodeObject(new Dictionary<string, object?>
            {
                ["shaderException"] = "procedural-urp-lit-v1",
                ["profile"] = profile
            });
            dependencies.Add(new MapPackageDependency("shader", ContentIdentity.Hash(shader), "shader-" + profile + ".json", shader));

            byte[] font = MapDocumentJson.EncodeObject(new Dictionary<string, object?>
            {
                ["fontException"] = "engine-allowlist-v1",
                ["policy"] = "procedural-v1"
            });
            dependencies.Add(new MapPackageDependency("font", ContentIdentity.Hash(font), "font-exception.json", font));
        }

        private static MeshData BuildSolidsMesh(CompoundPlan plan)
        {
            var positions = new List<Float3>();
            var normals = new List<Float3>();
            var tangents = new List<Float4>();
            var uv = new List<Float2>();
            var indices = new List<uint>();
            for (int i = 0; i < plan.CollisionBoxes.Count; i++)
            {
                var box = plan.CollisionBoxes[i];
                if (!box.Gameplay) continue;
                var local = PrimitiveGeometry.Box(box.Size, 0f, 1);
                uint baseIndex = (uint)positions.Count;
                for (int v = 0; v < local.Positions.Count; v++)
                {
                    var p = local.Positions[v];
                    positions.Add(new Float3(p.X + box.Center.X, p.Y + box.Center.Y, p.Z + box.Center.Z));
                    normals.Add(local.Normals[v]);
                    tangents.Add(local.Tangents[v]);
                    uv.Add(local.Uv[v]);
                }
                for (int t = 0; t < local.Indices.Count; t++)
                    indices.Add(baseIndex + local.Indices[t]);
            }
            if (positions.Count == 0)
            {
                var fallback = PrimitiveGeometry.Box(new Float3(1, 1, 1), 0f, 1);
                return fallback;
            }
            return new MeshData(positions, normals, tangents, uv, indices, new[] { new Submesh(0, (uint)indices.Count, 0) }, null, null);
        }

        private static byte[] EncodeMetadata(string mapId, CompoundPlan plan, CollisionData collision, ContentId collisionArtifactHash)
        {
            var colliders = new List<object?>();
            int primitiveIndex = 0;
            for (int i = 0; i < plan.CollisionBoxes.Count; i++)
            {
                var box = plan.CollisionBoxes[i];
                if (!box.Gameplay) continue;
                if (primitiveIndex >= collision.Boxes.Count || collision.Boxes[primitiveIndex].SurfaceTag != box.SurfaceTag)
                    throw new InvalidOperationException("Metadata collider order drifted from Collision SGAF.");
                // Capsules, then boxes, then grids — this publisher emits boxes only.
                colliders.Add(new Dictionary<string, object?>
                {
                    ["kind"] = "box",
                    ["primitiveIndex"] = primitiveIndex,
                    ["colliderStableId"] = box.StableId.ToString(CultureInfo.InvariantCulture),
                    ["surfaceId"] = box.SurfaceId.ToString(CultureInfo.InvariantCulture),
                    ["surfaceTag"] = (int)box.SurfaceTag
                });
                primitiveIndex++;
            }
            if (primitiveIndex != collision.Boxes.Count)
                throw new InvalidOperationException("Metadata collider count mismatch.");

            var spawns = BuildSpawnSlots(plan);
            var covers = new List<object?>();
            for (int i = 0; i < plan.Covers.Count; i++)
            {
                var c = plan.Covers[i];
                covers.Add(new Dictionary<string, object?>
                {
                    ["stableId"] = c.StableId.ToString(CultureInfo.InvariantCulture),
                    ["centerMeters"] = Vec(c.Center),
                    ["sizeMeters"] = new List<object?> { c.SizeX, c.SizeY, c.SizeZ },
                    ["peekStandingMeters"] = Vec(c.PeekStanding),
                    ["peekCrouchMeters"] = Vec(c.PeekCrouch),
                    ["hardCover"] = c.HardCover
                });
            }

            var patrol = new List<object?>();
            for (int i = 0; i < plan.RouteCorners.Count && patrol.Count < 64; i++)
                patrol.Add(Vec(plan.RouteCorners[i]));
            if (patrol.Count == 0) patrol.Add(Vec(plan.PlayerSpawn));
            var combat = new List<object?> { Vec(plan.ObjectiveCenter), Vec(plan.PlayerSpawn) };

            var root = new Dictionary<string, object?>
            {
                ["metadataSchema"] = MapRecipe.MetadataSchema,
                ["mapId"] = mapId,
                ["collisionArtifactSha256"] = collisionArtifactHash.ToString(),
                ["collisionRevision"] = checked((int)plan.CollisionRevision),
                ["navRevision"] = checked((int)plan.NavRevision),
                ["colliders"] = colliders,
                ["spawnSlots"] = spawns,
                ["covers"] = covers,
                ["routes"] = new Dictionary<string, object?>
                {
                    ["patrolDestinationsMeters"] = patrol,
                    ["combatDestinationsMeters"] = combat
                }
            };
            return MapDocumentJson.EncodeObject(root);
        }

        private static List<object?> BuildSpawnSlots(CompoundPlan plan)
        {
            // Schema requires >=24 unique slots (12/team). Shared layout with CompoundCatalogFactory (T058).
            var candidates = MapSpawnLayout.BuildBothTeams(plan);
            var slots = new List<object?>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                float yawDegrees = c.Yaw * (180f / MathF.PI);
                slots.Add(new Dictionary<string, object?>
                {
                    ["slotId"] = checked((int)c.SlotId),
                    ["team"] = (int)c.Team,
                    ["positionMeters"] = Vec(c.Position),
                    ["yawDegrees"] = yawDegrees
                });
            }
            return slots;
        }

        private static List<object?> Vec(Float3 v) => new List<object?> { v.X, v.Y, v.Z };

        private static ContentId HashNavSettings(float agentRadius, float agentHeight, float maxStep, float maxSlopeDegrees, float clearanceMargin, uint settingsVersion)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.UTF8, true);
            w.Write("shooter.nav-settings.v1");
            w.Write(settingsVersion);
            w.Write(agentRadius); w.Write(agentHeight); w.Write(maxStep); w.Write(maxSlopeDegrees); w.Write(clearanceMargin);
            return ContentIdentity.Hash(ms.ToArray());
        }

        private static byte[] EncodePackageRoot(
            string mapId,
            string visualProfile,
            ulong seed,
            ContentId toolchainHash,
            ContentId collisionHash,
            ContentId navigationHash,
            ContentId settingsHash,
            ContentId metadataHash,
            IReadOnlyList<MapPackageDependency> dependencies)
        {
            var deps = new List<object?>();
            for (int i = 0; i < dependencies.Count; i++)
            {
                var d = dependencies[i];
                deps.Add(new Dictionary<string, object?>
                {
                    ["path"] = d.Path,
                    ["role"] = d.Role,
                    ["sha256"] = d.Sha256.ToString()
                });
            }
            // Sort dependencies by role then path for stable package bytes.
            deps.Sort((a, b) =>
            {
                var da = (IReadOnlyDictionary<string, object?>)a!;
                var db = (IReadOnlyDictionary<string, object?>)b!;
                int role = string.CompareOrdinal((string)da["role"]!, (string)db["role"]!);
                return role != 0 ? role : string.CompareOrdinal((string)da["path"]!, (string)db["path"]!);
            });

            var root = new Dictionary<string, object?>
            {
                ["packageSchema"] = MapRecipe.PackageSchema,
                ["mapId"] = mapId,
                ["displayName"] = MapRecipe.DisplayName(mapId),
                ["recipeId"] = mapId,
                ["generatorId"] = MapRecipe.GeneratorIdFor(mapId),
                ["generatorVersion"] = MapRecipe.GeneratorVersion,
                ["seed"] = seed.ToString(CultureInfo.InvariantCulture),
                ["toolchainHash"] = toolchainHash.ToString(),
                ["visualProfile"] = visualProfile,
                ["collisionArtifactSha256"] = collisionHash.ToString(),
                ["navigationArtifactSha256"] = navigationHash.ToString(),
                ["navigationAdapterFormat"] = UnityNavMeshBakeCodec.AdapterFormat,
                ["navigationAdapterVersion"] = 1,
                ["navSettingsHash"] = settingsHash.ToString(),
                ["metadataSha256"] = metadataHash.ToString(),
                ["dependencies"] = deps,
                ["provenance"] = new Dictionary<string, object?>
                {
                    ["sourceKind"] = "numeric-recipes",
                    ["policy"] = "procedural-v1",
                    ["decision"] = "docs/decisions.md#adr-035--accepted-verified-release-map-packages-t055-boundary"
                },
                ["validationSummary"] = "validated-v1"
            };
            // Package root uses CanonicalJson-compatible integers/strings only (no floats).
            byte[] encoded = MapDocumentJson.EncodeObject(root);
            return CanonicalJson.Encode(encoded);
        }
    }
}
