using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Shooter.Bootstrap
{
    [DefaultExecutionOrder(1000)]
    public sealed class FoundationBenchmark : MonoBehaviour
    {
        private struct FrameSample { public double Frame, Main, Render, Gpu; public long Allocated; public uint Tick; }
        private struct TickSample { public uint Tick; public double Ms; public long Allocated; }
        [Serializable] private sealed class Metadata
        {
            public int schemaVersion = 1;
            public string workloadId = "B01-minimal-v1", seed = "460072030", profile, gpuAdapter, cpu, graphicsApi;
            public bool diagnostic;
            public int width, height, frames, ticks, warmupSeconds, captureSeconds;
            public string captureStartUtc, captureEndUtc;
            public float[] cameraMeters;
            public bool frameTimingEnabled, mainRecorderAvailable, renderRecorderAvailable, allocationRecorderAvailable;
        }
        [Serializable] private sealed class FailureMetadata
        {
            public int schemaVersion = 1;
            public string result = "FAIL", reason, utc;
            public bool diagnostic, injectedOverload;
            public uint completedTicks;
            public int frames, ticks, lastAdvanceTicks;
            public double elapsedSeconds, debtSeconds;
        }
        private readonly FrameSample[] frames = new FrameSample[262144];
        private TickSample[] ticks;
        private readonly FrameTiming[] timings = new FrameTiming[1];
        private ProfilerRecorder mainRecorder, renderRecorder, allocationRecorder;
        private FoundationEntry authority;
        private string output;
        private int frameCount, tickCount, warmupSeconds, captureSeconds;
        private uint firstTick, endTick;
        private long lastFrame;
        private bool diagnostic, validated, captureStarted, complete;
        private bool recordersDisposed, injectOverload;
        private DateTime startUtc;
        public void Initialize(FoundationEntry entry)
        {
            authority = entry;
            string workload = FoundationEntry.CommandLine("-shooterBenchmark", "");
            if (workload != "B01-minimal-v1") throw new ArgumentException("Only B01-minimal-v1 exists at M0");
            diagnostic = FoundationEntry.CommandLine("-shooterBenchmarkDiagnostic", "false") == "true";
            injectOverload = FoundationEntry.CommandLine("-shooterDiagnosticOverload", "false") == "true";
            if (injectOverload && !diagnostic) throw new ArgumentException("Overload injection is restricted to diagnostic captures");
            warmupSeconds = diagnostic ? 2 : 60; captureSeconds = diagnostic ? 5 : 180;
            firstTick = (uint)(warmupSeconds * 60); endTick = firstTick + (uint)(captureSeconds * 60);
            ticks = new TickSample[captureSeconds * 60];
            output = Path.GetFullPath(FoundationEntry.CommandLine("-shooterBenchmarkOutput", ""));
            if (!Directory.Exists(output)) throw new ArgumentException("Benchmark output directory must already exist");
            mainRecorder = Recorder(ProfilerCategory.Internal, "Main Thread");
            renderRecorder = Recorder(ProfilerCategory.Internal, "Render Thread");
            allocationRecorder = Recorder(ProfilerCategory.Memory, "GC Allocated In Frame");
            authority.StepCompleted += RecordTick;
            authority.SessionFailed += Abort;
            lastFrame = Stopwatch.GetTimestamp();
        }
        private static ProfilerRecorder Recorder(ProfilerCategory category, string name)
        { try { return ProfilerRecorder.StartNew(category, name, 1); } catch { return default; } }
        private void RecordTick(uint tick, double ms, long allocated)
        {
            if (tick < firstTick || tick >= endTick) return;
            if (tickCount >= ticks.Length) throw new InvalidOperationException("Benchmark tick capacity exceeded");
            ticks[tickCount++] = new TickSample { Tick = tick, Ms = ms, Allocated = allocated };
        }
        private void ValidateWorkload()
        {
            if (QualitySettings.names[QualitySettings.GetQualityLevel()] != "Low" || Screen.width != 1280 || Screen.height != 720 || SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11)
                throw new InvalidOperationException("B01 reference configuration requires native 1280x720 Low/D3D11");
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera == null || Vector3.Distance(camera.transform.position, new Vector3(0, 1.65f, -6)) > .0001f || Quaternion.Angle(camera.transform.rotation, Quaternion.identity) > .0001f)
                throw new InvalidOperationException("B01 camera recipe mismatch");
            var bodies = UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
            if (bodies.Length != 2) throw new InvalidOperationException("B01 must contain exactly floor and box colliders");
            bool floor = false, box = false;
            foreach (var body in bodies)
            {
                floor |= body.size == new Vector3(10, .1f, 10) && body.transform.position == new Vector3(0, -.05f, 0);
                box |= body.size == Vector3.one && body.transform.position == new Vector3(0, .5f, 0);
            }
            if (!floor || !box) throw new InvalidOperationException("B01 generated geometry recipe mismatch");
            validated = true;
        }
        private void Update()
        {
            if (authority == null || complete || authority.Failed) return;
            try
            {
                long now = Stopwatch.GetTimestamp(); double frameMs = (now - lastFrame) * 1000.0 / Stopwatch.Frequency; lastFrame = now;
                FrameTimingManager.CaptureFrameTimings();
                if (!authority.Ready) return;
                if (injectOverload && authority.CompletedTicks >= firstTick)
                {
                    // Stall one diagnostic frame; the real clock must retain debt and fail.
                    injectOverload = false;
                    Debug.Log("SHOOTER_DIAGNOSTIC_OVERLOAD_INJECTED");
                    System.Threading.Thread.Sleep(200);
                    return;
                }
                if (!validated) { ValidateWorkload(); return; }
                if (authority.CompletedTicks >= endTick) { Finish(); return; }
                if (authority.CompletedTicks < firstTick) return;
                if (!captureStarted) { captureStarted = true; startUtc = DateTime.UtcNow; }
                if (frameCount == frames.Length) throw new InvalidOperationException("Benchmark frame capacity exceeded; no samples silently dropped");
                double main = mainRecorder.Valid && mainRecorder.Count > 0 ? mainRecorder.LastValue / 1e6 : -1;
                double render = renderRecorder.Valid && renderRecorder.Count > 0 ? renderRecorder.LastValue / 1e6 : -1;
                double gpu = -1;
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                {
                    if (timings[0].cpuMainThreadFrameTime > 0) main = timings[0].cpuMainThreadFrameTime;
                    if (timings[0].cpuRenderThreadFrameTime > 0) render = timings[0].cpuRenderThreadFrameTime;
                    if (timings[0].gpuFrameTime > 0) gpu = timings[0].gpuFrameTime;
                }
                frames[frameCount++] = new FrameSample { Frame = frameMs, Main = main, Render = render, Gpu = gpu,
                    Allocated = allocationRecorder.Valid && allocationRecorder.Count > 0 ? allocationRecorder.LastValue : -1, Tick = authority.CompletedTicks };
            }
            catch (Exception error) { authority.Fail(error); }
        }
        private void Abort(Exception error)
        {
            if (complete) return;
            complete = true; enabled = false;
            DisposeRecorders();
            // Never emit capture.json or a successful completion marker from a partial capture.
            File.WriteAllText(Path.Combine(output, "failure.json"), JsonUtility.ToJson(new FailureMetadata {
                reason = error.ToString(), utc = DateTime.UtcNow.ToString("o"), diagnostic = diagnostic,
                injectedOverload = diagnostic && FoundationEntry.CommandLine("-shooterDiagnosticOverload", "false") == "true",
                completedTicks = authority.CompletedTicks, frames = frameCount, ticks = tickCount,
                elapsedSeconds = authority.LastElapsedSeconds, debtSeconds = authority.TickDebtSeconds, lastAdvanceTicks = authority.LastAdvanceTicks
            }, true) + "\n");
            Debug.Log("SHOOTER_BENCHMARK_ABORTED");
        }
        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        private void Finish()
        {
            if (tickCount != ticks.Length || frameCount == 0 || !captureStarted) throw new InvalidOperationException("Incomplete benchmark capture");
            DateTime endUtc = DateTime.UtcNow;
            DisposeRecorders();
            using (var writer = new StreamWriter(Path.Combine(output, "frames.csv")))
            {
                writer.WriteLine("tick,frameMs,mainMs,renderMs,gpuMs,managedFrameBytes");
                for (int i = 0; i < frameCount; i++) { var f = frames[i]; writer.WriteLine(f.Tick + "," + N(f.Frame) + "," + N(f.Main) + "," + N(f.Render) + "," + N(f.Gpu) + "," + f.Allocated); }
            }
            using (var writer = new StreamWriter(Path.Combine(output, "ticks.csv")))
            {
                writer.WriteLine("tick,simulationMs,managedTickBytes");
                for (int i = 0; i < tickCount; i++) { var t = ticks[i]; writer.WriteLine(t.Tick + "," + N(t.Ms) + "," + t.Allocated); }
            }
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            var metadata = new Metadata { diagnostic = diagnostic, profile = QualitySettings.names[QualitySettings.GetQualityLevel()], width = Screen.width, height = Screen.height,
                cpu = SystemInfo.processorType, gpuAdapter = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), frames = frameCount, ticks = tickCount,
                warmupSeconds = warmupSeconds, captureSeconds = captureSeconds, captureStartUtc = startUtc.ToString("o"), captureEndUtc = endUtc.ToString("o"),
                cameraMeters = new[] { camera.transform.position.x, camera.transform.position.y, camera.transform.position.z },
                frameTimingEnabled = FrameTimingManager.IsFeatureEnabled(), mainRecorderAvailable = mainAvailable,
                renderRecorderAvailable = renderAvailable, allocationRecorderAvailable = allocationAvailable };
            File.WriteAllText(Path.Combine(output, "capture.json"), JsonUtility.ToJson(metadata, true) + "\n");
            complete = true;
            Debug.Log("SHOOTER_BENCHMARK_COMPLETE diagnostic=" + diagnostic + " frames=" + frameCount + " ticks=" + tickCount);
            Application.Quit(0);
        }
        private void OnDestroy()
        {
            if (authority != null) { authority.StepCompleted -= RecordTick; authority.SessionFailed -= Abort; }
            DisposeRecorders();
        }
        private bool mainAvailable, renderAvailable, allocationAvailable;
        private void DisposeRecorders()
        {
            if (recordersDisposed) return;
            recordersDisposed = true;
            mainAvailable = mainRecorder.Valid; renderAvailable = renderRecorder.Valid; allocationAvailable = allocationRecorder.Valid;
            mainRecorder.Dispose(); renderRecorder.Dispose(); allocationRecorder.Dispose();
        }
    }
}
