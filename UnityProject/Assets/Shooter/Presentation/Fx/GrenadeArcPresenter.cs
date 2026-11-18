#nullable enable
using System;
using Shooter.Contracts;
using UnityEngine;

namespace Shooter.Presentation.Fx
{
    /// <summary>
    /// ADR-111: draws the <see cref="GrenadeArcPreview"/> while the local player cooks a grenade: a glowing arc that
    /// widens toward the target and a pulsing ring where it first lands. Presentation only.
    /// </summary>
    public sealed class GrenadeArcPresenter : IDisposable
    {
        public const int WorldLayerMask = 1 << 8;
        private const int RingSegments = 28;
        private static readonly Color ArcColor = new Color(1f, .86f, .42f, 1f);
        private static readonly Color RingColor = new Color(1f, .45f, .18f, 1f);
        private readonly GameObject root;
        private readonly LineRenderer arc;
        private readonly LineRenderer ring;
        private readonly Material arcMaterial;
        private readonly Material ringMaterial;
        private readonly Float3[] points = new Float3[GrenadeArcPreview.MaxPoints];
        private readonly Vector3[] positions = new Vector3[GrenadeArcPreview.MaxPoints];
        private readonly Vector3[] ringPositions = new Vector3[RingSegments];

        public GrenadeArcPresenter(Transform parent, int layer)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var shader = Shader.Find("Shooter/CombatParticle") ?? throw new InvalidOperationException("Required combat particle shader missing.");
            arcMaterial = Additive(shader, "Grenade arc", Texture2D.whiteTexture, 1.4f);
            ringMaterial = Additive(shader, "Grenade landing ring", Texture2D.whiteTexture, 1.8f);
            root = new GameObject("Grenade arc preview") { layer = layer };
            root.transform.SetParent(parent, false);
            arc = Line(root.transform, "Arc", arcMaterial, layer);
            arc.widthCurve = new AnimationCurve(new Keyframe(0f, .012f), new Keyframe(1f, .05f));
            arc.colorGradient = MakeGradient(ArcColor, .15f, .9f);
            ring = Line(root.transform, "Landing ring", ringMaterial, layer);
            ring.loop = true;
            ring.widthMultiplier = .045f;
            ring.colorGradient = MakeGradient(RingColor, .85f, .85f);
            root.SetActive(false);
        }

        /// <summary>Updates the preview for this frame; hides it when <paramref name="visible"/> is false.</summary>
        public void Step(bool visible, Vector3 eye, Vector3 forward, Vector3 right, Vector3 up, float fuseSecondsLeft, float timeSeconds)
        {
            if (!visible || !(fuseSecondsLeft > 0f)) { if (root.activeSelf) root.SetActive(false); return; }
            var origin = GrenadeArcPreview.Origin(F(eye), F(forward), F(right), F(up));
            var result = GrenadeArcPreview.Compute(origin, F(forward.normalized), fuseSecondsLeft, Sweep, points);
            if (result.Count < 2) { root.SetActive(false); return; }
            for (int i = 0; i < result.Count; i++) positions[i] = new Vector3(points[i].X, points[i].Y, points[i].Z);
            arc.positionCount = result.Count;
            arc.SetPositions(positions);
            ring.gameObject.SetActive(result.Landed);
            if (result.Landed)
            {
                var n = new Vector3(result.LandingNormal.X, result.LandingNormal.Y, result.LandingNormal.Z).normalized;
                var center = new Vector3(result.Landing.X, result.Landing.Y, result.Landing.Z) + n * .04f;
                var t = Vector3.Cross(n, Mathf.Abs(n.y) < .9f ? Vector3.up : Vector3.forward).normalized;
                var b = Vector3.Cross(n, t);
                float radius = .45f + .06f * Mathf.Sin(timeSeconds * 6f);
                for (int i = 0; i < RingSegments; i++)
                {
                    float a = i * Mathf.PI * 2f / RingSegments;
                    ringPositions[i] = center + (t * Mathf.Cos(a) + b * Mathf.Sin(a)) * radius;
                }
                ring.positionCount = RingSegments;
                ring.SetPositions(ringPositions);
            }
            if (!root.activeSelf) root.SetActive(true);
        }

        /// <summary>World sweep against the map collision layer (shared with the thrown-grenade flight).</summary>
        public static bool Sweep(Float3 from, Float3 delta, out Float3 point, out Float3 normal)
        {
            point = default; normal = default;
            var d = new Vector3(delta.X, delta.Y, delta.Z);
            float distance = d.magnitude;
            if (distance < 1e-5f) return false;
            var start = new Vector3(from.X, from.Y, from.Z);
            if (!Physics.SphereCast(start, CosmeticGrenadeFlight.Radius, d / distance, out var hit, distance,
                    WorldLayerMask, QueryTriggerInteraction.Ignore))
                return false;
            var c = start + d / distance * hit.distance;
            point = new Float3(c.x, c.y, c.z);
            normal = new Float3(hit.normal.x, hit.normal.y, hit.normal.z);
            return true;
        }

        public void Dispose()
        {
            Release(root); Release(arcMaterial); Release(ringMaterial);
        }

        private static Float3 F(Vector3 v) => new Float3(v.x, v.y, v.z);

        private static Material Additive(Shader shader, string name, Texture texture, float brightness)
        {
            var m = new Material(shader) { name = name };
            m.SetTexture("_BaseMap", texture);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_Brightness", brightness);
            return m;
        }

        private static LineRenderer Line(Transform parent, string name, Material material, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 2;
            line.alignment = LineAlignment.View;
            return line;
        }

        private static Gradient MakeGradient(Color color, float startAlpha, float endAlpha)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(endAlpha, 1f) });
            return g;
        }

        private static void Release(UnityEngine.Object? item)
        {
            if (item == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(item); else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
