#nullable enable
using System;
using UnityEngine;

namespace Shooter.Presentation.Fx
{
    /// <summary>
    /// ADR-111: a short additive ribbon swept by the knife edge during the cutting part of a swing.
    /// Camera-local, cosmetic only; the opacity comes from <c>FirstPersonSpecialActions.BladeTrailStrength</c>.
    /// </summary>
    public sealed class KnifeBladeTrail : IDisposable
    {
        public const int MaxSamples = 14;
        public const float SampleLifetimeSeconds = 0.11f;
        private static readonly Color Edge = new Color(.86f, .93f, 1f, 1f);
        private readonly Transform space;
        private readonly GameObject host;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly Vector3[] bases = new Vector3[MaxSamples];
        private readonly Vector3[] tips = new Vector3[MaxSamples];
        private readonly float[] ages = new float[MaxSamples];
        private readonly float[] strengths = new float[MaxSamples];
        private readonly Vector3[] vertices = new Vector3[MaxSamples * 2];
        private readonly Color[] colors = new Color[MaxSamples * 2];
        private readonly Vector2[] uvs = new Vector2[MaxSamples * 2];
        private readonly int[] triangles = new int[(MaxSamples - 1) * 6];
        private int count;

        public KnifeBladeTrail(Transform cameraSpace, int layer)
        {
            space = cameraSpace ?? throw new ArgumentNullException(nameof(cameraSpace));
            var shader = Shader.Find("Shooter/CombatParticle") ?? throw new InvalidOperationException("Required combat particle shader missing.");
            material = new Material(shader) { name = "Knife blade trail" };
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_Brightness", 1.6f);
            mesh = new Mesh { name = "Knife blade trail" };
            mesh.MarkDynamic();
            host = new GameObject("FP knife blade trail") { layer = layer };
            host.transform.SetParent(space, false);
            host.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            host.SetActive(false);
        }

        /// <summary>Adds this frame's edge segment (world space) when <paramref name="strength"/> &gt; 0 and ages the old ones.</summary>
        public void Step(Vector3 worldBase, Vector3 worldTip, float strength, float dtSeconds)
        {
            float dt = float.IsFinite(dtSeconds) && dtSeconds > 0f ? dtSeconds : 0f;
            int w = 0;
            for (int i = 0; i < count; i++)
            {
                float age = ages[i] + dt;
                if (age >= SampleLifetimeSeconds) continue;
                bases[w] = bases[i]; tips[w] = tips[i]; strengths[w] = strengths[i]; ages[w] = age; w++;
            }
            count = w;
            if (strength > 0.001f)
            {
                if (count == MaxSamples)
                {
                    Array.Copy(bases, 1, bases, 0, MaxSamples - 1); Array.Copy(tips, 1, tips, 0, MaxSamples - 1);
                    Array.Copy(ages, 1, ages, 0, MaxSamples - 1); Array.Copy(strengths, 1, strengths, 0, MaxSamples - 1);
                    count--;
                }
                bases[count] = space.InverseTransformPoint(worldBase);
                tips[count] = space.InverseTransformPoint(worldTip);
                ages[count] = 0f; strengths[count] = Mathf.Clamp01(strength); count++;
            }
            Rebuild();
        }

        public void Clear()
        {
            count = 0;
            host.SetActive(false);
        }

        private void Rebuild()
        {
            if (count < 2) { host.SetActive(false); return; }
            for (int i = 0; i < count; i++)
            {
                float life = 1f - ages[i] / SampleLifetimeSeconds;
                float a = strengths[i] * life * life;
                vertices[i * 2] = bases[i]; vertices[i * 2 + 1] = tips[i];
                // The edge (tip side) is bright; the inner side fades so the ribbon reads as a swept arc.
                colors[i * 2] = new Color(Edge.r, Edge.g, Edge.b, a * .15f);
                colors[i * 2 + 1] = new Color(Edge.r, Edge.g, Edge.b, a * .85f);
                float u = count > 1 ? i / (float)(count - 1) : 0f;
                uvs[i * 2] = new Vector2(u, 0f); uvs[i * 2 + 1] = new Vector2(u, 1f);
            }
            for (int i = count * 2; i < vertices.Length; i++) { vertices[i] = vertices[count * 2 - 1]; colors[i] = Color.clear; uvs[i] = Vector2.zero; }
            int t = 0;
            for (int i = 0; i < count - 1; i++)
            {
                int a0 = i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                triangles[t++] = a0; triangles[t++] = b0; triangles[t++] = a1;
                triangles[t++] = a1; triangles[t++] = b0; triangles[t++] = b1;
            }
            for (int i = t; i < triangles.Length; i++) triangles[i] = 0;
            mesh.Clear(true);
            mesh.vertices = vertices; mesh.colors = colors; mesh.uv = uvs; mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
            host.SetActive(true);
        }

        public void Dispose()
        {
            Release(host); Release(mesh); Release(material);
        }

        private static void Release(UnityEngine.Object? item)
        {
            if (item == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(item); else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
