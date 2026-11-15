#nullable enable
using UnityEngine;

namespace Shooter.Presentation.Session
{
    /// <summary>
    /// Battlefield V–like staging depth bands and scoped atmosphere for the cosmetic menu only.
    /// Numeric layout recipes; no imported backgrounds, generators, or gameplay adapters.
    /// </summary>
    public static class MenuStagingComposition
    {
        /// <summary>Near apron / contact under the lens (z &lt; mid props).</summary>
        public const float ApronBandZ = 8f;
        /// <summary>Showcase soldiers and soft hero pads (BindMenuShowcase feet).</summary>
        public const float SoldierBandMinZ = 0.2f;
        public const float SoldierBandMaxZ = 5.2f;
        /// <summary>Cargo / barriers / gantries framing the heroes.</summary>
        public const float MidPropBandZ = 14f;
        /// <summary>Distant hangar / skyline shells.</summary>
        public const float DistantShellBandZ = 42f;

        public const float FogDensity = .024f;
        public static readonly Color FogColor = new Color(.075f, .11f, .135f);
        public static readonly Color CameraClear = new Color(.07f, .105f, .125f);
        public static readonly Color AmbientSky = new Color(.28f, .30f, .27f);
        public static readonly Color AmbientEquator = new Color(.13f, .175f, .205f);
        public static readonly Color AmbientGround = new Color(.055f, .06f, .055f);

        public static readonly Vector3 CameraPosition = new Vector3(-1.45f, 1.58f, -3.25f);
        public static readonly Vector3 CameraLookAt = new Vector3(1.35f, 1.05f, 2.55f);
        public const float CameraFieldOfView = 34f;

        /// <summary>Orthographic overview framing so map solids sit inside a readable plate.</summary>
        public const float MapCardOrthoSize = 152f;
        public static readonly Vector3 MapCardCameraOffset = new Vector3(138f, 168f, -142f);
        public static readonly Color MapCardClear = new Color(.045f, .07f, .085f);

        public readonly struct KeyLight
        {
            public KeyLight(string name, LightType type, Color color, float intensity, Vector3 eulerOrPosition, float range = 0f, bool hardShadows = false)
            {
                Name = name;
                Type = type;
                Color = color;
                Intensity = intensity;
                EulerOrPosition = eulerOrPosition;
                Range = range;
                HardShadows = hardShadows;
            }

            public string Name { get; }
            public LightType Type { get; }
            public Color Color { get; }
            public float Intensity { get; }
            public Vector3 EulerOrPosition { get; }
            public float Range { get; }
            public bool HardShadows { get; }
        }

        /// <summary>Warm apron key, cool distant fill/rim, local hero bounce — StageLayer culled only.</summary>
        public static readonly KeyLight[] Lights =
        {
            new KeyLight("Warm key light", LightType.Directional, new Color(1f, .90f, .70f), 2.25f, new Vector3(38f, -34f, 0f), hardShadows: true),
            new KeyLight("Cool fill light", LightType.Directional, new Color(.38f, .58f, .74f), .38f, new Vector3(18f, 128f, 0f)),
            new KeyLight("Cool rim light", LightType.Directional, new Color(.48f, .74f, .90f), .96f, new Vector3(8f, 162f, 0f)),
            new KeyLight("Apron lamp point", LightType.Point, new Color(1f, .78f, .50f), 1.85f, new Vector3(-2.4f, 5.0f, 8.2f), range: 24f),
            new KeyLight("Hero warm bounce", LightType.Point, new Color(1f, .82f, .58f), .82f, new Vector3(1.35f, 2.35f, 1.1f), range: 9f),
            new KeyLight("Apron ground bounce", LightType.Point, new Color(1f, .74f, .48f), .62f, new Vector3(1.2f, 1.05f, 6.2f), range: 15f),
            new KeyLight("Distant shell wash", LightType.Point, new Color(.55f, .68f, .78f), .48f, new Vector3(4f, 9f, 36f), range: 42f)
        };

        public static bool InSoldierBand(float z) => z >= SoldierBandMinZ && z <= SoldierBandMaxZ;
        public static bool InDistantBand(float z) => z >= DistantShellBandZ - 8f;
        public static bool InApronBand(float z) => z <= ApronBandZ && z >= -3f;
    }
}
