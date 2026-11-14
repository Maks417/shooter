#nullable enable
using System;
using Shooter.Generation.Environment;

namespace Shooter.Generation
{
    /// <summary>
    /// Editor seam for ADR-035 unity.navmesh.bake.v1 publication.
    /// Software walkable-grid encoding is used by MapPackageBuilder today.
    /// Native AI Navigation 2.0.0 bake against installed authoritative collision remains Pending for FIX04 player evidence.
    /// </summary>
    public static class UnityNavMeshBakePublisher
    {
        public const string AdapterFormat = UnityNavMeshBakeCodec.AdapterFormat;
        public const uint AdapterVersion = UnityNavMeshBakeCodec.AdapterVersion;

        public static string PendingNativeBakeReason =>
            "Pinned AI Navigation bake against PhysX-installed collision is not yet evidenced in the release player; software NVB1 grid is published for package closure.";
    }
}
