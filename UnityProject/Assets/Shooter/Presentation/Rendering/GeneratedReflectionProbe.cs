#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Presentation
{
    /// <summary>One-shot reflection probe for ~10m diagnostic galleries. Bake/refresh once on setup, never per frame.</summary>
    public sealed class GeneratedReflectionProbe : IDisposable
    {
        public const float DefaultBoxSizeMeters = 12f;
        public const float DefaultIntensity = 1f;
        public const int DefaultResolution = 128;
        /// <summary>Product map outdoor probe: larger capture volume for arena compounds; bake once, never per frame.</summary>
        public const float ProductMapBoxSizeMeters = 48f;
        public const float ProductMapIntensity = 1.12f;

        private GameObject? root;
        private ReflectionProbe? probe;
        private bool disposed;

        public ReflectionProbe? Probe => probe;
        public bool BakeRan { get; private set; }
        public int BakeId { get; private set; } = -1;

        public static GeneratedReflectionProbe Apply(Transform parent) =>
            Apply(parent, DefaultBoxSizeMeters, DefaultIntensity, DefaultResolution);

        public static GeneratedReflectionProbe Apply(Transform parent, float boxSizeMeters, float intensity, int resolution)
        {
            Validate(boxSizeMeters, intensity, resolution);
            var owned = new GeneratedReflectionProbe();
            try
            {
                owned.Initialize(parent, boxSizeMeters, intensity, resolution);
                return owned;
            }
            catch
            {
                owned.Dispose();
                throw;
            }
        }

        private void Initialize(Transform parent, float boxSizeMeters, float intensity, int resolution)
        {
            root = new GameObject("Generated reflection probe");
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            probe = root.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.importance = 1;
            probe.intensity = intensity;
            probe.boxProjection = false;
            probe.size = new Vector3(boxSizeMeters, boxSizeMeters * .5f, boxSizeMeters);
            probe.center = Vector3.zero;
            probe.nearClipPlane = .1f;
            probe.farClipPlane = Mathf.Max(boxSizeMeters * 1.25f, 20f);
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.cullingMask = ~0;
            probe.resolution = resolution;
            probe.hdr = false;
            probe.shadowDistance = 0f;

            // One-shot capture for the diagnostic scene; callers must not RenderProbe every frame.
            BakeId = probe.RenderProbe();
            BakeRan = BakeId >= 0;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            probe = null;
            if (root != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
            root = null;
            BakeRan = false;
            BakeId = -1;
        }

        public static void Validate(float boxSizeMeters, float intensity, int resolution)
        {
            if (!Finite(boxSizeMeters) || boxSizeMeters < 1f || boxSizeMeters > 64f)
                throw new ArgumentOutOfRangeException(nameof(boxSizeMeters), "Box size must be finite in [1,64] meters.");
            if (!Finite(intensity) || intensity < 0f || intensity > 8f)
                throw new ArgumentOutOfRangeException(nameof(intensity), "Intensity must be finite in [0,8].");
            if (resolution != 16 && resolution != 32 && resolution != 64 && resolution != 128 && resolution != 256)
                throw new ArgumentOutOfRangeException(nameof(resolution), "Resolution must be 16/32/64/128/256.");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
