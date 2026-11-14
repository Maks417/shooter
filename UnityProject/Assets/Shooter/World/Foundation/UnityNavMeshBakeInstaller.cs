#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Generation.Environment;
using Shooter.World.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Shooter.World
{
    /// <summary>
    /// Installs a packaged ADR-035 NVB1 triangulation into Unity NavMesh for runtime path queries.
    /// Does not re-bake against collision: the triangulation is the published walkable surface.
    /// </summary>
    public sealed class UnityNavMeshBakeInstaller : IDisposable
    {
        private NavMeshData? data;
        private NavMeshDataInstance instance;
        private bool disposed;

        public UnityNavMeshBakeCodec.Bake Bake { get; }
        public NavSettings Settings { get; }
        public bool IsInstalled => !disposed && instance.valid;

        private UnityNavMeshBakeInstaller(UnityNavMeshBakeCodec.Bake bake, NavSettings settings, NavMeshData data, NavMeshDataInstance instance)
        {
            Bake = bake;
            Settings = settings;
            this.data = data;
            this.instance = instance;
        }

        public static UnityNavMeshBakeInstaller Install(UnityNavMeshBakeCodec.Bake bake, NavSettings settings)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));
            if (bake.Polygons.Count == 0) throw new ArgumentException("Bake has no polygons.", nameof(bake));

            var mesh = BuildMesh(bake);
            try
            {
                var sources = new List<NavMeshBuildSource>(1)
                {
                    new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.Mesh,
                        sourceObject = mesh,
                        transform = Matrix4x4.identity,
                        area = 0
                    }
                };

                // Triangulation already accounts for agent radius from the publisher bake.
                // Install with a near-zero radius so BuildNavMeshData does not shrink the surface again.
                var buildSettings = NavMesh.CreateSettings();
                buildSettings.agentRadius = 0.05f;
                buildSettings.agentHeight = settings.AgentHeight;
                buildSettings.agentClimb = settings.MaxStep;
                buildSettings.agentSlope = settings.MaxSlopeDegrees;
                // Unity requires voxelSize well below agentRadius; 0.08 with radius 0.05 yields empty NavMeshData.
                buildSettings.overrideVoxelSize = true;
                buildSettings.voxelSize = 0.02f;
                buildSettings.overrideTileSize = true;
                buildSettings.tileSize = 64;

                Vector3 min = new Vector3(bake.BoundsMin.X, bake.BoundsMin.Y, bake.BoundsMin.Z);
                Vector3 max = new Vector3(bake.BoundsMax.X, bake.BoundsMax.Y, bake.BoundsMax.Z);
                var bounds = new Bounds((min + max) * 0.5f, (max - min) + Vector3.one * 2f);
                NavMeshData data = NavMeshBuilder.BuildNavMeshData(
                    buildSettings,
                    sources,
                    bounds,
                    Vector3.zero,
                    Quaternion.identity);
                if (data == null) throw new InvalidOperationException("NVB1 NavMesh install failed to build NavMeshData.");
                NavMeshDataInstance instance = NavMesh.AddNavMeshData(data);
                if (!instance.valid)
                {
                    UnityEngine.Object.Destroy(data);
                    throw new InvalidOperationException("NVB1 NavMesh install failed to add NavMeshData.");
                }
                return new UnityNavMeshBakeInstaller(bake, settings, data, instance);
            }
            finally
            {
                UnityEngine.Object.Destroy(mesh);
            }
        }

        public bool TrySamplePosition(Float3 point, float maxDistance, out Float3 sampled)
        {
            sampled = default;
            if (!IsInstalled) return false;
            if (NavMesh.SamplePosition(UnityWorldQueries.V(point), out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                sampled = UnityWorldQueries.F(hit.position);
                return true;
            }
            return false;
        }

        public bool TryCalculatePath(Float3 start, Float3 goal, List<Float3> corners)
        {
            if (corners == null) throw new ArgumentNullException(nameof(corners));
            corners.Clear();
            if (!IsInstalled) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(UnityWorldQueries.V(start), UnityWorldQueries.V(goal), NavMesh.AllAreas, path) ||
                path.status == NavMeshPathStatus.PathInvalid ||
                path.corners == null ||
                path.corners.Length == 0)
            {
                // Fall back to packaged triangulation graph if engine path fails after install.
                return UnityNavMeshBakeCodec.TryFindPath(Bake, start, goal, corners);
            }
            for (int i = 0; i < path.corners.Length; i++)
                corners.Add(UnityWorldQueries.F(path.corners[i]));
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (instance.valid) instance.Remove();
            instance = default;
            if (data != null)
            {
                UnityEngine.Object.Destroy(data);
                data = null;
            }
        }

        private static Mesh BuildMesh(UnityNavMeshBakeCodec.Bake bake)
        {
            var mesh = new Mesh { name = "NVB1-install" };
            var verts = new Vector3[bake.Vertices.Count];
            for (int i = 0; i < bake.Vertices.Count; i++)
            {
                var v = bake.Vertices[i];
                verts[i] = new Vector3(v.X, v.Y, v.Z);
            }
            var indices = new List<int>(bake.Polygons.Count * 6);
            for (int p = 0; p < bake.Polygons.Count; p++)
            {
                var poly = bake.Polygons[p];
                // Fan triangulation for 3..6-gons. Winding must face +Y so Unity NavMesh keeps the surface.
                for (int i = 1; i + 1 < poly.Indices.Length; i++)
                {
                    indices.Add((int)poly.Indices[0]);
                    indices.Add((int)poly.Indices[i + 1]);
                    indices.Add((int)poly.Indices[i]);
                }
            }
            mesh.SetVertices(verts);
            mesh.SetTriangles(indices, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
