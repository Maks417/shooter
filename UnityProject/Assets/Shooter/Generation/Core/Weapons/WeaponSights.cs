#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>
    /// ADR-107 public face of the rail-mounted sights: which sights exist, which mount on which archetype, where their
    /// line of sight sits and how their LOD meshes are built. Sight names are bare (<c>collimator</c>); loadout ids are
    /// <c>sights/&lt;name&gt;</c>.
    /// </summary>
    public static class WeaponSights
    {
        public const string GeneratorId = "geometry.weapon-sight";
        public const string GeneratorVersion = "1.1.0";

        /// <summary>Every rail-mounted sight, in loadout order.</summary>
        public static string[] All => (string[])ArsenalGeometry.MountedSights.Clone();

        public static bool IsSight(string? sight) => sight != null && Array.IndexOf(ArsenalGeometry.MountedSights, sight) >= 0;

        /// <summary>Archetypes with a sight rail (the 1.4.0 long guns).</summary>
        public static bool IsMountKind(string? kind) => kind != null && ArsenalGeometry.IsMountKind(kind);

        /// <summary>False when the sight is part of the gun body (AK iron sights) and has no mesh of its own.</summary>
        public static bool HasMesh(string kind, string sight) => ArsenalGeometry.SightHasMesh(kind, sight);

        /// <summary>Packaged mesh key of <paramref name="sight"/> on <paramref name="kind"/>: <c>sight-&lt;sight&gt;-&lt;kind&gt;</c>.</summary>
        public static string MeshKey(string sight, string kind) => "sight-" + sight + "-" + kind;

        /// <summary>Line of sight in the gun's weapon space (metres, X = 0).</summary>
        public static Float3 EyeFor(string kind, string sight) => ArsenalGeometry.MountedSightEye(kind, sight);

        /// <summary>LOD0..3 in the gun's weapon space; compile-time only (players load packaged bytes).</summary>
        public static MeshData[] BuildLods(string kind, string sight) => ProfileArsenalGeometry.BuildSight(kind, sight);
    }
}
