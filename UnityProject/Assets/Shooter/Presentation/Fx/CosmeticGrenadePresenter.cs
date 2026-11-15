#if UNITY_5_3_OR_NEWER
#nullable enable
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Presentation.Fx
{
    /// <summary>World mesh proxies for <see cref="CosmeticGrenadeFlight"/> (procedural sphere stand-in).</summary>
    public sealed class CosmeticGrenadePresenter : MonoBehaviour
    {
        public const int VisualLayer = AnalyticFxPresenter.VisualLayer;

        private CosmeticGrenadeFlight? flight;
        private Transform[] slots = System.Array.Empty<Transform>();
        private MeshRenderer[] renderers = System.Array.Empty<MeshRenderer>();
        private Mesh? sphereMesh;
        private Material? material;
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        public static CosmeticGrenadePresenter Create(Transform parent, CosmeticGrenadeFlight flight)
        {
            if (parent == null) throw new System.ArgumentNullException(nameof(parent));
            if (flight == null) throw new System.ArgumentNullException(nameof(flight));
            var root = new GameObject("Cosmetic grenades");
            root.transform.SetParent(parent, false);
            root.layer = VisualLayer;
            var presenter = root.AddComponent<CosmeticGrenadePresenter>();
            presenter.Initialize(flight);
            return presenter;
        }

        private void Initialize(CosmeticGrenadeFlight flight)
        {
            this.flight = flight;
            sphereMesh = CreateSphere();
            material = CreateMaterial(new Color(0.28f, 0.42f, 0.22f, 1f));
            slots = new Transform[CosmeticGrenadeFlight.MaxLive];
            renderers = new MeshRenderer[CosmeticGrenadeFlight.MaxLive];
            for (int i = 0; i < CosmeticGrenadeFlight.MaxLive; i++)
            {
                var go = new GameObject("Grenade " + i) { layer = VisualLayer };
                go.transform.SetParent(transform, false);
                go.SetActive(false);
                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = sphereMesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                slots[i] = go.transform;
                renderers[i] = renderer;
            }
        }

        public void Sync()
        {
            if (flight == null) return;
            for (int i = 0; i < CosmeticGrenadeFlight.MaxLive; i++)
            {
                var live = flight[i];
                var slot = slots[i];
                if (!live.Active)
                {
                    if (slot.gameObject.activeSelf) slot.gameObject.SetActive(false);
                    continue;
                }
                if (!slot.gameObject.activeSelf) slot.gameObject.SetActive(true);
                slot.localPosition = new Vector3(live.Position.X, live.Position.Y, live.Position.Z);
                slot.localScale = Vector3.one * 0.12f;
                propertyBlock.SetColor("_BaseColor", new Color(0.28f, 0.42f, 0.22f, 1f));
                renderers[i].SetPropertyBlock(propertyBlock);
            }
        }

        public void ClearVisuals()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && slots[i].gameObject.activeSelf)
                    slots[i].gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (sphereMesh != null) Destroy(sphereMesh);
        }

        private static Mesh CreateSphere()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Destroy(go);
            return mesh;
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }
    }
}
#endif
