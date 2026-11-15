#if UNITY_5_3_OR_NEWER
#nullable enable
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Presentation.Rendering
{
    /// <summary>
    /// Screen-space rifle PiP optic: keep hip FOV on the gameplay camera, blur the periphery,
    /// and digitally zoom only inside the circular lens. No second optic camera / RTT recursion.
    /// </summary>
    public sealed class AdsScopePipPresenter : MonoBehaviour
    {
        private Camera? targetCamera;
        private Material? material;
        private RenderTexture? sharp;
        private RenderTexture? blurA;
        private RenderTexture? blurB;
        private float strength;
        private float zoom = 1f;
        private float lensRadius = View.AdsOpticVisual.LensRadiusFraction;
        private bool subscribed;

        public static AdsScopePipPresenter Attach(Camera camera)
        {
            if (camera == null) throw new System.ArgumentNullException(nameof(camera));
            var existing = camera.GetComponent<AdsScopePipPresenter>();
            if (existing != null) return existing;
            var presenter = camera.gameObject.AddComponent<AdsScopePipPresenter>();
            presenter.targetCamera = camera;
            presenter.EnsureMaterial();
            return presenter;
        }

        /// <summary>
        /// Update PiP parameters for this frame. Strength 0 releases the camera target path.
        /// </summary>
        public void Configure(float pipStrength, float lensUvZoom, float lensRadiusFraction)
        {
            strength = Mathf.Clamp01(pipStrength);
            zoom = Mathf.Clamp(lensUvZoom, 0.05f, 1f);
            lensRadius = Mathf.Clamp(lensRadiusFraction, 0.05f, 0.9f);
            if (strength <= 0.01f)
                ReleaseCameraTarget();
        }

        private void OnEnable()
        {
            if (!subscribed)
            {
                RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
                subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
                subscribed = false;
            }
            ReleaseCameraTarget();
        }

        private void OnDestroy()
        {
            OnDisable();
            if (material != null)
            {
                Destroy(material);
                material = null;
            }
            ReleaseRts();
        }

        private void LateUpdate()
        {
            // Bind the sharp RT before URP renders this camera when PiP is active.
            if (targetCamera == null || strength <= 0.01f) return;
            if (!EnsureMaterial()) return;
            int w = Mathf.Max(8, Screen.width);
            int h = Mathf.Max(8, Screen.height);
            if (!EnsureSharp(w, h)) return;
            if (targetCamera.targetTexture != sharp)
                targetCamera.targetTexture = sharp;
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam != targetCamera || strength <= 0.01f) return;
            if (sharp == null || material == null || targetCamera == null) return;
            if (targetCamera.targetTexture != sharp) return;

            Material mat = material;
            int w = sharp.width;
            int h = sharp.height;
            float aspect = w / (float)h;

            int bw = Mathf.Max(4, w / 2);
            int bh = Mathf.Max(4, h / 2);
            if (blurA == null || blurA.width != bw || blurA.height != bh)
            {
                ReleaseBlurRts();
                blurA = CreateRt(bw, bh, 0, "AdsScopePip.BlurA");
                blurB = CreateRt(bw, bh, 0, "AdsScopePip.BlurB");
            }

            mat.SetVector("_BlurOffset", new Vector4(1.2f, 0f, 0f, 0f));
            Graphics.Blit(sharp, blurA, mat, 0);
            mat.SetVector("_BlurOffset", new Vector4(0f, 1.2f, 0f, 0f));
            Graphics.Blit(blurA, blurB, mat, 0);
            mat.SetVector("_BlurOffset", new Vector4(1.6f, 0f, 0f, 0f));
            Graphics.Blit(blurB, blurA, mat, 0);
            mat.SetVector("_BlurOffset", new Vector4(0f, 1.6f, 0f, 0f));
            Graphics.Blit(blurA, blurB, mat, 0);

            mat.SetTexture("_BlurTex", blurB);
            mat.SetFloat("_LensRadius", lensRadius);
            mat.SetFloat("_Zoom", zoom);
            mat.SetFloat("_Strength", strength);
            mat.SetFloat("_RimWidth", 0.045f);
            mat.SetFloat("_Aspect", aspect);
            Graphics.Blit(sharp, (RenderTexture?)null, mat, 1);
        }

        private bool EnsureMaterial()
        {
            if (material != null) return true;
            var shader = Shader.Find("Shooter/AdsScopePip");
            if (shader == null || !shader.isSupported)
                return false;
            material = new Material(shader)
            {
                name = "AdsScopePip",
                hideFlags = HideFlags.HideAndDontSave
            };
            return true;
        }

        private bool EnsureSharp(int w, int h)
        {
            if (sharp != null && sharp.width == w && sharp.height == h) return true;
            ReleaseCameraTarget();
            if (sharp != null) { sharp.Release(); Destroy(sharp); sharp = null; }
            sharp = CreateRt(w, h, 24, "AdsScopePip.Sharp");
            return sharp != null;
        }

        private static RenderTexture CreateRt(int w, int h, int depth, string name)
        {
            var rt = new RenderTexture(w, h, depth, RenderTextureFormat.ARGB32)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            rt.Create();
            return rt;
        }

        private void ReleaseCameraTarget()
        {
            if (targetCamera != null && sharp != null && targetCamera.targetTexture == sharp)
                targetCamera.targetTexture = null;
        }

        private void ReleaseBlurRts()
        {
            if (blurA != null) { blurA.Release(); Destroy(blurA); blurA = null; }
            if (blurB != null) { blurB.Release(); Destroy(blurB); blurB = null; }
        }

        private void ReleaseRts()
        {
            ReleaseCameraTarget();
            if (sharp != null) { sharp.Release(); Destroy(sharp); sharp = null; }
            ReleaseBlurRts();
        }
    }
}
#endif
