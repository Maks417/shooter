#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Presentation
{
    /// <summary>Unity-free sky/fog parameter math. Extractable to Generation.Core for offline tests.</summary>
    public static class GeneratedSkyMath
    {
        public readonly struct Rgb
        {
            public readonly float R, G, B;
            public Rgb(float r, float g, float b) { R = r; G = g; B = b; }
        }

        // Gallery/diagnostic daylight: cool zenith, brighter horizon haze, neutral ground bounce.
        public static readonly Rgb DefaultZenith = new Rgb(.18f, .28f, .42f);
        public static readonly Rgb DefaultHorizon = new Rgb(.55f, .62f, .70f);
        public static readonly Rgb DefaultGround = new Rgb(.22f, .24f, .27f);
        public const float DefaultFogStartMeters = 18f;
        public const float DefaultFogEndMeters = 48f;
        public const float DefaultFogDensity = .012f;

        public static void Validate(Rgb zenith, Rgb horizon, Rgb ground, bool fogEnabled, float fogStartMeters, float fogEndMeters, float fogDensity)
        {
            ValidateColor(zenith, nameof(zenith));
            ValidateColor(horizon, nameof(horizon));
            ValidateColor(ground, nameof(ground));
            if (!fogEnabled) return;
            if (!Finite(fogStartMeters) || fogStartMeters < 0 || !Finite(fogEndMeters) || fogEndMeters <= fogStartMeters ||
                !Finite(fogDensity) || fogDensity < 0 || fogDensity > 1)
                throw new ArgumentOutOfRangeException(nameof(fogEndMeters), "Fog params must be finite with end>start and density in [0,1].");
        }

        public static Rgb SampleGradient(Rgb zenith, Rgb horizon, Rgb ground, float elevation)
        {
            ValidateColor(zenith, nameof(zenith));
            ValidateColor(horizon, nameof(horizon));
            ValidateColor(ground, nameof(ground));
            if (!Finite(elevation) || elevation < -1 || elevation > 1)
                throw new ArgumentOutOfRangeException(nameof(elevation), "Elevation must be finite in [-1,1] (ground..zenith).");
            if (elevation >= 0) return Lerp(horizon, zenith, elevation);
            return Lerp(horizon, ground, -elevation);
        }

        public static Rgb Lerp(Rgb a, Rgb b, float t)
        {
            if (!Finite(t) || t < 0 || t > 1) throw new ArgumentOutOfRangeException(nameof(t));
            return new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        }

        private static void ValidateColor(Rgb color, string name)
        {
            if (!Finite(color.R) || !Finite(color.G) || !Finite(color.B) ||
                color.R < 0 || color.G < 0 || color.B < 0 || color.R > 8 || color.G > 8 || color.B > 8)
                throw new ArgumentOutOfRangeException(name, "RGB must be finite and in [0,8] linear.");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Applies deterministic Trilight ambient, optional distance fog and a one-shot procedural sky gradient. Does not create the directional sun.</summary>
    public sealed class GeneratedSky : IDisposable
    {
        private GameObject? root;
        private Material? skybox;
        private Texture2D? gradient;
        private bool disposed;

        public Color Zenith { get; private set; }
        public Color Horizon { get; private set; }
        public Color Ground { get; private set; }
        public bool FogEnabled { get; private set; }

        public static GeneratedSky Apply(Transform parent, bool enableFog = true) =>
            Apply(parent,
                GeneratedSkyMath.DefaultZenith,
                GeneratedSkyMath.DefaultHorizon,
                GeneratedSkyMath.DefaultGround,
                enableFog,
                GeneratedSkyMath.DefaultFogStartMeters,
                GeneratedSkyMath.DefaultFogEndMeters,
                GeneratedSkyMath.DefaultFogDensity);

        public static GeneratedSky Apply(
            Transform parent,
            GeneratedSkyMath.Rgb zenith,
            GeneratedSkyMath.Rgb horizon,
            GeneratedSkyMath.Rgb ground,
            bool enableFog,
            float fogStartMeters,
            float fogEndMeters,
            float fogDensity)
        {
            GeneratedSkyMath.Validate(zenith, horizon, ground, enableFog, fogStartMeters, fogEndMeters, fogDensity);
            var sky = new GeneratedSky();
            try
            {
                sky.Initialize(parent, zenith, horizon, ground, enableFog, fogStartMeters, fogEndMeters, fogDensity);
                return sky;
            }
            catch
            {
                sky.Dispose();
                throw;
            }
        }

        private void Initialize(
            Transform parent,
            GeneratedSkyMath.Rgb zenith,
            GeneratedSkyMath.Rgb horizon,
            GeneratedSkyMath.Rgb ground,
            bool enableFog,
            float fogStartMeters,
            float fogEndMeters,
            float fogDensity)
        {
            Zenith = ToColor(zenith);
            Horizon = ToColor(horizon);
            Ground = ToColor(ground);
            FogEnabled = enableFog;
            root = new GameObject("Generated sky");
            if (parent != null) root.transform.SetParent(parent, false);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Zenith;
            RenderSettings.ambientEquatorColor = Horizon;
            RenderSettings.ambientGroundColor = Ground;

            // Panoramic accepts a generated lat-long strip; Cubemap would need a Cubemap asset.
            gradient = BuildGradientTexture(zenith, horizon, ground);
            var shader = Shader.Find("Skybox/Panoramic");
            if (shader != null)
            {
                skybox = new Material(shader) { name = "Generated sky gradient" };
                if (skybox.HasProperty("_MainTex")) skybox.SetTexture("_MainTex", gradient);
                if (skybox.HasProperty("_Exposure")) skybox.SetFloat("_Exposure", 1f);
                if (skybox.HasProperty("_Rotation")) skybox.SetFloat("_Rotation", 0f);
                RenderSettings.skybox = skybox;
            }

            RenderSettings.fog = enableFog;
            if (enableFog)
            {
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = Horizon;
                RenderSettings.fogStartDistance = fogStartMeters;
                RenderSettings.fogEndDistance = fogEndMeters;
                // Density retained for callers that switch to Exponential later; Linear uses start/end.
                RenderSettings.fogDensity = fogDensity;
            }
        }

        public Color SampleBackground(float elevation) =>
            ToColor(GeneratedSkyMath.SampleGradient(FromColor(Zenith), FromColor(Horizon), FromColor(Ground), elevation));

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (RenderSettings.skybox == skybox) RenderSettings.skybox = null;
            RenderSettings.fog = false;
            DestroyOwned(skybox);
            DestroyOwned(gradient);
            DestroyOwned(root);
            skybox = null;
            gradient = null;
            root = null;
        }

        private static Texture2D BuildGradientTexture(GeneratedSkyMath.Rgb zenith, GeneratedSkyMath.Rgb horizon, GeneratedSkyMath.Rgb ground)
        {
            const int height = 32;
            var texture = new Texture2D(4, height, TextureFormat.RGBA32, false, true)
            {
                name = "Generated sky gradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < height; y++)
            {
                // v=0 bottom (ground), v=1 top (zenith); elevation maps [-1,1].
                float elevation = height == 1 ? 1f : -1f + 2f * y / (height - 1);
                var sample = GeneratedSkyMath.SampleGradient(zenith, horizon, ground, elevation);
                var color = new Color(sample.R, sample.G, sample.B, 1f);
                for (int x = 0; x < 4; x++) texture.SetPixel(x, y, color);
            }
            texture.Apply(false, true);
            return texture;
        }

        private static void DestroyOwned(UnityEngine.Object? value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        private static Color ToColor(GeneratedSkyMath.Rgb rgb) => new Color(rgb.R, rgb.G, rgb.B, 1f);
        private static GeneratedSkyMath.Rgb FromColor(Color color) => new GeneratedSkyMath.Rgb(color.r, color.g, color.b);
    }
}
