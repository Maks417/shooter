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

        // Bodycam/BFV grounded daylight: cooler zenith, brighter horizon band, warm ground bounce.
        // Linear fog stays mid-range (~40–100 m) so 5v5/6v6 silhouettes remain readable.
        public static readonly Rgb DefaultZenith = new Rgb(.24f, .36f, .52f);
        public static readonly Rgb DefaultHorizon = new Rgb(.64f, .66f, .68f);
        public static readonly Rgb DefaultGround = new Rgb(.22f, .20f, .16f);
        public const float DefaultFogStartMeters = 40f;
        public const float DefaultFogEndMeters = 100f;
        public const float DefaultFogDensity = .007f;

        /// <summary>
        /// Registry atmosphere presets (presentation only). Does not change collision, nav, or authority.
        /// compound: cooler plaster/concrete air; dusty-yard: warmer dusty yard haze; petroleum: petroleum haze.
        /// </summary>
        public static void ResolveProductPalette(
            string mapId,
            out Rgb zenith,
            out Rgb horizon,
            out Rgb ground,
            out float fogStartMeters,
            out float fogEndMeters,
            out float fogDensity)
        {
            fogStartMeters = DefaultFogStartMeters;
            fogEndMeters = DefaultFogEndMeters;
            fogDensity = DefaultFogDensity;
            // ADR-083: the atmosphere preset comes from the map registry (presentation.sky).
            string preset = Shooter.Generation.Environment.MapRegistry.TryGet(mapId, out var definition) ? definition.SkyPreset : "default";
            if (preset == "compound")
            {
                // Cooler outdoor compound: deeper blue zenith, clean cool horizon, modest depth fog.
                zenith = new Rgb(.20f, .34f, .50f);
                horizon = new Rgb(.58f, .64f, .70f);
                ground = new Rgb(.24f, .22f, .17f);
                // ADR-116: fog starts later and thins out so distant walls keep their colour.
                fogStartMeters = 42f;
                fogEndMeters = 130f;
                fogDensity = .0068f;
                return;
            }
            if (preset == "dusty-yard")
            {
                // Warmer dusty yard: muted zenith, amber haze, earlier fog without washing silhouettes.
                zenith = new Rgb(.30f, .38f, .44f);
                horizon = new Rgb(.72f, .62f, .50f);
                ground = new Rgb(.34f, .26f, .16f);
                fogStartMeters = 40f;
                fogEndMeters = 120f;
                fogDensity = .008f;
                return;
            }
            if (preset == "petroleum")
            {
                // Petroleum yard: cooler industrial zenith, oily amber horizon, denser mid fog.
                zenith = new Rgb(.22f, .30f, .36f);
                horizon = new Rgb(.64f, .54f, .42f);
                ground = new Rgb(.28f, .22f, .14f);
                fogStartMeters = 38f;
                fogEndMeters = 115f;
                fogDensity = .0085f;
                return;
            }
            zenith = DefaultZenith;
            horizon = DefaultHorizon;
            ground = DefaultGround;
        }

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
        private Cubemap? environment;
        private Texture? priorReflection;
        private DefaultReflectionMode priorReflectionMode;
        public Cubemap? EnvironmentReflection => environment;
        private bool disposed;

        /// <summary>Product maps: slightly deeper ambient so hard shadows and metal/concrete contrast hold on Low720.</summary>
        public const float ProductAmbientIntensity = 0.88f;
        public const float DefaultAmbientIntensity = 0.88f;

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
                GeneratedSkyMath.DefaultFogDensity,
                DefaultAmbientIntensity);

        public static GeneratedSky Apply(
            Transform parent,
            GeneratedSkyMath.Rgb zenith,
            GeneratedSkyMath.Rgb horizon,
            GeneratedSkyMath.Rgb ground,
            bool enableFog,
            float fogStartMeters,
            float fogEndMeters,
            float fogDensity) =>
            Apply(parent, zenith, horizon, ground, enableFog, fogStartMeters, fogEndMeters, fogDensity, DefaultAmbientIntensity);

        public static GeneratedSky Apply(
            Transform parent,
            GeneratedSkyMath.Rgb zenith,
            GeneratedSkyMath.Rgb horizon,
            GeneratedSkyMath.Rgb ground,
            bool enableFog,
            float fogStartMeters,
            float fogEndMeters,
            float fogDensity,
            float ambientIntensity)
        {
            GeneratedSkyMath.Validate(zenith, horizon, ground, enableFog, fogStartMeters, fogEndMeters, fogDensity);
            if (float.IsNaN(ambientIntensity) || float.IsInfinity(ambientIntensity) || ambientIntensity < 0f || ambientIntensity > 2f)
                throw new ArgumentOutOfRangeException(nameof(ambientIntensity), "Ambient intensity must be finite in [0,2].");
            var sky = new GeneratedSky();
            try
            {
                sky.Initialize(parent, zenith, horizon, ground, enableFog, fogStartMeters, fogEndMeters, fogDensity, ambientIntensity);
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
            float fogDensity,
            float ambientIntensity)
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
            // Slightly under-unity ambient so directional hard shadows read on Low/iGPU.
            RenderSettings.ambientIntensity = ambientIntensity;

            // Panoramic accepts a generated lat-long strip; Cubemap would need a Cubemap asset.
            gradient = BuildGradientTexture(zenith, horizon, ground);
            var shader = Shader.Find("Skybox/Panoramic");
            if (shader != null)
            {
                skybox = new Material(shader) { name = "Generated sky gradient" };
                if (skybox.HasProperty("_MainTex")) skybox.SetTexture("_MainTex", gradient);
                if (skybox.HasProperty("_Exposure")) skybox.SetFloat("_Exposure", 1.05f);
                if (skybox.HasProperty("_Rotation")) skybox.SetFloat("_Rotation", 0f);
                RenderSettings.skybox = skybox;
            }

            // Global fallback covers the entire 256 m arena, including positions outside the local probe.
            // Generated from the same analytic sky; 64 px faces with mips, no per-frame capture.
            priorReflection = RenderSettings.customReflectionTexture;
            priorReflectionMode = RenderSettings.defaultReflectionMode;
            environment = BuildEnvironment(zenith,horizon,ground);
            RenderSettings.customReflectionTexture = environment;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.fog = enableFog;
            if (enableFog)
            {
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = Horizon;
                baseFogStart = fogStartMeters;
                baseFogEnd = fogEndMeters;
                ApplyFogScale(Shooter.Presentation.Rendering.ProductPostProcessing.CurrentTier.FogDistanceScale);
                // Density retained for callers that switch to Exponential later; Linear uses start/end.
                RenderSettings.fogDensity = fogDensity;
            }
        }

        /// <summary>Product map sky: numeric 3D-noise cloud layer uploaded once; reflection uses the same cube.</summary>
        public void ApplyMap(string mapId)
        {
            var definition=Shooter.Generation.Environment.MapRegistry.Require(mapId);
            var data=Shooter.Generation.Environment.AtmosphereBaker.Sky(definition.SkyPreset);
            const int n=Shooter.Generation.Environment.AtmosphereBaker.CubeSize;
            var cube=new Cubemap(n,TextureFormat.RGBAHalf,true){name="Numeric "+definition.SkyPreset+" atmosphere",filterMode=FilterMode.Trilinear};
            var bytes=data.MipBytes[0];
            for(int face=0;face<6;face++)
            {
                var pixels=new Color[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++){int p=(y*n*6+face*n+x)*4;pixels[y*n+x]=new Color(bytes[p]/255f,bytes[p+1]/255f,bytes[p+2]/255f,1);}
                cube.SetPixels(pixels,(CubemapFace)face);
            }
            cube.Apply(true,false);DestroyOwned(environment);environment=cube;
            RenderSettings.customReflectionTexture=cube;RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;
            DestroyOwned(skybox);
            skybox=new Material(Shader.Find("Shooter/AnalyticSky")??throw new InvalidOperationException("Required analytic sky shader missing.")){name="Numeric map atmosphere"};
            skybox.SetTexture("_Environment",cube);RenderSettings.skybox=skybox;
            baseHeightFog=new Vector4(.009f,definition.SkyPreset=="petroleum"?.08f:.15f,1,0);
            ApplyFogScale(Shooter.Presentation.Rendering.ProductPostProcessing.CurrentTier.FogDistanceScale);
            Shader.SetGlobalColor("_ShooterFogTint",Horizon);
        }

        private static float baseFogStart = GeneratedSkyMath.DefaultFogStartMeters, baseFogEnd = GeneratedSkyMath.DefaultFogEndMeters;
        private static Vector4 baseHeightFog;

        /// <summary>
        /// ADR-103: higher tiers push the map's linear fog (and thin height fog) further out so distant and scoped
        /// views keep contrast; Low keeps the reviewed 34-100 m readability band. Colour and grade are unchanged.
        /// </summary>
        public static void ApplyFogScale(float scale)
        {
            if (!(scale >= 1f) || float.IsInfinity(scale)) scale = 1f;
            RenderSettings.fogStartDistance = baseFogStart * scale;
            RenderSettings.fogEndDistance = baseFogEnd * scale;
            if (baseHeightFog.z > 0f)
                Shader.SetGlobalVector("_ShooterHeightFog", new Vector4(baseHeightFog.x / scale, baseHeightFog.y, baseHeightFog.z, baseHeightFog.w));
        }

        public Color SampleBackground(float elevation) =>
            ToColor(GeneratedSkyMath.SampleGradient(FromColor(Zenith), FromColor(Horizon), FromColor(Ground), elevation));

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (RenderSettings.skybox == skybox) RenderSettings.skybox = null;
            RenderSettings.fog = false;
            baseHeightFog = Vector4.zero;
            RenderSettings.ambientIntensity = 1f;
            if (RenderSettings.customReflectionTexture == environment)
            {
                RenderSettings.customReflectionTexture = priorReflection;
                RenderSettings.defaultReflectionMode = priorReflectionMode;
            }
            DestroyOwned(environment); environment=null;
            DestroyOwned(skybox);
            DestroyOwned(gradient);
            DestroyOwned(root);
            skybox = null;
            gradient = null;
            root = null;
        }

        private static Cubemap BuildEnvironment(GeneratedSkyMath.Rgb zenith,GeneratedSkyMath.Rgb horizon,GeneratedSkyMath.Rgb ground)
        {
            const int size=64;
            var cube=new Cubemap(size,TextureFormat.RGBAHalf,true) { name="Procedural arena reflection",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp };
            Vector3 sun=(Quaternion.Euler(44,-32,0)*Vector3.back).normalized;
            for(int face=0;face<6;face++)
            {
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++) for(int x=0;x<size;x++)
                {
                    float u=2*(x+.5f)/size-1, v=2*(y+.5f)/size-1;
                    Vector3 direction=face switch { 0=>new Vector3(1,-v,-u),1=>new Vector3(-1,-v,u),2=>new Vector3(u,1,v),3=>new Vector3(u,-1,-v),4=>new Vector3(u,-v,1),_=>new Vector3(-u,-v,-1) };
                    direction.Normalize();
                    var rgb=GeneratedSkyMath.SampleGradient(zenith,horizon,ground,direction.y);
                    float lobe=Mathf.Pow(Mathf.Max(0,Vector3.Dot(direction,sun)),256)*1.5f;
                    pixels[y*size+x]=new Color(rgb.R+lobe,rgb.G+lobe*.94f,rgb.B+lobe*.84f,1);
                }
                cube.SetPixels(pixels,(CubemapFace)face);
            }
            cube.Apply(true,false);
            return cube;
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
