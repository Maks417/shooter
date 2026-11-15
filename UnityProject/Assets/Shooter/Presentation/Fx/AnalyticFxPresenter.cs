#if UNITY_5_3_OR_NEWER
#nullable enable
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Presentation.Fx
{
    /// <summary>
    /// World-space mesh proxies for <see cref="AnalyticFxPool"/> (muzzle / spark / smoke).
    /// Primitive meshes + URP Lit/Unlit colors only; no sprites or particle packs.
    /// </summary>
    public sealed class AnalyticFxPresenter : MonoBehaviour
    {
        public const int VisualLayer = 11;

        private AnalyticFxPool? pool;
        private Transform[] slots = System.Array.Empty<Transform>();
        private MeshRenderer[] renderers = System.Array.Empty<MeshRenderer>();
        private MeshFilter[] filters = System.Array.Empty<MeshFilter>();
        private Material? muzzleMaterial;
        private Material? sparkMaterial;
        private Material? smokeMaterial;
        private Mesh? quadMesh;
        private Mesh? sphereMesh;
        private Mesh? capsuleMesh;
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        public static AnalyticFxPresenter Create(Transform parent, AnalyticFxPool pool)
        {
            if (parent == null) throw new System.ArgumentNullException(nameof(parent));
            if (pool == null) throw new System.ArgumentNullException(nameof(pool));
            var root = new GameObject("Analytic combat FX");
            root.transform.SetParent(parent, false);
            root.layer = VisualLayer;
            var presenter = root.AddComponent<AnalyticFxPresenter>();
            presenter.Initialize(pool);
            return presenter;
        }

        private void Initialize(AnalyticFxPool pool)
        {
            this.pool = pool;
            AcquireMeshes();
            CreateMaterials();
            slots = new Transform[AnalyticFxPool.MaxInstances];
            renderers = new MeshRenderer[AnalyticFxPool.MaxInstances];
            filters = new MeshFilter[AnalyticFxPool.MaxInstances];
            for (int i = 0; i < AnalyticFxPool.MaxInstances; i++)
            {
                var go = new GameObject("Fx " + i) { layer = VisualLayer };
                go.transform.SetParent(transform, false);
                go.SetActive(false);
                filters[i] = go.AddComponent<MeshFilter>();
                renderers[i] = go.AddComponent<MeshRenderer>();
                renderers[i].shadowCastingMode = ShadowCastingMode.Off;
                renderers[i].receiveShadows = false;
                renderers[i].lightProbeUsage = LightProbeUsage.Off;
                renderers[i].reflectionProbeUsage = ReflectionProbeUsage.Off;
                slots[i] = go.transform;
            }
        }

        /// <param name="suppressMuzzleFlash">
        /// Accessibility: hide muzzle mesh proxies (and leave impact/smoke readable).
        /// </param>
        public void Sync(bool suppressMuzzleFlash = false)
        {
            if (pool == null) return;
            for (int i = 0; i < AnalyticFxPool.MaxInstances; i++)
            {
                var fx = pool.Get(i);
                var slot = slots[i];
                bool hide = !fx.Active || (suppressMuzzleFlash && fx.Kind == FxKind.MuzzleFlash);
                if (hide)
                {
                    if (slot.gameObject.activeSelf) slot.gameObject.SetActive(false);
                    continue;
                }

                float scale = AnalyticFxPool.EvaluateScale(fx.Kind, fx.Age, fx.Lifetime);
                float opacity = AnalyticFxPool.EvaluateOpacity(fx.Kind, fx.Age, fx.Lifetime);
                Vector3 pos = new Vector3(fx.Position.X, fx.Position.Y, fx.Position.Z);
                Vector3 normal = new Vector3(fx.Normal.X, fx.Normal.Y, fx.Normal.Z);
                if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;
                else normal.Normalize();

                Mesh? mesh;
                Material? material;
                Vector3 localScale;
                Quaternion rotation;
                switch (fx.Kind)
                {
                    case FxKind.MuzzleFlash:
                        mesh = sphereMesh;
                        material = muzzleMaterial;
                        // Elongated along shot direction so muzzle reads as a flash cone, not a marble.
                        localScale = new Vector3(scale * 1.05f, scale * 1.05f, scale * 2.35f);
                        rotation = Quaternion.LookRotation(normal, Vector3.up);
                        pos += normal * (scale * 0.52f);
                        break;
                    case FxKind.ImpactSpark:
                        mesh = capsuleMesh;
                        material = sparkMaterial;
                        localScale = new Vector3(scale * 0.52f, scale * 2.25f, scale * 0.52f);
                        rotation = Quaternion.FromToRotation(Vector3.up, normal);
                        pos += normal * (scale * 0.65f);
                        break;
                    default:
                        mesh = sphereMesh != null ? sphereMesh : quadMesh;
                        material = smokeMaterial;
                        localScale = new Vector3(scale * 1.22f, scale * 0.98f, scale * 1.22f);
                        rotation = Quaternion.identity;
                        pos += normal * (scale * 0.28f);
                        break;
                }

                filters[i].sharedMesh = mesh;
                renderers[i].sharedMaterial = material;
                ApplyInstanceVisual(i, fx.Kind, opacity);
                slot.SetPositionAndRotation(pos, rotation);
                slot.localScale = localScale;
                if (!slot.gameObject.activeSelf) slot.gameObject.SetActive(true);
            }
        }

        public void ClearVisuals()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].gameObject.activeSelf)
                    slots[i].gameObject.SetActive(false);
            }
        }

        private void ApplyInstanceVisual(int index, FxKind kind, float opacity)
        {
            propertyBlock.Clear();
            switch (kind)
            {
                case FxKind.MuzzleFlash:
                    propertyBlock.SetColor("_BaseColor", new Color(1f, 0.82f, 0.34f, opacity));
                    if (muzzleMaterial != null && muzzleMaterial.HasProperty("_EmissionColor"))
                        propertyBlock.SetColor("_EmissionColor", new Color(6.8f, 3.1f, 0.45f) * opacity);
                    break;
                case FxKind.ImpactSpark:
                    propertyBlock.SetColor("_BaseColor", new Color(1f, 0.96f, 0.7f, opacity));
                    if (sparkMaterial != null && sparkMaterial.HasProperty("_EmissionColor"))
                        propertyBlock.SetColor("_EmissionColor", new Color(5.4f, 3.0f, 0.7f) * opacity);
                    break;
                default:
                    // Warm-grey smoke so impacts read against cool outdoor fill.
                    propertyBlock.SetColor("_BaseColor", new Color(0.62f, 0.60f, 0.56f, opacity));
                    break;
            }
            renderers[index].SetPropertyBlock(propertyBlock);
        }

        private void CreateMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (lit == null && unlit == null)
                throw new System.InvalidOperationException("Required locked URP Lit/Unlit shader missing; no art fallback");

            muzzleMaterial = BuildEmissive(lit != null ? lit : unlit!, "Analytic muzzle flash", new Color(1f, 0.8f, 0.32f, 1f), new Color(6f, 2.8f, 0.4f));
            sparkMaterial = BuildEmissive(lit != null ? lit : unlit!, "Analytic impact spark", new Color(1f, 0.95f, 0.55f, 1f), new Color(4.5f, 2.6f, 0.6f));
            smokeMaterial = BuildTransparent(unlit != null ? unlit : lit!, "Analytic smoke proxy", new Color(0.62f, 0.60f, 0.56f, 0.45f));
        }

        private static Material BuildEmissive(Shader shader, string name, Color baseColor, Color emission)
        {
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return mat;
        }

        private static Material BuildTransparent(Shader shader, string name, Color color)
        {
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            return mat;
        }

        private void AcquireMeshes()
        {
            quadMesh = ExtractPrimitiveMesh(PrimitiveType.Quad, out var q);
            sphereMesh = ExtractPrimitiveMesh(PrimitiveType.Sphere, out var s);
            capsuleMesh = ExtractPrimitiveMesh(PrimitiveType.Capsule, out var c);
            if (q != null) Destroy(q);
            if (s != null) Destroy(s);
            if (c != null) Destroy(c);
        }

        private static Mesh ExtractPrimitiveMesh(PrimitiveType type, out GameObject host)
        {
            host = GameObject.CreatePrimitive(type);
            host.hideFlags = HideFlags.HideAndDontSave;
            var filter = host.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            var col = host.GetComponent<Collider>();
            if (col != null) Destroy(col);
            if (mesh == null) throw new System.InvalidOperationException("Primitive mesh missing for " + type);
            return mesh;
        }

        private void OnDestroy()
        {
            ClearVisuals();
            if (muzzleMaterial != null) Destroy(muzzleMaterial);
            if (sparkMaterial != null) Destroy(sparkMaterial);
            if (smokeMaterial != null) Destroy(smokeMaterial);
            muzzleMaterial = null;
            sparkMaterial = null;
            smokeMaterial = null;
            pool = null;
        }
    }
}
#endif
