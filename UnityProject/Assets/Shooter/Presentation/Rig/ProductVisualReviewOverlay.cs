#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Shooter.Presentation.View;
using UnityEngine;

namespace Shooter.Presentation.Rig
{
    /// <summary>
    /// T062 live pose overlay + JSON sample capture for product visual defect inventory.
    /// Does not edit generator/recipe sources, does not claim P05 PASS, and never authorizes a fourth clearance search.
    /// </summary>
    public sealed class ProductVisualReviewOverlay : MonoBehaviour
    {
        private ProductActorPresentation? actors;
        private Camera? camera;
        private readonly List<ProductVisualReviewSample> sampleScratch = new List<ProductVisualReviewSample>(16);
        private string outputDirectory = "";
        private string mapId = "";
        private string profile = "";
        private uint lastWrittenTick = uint.MaxValue;
        private int framesSinceWrite;
        private int samplesWritten;
        private bool enabledOverlay = true;
        private GUIStyle? labelStyle;
        private Material? lineMaterial;

        public bool OverlayEnabled
        {
            get => enabledOverlay;
            set => enabledOverlay = value;
        }

        public string OutputDirectory => outputDirectory;
        public int SamplesWritten => samplesWritten;
        public float LastMinArmClearance { get; private set; } = float.NaN;
        public float LastMaxEyeSightError { get; private set; }

        public static ProductVisualReviewOverlay Attach(
            Transform parent,
            ProductActorPresentation actors,
            Camera camera,
            string outputDirectory)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            if (camera == null) throw new ArgumentNullException(nameof(camera));

            var existing = parent.GetComponentInChildren<ProductVisualReviewOverlay>();
            if (existing != null)
            {
                existing.Configure(actors, camera, outputDirectory);
                return existing;
            }

            var overlay = new GameObject("Product visual review overlay").AddComponent<ProductVisualReviewOverlay>();
            overlay.transform.SetParent(parent, false);
            overlay.Configure(actors, camera, outputDirectory);
            return overlay;
        }

        public void Configure(ProductActorPresentation actors, Camera camera, string outputDirectory)
        {
            this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            this.outputDirectory = outputDirectory ?? "";
            if (this.outputDirectory.Length > 0)
            {
                Directory.CreateDirectory(this.outputDirectory);
                ProductVisualDefectCatalog.ValidateImmutableP05Fail();
                File.WriteAllText(
                    Path.Combine(this.outputDirectory, "p05-status.json"),
                    "{\"gateId\":\"P05\",\"visualAcceptance\":\"FAIL\",\"adrId\":\"ADR-024\",\"fourthClearanceSearchAuthorized\":false,\"note\":\"Immutable FAIL; overlay tooling only.\"}",
                    Encoding.UTF8);
            }
            Debug.Log("SHOOTER_VISUAL_REVIEW overlay=1 output=" + (this.outputDirectory.Length == 0 ? "(hud-only)" : this.outputDirectory) +
                      " p05=" + ProductVisualDefectCatalog.VisualAcceptance);
        }

        public void SetContext(string mapId, string profile, uint completedTick)
        {
            this.mapId = mapId ?? "";
            this.profile = profile ?? "";
            actors?.SetReviewContext(this.mapId, this.profile, completedTick);
        }

        private void LateUpdate()
        {
            if (!enabledOverlay || actors == null) return;
            actors.CollectReviewSamples(sampleScratch);
            LastMinArmClearance = actors.AggregateMinArmBodyClearanceMeters;
            LastMaxEyeSightError = actors.AggregateMaxEyeSightLineErrorMeters;
            if (outputDirectory.Length == 0 || sampleScratch.Count == 0) return;

            framesSinceWrite++;
            uint tick = sampleScratch[0].CompletedTick;
            // Bound write rate: every ~30 frames or when tick advances by 30.
            bool tickGate = lastWrittenTick == uint.MaxValue || tick >= lastWrittenTick + 30;
            if (!tickGate && framesSinceWrite < 30) return;
            framesSinceWrite = 0;
            lastWrittenTick = tick;
            WriteSampleBatch(tick);
        }

        private void WriteSampleBatch(uint tick)
        {
            var name = "sample-tick-" + tick.ToString(CultureInfo.InvariantCulture) + "-" +
                       DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture) + ".json";
            var path = Path.Combine(outputDirectory, name);
            var sb = new StringBuilder(1024 + sampleScratch.Count * 512);
            sb.Append("{\"schemaId\":\"shooter.product-visual-review-sample-batch.v1\",");
            sb.Append("\"taskId\":\"T062\",\"fixId\":\"FIX11\",");
            sb.Append("\"p05VisualAcceptance\":\"FAIL\",\"fourthClearanceSearchAuthorized\":false,");
            sb.Append("\"mapId\":\"").Append(Escape(mapId)).Append("\",");
            sb.Append("\"profile\":\"").Append(Escape(profile)).Append("\",");
            sb.Append("\"completedTick\":").Append(tick.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"aggregateMinArmBodyClearanceMeters\":");
            AppendNullable(sb, LastMinArmClearance);
            sb.Append(",\"aggregateMaxEyeSightLineErrorMeters\":");
            AppendNullable(sb, LastMaxEyeSightError);
            sb.Append(",\"samples\":[");
            for (int i = 0; i < sampleScratch.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(sampleScratch[i].ToJsonObject());
            }
            sb.Append("]}");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            samplesWritten++;
            Debug.Log("SHOOTER_VISUAL_REVIEW_SAMPLE path=" + path + " actors=" + sampleScratch.Count +
                      " armClear=" + LastMinArmClearance.ToString("R", CultureInfo.InvariantCulture) +
                      " eyeSight=" + LastMaxEyeSightError.ToString("R", CultureInfo.InvariantCulture));
        }

        private void OnRenderObject()
        {
            if (!enabledOverlay || camera == null || sampleScratch.Count == 0) return;
            if (Camera.current != camera) return;
            EnsureLineMaterial();
            lineMaterial!.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);
            for (int i = 0; i < sampleScratch.Count; i++)
            {
                var s = sampleScratch[i];
                if (s.Corpse || !s.Alive) continue;
                // Eye -> sight residual (P05-D02).
                GL.Color(s.ReproducesEyeSightResidual ? new Color(1f, 0.2f, 0.1f, 1f) : new Color(0.2f, 0.9f, 0.3f, 1f));
                GL.Vertex(V(s.RightEye));
                GL.Vertex(V(s.SightWorld));
                // Weapon axis marker.
                GL.Color(new Color(0.9f, 0.85f, 0.2f, 1f));
                GL.Vertex(V(s.WeaponPosition));
                GL.Vertex(V(s.SightWorld));
                // Clearance proxy cue at chest height (P05-D01 / D03).
                GL.Color(s.ReproducesForearmStrapProxy ? new Color(1f, 0.05f, 0.05f, 1f) : new Color(0.2f, 0.6f, 1f, 1f));
                var chest = new Vector3(s.Feet.X, s.Feet.Y + 1.25f, s.Feet.Z);
                float mark = s.ReproducesForearmStrapProxy ? 0.18f : 0.08f;
                GL.Vertex(chest + new Vector3(-mark, 0, 0));
                GL.Vertex(chest + new Vector3(mark, 0, 0));
                GL.Vertex(chest + new Vector3(0, 0, -mark));
                GL.Vertex(chest + new Vector3(0, 0, mark));
            }
            GL.End();
            GL.PopMatrix();
        }

        private void OnGUI()
        {
            if (!enabledOverlay) return;
            var style = LabelStyle();
            GUI.Label(new Rect(24, Screen.height - 148, 980, 22),
                "T062 visual review  P05=" + ProductVisualDefectCatalog.VisualAcceptance +
                "  ADR-024  fourthSearch=false",
                style);
            GUI.Label(new Rect(24, Screen.height - 126, 980, 22),
                "armClearMin=" + Format(LastMinArmClearance) +
                "  eyeSightMax=" + Format(LastMaxEyeSightError) +
                "  samplesWritten=" + samplesWritten +
                "  actors=" + sampleScratch.Count,
                style);
            GUI.Label(new Rect(24, Screen.height - 104, 980, 22),
                "defects: P05-D01 forearm/strap Unresolved | P05-D02 eye/sight Unresolved | no generator search",
                style);
        }

        private void EnsureLineMaterial()
        {
            if (lineMaterial != null) return;
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;
            lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }

        private GUIStyle LabelStyle()
        {
            if (labelStyle != null) return labelStyle;
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = Color.yellow }
            };
            return labelStyle;
        }

        private static Vector3 V(Contracts.Float3 v) => new Vector3(v.X, v.Y, v.Z);

        private static string Format(float value) =>
            float.IsNaN(value) ? "n/a" : value.ToString("0.####", CultureInfo.InvariantCulture);

        private static void AppendNullable(StringBuilder sb, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) sb.Append("null");
            else sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static string Escape(string value) =>
            (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                if (Application.isPlaying) Destroy(lineMaterial);
                else DestroyImmediate(lineMaterial);
                lineMaterial = null;
            }
        }
    }
}
