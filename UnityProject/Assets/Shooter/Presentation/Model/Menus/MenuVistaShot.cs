#nullable enable
using System;

namespace Shooter.Presentation.Menus
{
    /// <summary>
    /// ADR-123 main-menu vista: the squad stands on a rooftop overlook at the stage origin and the focused map's real
    /// packaged geometry lies below and ahead of it. One shot per registered map: how the map is turned, how far below
    /// the roof it sits and how far ahead its centre lies. Pure numbers; the Unity scene applies them.
    /// </summary>
    public readonly struct MenuVistaShot
    {
        public MenuVistaShot(string mapId, float yawDegrees, float dropMeters, float nearGapMeters, float lateralMeters)
        {
            MapId = mapId;
            YawDegrees = yawDegrees;
            DropMeters = dropMeters;
            NearGapMeters = nearGapMeters;
            LateralMeters = lateralMeters;
        }

        public string MapId { get; }
        /// <summary>Rotation of the map about its centre (degrees, clockwise seen from above).</summary>
        public float YawDegrees { get; }
        /// <summary>How far the map's ground lies below the overlook roof.</summary>
        public float DropMeters { get; }
        /// <summary>Gap between the roof's far edge and the map's near edge.</summary>
        public float NearGapMeters { get; }
        /// <summary>Sideways shift of the map centre (positive = right of the camera axis).</summary>
        public float LateralMeters { get; }

        /// <summary>Far edge of the rooftop overlook (stage z, metres); the parapet sits on it.</summary>
        public const float RoofEdgeZ = 6.5f;

        /// <summary>Cross-fade between two vistas when the focused map changes (seconds; reduced motion snaps).</summary>
        public const float CrossFadeSeconds = 0.4f;

        /// <summary>Stage render rate: Low renders the vista at half rate, other tiers at 30 Hz.</summary>
        public static float RenderHz(int qualityTier) => qualityTier <= 0 ? 15f : 30f;

        private static readonly MenuVistaShot[] Shots =
        {
            new MenuVistaShot("training-compound", 35f, 4f, 4f, -6f),
            new MenuVistaShot("depot-yard", -20f, 4.5f, 4f, -4f),
            new MenuVistaShot("oil-depot", 55f, 5f, 6f, -8f),
        };

        /// <summary>Default framing for a map without a reviewed shot (still a valid vista).</summary>
        public static MenuVistaShot Default(string mapId) => new MenuVistaShot(mapId, 30f, 4.5f, 5f, -6f);

        public static bool HasReviewedShot(string mapId)
        {
            foreach (var shot in Shots) if (shot.MapId == mapId) return true;
            return false;
        }

        public static MenuVistaShot For(string mapId)
        {
            foreach (var shot in Shots) if (shot.MapId == mapId) return shot;
            return Default(mapId);
        }

        /// <summary>
        /// Where the map's centre goes on the stage (x, ground y, z) for a map whose footprint, after the shot's yaw,
        /// has the given half depth along the stage z axis.
        /// </summary>
        public (float X, float Y, float Z) Anchor(float rotatedHalfDepth) =>
            (LateralMeters, -DropMeters, RoofEdgeZ + NearGapMeters + Math.Max(0f, rotatedHalfDepth));

        /// <summary>Half extent along stage z of an axis-aligned footprint after rotating it by the shot's yaw.</summary>
        public float RotatedHalfDepth(float halfWidthX, float halfDepthZ)
        {
            double r = YawDegrees * Math.PI / 180.0;
            return (float)(Math.Abs(Math.Sin(r)) * halfWidthX + Math.Abs(Math.Cos(r)) * halfDepthZ);
        }
    }
}
