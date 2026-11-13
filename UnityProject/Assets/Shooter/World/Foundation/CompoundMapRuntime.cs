#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation.Environment;
using Shooter.World.Navigation;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.World
{
    /// <summary>
    /// M8 T037 real map runtime: installs compound collision into PhysX and exposes gameplay pathfinding
    /// with real corners (CompoundConnectivity). Shipping composition uses this; FakeMapRuntime is rejected.
    /// </summary>
    public sealed class CompoundMapRuntime : IMapRuntime, IDisposable
    {
        private readonly List<GameObject> solids = new List<GameObject>();
        private readonly Transform? parent;
        private MapGameplayDefinition? definition;
        private CompoundPlan? plan;
        private UnityWorldQueries? world;
        private RevisionedPathAdapter? navigation;
        private bool disposed;

        public CompoundMapRuntime(Transform? parent = null)
        {
            this.parent = parent;
        }

        public IWorldQueries WorldQueries => world ?? throw new InvalidOperationException("Map not loaded.");
        public INavigation Navigation => navigation ?? throw new InvalidOperationException("Map not loaded.");
        public CompoundPlan? Plan => plan;
        public uint CollisionRevision => plan?.CollisionRevision ?? 0;
        public uint NavRevision => plan?.NavRevision ?? 0;

        public Task<GameOperationResult> LoadAsync(MapGameplayDefinition definition, CancellationToken cancel)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            cancel.ThrowIfCancellationRequested();
            UnloadInternal();

            this.definition = definition;
            ulong seed = definition.MapId.Value switch
            {
                "training-compound" => 460072030UL,
                "depot-yard" => 460072031UL,
                _ => 0UL
            };
            if (seed == 0)
                return Task.FromResult(new GameOperationResult(GameErrorCode.MissingContent, "unknown-map-seed", "No packaged compound seed for map."));

            plan = definition.MapId.Value == "depot-yard"
                ? DepotPlanner.Generate(seed)
                : CompoundPlanner.Generate(seed);
            var report = CompoundValidation.Validate(plan);
            if (!report.Passed)
                return Task.FromResult(new GameOperationResult(GameErrorCode.CorruptContent, "compound-invalid", report.Diagnostic));

            world = new UnityWorldQueries(plan.CollisionRevision);
            InstallCollision(plan);
            Physics.SyncTransforms();

            var boxes = plan.CollisionBoxes;
            float clearance = CompoundConnectivity.RequiredClearance;
            navigation = new RevisionedPathAdapter(
                plan.NavRevision,
                LocateActor,
                (a, b, corners) =>
                {
                    if (corners == null) return false;
                    return CompoundConnectivity.TryFindPath(boxes, a, b, corners, clearance);
                });

            // Gameplay navigation is RevisionedPathAdapter (real compound path corners). Unity NavMesh bake is not required for M8 AI.
            return Task.FromResult(GameOperationResult.Ok);
        }

        public bool TryValidateSpawn(SpawnSlot slot, out string diagnostic)
        {
            if (definition == null || plan == null)
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
            if (CompoundConnectivity.IsBlocked(plan.CollisionBoxes, slot.Position.X, slot.Position.Z, CompoundConnectivity.RequiredClearance))
            {
                diagnostic = "blocked";
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
            plan = null;
            definition = null;
        }

        private Float3? LocateActor(EntityId actor)
        {
            // Navigation locate is supplied by authority through RevisionedPathAdapter callers that pass actor feet
            // via the solve path when tickets are polled; ticket origin uses this callback. Product session updates
            // actor positions before poll by setting ActorLocator.
            return ActorLocator != null ? ActorLocator(actor) : null;
        }

        public Func<EntityId, Float3?>? ActorLocator { get; set; }

        private void InstallCollision(CompoundPlan compound)
        {
            // Ground plane covering map extent.
            CreateSolid(
                "Compound floor",
                new Vector3(0f, -0.05f, 0f),
                new Vector3(CompoundPlan.MapHalfExtent * 2f, 0.1f, CompoundPlan.MapHalfExtent * 2f),
                1,
                100,
                1);

            for (int i = 0; i < compound.CollisionBoxes.Count; i++)
            {
                var box = compound.CollisionBoxes[i];
                CreateSolid(
                    "Compound solid " + box.StableId,
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

    }
}
