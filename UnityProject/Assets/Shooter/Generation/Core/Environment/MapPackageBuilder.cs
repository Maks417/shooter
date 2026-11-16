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
        public const int MapSurfaceResolution = 128;
        public const int MapSurfaceTileMm = 2000;
        public const ulong MapSurfaceSeed = 460072030UL;
        // Exactly four immutable bakes shared by the two maps and all graphics profiles.
        // Lazy publication is thread safe; package encoders copy the frozen plane bytes.
        private static readonly Lazy<MaterialBake[]> surfaceBakes = new Lazy<MaterialBake[]>(CreateSurfaceBakes, true);

        public static MapPackageBuild Build(string mapId, string visualProfile, ContentId toolchainHash)
        {
            if (visualProfile != "gameplay" && visualProfile != "low" && visualProfile != "balanced" && visualProfile != "high")
                throw new ArgumentOutOfRangeException(nameof(visualProfile));
            ulong seed = MapRecipe.ApprovedSeed(mapId);
            var plan = ProductMapPlanner.Generate(mapId);
            ProductMapPlanner.ValidateOrThrow(mapId, plan);

            var collision = ToCollisionData(plan);
            byte[] collisionBody = GeometryCodec.EncodeCollision(collision);
            var generatorId = MapRecipe.GeneratorIdFor(mapId);
            var fingerprint = GeneratorFingerprint.Compute(generatorId, MapRecipe.GeneratorVersion);
            byte[] collisionSgaf = ArtifactCodec.Encode(ContentKind.Collision, collisionBody, fingerprint, toolchainHash);
            var collisionHash = ContentIdentity.Hash(collisionSgaf);

            // Locked shooter.nav-settings.v1 SliceDefault (must match World.Navigation.NavSettings).
            const float agentRadius = 0.32f, agentHeight = 1.8f, maxStep = 0.25f, maxSlope = 45f, clearanceMargin = 0.05f;
            float requiredClearance = agentRadius + clearanceMargin;
            var settingsHash = HashNavSettings(agentRadius, agentHeight, maxStep, maxSlope, clearanceMargin, 1);
            UnityNavMeshBakeCodec.Bake bake;
            string navProvenance;
            if (NavBakePublication.TryNative(
                    plan.CollisionBoxes,
                    CompoundPlan.MapHalfExtent,
                    agentRadius,
                    agentHeight,
                    maxStep,
                    maxSlope,
                    clearanceMargin,
                    out var nativeBake,
                    out navProvenance))
            {
                bake = nativeBake;
            }
            else
            {
                // Pure tests / MapContent without Unity editor bridge: software grid only, honest pending provenance.
                bake = UnityNavMeshBakeCodec.BuildWalkableGrid(plan.CollisionBoxes, CompoundPlan.MapHalfExtent, 8f, requiredClearance);
                navProvenance = NavBakePublication.SoftwarePendingProvenance;
            }
            byte[] bakeBlob = UnityNavMeshBakeCodec.Encode(bake);
            var navigation = new NavigationData(UnityNavMeshBakeCodec.AdapterFormat, UnityNavMeshBakeCodec.AdapterVersion, settingsHash, bakeBlob);
            byte[] navigationBody = GeometryCodec.EncodeNavigation(navigation);
            byte[] navigationSgaf = ArtifactCodec.Encode(ContentKind.Navigation, navigationBody, fingerprint, toolchainHash);
            var navigationHash = ContentIdentity.Hash(navigationSgaf);

            byte[] metadataBytes = EncodeMetadata(mapId, plan, collision, collisionHash);
            var metadataHash = ContentIdentity.Hash(metadataBytes);

            var dependencies = new List<MapPackageDependency>
            {
                new MapPackageDependency("collision", collisionHash, "collision-gameplay.bin", collisionSgaf),
                new MapPackageDependency("navigation", navigationHash, "navigation-gameplay.bin", navigationSgaf),
                new MapPackageDependency("metadata", metadataHash, "map-metadata.json", metadataBytes)
            };

            if (visualProfile != "gameplay")
                AddVisualClosure(dependencies, mapId, plan, fingerprint, toolchainHash, visualProfile);

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

        private static void AddVisualClosure(
            List<MapPackageDependency> dependencies,
            string mapId,
            CompoundPlan plan,
            ContentId mapFingerprint,
            ContentId toolchainHash,
            string profile)
        {
            AddSgaf(dependencies, "mesh", MapVisualClosure.MapSolidsMeshPath, ContentKind.Mesh,
                GeometryCodec.EncodeMesh(MapSolidsMesh.Build(plan, mapId)), mapFingerprint, toolchainHash);
            AddSgaf(dependencies, "mesh", MapVisualClosure.UnitBoxMeshPath, ContentKind.Mesh,
                GeometryCodec.EncodeMesh(PrimitiveGeometry.Box(new Float3(1f, 1f, 1f), 0f, 1)), mapFingerprint, toolchainHash);
            for (int hand = 0; hand < MapVisualClosure.FirstPersonHandPaths.Length; hand++)
            {
                AddSgaf(dependencies, "mesh", MapVisualClosure.FirstPersonHandPaths[hand], ContentKind.Mesh,
                    GeometryCodec.EncodeMesh(FirstPersonHandGeometry.Build((byte)(hand / 2), hand % 2 == 1)), mapFingerprint, toolchainHash);
                AddSgaf(dependencies, "skeleton", MapVisualClosure.FirstPersonHandSkeletonPath(hand), ContentKind.Skeleton,
                    GeometryCodec.EncodeSkeleton(new FirstPersonHandRig((byte)(hand/2),hand%2==1).Skeleton), mapFingerprint, toolchainHash);
            }

            ContentId soldierFp = MapVisualClosure.SoldierFingerprint;
            AddSgaf(dependencies, "skeleton", MapVisualClosure.SoldierSkeletonPath, ContentKind.Skeleton,
                GeometryCodec.EncodeSkeleton(SoldierSkeleton.Definition), soldierFp, toolchainHash);
            var soldierRecipe = SoldierRecipe.Parse(Encoding.ASCII.GetBytes(
                "{\"schemaId\":\"shooter.soldier.v1\",\"recipeId\":\"soldiers/baseline\",\"generatorId\":\"soldier.loft\",\"generatorVersion\":\"1.0.3\",\"seed\":\"460072030\",\"parameters\":{\"heightMm\":1800,\"shoulderWidthMm\":420,\"hipWidthMm\":280,\"uniformOffsetMm\":6,\"nearTorsoSegments\":16,\"nearLimbSegments\":12,\"walkCycleMm\":1400,\"runCycleMm\":2200,\"walkDutyPpm\":500000,\"runDutyPpm\":350000},\"dependencies\":[]}"));
            var soldierBuild = SoldierGeometry.Build(soldierRecipe);
            for (int lod = 0; lod < MapVisualClosure.SoldierLodPaths.Length; lod++)
                AddSgaf(dependencies, "mesh", MapVisualClosure.SoldierLodPaths[lod], ContentKind.Mesh,
                    GeometryCodec.EncodeMesh(soldierBuild.Lods[lod]), soldierFp, toolchainHash);

            foreach(Shooter.Generation.Fx.FlipbookKind kind in Enum.GetValues(typeof(Shooter.Generation.Fx.FlipbookKind)))
                AddSgaf(dependencies,"material",Shooter.Generation.Fx.FxFlipbook.Path(kind),ContentKind.Material,
                    GeometryCodec.EncodeMaterial(Shooter.Generation.Fx.FxFlipbook.Bake(kind,profile=="high"?64:32)),Shooter.Generation.Fx.FxFlipbook.Fingerprint,toolchainHash);
            AddSgaf(dependencies,"material",ProceduralDecals.AtlasPath,ContentKind.Material,
                GeometryCodec.EncodeMaterial(ProceduralDecals.Bake()),ProceduralDecals.Fingerprint,toolchainHash);
            var definition=MapRegistry.Require(mapId);
            var decals=new List<ArenaDecal>(definition.AdditionalDecals);
            if(definition.HasLayoutDocument)decals.AddRange(definition.LayoutDocument.Decals);
            AddJson(dependencies,"shader",ProceduralDecals.PlacementsPath,ProceduralDecals.EncodePlacements(decals));
            // Weapon meshes live in the shared weapon package (ADR-078), not in map packages.
            // ADR-081 baked ambient-occlusion volume from the plan's collision boxes (deterministic, cached per map).
            byte[] occlusion = MapAmbientOcclusionVolume.Bake(mapId + ":" + plan.CollisionHash, plan.CollisionBoxes);
            AddSgaf(dependencies, "material", MapAmbientOcclusionVolume.Path, ContentKind.Material,
                GeometryCodec.EncodeMaterial(MapAmbientOcclusionVolume.ToAtlas(occlusion)), mapFingerprint, toolchainHash);

            for (int i = 0; i < MapVisualClosure.MapMaterialPaths.Length; i++)
            {
                string path = MapVisualClosure.MapMaterialPaths[i];
                MaterialBake bake = NoiseSurface(mapId, profile, i);
                var surfaceFingerprint = GeneratorFingerprint.Compute(NoiseMaterialRecipe.Generator, NoiseMaterialRecipe.Version);
                AddSgaf(dependencies, "material", path, ContentKind.Material, GeometryCodec.EncodeMaterial(bake.Albedo), surfaceFingerprint, toolchainHash);
                AddSgaf(dependencies, "material", MapVisualClosure.MaterialCompanionPath(path, "normal"), ContentKind.Material,
                    GeometryCodec.EncodeMaterial(bake.Normal), surfaceFingerprint, toolchainHash);
                AddSgaf(dependencies, "material", MapVisualClosure.MaterialCompanionPath(path, "mask"), ContentKind.Material,
                    GeometryCodec.EncodeMaterial(bake.Mask), surfaceFingerprint, toolchainHash);
            }
            for (int family = 0; family < MapVisualClosure.FirstPersonSurfacePaths.Length; family++)
            {
                string path = MapVisualClosure.FirstPersonSurfacePaths[family];
                MaterialBake bake = FirstPersonSurfaceBaker.Bake(family);
                AddSgaf(dependencies, "material", path, ContentKind.Material, GeometryCodec.EncodeMaterial(bake.Albedo), mapFingerprint, toolchainHash);
                AddSgaf(dependencies, "material", MapVisualClosure.MaterialCompanionPath(path, "normal"), ContentKind.Material,
                    GeometryCodec.EncodeMaterial(bake.Normal), mapFingerprint, toolchainHash);
                AddSgaf(dependencies, "material", MapVisualClosure.MaterialCompanionPath(path, "mask"), ContentKind.Material,
                    GeometryCodec.EncodeMaterial(bake.Mask), mapFingerprint, toolchainHash);
            }
            AddSoldierUniform(dependencies,MapVisualClosure.SoldierMaterialPaths[0],1,toolchainHash);
            AddSolidMaterial(dependencies, MapVisualClosure.SoldierMaterialPaths[1], 15, 20, 31, 255, mapFingerprint, toolchainHash);
            AddSolidMaterial(dependencies, MapVisualClosure.SoldierMaterialPaths[2], 122, 82, 56, 255, mapFingerprint, toolchainHash);
            AddSolidMaterial(dependencies, MapVisualClosure.SoldierMaterialPaths[3], 10, 20, 31, 255, mapFingerprint, toolchainHash);
            AddSoldierUniform(dependencies,MapVisualClosure.SoldierMaterialPaths[4],2,toolchainHash);
            foreach(byte team in new byte[]{1,2})
            {
                AddSolidMaterial(dependencies,MapVisualClosure.SoldierBandPath(team),team==1?(byte)55:(byte)160,team==1?(byte)135:(byte)72,team==1?(byte)160:(byte)48,255,mapFingerprint,toolchainHash);
                AddSgaf(dependencies,"material",MapVisualClosure.SoldierPatchPath(team),ContentKind.Material,GeometryCodec.EncodeMaterial(SoldierInsignia.Bake(team)),mapFingerprint,toolchainHash);
            }
            AddSolidMaterial(dependencies, MapVisualClosure.SoldierMaterialPaths[5], 31, 18, 13, 255, mapFingerprint, toolchainHash);
            AddSolidMaterial(dependencies, MapVisualClosure.SoldierMaterialPaths[6], 122, 82, 56, 255, mapFingerprint, toolchainHash);
            AddSolidMaterial(dependencies, MapVisualClosure.SoldierMaterialPaths[7], 26, 10, 8, 255, mapFingerprint, toolchainHash);

            byte[] shader = MapDocumentJson.EncodeObject(new Dictionary<string, object?>
            {
                ["shaderException"] = "procedural-urp-lit-v1",
                ["unityShader"] = "Universal Render Pipeline/Lit",
                ["retainedByPlayer"] = true,
                ["profile"] = profile,
                ["policy"] = "procedural-v1",
                ["decision"] = "docs/decisions.md#adr-035--accepted-verified-release-map-packages-t055-boundary"
            });
            AddJson(dependencies, "shader", MapVisualClosure.ShaderPath(profile), shader);

            byte[] font = MapDocumentJson.EncodeObject(new Dictionary<string, object?>
            {
                ["fontException"] = "ui-typeface-bank-v1",
                ["policy"] = "procedural-v1",
                ["retainedByPlayer"] = true,
                ["decision"] = "docs/decisions.md#adr-066--accepted-non-commercial-ui-typeface-bank-barlow-condensed"
            });
            AddJson(dependencies, "font", MapVisualClosure.FontPath, font);

            ValidateVisualClosure(dependencies, mapId, profile, toolchainHash, mapFingerprint, soldierFp);
        }

        private static void AddSgaf(
            List<MapPackageDependency> dependencies,
            string role,
            string path,
            ContentKind kind,
            byte[] body,
            ContentId generatorFingerprint,
            ContentId toolchainHash)
        {
            byte[] sgaf = ArtifactCodec.Encode(kind, body, generatorFingerprint, toolchainHash);
            dependencies.Add(new MapPackageDependency(role, ContentIdentity.Hash(sgaf), path, sgaf));
        }

        private static void AddJson(List<MapPackageDependency> dependencies, string role, string path, byte[] json)
        {
            dependencies.Add(new MapPackageDependency(role, ContentIdentity.Hash(json), path, json));
        }

        private static readonly Dictionary<string, MaterialBake> noiseBakes = new Dictionary<string, MaterialBake>(StringComparer.Ordinal);
        private static MaterialBake NoiseSurface(string mapId, string profile, int slot)
        {
            string role = MapRegistry.Require(mapId).MaterialRoles[slot]; int n = profile == "high" ? 256 : 128;
            string key = role + ":" + n;
            lock (noiseBakes)
            {
                if (!noiseBakes.TryGetValue(key, out var bake))
                    noiseBakes.Add(key, bake = NoiseMaterialBaker.Bake(new NoiseMaterialRecipe(role, n, MapSurfaceSeed)));
                return bake;
            }
        }

        private static MaterialBake[] CreateSurfaceBakes()
        {
            string[] names = { "concrete", "cover", "ground", "asphalt" };
            string[] families = { "concrete", "metal", "soil", "asphalt" };
            var bakes = new MaterialBake[names.Length];
            for (int i = 0; i < bakes.Length; i++)
            {
                // Versioned map 1.0.1 composes the unchanged material.surface 1.0.1 algorithm.
                // Keep all inputs explicit and independent of profile, map load order and gameplay RNG.
                string source = "{\"schemaId\":\"" + MaterialRecipe.Schema + "\",\"recipeId\":\"materials/map-" + names[i] +
                    "\",\"generatorId\":\"" + MaterialRecipe.Generator + "\",\"generatorVersion\":\"" + MaterialRecipe.Version +
                    "\",\"seed\":\"" + MapSurfaceSeed.ToString(CultureInfo.InvariantCulture) + "\",\"parameters\":{\"family\":\"" + families[i] +
                    "\",\"resolution\":" + MapSurfaceResolution.ToString(CultureInfo.InvariantCulture) + ",\"tileMm\":" +
                    MapSurfaceTileMm.ToString(CultureInfo.InvariantCulture) + "},\"dependencies\":[]}";
                bakes[i] = MaterialBaker.Bake(MaterialRecipe.Parse(Encoding.ASCII.GetBytes(source)));
            }
            return bakes;
        }

        private static void AddSoldierUniform(List<MapPackageDependency> dependencies,string path,byte team,ContentId toolchain)
        {
            var bake=NoiseMaterialBaker.Bake(new NoiseMaterialRecipe("camo-fabric",128,460072030UL+team,250));
            var fp=GeneratorFingerprint.Compute(NoiseMaterialRecipe.Generator,NoiseMaterialRecipe.Version);
            AddSgaf(dependencies,"material",path,ContentKind.Material,GeometryCodec.EncodeMaterial(bake.Albedo),fp,toolchain);
            AddSgaf(dependencies,"material",MapVisualClosure.MaterialCompanionPath(path,"normal"),ContentKind.Material,GeometryCodec.EncodeMaterial(bake.Normal),fp,toolchain);
            AddSgaf(dependencies,"material",MapVisualClosure.MaterialCompanionPath(path,"mask"),ContentKind.Material,GeometryCodec.EncodeMaterial(bake.Mask),fp,toolchain);
        }

        private static void AddSolidMaterial(
            List<MapPackageDependency> dependencies,
            string path,
            byte r, byte g, byte b, byte a,
            ContentId fingerprint,
            ContentId toolchainHash)
        {
            var material = new MaterialData(1, 1, "rgba8-srgb", new[] { new byte[] { r, g, b, a } });
            AddSgaf(dependencies, "material", path, ContentKind.Material, GeometryCodec.EncodeMaterial(material), fingerprint, toolchainHash);
        }

        private static void ValidateVisualClosure(
            List<MapPackageDependency> dependencies,
            string mapId,
            string profile,
            ContentId toolchainHash,
            ContentId mapFingerprint,
            ContentId soldierFingerprint)
        {
            if (!MapVisualClosure.TryExtract(dependencies, mapId, profile, toolchainHash, out var payloads, out string diagnostic))
                throw new InvalidOperationException("Visual closure incomplete: " + diagnostic);

            var skeleton = MapVisualClosure.DecodeSoldierSkeleton(payloads);
            if (skeleton.Joints.Count != SoldierSkeleton.JointCount)
                throw new InvalidOperationException("Soldier skeleton joint count mismatch.");
            byte[] expectedSkeleton = GeometryCodec.EncodeSkeleton(SoldierSkeleton.Definition);
            byte[] actualSkeleton = GeometryCodec.EncodeSkeleton(skeleton);
            if (expectedSkeleton.Length != actualSkeleton.Length)
                throw new InvalidOperationException("Soldier skeleton identity mismatch.");
            for (int i = 0; i < expectedSkeleton.Length; i++)
                if (expectedSkeleton[i] != actualSkeleton[i])
                    throw new InvalidOperationException("Soldier skeleton identity mismatch.");

            void RequireMesh(string path, ContentId fingerprint)
            {
                byte[] body = ArtifactCodec.Decode(payloads.Bytes(path), ContentKind.Mesh, fingerprint, toolchainHash);
                var mesh = GeometryCodec.DecodeMesh(body);
                if (mesh.Positions.Count == 0 || mesh.Indices.Count < 3)
                    throw new InvalidOperationException("Visual mesh empty: " + path);
            }
            RequireMesh(MapVisualClosure.MapSolidsMeshPath, mapFingerprint);
            RequireMesh(MapVisualClosure.UnitBoxMeshPath, mapFingerprint);
            for (int hand = 0; hand < MapVisualClosure.FirstPersonHandPaths.Length; hand++)
            {
                byte slot = (byte)(hand/2); bool support = hand%2==1;
                var mesh = MapVisualClosure.DecodeFirstPersonHand(payloads,slot,support);
                var handSkeleton = MapVisualClosure.DecodeFirstPersonHandSkeleton(payloads,slot,support);
                var expected = GeometryCodec.EncodeSkeleton(new FirstPersonHandRig(slot,support).Skeleton);
                if (mesh.Joints == null || mesh.Weights == null || ContentIdentity.Hash(expected) != ContentIdentity.Hash(GeometryCodec.EncodeSkeleton(handSkeleton)))
                    throw new InvalidOperationException("First-person articulated hand identity mismatch.");
                foreach (var joints in mesh.Joints)
                    if (joints.X >= FirstPersonHandRig.JointCount || joints.Y >= FirstPersonHandRig.JointCount || joints.Z >= FirstPersonHandRig.JointCount || joints.W >= FirstPersonHandRig.JointCount)
                        throw new InvalidOperationException("First-person hand skin index outside its camera-only skeleton.");
            }
            for (int i = 0; i < MapVisualClosure.SoldierLodPaths.Length; i++)
                RequireMesh(MapVisualClosure.SoldierLodPaths[i], soldierFingerprint);

            var decalAtlas=MapVisualClosure.DecodeMaterial(payloads,ProceduralDecals.AtlasPath,ProceduralDecals.Fingerprint);
            if(decalAtlas.Width!=256||decalAtlas.Height!=128||decalAtlas.ChannelFormat!="rgba8-linear")throw new InvalidOperationException("Invalid numeric decal atlas.");
            foreach(Shooter.Generation.Fx.FlipbookKind kind in Enum.GetValues(typeof(Shooter.Generation.Fx.FlipbookKind)))
            {
                var fx=MapVisualClosure.DecodeMaterial(payloads,Shooter.Generation.Fx.FxFlipbook.Path(kind),Shooter.Generation.Fx.FxFlipbook.Fingerprint);
                if(fx.Width!=(profile=="high"?512:256)||fx.Height!=fx.Width||fx.ChannelFormat!="rgba8-linear")throw new InvalidOperationException("Invalid numeric FX atlas.");
            }
            var occlusionAtlas = MapVisualClosure.DecodeMapMaterial(payloads, MapAmbientOcclusionVolume.Path);
            MapAmbientOcclusionVolume.FromAtlas(occlusionAtlas);

            void RequireMaterial(string path)
            {
                var material = MapVisualClosure.DecodeMapMaterial(payloads, path);
                if (material.Width == 0 || material.Height == 0 || material.MipBytes.Count == 0)
                    throw new InvalidOperationException("Visual material empty: " + path);
            }
            foreach (string path in MapVisualClosure.MapMaterialPaths)
            {
                RequireMaterial(path);
                var albedo = MapVisualClosure.DecodeMapMaterial(payloads, path);
                if (albedo.Width != (profile=="high"?256:128) || albedo.Height != albedo.Width ||
                    albedo.ChannelFormat != "rgba8-srgb" || albedo.MipBytes.Count != (profile=="high"?9:8))
                    throw new InvalidOperationException("Map surface albedo is not a complete production plane: " + path);
                foreach (string plane in new[] { "normal", "mask" })
                {
                    string companionPath = MapVisualClosure.MaterialCompanionPath(path, plane);
                    RequireMaterial(companionPath);
                    var companion = MapVisualClosure.DecodeMapMaterial(payloads, companionPath);
                    if (companion.Width != albedo.Width || companion.Height != albedo.Height ||
                        companion.MipBytes.Count != albedo.MipBytes.Count || companion.ChannelFormat != "rgba8-linear")
                        throw new InvalidOperationException("Map surface companion mismatch: " + companionPath);
                }
            }
            foreach (string path in MapVisualClosure.FirstPersonSurfacePaths)
            {
                RequireMaterial(path);
                var albedo = MapVisualClosure.DecodeMapMaterial(payloads, path);
                if (albedo.Width != FirstPersonSurfaceBaker.Resolution || albedo.Height != albedo.Width ||
                    albedo.ChannelFormat != "rgba8-srgb" || albedo.MipBytes.Count != 8)
                    throw new InvalidOperationException("First-person surface requires a complete 128px production plane: " + path);
                foreach (string plane in new[] { "normal", "mask" })
                {
                    string companionPath = MapVisualClosure.MaterialCompanionPath(path, plane);
                    RequireMaterial(companionPath);
                    var companion = MapVisualClosure.DecodeMapMaterial(payloads, companionPath);
                    if (companion.Width != albedo.Width || companion.Height != albedo.Height ||
                        companion.ChannelFormat != "rgba8-linear" || companion.MipBytes.Count != 8)
                        throw new InvalidOperationException("First-person surface companion mismatch: " + companionPath);
                }
            }
            for (int i = 0; i < MapVisualClosure.SoldierMaterialPaths.Length; i++) RequireMaterial(MapVisualClosure.SoldierMaterialPaths[i]);

            var shader = MapJsonValue.Parse(payloads.Bytes(MapVisualClosure.ShaderPath(profile))).Object;
            if (shader["shaderException"].String != "procedural-urp-lit-v1" ||
                shader["unityShader"].String != "Universal Render Pipeline/Lit" ||
                !shader["retainedByPlayer"].Boolean)
                throw new InvalidOperationException("Shader provenance incomplete.");
            var font = MapJsonValue.Parse(payloads.Bytes(MapVisualClosure.FontPath)).Object;
            if (font["fontException"].String != "ui-typeface-bank-v1" || !font["retainedByPlayer"].Boolean)
                throw new InvalidOperationException("Font provenance incomplete.");
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
