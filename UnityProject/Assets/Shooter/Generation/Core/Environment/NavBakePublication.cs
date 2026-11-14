#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// Publication seam for ADR-035 unity.navmesh.bake.v1.
    /// Pure tools leave <see cref="NativeProvider"/> unset and publish the software grid with pending provenance.
    /// Unity editor registers a pinned AI Navigation 2.0.0 bake against authoritative collision.
    /// </summary>
    public static class NavBakePublication
    {
        public const string SoftwarePendingProvenance = "software-walkable-grid;unity-native-bake-pending";
        public const string NativeProvenance = "unity-ai-navigation-2.0.0;pinned-physx-collision-bake";

        /// <summary>
        /// Optional native bake hook. Returns true only when a real Unity NavMesh triangulation was produced.
        /// Must not wrap <see cref="UnityNavMeshBakeCodec.BuildWalkableGrid"/> and claim native provenance.
        /// </summary>
        public static Func<IReadOnlyList<CollisionBox>, float, float, float, float, float, float, NativeBake?>? NativeProvider { get; set; }

        public sealed class NativeBake
        {
            public UnityNavMeshBakeCodec.Bake Bake { get; }
            public string Provenance { get; }
            public NativeBake(UnityNavMeshBakeCodec.Bake bake, string provenance)
            {
                Bake = bake ?? throw new ArgumentNullException(nameof(bake));
                if (string.IsNullOrWhiteSpace(provenance)) throw new ArgumentException("Provenance required.", nameof(provenance));
                if (provenance.IndexOf("software-walkable-grid", StringComparison.Ordinal) >= 0)
                    throw new ArgumentException("Native bake provenance must not label the software grid.", nameof(provenance));
                Provenance = provenance;
            }
        }

        public static bool TryNative(
            IReadOnlyList<CollisionBox> boxes,
            float halfExtent,
            float agentRadius,
            float agentHeight,
            float maxStep,
            float maxSlopeDegrees,
            float clearanceMargin,
            out UnityNavMeshBakeCodec.Bake bake,
            out string provenance)
        {
            bake = null!;
            provenance = SoftwarePendingProvenance;
            var provider = NativeProvider;
            if (provider == null) return false;
            var result = provider(boxes, halfExtent, agentRadius, agentHeight, maxStep, maxSlopeDegrees, clearanceMargin);
            if (result == null || result.Bake.Polygons.Count == 0) return false;
            bake = result.Bake;
            provenance = result.Provenance;
            return true;
        }
    }
}
