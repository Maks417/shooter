#nullable enable
using Shooter.Presentation.Hud;
using UnityEngine;

namespace Shooter.Presentation.Session
{
    /// <summary>
    /// ADR-084 analytic UI shapes drawn through IMGUI (Repaint only) with the code-only <c>Shooter/UiSdf</c> shader.
    /// Coordinates are the caller's current GUI space (UI scale, groups and scroll views apply as usual).
    /// When the shader is unavailable every call returns false so callers keep the flat white-texture path;
    /// the miss is logged once, never hidden behind imported art.
    /// </summary>
    public static class UiSdf
    {
        public enum Gradient : byte { None = 0, Vertical = 1, Horizontal = 2, Radial = 3 }

        /// <summary>Per-shape style. Colours are authored in sRGB like the brand palette.</summary>
        public struct Style
        {
            public Color Fill;
            public Color Fill2;
            public Gradient Gradient;
            public float Radius;
            public float Stroke;
            public Vector2 ShadowOffset;
            public float ShadowSoftness;
            public float ShadowAlpha;
            public float GlowWidth;
            public float GlowAlpha;
            public Color GlowColor;
            /// <summary>Glint sweep 0..1, or a negative value for none.</summary>
            public float Glint;
            public float GlintAlpha;

            public static Style Solid(Color fill, float radius = 0f) =>
                new Style { Fill = fill, Fill2 = fill, Radius = radius, Glint = -1f };

            public Style WithShadow(float alpha = UiShapeParams.ShadowAlpha, float softness = UiShapeParams.ShadowSoftness,
                float offset = UiShapeParams.ShadowOffset)
            {
                var s = this;
                s.ShadowAlpha = alpha;
                s.ShadowSoftness = softness;
                s.ShadowOffset = new Vector2(0f, offset);
                return s;
            }

            public Style WithGlow(Color color, float width = UiShapeParams.FocusGlowWidth, float alpha = UiShapeParams.FocusGlowAlpha)
            {
                var s = this;
                s.GlowColor = color;
                s.GlowWidth = width;
                s.GlowAlpha = alpha;
                return s;
            }

            public Style WithGradient(Color end, Gradient mode)
            {
                var s = this;
                s.Fill2 = end;
                s.Gradient = mode;
                return s;
            }

            public Style WithGlint(float position, float alpha = 0.55f)
            {
                var s = this;
                s.Glint = position;
                s.GlintAlpha = alpha;
                return s;
            }
        }

        private static Material? material;
        private static bool missingLogged;
        private static readonly int SizePad = Shader.PropertyToID("_SizePad");
        private static readonly int ShapeA = Shader.PropertyToID("_ShapeA");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int Color2Id = Shader.PropertyToID("_Color2");
        private static readonly int ShadowId = Shader.PropertyToID("_Shadow");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int ArcId = Shader.PropertyToID("_Arc");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int ClipCircleId = Shader.PropertyToID("_ClipCircle");
        private static Vector4 clipCircle;

        /// <summary>True while a <see cref="BeginCircleClip"/> mask applies to every SDF draw.</summary>
        public static bool CircleClipActive => clipCircle.z > 0f;

        /// <summary>
        /// ADR-104: mask every following SDF draw to a circle given in the current GUI space (no groups: the mask is
        /// resolved through <see cref="GUI.matrix"/> only). Flat fallbacks are not masked; callers cull them. The circle
        /// is kept in device px and re-expressed in each quad's own local frame at draw time (<see cref="LocalClip"/>),
        /// so the shader never depends on the render pipeline's view matrices inside IMGUI.
        /// </summary>
        public static void BeginCircleClip(Vector2 centre, float radius)
        {
            Vector3 c = GUI.matrix.MultiplyPoint3x4(new Vector3(centre.x, centre.y, 0f));
            float scale = DeviceScale();
            clipCircle = new Vector4(c.x, c.y, Mathf.Max(0f, radius) * scale, 1f);
        }

        public static void EndCircleClip() => clipCircle = Vector4.zero;

        /// <summary>
        /// The active clip circle in the local, centred, y-down logical frame of <paramref name="quad"/> (the frame the
        /// shader's <c>p</c> lives in) under the current <see cref="GUI.matrix"/>: centre x, y, radius, anti-alias width.
        /// Zero when no clip is active.
        /// </summary>
        internal static Vector4 LocalClip(Rect quad)
        {
            if (clipCircle.z <= 0f) return Vector4.zero;
            Vector3 local = GUI.matrix.inverse.MultiplyPoint3x4(new Vector3(clipCircle.x, clipCircle.y, 0f));
            float scale = DeviceScale();
            return new Vector4(local.x - quad.center.x, local.y - quad.center.y, clipCircle.z / scale, 1f / scale);
        }

        /// <summary>True when the SDF shader loaded (players preload it; see ShooterBuild).</summary>
        public static bool Available => EnsureMaterial() != null;

        /// <summary>Test/diagnostic switch: force the legacy flat path.</summary>
        public static bool Disabled { get; set; }

        private static Material? EnsureMaterial()
        {
            if (Disabled) return null;
            if (material != null) return material;
            var shader = Shader.Find("Shooter/UiSdf");
            if (shader == null || !shader.isSupported)
            {
                if (!missingLogged)
                {
                    missingLogged = true;
                    Debug.LogWarning("SHOOTER_UI_SDF unavailable; flat IMGUI primitives in use");
                }
                return null;
            }
            material = new Material(shader) { name = "UiSdf", hideFlags = HideFlags.HideAndDontSave };
            Debug.Log("SHOOTER_UI_SDF ready");
            return material;
        }

        private static Color Out(Color c) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? new Color(c.linear.r, c.linear.g, c.linear.b, c.a) : c;

        private static Vector4 Out4(Color c)
        {
            var o = Out(c);
            return new Vector4(o.r, o.g, o.b, o.a);
        }

        /// <summary>Current GUI-to-device scale (UI scale times any rotation-free matrix scale).</summary>
        private static float DeviceScale()
        {
            Matrix4x4 m = GUI.matrix;
            float sx = new Vector2(m.m00, m.m10).magnitude;
            return sx <= 0f ? 1f : sx;
        }

        public static bool Rect(Rect r, in Style style) => Shape(r, UiShapeKind.RoundedRect, style);

        public static bool Circle(Rect r, in Style style) => Shape(r, UiShapeKind.Circle, style);

        /// <summary>Ring arc inside <paramref name="r"/>; angles in radians, 0 = up, clockwise positive.</summary>
        public static bool Arc(Rect r, float startRadians, float endRadians, float thickness, in Style style)
        {
            var span = UiShapeParams.NormaliseArc(startRadians, endRadians);
            return Shape(r, UiShapeKind.Arc, style, new Vector4(span.Start, span.Sweep, thickness, 0f));
        }

        /// <summary>Full-rect edge vignette with quadratic falloff over <paramref name="band"/> logical px.</summary>
        public static bool EdgeFade(Rect r, Color tint, float band, float strength)
        {
            if (strength <= 0f) return true;
            var mat = EnsureMaterial();
            if (mat == null) return false;
            if (Event.current == null || Event.current.type != EventType.Repaint) return true;
            mat.SetVector(SizePad, new Vector4(r.width, r.height, 0f, UiShapeParams.AntiAliasPixels(DeviceScale())));
            mat.SetVector(ShapeA, new Vector4((float)UiShapeKind.EdgeFade, 0f, 0f, 0f));
            mat.SetVector(ColorId, Out4(tint));
            mat.SetVector(FadeId, new Vector4(band, Mathf.Clamp01(strength), 0f, 0f));
            mat.SetVector(ClipCircleId, LocalClip(r));
            mat.SetVector(ShadowId, Vector4.zero);
            mat.SetVector(GlowId, new Vector4(0f, 0f, -1f, 0f));
            Graphics.DrawTexture(r, Texture2D.whiteTexture, new Rect(0, 0, 1, 1), 0, 0, 0, 0, Color.white, mat);
            return true;
        }

        public static bool Shape(Rect r, UiShapeKind kind, in Style style, Vector4 arc = default)
        {
            var mat = EnsureMaterial();
            if (mat == null) return false;
            if (Event.current == null || Event.current.type != EventType.Repaint) return true;
            if (r.width <= 0f || r.height <= 0f) return true;
            float shadowSoft = style.ShadowAlpha > 0f ? style.ShadowSoftness : 0f;
            float shadowOffset = style.ShadowAlpha > 0f ? Mathf.Max(Mathf.Abs(style.ShadowOffset.x), Mathf.Abs(style.ShadowOffset.y)) : 0f;
            float glow = style.GlowAlpha > 0f ? style.GlowWidth : 0f;
            float pad = UiShapeParams.Padding(shadowSoft, shadowOffset, glow);
            Rect quad = new Rect(r.x - pad, r.y - pad, r.width + pad * 2f, r.height + pad * 2f);
            float radius = UiShapeParams.FitRadius(style.Radius, r.width, r.height);
            mat.SetVector(SizePad, new Vector4(quad.width, quad.height, pad, UiShapeParams.AntiAliasPixels(DeviceScale())));
            mat.SetVector(ShapeA, new Vector4((float)kind, radius, Mathf.Max(0f, style.Stroke), (float)style.Gradient));
            mat.SetVector(ColorId, Out4(style.Fill));
            mat.SetVector(Color2Id, Out4(style.Gradient == Gradient.None ? style.Fill : style.Fill2));
            mat.SetVector(ShadowId, new Vector4(style.ShadowOffset.x, style.ShadowOffset.y, style.ShadowSoftness, Mathf.Clamp01(style.ShadowAlpha)));
            mat.SetVector(GlowId, new Vector4(glow, Mathf.Clamp01(style.GlowAlpha), style.Glint, Mathf.Clamp01(style.GlintAlpha)));
            mat.SetVector(GlowColorId, Out4(style.GlowColor));
            mat.SetVector(ArcId, arc);
            mat.SetVector(ClipCircleId, LocalClip(quad));
            Graphics.DrawTexture(quad, Texture2D.whiteTexture, new Rect(0, 0, 1, 1), 0, 0, 0, 0, Color.white, mat);
            return true;
        }

        private static readonly int GlyphRectId = Shader.PropertyToID("_GlyphRect");
        private static readonly int GlyphMapId = Shader.PropertyToID("_GlyphMap");

        /// <summary>
        /// ADR-085 atlas icon fitted (aspect kept, centred) inside <paramref name="r"/>; the style's fill, gradient,
        /// stroke, shadow and glow apply to the glyph. False when the shader or the icon is unavailable.
        /// </summary>
        public static bool Icon(Rect r, string name, in Style style)
        {
            var mat = EnsureMaterial();
            if (mat == null) return false;
            if (!UiIconAtlas.TryGet(name, out var entry)) return false;
            var tex = UiIconAtlas.Texture;
            if (tex == null) return false;
            if (Event.current == null || Event.current.type != EventType.Repaint) return true;
            if (r.width <= 0f || r.height <= 0f) return true;
            float h = Mathf.Min(r.height, r.width / entry.Aspect);
            float w = h * entry.Aspect;
            Rect fit = new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h);
            float shadowSoft = style.ShadowAlpha > 0f ? style.ShadowSoftness : 0f;
            float shadowOffset = style.ShadowAlpha > 0f ? Mathf.Max(Mathf.Abs(style.ShadowOffset.x), Mathf.Abs(style.ShadowOffset.y)) : 0f;
            float glow = style.GlowAlpha > 0f ? style.GlowWidth : 0f;
            float pad = UiShapeParams.Padding(shadowSoft, shadowOffset, glow);
            Rect quad = new Rect(fit.x - pad, fit.y - pad, fit.width + pad * 2f, fit.height + pad * 2f);
            mat.SetVector(SizePad, new Vector4(quad.width, quad.height, pad, UiShapeParams.AntiAliasPixels(DeviceScale())));
            mat.SetVector(ShapeA, new Vector4((float)UiShapeKind.Glyph, 0f, Mathf.Max(0f, style.Stroke), (float)style.Gradient));
            mat.SetVector(ColorId, Out4(style.Fill));
            mat.SetVector(Color2Id, Out4(style.Gradient == Gradient.None ? style.Fill : style.Fill2));
            mat.SetVector(ShadowId, new Vector4(style.ShadowOffset.x, style.ShadowOffset.y, style.ShadowSoftness, Mathf.Clamp01(style.ShadowAlpha)));
            mat.SetVector(GlowId, new Vector4(glow, Mathf.Clamp01(style.GlowAlpha), style.Glint, Mathf.Clamp01(style.GlintAlpha)));
            mat.SetVector(GlowColorId, Out4(style.GlowColor));
            float aw = tex.width, ah = tex.height;
            // Atlas entry Y is top-down; the texture was flipped on upload.
            mat.SetVector(GlyphRectId, new Vector4(entry.X / aw, (ah - entry.Y - entry.Height) / ah, entry.Width / aw, entry.Height / ah));
            mat.SetVector(GlyphMapId, new Vector4(Generation.Icons.IconAtlas.SpreadPixels, fit.height / entry.Height, 0f, 0f));
            mat.SetVector(ClipCircleId, LocalClip(quad));
            Graphics.DrawTexture(quad, tex, new Rect(0, 0, 1, 1), 0, 0, 0, 0, Color.white, mat);
            return true;
        }

        /// <summary>Anti-aliased capsule line between two GUI points.</summary>
        public static bool Line(Vector2 a, Vector2 b, Color color, float thickness)
        {
            if (EnsureMaterial() == null) return false;
            if (Event.current == null || Event.current.type != EventType.Repaint) return true;
            float length = Vector2.Distance(a, b);
            if (length <= 0.001f) return true;
            float t = Mathf.Max(0.5f, thickness);
            Matrix4x4 prior = GUI.matrix;
            GUI.matrix = prior * Matrix4x4.TRS(new Vector3(a.x, a.y, 0),
                Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg), Vector3.one);
            // Extend by half the thickness on each end so joined segments meet without gaps.
            Shape(new Rect(-t * 0.5f, -t * 0.5f, length + t, t), UiShapeKind.RoundedRect, Style.Solid(color, t * 0.5f));
            GUI.matrix = prior;
            return true;
        }
    }
}
