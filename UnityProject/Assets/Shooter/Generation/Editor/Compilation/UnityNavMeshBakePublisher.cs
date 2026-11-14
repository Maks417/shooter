#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Generation.Environment;

namespace Shooter.Generation
{
    /// <summary>
    /// Editor/publication seam for ADR-035 unity.navmesh.bake.v1.
    /// Native bake requires the Unity editor bridge to register <see cref="NavBakePublication.NativeProvider"/>.
    /// Pure MapContent / Generation tests keep the software grid and must retain pending provenance.
    /// </summary>
    public static class UnityNavMeshBakePublisher
    {
        public const string AdapterFormat = UnityNavMeshBakeCodec.AdapterFormat;
        public const uint AdapterVersion = UnityNavMeshBakeCodec.AdapterVersion;
        public const string NativeProvenance = NavBakePublication.NativeProvenance;
        public const string SoftwarePendingProvenance = NavBakePublication.SoftwarePendingProvenance;

        public static string PendingNativeBakeReason =>
            "Pinned AI Navigation 2.0.0 bake against PhysX-installed collision requires the Unity editor bake bridge; pure tooling still publishes software NVB1 with pending provenance and must not be labeled native.";

        public static bool IsNativeProviderRegistered => NavBakePublication.NativeProvider != null;

        /// <summary>
        /// Attempts a registered native Unity bake. Returns false (with pending provenance) when the editor bridge is absent.
        /// </summary>
        public static bool TryBakeNative(
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
            return NavBakePublication.TryNative(
                boxes, halfExtent, agentRadius, agentHeight, maxStep, maxSlopeDegrees, clearanceMargin,
                out bake, out provenance);
        }

        /// <summary>
        /// Pure-test / non-Unity fallback only. Callers must record <see cref="SoftwarePendingProvenance"/>.
        /// </summary>
        public static UnityNavMeshBakeCodec.Bake BuildSoftwareFallback(
            IReadOnlyList<CollisionBox> boxes,
            float halfExtent,
            float cellSize,
            float clearance)
        {
            return UnityNavMeshBakeCodec.BuildWalkableGrid(boxes, halfExtent, cellSize, clearance);
        }
    }
}
