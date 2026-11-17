#if UNITY_5_3_OR_NEWER
#nullable enable
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Shooter.Presentation.Rendering
{
    /// <summary>
    /// Rifle PiP optic: keep hip FOV on the gameplay camera and blur the periphery. ADR-103 (supersedes the ADR-065
    /// "no second camera" rule): once the near-field rifle is hidden, a child lens camera renders the scene at the
    /// optic FOV into a lens-sized target, so distant detail is real geometry at native resolution rather than an 8x
    /// stretch of the hip frame. Before that (and under reduced camera motion) the lens keeps the digital zoom.
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
        private Camera? lensCamera;
        private RenderTexture? lensTarget;
        private bool lensRequested;
        private float lensFovDegrees = 60f;

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
        public void Configure(float pipStrength, float lensUvZoom, float lensRadiusFraction) =>
            Configure(pipStrength, lensUvZoom, lensRadiusFraction, useLensCamera: false, lensCameraFovDegrees: 60f);

        /// <summary>ADR-103 overload: request the optical lens camera at a vertical FOV for this frame.</summary>
        public void Configure(float pipStrength, float lensUvZoom, float lensRadiusFraction, bool useLensCamera, float lensCameraFovDegrees)
        {
            strength = Mathf.Clamp01(pipStrength);
            zoom = Mathf.Clamp(lensUvZoom, 0.05f, 1f);
            lensRadius = Mathf.Clamp(lensRadiusFraction, 0.05f, 0.9f);
            lensRequested = useLensCamera && strength > 0.01f && float.IsFinite(lensCameraFovDegrees);
            lensFovDegrees = Mathf.Clamp(lensCameraFovDegrees, 1f, 120f);
            if (strength <= 0.01f)
                ReleaseCameraTarget();
            if (!lensRequested && lensCamera != null) lensCamera.enabled = false;
        }

        /// <summary>True while the optical lens camera renders this frame (diagnostics/tests).</summary>
        public bool LensCameraActive => lensCamera != null && lensCamera.enabled;

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
            if (lensCamera != null)
            {
                ProductPostProcessing.Detach(lensCamera);
                Destroy(lensCamera.gameObject);
                lensCamera = null;
            }
            ReleaseLensTarget();
        }

        private void LateUpdate()
        {
            // Bind the sharp RT before URP renders this camera when PiP is active.
            if (targetCamera == null || strength <= 0.01f)
            {
                if (lensCamera != null) lensCamera.enabled = false;
                return;
            }
            if (!EnsureMaterial()) return;
            int w = Mathf.Max(8, Screen.width);
            int h = Mathf.Max(8, Screen.height);
            if (!EnsureSharp(w, h)) return;
            if (targetCamera.targetTexture != sharp)
                targetCamera.targetTexture = sharp;
            UpdateLensCamera(h);
        }

        private void UpdateLensCamera(int screenHeight)
        {
            if (!lensRequested || targetCamera == null)
            {
                if (lensCamera != null) lensCamera.enabled = false;
                return;
            }
            var tier = ProductPostProcessing.CurrentTier;
            // Lens diameter in screen pixels, capped by the tier budget (1024 on High covers 1080p's ~908 px lens).
            int diameter = Mathf.CeilToInt(2f * lensRadius * screenHeight);
            int size = Mathf.Clamp(Mathf.CeilToInt(diameter / 64f) * 64, 128, tier.ScopeLensTextureSize);
            if (!EnsureLensTarget(size, tier.MsaaSamples)) return;
            if (lensCamera == null)
            {
                var go = new GameObject("Optical scope lens camera") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(targetCamera.transform, false);
                lensCamera = go.AddComponent<Camera>();
                lensCamera.enabled = false;
            }
            if (!lensCamera.enabled)
            {
                lensCamera.CopyFrom(targetCamera);
                ProductPostProcessing.Attach(lensCamera);
            }
            // Follow the gameplay camera exactly (recoil, sway, damage punch) as a child at identity.
            lensCamera.transform.SetParent(targetCamera.transform, false);
            lensCamera.transform.localPosition = Vector3.zero;
            lensCamera.transform.localRotation = Quaternion.identity;
            lensCamera.cullingMask = targetCamera.cullingMask;
            lensCamera.clearFlags = targetCamera.clearFlags;
            lensCamera.backgroundColor = targetCamera.backgroundColor;
            lensCamera.farClipPlane = targetCamera.farClipPlane;
            lensCamera.nearClipPlane = targetCamera.nearClipPlane;
            lensCamera.depth = targetCamera.depth - 1f; // render before the gameplay camera composites
            lensCamera.targetTexture = lensTarget;
            lensCamera.aspect = 1f;
            lensCamera.fieldOfView = lensFovDegrees;
            lensCamera.enabled = true;
        }

        private bool EnsureLensTarget(int size, int msaa)
        {
            msaa = msaa >= 4 ? 4 : msaa >= 2 ? 2 : 1;
            if (lensTarget != null && lensTarget.width == size && lensTarget.antiAliasing == msaa) return true;
            if (lensCamera != null && lensCamera.targetTexture == lensTarget) lensCamera.targetTexture = null;
            ReleaseLensTarget();
            lensTarget = CreateRt(size, size, true, "AdsScopePip.Lens", msaa);
            return lensTarget != null;
        }

        private void ReleaseLensTarget()
        {
            if (lensTarget != null) { lensTarget.Release(); Destroy(lensTarget); lensTarget = null; }
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
                blurA = CreateRt(bw, bh, false, "AdsScopePip.BlurA");
                blurB = CreateRt(bw, bh, false, "AdsScopePip.BlurB");
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
            bool lens = lensCamera != null && lensCamera.enabled && lensTarget != null;
            mat.SetFloat("_UseLensTex", lens ? 1f : 0f);
            if (lens) mat.SetTexture("_LensTex", lensTarget);
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
            sharp = CreateRt(w, h, true, "AdsScopePip.Sharp", ProductPostProcessing.CurrentTier.MsaaSamples);
            return sharp != null;
        }

        private static RenderTexture CreateRt(int w, int h, bool depth, string name, int msaa = 1)
        {
            // ADR-103: camera targets get 32-bit float depth (no D24 precision loss at range) and the tier's MSAA.
            var descriptor = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0)
            {
                depthStencilFormat = depth ? GraphicsFormat.D32_SFloat_S8_UInt : GraphicsFormat.None,
                msaaSamples = Mathf.Max(1, msaa),
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear
            };
            var rt = new RenderTexture(descriptor)
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
