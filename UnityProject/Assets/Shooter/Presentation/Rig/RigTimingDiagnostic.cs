using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Shooter.Presentation
{
    /// <summary>Release diagnostic of eight real skinned rigs. Separate from full reference V05 acceptance.</summary>
    public sealed class RigTimingDiagnostic : MonoBehaviour
    {
        [Serializable] private sealed class Proof
        {
            public string result = "Verified", workload = "M1-eight-rig-diagnostic-v1", gateStatus = "DiagnosticOnly";
            public string scope = "Eight Evaluate calls plus bone/weapon transform application per rendered frame; excludes engine skinning/render costs; short diagnostic only";
            public string contentPlanHash, sourceCommit, toolchainHash, cpu, gpu, scenario, rawCsv;
            public int rigs = 8, samples;
            public double cpuMillisecondsP95, cpuMillisecondsMax;
            public float gripErrorMaxMeters, plantSlideMaxMeters, plantTargetErrorMaxMeters;
            public float armBodyClearanceMinMeters, eyeSightLineErrorMaxMeters, stockErrorMaxMeters;
            public string handErrorMethod = "Wrist to current procedural task target; support hand intentionally leaves the grip during reload";
            public bool bodyProxyClearanceNonnegative;
            public bool gripWithinTwoCentimeters, plantSlideWithinFiveCentimeters;
        }
        private readonly double[] samples = new double[262144];
        private SoldierGallery gallery;
        private int count;
        private uint sequence;
        private float started;
        private string output, source, toolchain;
        public void Initialize(SoldierGallery view, string reportPath, string sourceCommit, string toolchainHash)
        {
            if (view == null || view.RigCount != 8) throw new ArgumentException("Eight real rigs are required.");
            output = Path.GetFullPath(reportPath);
            if (!Directory.Exists(Path.GetDirectoryName(output)) || File.Exists(output)) throw new IOException("Fresh existing diagnostic output directory required.");
            gallery = view; source = sourceCommit; toolchain = toolchainHash; started = Time.realtimeSinceStartup;
        }
        private void LateUpdate()
        {
            if (gallery == null) return;
            try
            {
                float elapsed = Time.realtimeSinceStartup - started;
                if (elapsed < 2) { sequence = gallery.PoseSequence; return; }
                if (sequence == gallery.PoseSequence) throw new InvalidOperationException("Eight-rig diagnostic missed its pose update.");
                sequence = gallery.PoseSequence;
                double ms = gallery.LastEightRigMilliseconds;
                if (double.IsNaN(ms) || double.IsInfinity(ms) || ms <= 0 || count == samples.Length) throw new InvalidOperationException("Invalid/bounded rig timing sample.");
                samples[count++] = ms;
                if (elapsed < 12) return;
                if (count < 60) throw new InvalidOperationException("Rig diagnostic captured fewer than60 rendered samples.");
                string csv = Path.Combine(Path.GetDirectoryName(output),"rig-samples.csv");
                using (var writer = new StreamWriter(csv,false))
                { writer.WriteLine("frame,eightRigCpuMs"); for (int i=0;i<count;i++) writer.WriteLine(i+","+samples[i].ToString("R",CultureInfo.InvariantCulture)); }
                var sorted = samples.Take(count).OrderBy(v=>v).ToArray();
                var proof = new Proof { contentPlanHash=gallery.ContentPlanHash, sourceCommit=source,toolchainHash=toolchain,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,
                    scenario=gallery.Scenario,rawCsv=csv,samples=count,cpuMillisecondsP95=sorted[(int)Math.Ceiling(.95*count)-1],cpuMillisecondsMax=sorted[count-1],
                    gripErrorMaxMeters=gallery.MaxGripError,plantSlideMaxMeters=gallery.MaxPlantSlide,plantTargetErrorMaxMeters=gallery.MaxPlantTargetError,
                    armBodyClearanceMinMeters=gallery.MinArmBodyClearance,eyeSightLineErrorMaxMeters=gallery.MaxEyeSightLineError,stockErrorMaxMeters=gallery.MaxStockError,
                    bodyProxyClearanceNonnegative=gallery.MinArmBodyClearance>=0,
                    gripWithinTwoCentimeters=gallery.MaxGripError<=.02f,plantSlideWithinFiveCentimeters=gallery.MaxPlantSlide<=.05f };
                File.WriteAllText(output,JsonUtility.ToJson(proof,true)+"\n");
                Debug.Log("SHOOTER_RIG_DIAGNOSTIC_COMPLETE samples="+count);enabled=false;Application.Quit(0);
            }
            catch (Exception error) { Debug.LogError("SHOOTER_FAILURE: "+error);enabled=false;Application.Quit(1); }
        }
    }
}
