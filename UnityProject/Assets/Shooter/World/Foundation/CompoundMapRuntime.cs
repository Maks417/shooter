#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.World.Navigation;
using Shooter.World.Runtime;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.World
{
    /// <summary>
    /// Shipping map runtime: installs verified map-package collision/nav/metadata.
    /// Never regenerates recipes from seeds on the release path (ADR-035 / T055).
    /// </summary>
    public sealed class CompoundMapRuntime : IMapRuntime, IMapCoverSource, IDisposable
    {
        private readonly List<GameObject> solids = new List<GameObject>();

        /// <summary>Allow the packaged NVB1 graph to answer when the native NavMesh sample/query fails. On in product composition so bots never idle; fallback paths are counted separately and never reported as native Complete.</summary>
        public bool AllowPackagedPathFallback { get; set; }

        /// <summary>Read-only per-reason path failure counters (cumulative since construction).</summary>
        public int NavStartSampleMisses { get; private set; }
        public int NavGoalSampleMisses { get; private set; }
        public int NavCalculatePathFailures { get; private set; }
        public int NavPartialPaths { get; private set; }
        public int NavFallbackPathsUsed { get; private set; }
        public int NavFallbackPathsFailed { get; private set; }

        /// <summary>One-line summary of the nav counters for diagnostics logs.</summary>
        public string NavCounterSummary =>
            "navStartMiss=" + NavStartSampleMisses + " navGoalMiss=" + NavGoalSampleMisses +
            " navCalcFail=" + NavCalculatePathFailures + " navPartial=" + NavPartialPaths +
            " navFallbackUsed=" + NavFallbackPathsUsed + " navFallbackFailed=" + NavFallbackPathsFailed;
        private readonly Transform? parent;
        private readonly VerifiedMapPackageStore? packageStore;
        private readonly Func<MapId, ContentId>? gameplayRootResolver;
        private MapGameplayDefinition? definition;
        private List<CollisionBox>? packagedBoxes;
        private readonly List<MapCoverProxy> packagedCovers = new List<MapCoverProxy>();
        private UnityWorldQueries? world;
        private RevisionedPathAdapter? navigation;
        private UnityNavMeshBakeInstaller? navInstall;
        private PublishedMapPackage? loadedPackage;
        private bool disposed;

        public CompoundMapRuntime(Transform? parent = null, VerifiedMapPackageStore? packageStore = null, Func<MapId, ContentId>? gameplayRootResolver = null)
        {
            this.parent = parent;
            this.packageStore = packageStore;
            this.gameplayRootResolver = gameplayRootResolver;
        }

        public IWorldQueries WorldQueries => world ?? throw new InvalidOperationException("Map not loaded.");
        public INavigation Navigation => navigation ?? throw new InvalidOperationException("Map not loaded.");
        public IReadOnlyList<CollisionBox>? PackagedBoxes => packagedBoxes;
        /// <summary>ADR-112: the package's authored cover objects (empty before a load or for packages without any).</summary>
        public IReadOnlyList<MapCoverProxy> Covers => packagedCovers;
        public PublishedMapPackage? LoadedPackage => loadedPackage;
        public uint CollisionRevision => definition?.CollisionRevision ?? 0;
        public uint NavRevision => definition?.NavRevision ?? 0;

        public Task<GameOperationResult> LoadAsync(MapGameplayDefinition definition, CancellationToken cancel)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            cancel.ThrowIfCancellationRequested();
            UnloadInternal();

            this.definition = definition;
            if (packageStore == null || gameplayRootResolver == null)
                return Task.FromResult(new GameOperationResult(GameErrorCode.MissingContent, "map-package-store-missing", "Verified map package store is required; seed regeneration is forbidden."));

            ContentId expectedRoot;
            try { expectedRoot = gameplayRootResolver(definition.MapId); }
            catch (Exception error)
            {
                return Task.FromResult(new GameOperationResult(GameErrorCode.MissingContent, "map-root-unresolved", error.Message));
            }

            if (!packageStore.TryLoadGameplay(definition.MapId, expectedRoot, out var published, out string diagnostic))
            {
                return Task.FromResult(new GameOperationResult(GameErrorCode.MissingContent, "map-package-missing",
                    "Verified package load failed: " + diagnostic + ". Repair required; no silent map substitution."));
            }

            cancel.ThrowIfCancellationRequested();
            try
            {
                var fingerprint = GeneratorFingerprint.Compute(MapRecipe.GeneratorIdFor(definition.MapId.Value), MapRecipe.GeneratorVersion);
                var toolchain = new ContentId(JsonValue.Parse(published.PackageRootBytes).Object["toolchainHash"].String);
                byte[] collisionBody = ArtifactCodec.Decode(published.CollisionSgaf, ContentKind.Collision, fingerprint, toolchain);
                var collision = GeometryCodec.DecodeCollision(collisionBody);
                byte[] navigationBody = ArtifactCodec.Decode(published.NavigationSgaf, ContentKind.Navigation, fingerprint, toolchain);
                var navigationData = GeometryCodec.DecodeNavigation(navigationBody);
                if (navigationData.Format != UnityNavMeshBakeCodec.AdapterFormat || navigationData.Version != UnityNavMeshBakeCodec.AdapterVersion)
                    return Task.FromResult(new GameOperationResult(GameErrorCode.CorruptContent, "nav-adapter-unsupported", "Expected unity.navmesh.bake.v1."));
                var bake = UnityNavMeshBakeCodec.Decode(navigationData.Blob);
                if (bake.Polygons.Count == 0)
                    return Task.FromResult(new GameOperationResult(GameErrorCode.CorruptContent, "nav-empty", "Navigation bake has no polygons."));

                var meta = MapJsonValue.Parse(published.MetadataBytes).Object;
                if (meta["mapId"].String != definition.MapId.Value)
                    return Task.FromResult(new GameOperationResult(GameErrorCode.CorruptContent, "metadata-map-mismatch", "Metadata mapId mismatch."));
                var colliderRows = meta["colliders"].Array;
                if (colliderRows.Count != collision.Boxes.Count)
                    return Task.FromResult(new GameOperationResult(GameErrorCode.CorruptContent, "metadata-count-mismatch", "Collider metadata disagrees with Collision body."));

                var boxes = new List<CollisionBox>(collision.Boxes.Count);
                for (int i = 0; i < collision.Boxes.Count; i++)
                {
                    var row = colliderRows[i].Object;
                    if (row["kind"].String != "box" || row["primitiveIndex"].Int32 != i)
                        throw new InvalidOperationException("Collider metadata order invalid.");
                    if ((int)row["surfaceTag"].Number != collision.Boxes[i].SurfaceTag)
                        throw new InvalidOperationException("Collider surfaceTag mismatch.");
                    ulong stableId = ulong.Parse(row["colliderStableId"].String, CultureInfo.InvariantCulture);
                    ulong surfaceId = ulong.Parse(row["surfaceId"].String, CultureInfo.InvariantCulture);
                    if (stableId == 0 || surfaceId == 0) throw new InvalidOperationException("Stable IDs must be nonzero.");
                    var shape = collision.Boxes[i].Shape;
                    boxes.Add(new CollisionBox(stableId, surfaceId, collision.Boxes[i].SurfaceTag, shape.Center, shape.Size, true));
                }

                packagedCovers.Clear();
                if (meta.TryGetValue("covers", out var coverRows))
                {
                    foreach (var coverValue in coverRows.Array)
                    {
                        var row = coverValue.Object;
                        ulong coverId = ulong.Parse(row["stableId"].String, CultureInfo.InvariantCulture);
                        var center = row["centerMeters"].Array;
                        var size = row["sizeMeters"].Array;
                        packagedCovers.Add(new MapCoverProxy(coverId,
                            new Shooter.Contracts.Float3((float)center[0].Number, (float)center[1].Number, (float)center[2].Number),
                            new Shooter.Contracts.Float3((float)size[0].Number, (float)size[1].Number, (float)size[2].Number),
                            row["hardCover"].Boolean));
                    }
                }

                loadedPackage = published;
                packagedBoxes = boxes;
                world = new UnityWorldQueries(definition.CollisionRevision);
                InstallCollision(boxes);
                Physics.SyncTransforms();

                // Install packaged NVB1 triangulation into Unity NavMesh; path/clearance/corners come from the bake adapter.
                // Does not re-bake against collision and does not route through CompoundConnectivity software grid.
                var settings = NavSettings.SliceDefault;
                navInstall = UnityNavMeshBakeInstaller.Install(bake, settings);
                var install = navInstall;
                navigation = new RevisionedPathAdapter(
                    definition.NavRevision,
                    LocateActor,
                    (a, b, corners) => SolvePath(install, settings, a, b, corners));

                return Task.FromResult(GameOperationResult.Ok);
            }
            catch (OperationCanceledException)
            {
                UnloadInternal();
                return Task.FromResult(new GameOperationResult(GameErrorCode.Cancelled, "map-load-cancelled", "Map load cancelled; partial install released."));
            }
            catch (Exception error)
            {
                UnloadInternal();
                return Task.FromResult(new GameOperationResult(GameErrorCode.CorruptContent, "map-package-corrupt",
                    "Corrupt package retained for repair: " + Trim(error.Message)));
            }
        }

        private NavPathOutcome SolvePath(UnityNavMeshBakeInstaller install, NavSettings settings, Float3 a, Float3 b, List<Float3>? corners)
        {
            if (corners == null) return NavPathOutcome.Failed;
            float radius = settings.RequiredClearance * 2f;
            bool startOk = install.TrySamplePosition(a, radius, out var snappedA);
            bool goalOk = install.TrySamplePosition(b, radius, out var snappedB);
            if (!startOk) NavStartSampleMisses++;
            if (!goalOk) NavGoalSampleMisses++;
            if (startOk && goalOk)
            {
                var outcome = install.CalculatePath(snappedA, snappedB, corners, false);
                if (outcome == NavPathOutcome.Complete) return outcome;
                if (outcome == NavPathOutcome.Partial) { NavPartialPaths++; return outcome; }
                NavCalculatePathFailures++;
            }
            if (!AllowPackagedPathFallback) return NavPathOutcome.Failed;
            if (install.TryCalculatePackagedPath(a, b, corners))
            {
                NavFallbackPathsUsed++;
                return NavPathOutcome.FallbackComplete;
            }
            NavFallbackPathsFailed++;
            corners.Clear();
            return NavPathOutcome.Failed;
        }

        public bool TryValidateSpawn(SpawnSlot slot, out string diagnostic)
        {
            if (definition == null || packagedBoxes == null)
            {
                diagnostic = "not-loaded";
                return false;
            }
            if (float.IsNaN(slot.Position.X) || float.IsNaN(slot.Position.Y) || float.IsNaN(slot.Position.Z))
            {
                diagnostic = "nan-position";
                return false;
            }
            var bounds = definition.Bounds;
            if (slot.Position.X < bounds.Min.X || slot.Position.X > bounds.Max.X ||
                slot.Position.Z < bounds.Min.Z || slot.Position.Z > bounds.Max.Z)
            {
                diagnostic = "out-of-bounds";
                return false;
            }
            // Standing clearance: capsule radius + margin against packaged collision (same as gameplay nav).
            if (CompoundConnectivity.IsBlocked(packagedBoxes, slot.Position.X, slot.Position.Z, CompoundConnectivity.RequiredClearance))
            {
                diagnostic = "blocked";
                return false;
            }
            if (navInstall != null &&
                !navInstall.TrySamplePosition(slot.Position, CompoundConnectivity.RequiredClearance * 2f, out _))
            {
                diagnostic = "nav-unreachable";
                return false;
            }
            diagnostic = "ok";
            return true;
        }

        public GameOperationResult Unload()
        {
            UnloadInternal();
            return GameOperationResult.Ok;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            UnloadInternal();
        }

        private void UnloadInternal()
        {
            for (int i = 0; i < solids.Count; i++)
                if (solids[i] != null) UnityEngine.Object.Destroy(solids[i]);
            solids.Clear();
            world?.Dispose();
            world = null;
            navigation = null;
            navInstall?.Dispose();
            navInstall = null;
            packagedBoxes = null;
            packagedCovers.Clear();
            definition = null;
            loadedPackage = null;
        }

        private Float3? LocateActor(EntityId actor)
        {
            return ActorLocator != null ? ActorLocator(actor) : null;
        }

        public Func<EntityId, Float3?>? ActorLocator { get; set; }

        private void InstallCollision(IReadOnlyList<CollisionBox> boxes)
        {
            CreateSolid(
                "Packaged floor",
                new Vector3(0f, -0.05f, 0f),
                new Vector3(CompoundPlan.MapHalfExtent * 2f, 0.1f, CompoundPlan.MapHalfExtent * 2f),
                1,
                100,
                1);

            for (int i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                CreateSolid(
                    "Packaged solid " + box.StableId,
                    new Vector3(box.Center.X, box.Center.Y, box.Center.Z),
                    new Vector3(box.Size.X, box.Size.Y, box.Size.Z),
                    box.StableId,
                    box.SurfaceId,
                    box.SurfaceTag);
            }
        }

        private void CreateSolid(string label, Vector3 position, Vector3 size, ulong stableId, ulong surfaceId, ushort surfaceTag)
        {
            var item = new GameObject(label) { layer = 8, isStatic = true };
            if (parent != null) item.transform.SetParent(parent, false);
            item.transform.position = position;
            item.AddComponent<BoxCollider>().size = size;
            item.AddComponent<StableSurface>().Initialize(stableId, surfaceId, surfaceTag);
            solids.Add(item);
        }

        private static string Trim(string message) => message.Length <= 180 ? message : message.Substring(0, 180);
    }
}
