#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Generation.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Shooter.Build
{
    /// <summary>
    /// Registers pinned AI Navigation bake against authoritative collision boxes for map package publication.
    /// Lives in the Unity editor assembly so pure MapContent tooling stays free of engine references.
    /// </summary>
    [InitializeOnLoad]
    public static class UnityNavMeshBakeEditorBridge
    {
        static UnityNavMeshBakeEditorBridge()
        {
            NavBakePublication.NativeProvider = BakeNative;
        }

        private static NavBakePublication.NativeBake? BakeNative(
            IReadOnlyList<CollisionBox> boxes,
            float halfExtent,
            float agentRadius,
            float agentHeight,
            float maxStep,
            float maxSlopeDegrees,
            float clearanceMargin)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            if (halfExtent <= 0f || agentRadius <= 0f || agentHeight <= 0f) return null;

            // Copy the built-in agent (ID 0) instead of NavMesh.CreateSettings(): creating an agent type bumps
            // m_LastAgentTypeID, which makes Unity reserialize ProjectSettings/NavMeshAreas.asset and dirty the
            // source tree on every native publication. The bake consumes the struct values below, not the ID.
            var buildSettings = NavMesh.GetSettingsByID(0);
            buildSettings.agentRadius = agentRadius;
            buildSettings.agentHeight = agentHeight;
            buildSettings.agentClimb = maxStep;
            buildSettings.agentSlope = maxSlopeDegrees;
            buildSettings.overrideVoxelSize = true;
            buildSettings.voxelSize = Mathf.Clamp(agentRadius / 3f, 0.05f, 0.2f);
            buildSettings.overrideTileSize = true;
            buildSettings.tileSize = 64;
            // ADR-114: pad each solid's Not-Walkable footprint by the locked clearance margin plus a quarter voxel. The
            // agent-radius erosion is voxel-quantized and chamfered at convex wall ends (measured 0.259 m from a corner
            // with the margin alone), so without the pad a nav vertex lands closer than radius - 0.05 to the wall.
            float pad = Math.Max(0f, clearanceMargin) + buildSettings.voxelSize * 0.25f;

            var sources = new List<NavMeshBuildSource>(boxes.Count + 1)
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    size = new Vector3(halfExtent * 2f, 0.1f, halfExtent * 2f),
                    area = 0,
                    transform = Matrix4x4.TRS(new Vector3(0f, -0.05f, 0f), Quaternion.identity, Vector3.one)
                }
            };
            for (int i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                if (!box.Gameplay) continue;
                // Thin stair/catwalk slabs are walkable Box sources; tall solids remain Not-Walkable modifiers.
                if (CompoundConnectivity.IsWalkableSurface(box))
                {
                    sources.Add(new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.Box,
                        size = new Vector3(box.Size.X, Math.Max(box.Size.Y, 0.05f), box.Size.Z),
                        area = 0,
                        transform = Matrix4x4.TRS(
                            new Vector3(box.Center.X, box.Center.Y, box.Center.Z),
                            Quaternion.identity,
                            Vector3.one)
                    });
                    continue;
                }
                // Tall solids are Not-Walkable modifier volumes (they add no geometry). Unity marks a surface only when
                // its voxel top lies inside the volume, so a volume that starts exactly on the floor top (y = 0) leaves
                // the floor walkable inside every wall (ADR-114). The volume therefore reaches from one agent height
                // below the box (any surface whose standing capsule would hit the box) to just under the box top
                // (a deck resting on the box is not cut). A ground wall therefore cuts the floor down to -agentHeight,
                // while a high beam (base above agent height) leaves the walkway beneath it walkable.
                float minY = box.Center.Y - box.Size.Y * 0.5f;
                float maxY = box.Center.Y + box.Size.Y * 0.5f;
                float bottom = minY - agentHeight;
                float top = Math.Max(maxY - 0.1f, bottom + 0.2f);
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox,
                    size = new Vector3(box.Size.X + 2f * pad, top - bottom, box.Size.Z + 2f * pad),
                    area = 1,
                    transform = Matrix4x4.TRS(
                        new Vector3(box.Center.X, (bottom + top) * 0.5f, box.Center.Z),
                        Quaternion.identity,
                        Vector3.one)
                });
            }

            var bounds = new Bounds(Vector3.zero, new Vector3(halfExtent * 2f + 4f, 16f, halfExtent * 2f + 4f));
            NavMeshData? data = null;
            NavMeshDataInstance instance = default;
            try
            {
                data = NavMeshBuilder.BuildNavMeshData(
                    buildSettings,
                    sources,
                    bounds,
                    Vector3.zero,
                    Quaternion.identity);
                if (data == null) return null;
                instance = NavMesh.AddNavMeshData(data);
                if (!instance.valid) return null;

                var triangulation = NavMesh.CalculateTriangulation();
                if (triangulation.vertices == null || triangulation.indices == null ||
                    triangulation.vertices.Length < 3 || triangulation.indices.Length < 3)
                    return null;

                var vertices = new List<Float3>(triangulation.vertices.Length);
                for (int i = 0; i < triangulation.vertices.Length; i++)
                {
                    var v = triangulation.vertices[i];
                    vertices.Add(new Float3(v.x, v.y, v.z));
                }

                var polys = new List<UnityNavMeshBakeCodec.Polygon>(triangulation.indices.Length / 3);
                for (int i = 0; i + 2 < triangulation.indices.Length; i += 3)
                {
                    uint i0 = (uint)triangulation.indices[i];
                    uint i1 = (uint)triangulation.indices[i + 1];
                    uint i2 = (uint)triangulation.indices[i + 2];
                    if (i0 >= (uint)vertices.Count || i1 >= (uint)vertices.Count || i2 >= (uint)vertices.Count)
                        continue;
                    ushort area = 0;
                    if (triangulation.areas != null && (i / 3) < triangulation.areas.Length)
                        area = (ushort)Math.Clamp(triangulation.areas[i / 3], 0, ushort.MaxValue);
                    polys.Add(new UnityNavMeshBakeCodec.Polygon(new[] { i0, i1, i2 }, area, 0));
                }
                if (polys.Count == 0) return null;

                Vector3 bmin = triangulation.vertices[0];
                Vector3 bmax = triangulation.vertices[0];
                for (int i = 1; i < triangulation.vertices.Length; i++)
                {
                    bmin = Vector3.Min(bmin, triangulation.vertices[i]);
                    bmax = Vector3.Max(bmax, triangulation.vertices[i]);
                }

                var bake = new UnityNavMeshBakeCodec.Bake(
                    vertices,
                    polys,
                    new Float3(bmin.x, bmin.y, bmin.z),
                    new Float3(bmax.x, bmax.y, bmax.z));
                // Round-trip encode validates NVB1 constraints before publication.
                _ = UnityNavMeshBakeCodec.Encode(bake);
                return new NavBakePublication.NativeBake(bake, NavBakePublication.NativeProvenance);
            }
            finally
            {
                if (instance.valid) instance.Remove();
                if (data != null) UnityEngine.Object.DestroyImmediate(data);
            }
        }
    }
}
