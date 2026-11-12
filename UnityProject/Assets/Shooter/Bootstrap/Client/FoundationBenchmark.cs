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
        private struct FrameSample { public double Frame; public long Allocated, EndTimestamp, RecorderMain, RecorderRender, MainCount, RenderCount; public uint Tick; }
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
            public int collectorVersion = 3, mappedTimings, ambiguousTimings, drainFrames;
            public int preconditionSeconds;
            public uint captureStartTick;
            public string renderBudgetMethod = "nativeCpuFrameStartToPresentUpperBound-v1";
            public bool frameAllocationAvailable, tickAllocationAvailable, timestampFrequencyMatches;
            public long allocationMainControlBytes, allocationWorkerControlBytes, stopwatchFrequency, nativeCpuFrequency;
            public long allocationEndControlBytes, tickAllocationEndControlBytes, legacyBclControlBytes;
            public int configuredVSyncCount, targetFrameRate, presentationConfigurationChecks;
            public string allocationMethod, allocationDiagnostic;
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
        private readonly FrameTiming[] timings = new FrameTiming[32];
        private readonly FrameTimingLedger ledger = new FrameTimingLedger(262144);
        private ProfilerRecorder mainRecorder, renderRecorder, allocationRecorder;
        private FoundationEntry authority;
        private string output;
        private int frameCount, tickCount, warmupSeconds, captureSeconds, preconditionSeconds;
        private uint firstTick, endTick;
        private long previousTimestamp, beforePreviousTimestamp, previousAllocated;
        private uint previousTick;
        private ulong timingFrequency;
        private int drainFrames;
        private int presentationConfigurationChecks;
        private bool previousValid, injectAllocation;
        private object diagnosticAllocationRoot;
        private DateTime previousUtc, endUtc;
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
            injectAllocation = FoundationEntry.CommandLine("-shooterDiagnosticAllocate", "false") == "true";
            if ((injectOverload || injectAllocation) && !diagnostic) throw new ArgumentException("Fault injection is restricted to diagnostic captures");
            warmupSeconds = diagnostic ? 2 : 60; captureSeconds = diagnostic ? 5 : 180;
            preconditionSeconds = diagnostic ? 0 : int.Parse(FoundationEntry.CommandLine("-shooterPreconditionSeconds", "0"), CultureInfo.InvariantCulture);
            if (preconditionSeconds < 0 || preconditionSeconds > 1800) throw new ArgumentException("Preconditioning must be between zero and 1800 seconds");
            firstTick = (uint)((preconditionSeconds + warmupSeconds) * 60); endTick = firstTick + (uint)(captureSeconds * 60);
            ticks = new TickSample[captureSeconds * 60];
            output = Path.GetFullPath(FoundationEntry.CommandLine("-shooterBenchmarkOutput", ""));
            if (!Directory.Exists(output)) throw new ArgumentException("Benchmark output directory must already exist");
            mainRecorder = Recorder(ProfilerCategory.Internal, "CPU Main Thread Frame Time");
            renderRecorder = Recorder(ProfilerCategory.Internal, "CPU Render Thread Frame Time");
            allocationRecorder = Recorder(ProfilerCategory.Memory, "GC Allocated In Frame");
            authority.StepCompleted += RecordTick;
            authority.SessionFailed += Abort;
            timingFrequency = FrameTimingManager.GetCpuTimerFrequency();
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
            ValidatePresentationConfiguration();
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
        private static void ValidatePresentationConfiguration()
        {
            if (QualitySettings.vSyncCount != 0 || Application.targetFrameRate != -1)
                throw new InvalidOperationException("B01 requires application VSync off and uncapped presentation");
        }
        private void Update()
        {
            if (authority == null || complete || authority.Failed) return;
            try
            {
                long now = Stopwatch.GetTimestamp(), allocated = AllocationMeter.TotalBytes;
                DateTime utc = DateTime.UtcNow;
                // Close the preceding frame, including its LateUpdate/engine/worker managed work.
                if (previousValid && previousTick >= firstTick && previousTick < endTick)
                {
                    ValidatePresentationConfiguration(); presentationConfigurationChecks++;
                    if (frameCount == frames.Length) throw new InvalidOperationException("Benchmark frame capacity exceeded; no samples silently dropped");
                    if (!captureStarted) { captureStarted = true; startUtc = previousUtc; }
                    var mainSample = mainRecorder.Valid && mainRecorder.Count == 1 ? mainRecorder.GetSample(0) : default;
                    var renderSample = renderRecorder.Valid && renderRecorder.Count == 1 ? renderRecorder.GetSample(0) : default;
                    frames[frameCount++] = new FrameSample { Frame = (now - previousTimestamp) * 1000.0 / Stopwatch.Frequency,
                        Allocated = allocated < 0 || previousAllocated < 0 ? -1 : allocated - previousAllocated,
                        EndTimestamp = now, Tick = previousTick, RecorderMain = mainSample.Value, RecorderRender = renderSample.Value,
                        MainCount = mainSample.Count, RenderCount = renderSample.Count };
                    ledger.Add(beforePreviousTimestamp, previousTimestamp);
                    endUtc = utc;
                }
                PollTimings(now);
                FrameTimingManager.CaptureFrameTimings();
                bool ready = authority.Ready; uint tick = authority.CompletedTicks;
                beforePreviousTimestamp = previousTimestamp; previousTimestamp = now;
                previousAllocated = allocated; previousTick = tick; previousUtc = utc; previousValid = ready && validated;
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
                if (tick >= endTick) { if (++drainFrames >= 64) Finish(); return; }
                if (injectAllocation && tick >= firstTick) diagnosticAllocationRoot = new byte[4096];
            }
            catch (Exception error) { authority.Fail(error); }
        }
        private void PollTimings(long now)
        {
            uint count = FrameTimingManager.GetLatestTimings((uint)timings.Length, timings);
            if (timingFrequency == 0 || timingFrequency != (ulong)Stopwatch.Frequency) return;
            for (int i = 0; i < count; i++)
            {
                var t = timings[i];
                // Check epoch/range as well as frequency; never infer a value across clock domains.
                if (t.frameStartTimestamp > (ulong)now || (ulong)now - t.frameStartTimestamp > timingFrequency * 30) continue;
                if (t.cpuTimePresentCalled > (ulong)now) continue;
                ledger.Observe(t.frameStartTimestamp, t.cpuMainThreadFrameTime, t.cpuRenderThreadFrameTime, t.gpuFrameTime, t.cpuTimePresentCalled, t.firstSubmitTimestamp);
            }
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
            DisposeRecorders();
            // Outside sampling: reject a constant-zero or disconnected meter, even after a long capture.
            long allBefore = AllocationMeter.TotalBytes, threadBefore = AllocationMeter.ThreadBytes, legacyBefore = -1;
            try { legacyBefore = GC.GetAllocatedBytesForCurrentThread(); } catch { }
            diagnosticAllocationRoot = new byte[4096];
            long endControl = allBefore < 0 ? -1 : AllocationMeter.TotalBytes - allBefore;
            long tickEndControl = threadBefore < 0 ? -1 : AllocationMeter.ThreadBytes - threadBefore;
            long legacyControl = -1;
            if (legacyBefore >= 0) try { legacyControl = GC.GetAllocatedBytesForCurrentThread() - legacyBefore; } catch { }
            int mapped = 0, ambiguous = 0;
            using (var writer = new StreamWriter(Path.Combine(output, "frames.csv")))
            {
                writer.WriteLine("tick,frameMs,mainMs,renderMs,gpuMs,managedFrameBytes,nativeFrameStartTimestamp,lowerTimestamp,upperTimestamp,endTimestamp,ambiguous,recorderMainNanoseconds,recorderRenderNanoseconds,recorderMainCount,recorderRenderCount,nativePresentCalledTimestamp,nativeFirstSubmitTimestamp");
                for (int i = 0; i < frameCount; i++)
                {
                    var f = frames[i]; var t = ledger[i];
                    if (t.NativeTimestamp != 0 && !t.Ambiguous) mapped++;
                    if (t.Ambiguous) ambiguous++;
                    writer.WriteLine(f.Tick + "," + N(f.Frame) + "," + N(t.Main) + "," + N(t.Render) + "," + N(t.Gpu) + "," + f.Allocated + "," + t.NativeTimestamp
                        + "," + t.LowerExclusive + "," + t.UpperInclusive + "," + f.EndTimestamp + "," + t.Ambiguous
                        + "," + f.RecorderMain + "," + f.RecorderRender + "," + f.MainCount + "," + f.RenderCount
                        + "," + t.PresentTimestamp + "," + t.FirstSubmitTimestamp);
                }
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
                renderRecorderAvailable = renderAvailable, allocationRecorderAvailable = allocationAvailable,
                frameAllocationAvailable = AllocationMeter.AllThreadsAvailable && endControl >= 4096,
                tickAllocationAvailable = AllocationMeter.ThreadAvailable && tickEndControl >= 4096,
                allocationMethod = AllocationMeter.Method, allocationDiagnostic = AllocationMeter.Diagnostic,
                allocationMainControlBytes = AllocationMeter.MainControlBytes, allocationWorkerControlBytes = AllocationMeter.WorkerControlBytes,
                allocationEndControlBytes = endControl, tickAllocationEndControlBytes = tickEndControl, legacyBclControlBytes = legacyControl,
                configuredVSyncCount = QualitySettings.vSyncCount, targetFrameRate = Application.targetFrameRate,
                presentationConfigurationChecks = presentationConfigurationChecks,
                preconditionSeconds = preconditionSeconds, captureStartTick = firstTick,
                stopwatchFrequency = Stopwatch.Frequency, nativeCpuFrequency = (long)timingFrequency,
                timestampFrequencyMatches = timingFrequency == (ulong)Stopwatch.Frequency, mappedTimings = mapped, ambiguousTimings = ambiguous, drainFrames = drainFrames };
            File.WriteAllText(Path.Combine(output, "capture.json"), JsonUtility.ToJson(metadata, true) + "\n");
            complete = true;
            GC.KeepAlive(diagnosticAllocationRoot);
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
