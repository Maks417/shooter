#nullable enable
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Shooter.Game.Session;
using Shooter.Presentation;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Shooter.Bootstrap
{
    /// <summary>
    /// Collects rendered product TDM frame/tick/counter telemetry for FIX12 analysis.
    /// Does not alter match rules or workload identities to improve measured scores.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class ProductTdmBenchmark : MonoBehaviour
    {
        private struct FrameSample
        {
            public double Frame;
            public long Allocated, EndTimestamp, RecorderMain, RecorderRender, MainCount, RenderCount;
            public uint Tick;
        }

        private struct TickSample
        {
            public uint Tick;
            public double Ms;
            public long Allocated;
            public long Team1Kills, Team2Kills, Respawns, AiRays, PathRequests, Shots, DamageEvents, LeaseAcquires, QuerySamples;
        }

        [Serializable]
        private sealed class Metadata
        {
            public int schemaVersion = 1;
            public string workloadId = "";
            public string seed = "";
            public string mapId = "";
            public string profile = "";
            public string gpuAdapter = "";
            public string cpu = "";
            public string graphicsApi = "";
            public string contentManifestHash = "";
            public string packageRootHash = "";
            public bool diagnostic;
            public bool productTdm = true;
            public int width, height, frames, ticks, warmupSeconds, captureSeconds, teamSize, actors;
            public string captureStartUtc = "", captureEndUtc = "";
            public bool frameTimingEnabled, mainRecorderAvailable, renderRecorderAvailable, allocationRecorderAvailable;
            public int collectorVersion = 5;
            public int mappedTimings, ambiguousTimings, drainFrames, preconditionSeconds;
            public uint captureStartTick;
            public string renderBudgetMethod = "nativeCpuFrameStartToPresentUpperBound-v1";
            public bool frameAllocationAvailable, tickAllocationAvailable, timestampFrequencyMatches;
            public long allocationMainControlBytes, allocationWorkerControlBytes, stopwatchFrequency, nativeCpuFrequency;
            public long allocationEndControlBytes, tickAllocationEndControlBytes;
            public int configuredVSyncCount, targetFrameRate, presentationConfigurationChecks;
            public string allocationMethod = "", allocationDiagnostic = "";
            public long team1Kills, team2Kills, activeTicks, respawns, aiRays, pathRequests, shots, damageEvents;
            public long leaseAcquires, querySamples, contentBindings, renderFrames;
            public int matchesCompleted, matchIndex;
            public long cumulativeActiveTicks, cumulativeRespawns, cumulativeQuerySamples, cumulativeContentBindings;
            public string sessionEpoch = "";
            public string counterSemantics = "respawns=life-replacement;querySamples=IWorldQueries;contentBindings=verified-package-roots";
        }

        [Serializable]
        private sealed class FailureMetadata
        {
            public int schemaVersion = 1;
            public string result = "FAIL", reason = "", utc = "";
            public bool diagnostic;
            public uint completedTicks;
            public int frames, ticks;
        }

        // Uncapped presentation runs ~700 fps on the reference iGPU; a fixed 262144 buffer overflowed after ~6 minutes of a
        // 30-minute soak. Capacity scales with the capture window at a 1000 fps ceiling (overflow still fails explicitly).
        private const int MinFrameCapacity = 262144;
        private const int MaxCapturedFramesPerSecond = 1000;
        private FrameSample[] frames = Array.Empty<FrameSample>();
        private TickSample[] ticks = Array.Empty<TickSample>();
        private readonly FrameTiming[] timings = new FrameTiming[32];
        private FrameTimingLedger ledger = new FrameTimingLedger(1);
        private ProfilerRecorder mainRecorder, renderRecorder;
        private FoundationEntry? authority;
        private ProductTdmHost? host;
        private ProductTdmWorkloads.Definition workload;
        private string output = "";
        private int frameCount, tickCount, warmupSeconds, captureSeconds, preconditionSeconds;
        private uint firstTick, endTick;
        private long previousTimestamp, beforePreviousTimestamp, previousAllocated;
        private uint previousTick;
        private ulong timingFrequency;
        private int drainFrames, presentationConfigurationChecks;
        private bool previousValid, diagnostic, validated, captureStarted, complete, recordersDisposed;
        private DateTime previousUtc, endUtc, startUtc;
        private bool mainAvailable, renderAvailable;
        private readonly BenchmarkProgressWatchdog progressWatchdog = new BenchmarkProgressWatchdog();

        public void Initialize(FoundationEntry entry, ProductTdmHost host, ProductTdmWorkloads.Definition workload)
        {
            authority = entry ?? throw new ArgumentNullException(nameof(entry));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.workload = workload;
            diagnostic = FoundationEntry.CommandLine("-shooterBenchmarkDiagnostic", "false") == "true";
            if (workload.Soak && diagnostic)
                throw new ArgumentException("Soak workloads reject short diagnostic windows.");
            warmupSeconds = diagnostic ? 2 : (workload.Soak ? 0 : workload.WarmupTicks / 60);
            captureSeconds = diagnostic ? 5 : workload.CaptureTicks / 60;
            preconditionSeconds = diagnostic || workload.Soak
                ? 0
                : int.Parse(FoundationEntry.CommandLine("-shooterPreconditionSeconds", "0"), CultureInfo.InvariantCulture);
            if (preconditionSeconds < 0 || preconditionSeconds > 1800)
                throw new ArgumentException("Preconditioning must be between zero and 1800 seconds");
            firstTick = (uint)((preconditionSeconds + warmupSeconds) * 60);
            endTick = firstTick + (uint)(captureSeconds * 60);
            ticks = new TickSample[captureSeconds * 60];
            int frameCapacity = Math.Max(MinFrameCapacity, checked(captureSeconds * MaxCapturedFramesPerSecond));
            frames = new FrameSample[frameCapacity];
            ledger = new FrameTimingLedger(frameCapacity);
            output = Path.GetFullPath(FoundationEntry.CommandLine("-shooterBenchmarkOutput", ""));
            if (!Directory.Exists(output)) throw new ArgumentException("Benchmark output directory must already exist");
            mainRecorder = StartRecorder(ProfilerCategory.Internal, "CPU Main Thread Frame Time");
            renderRecorder = StartRecorder(ProfilerCategory.Internal, "CPU Render Thread Frame Time");
            authority.StepCompleted += RecordTick;
            authority.SessionFailed += Abort;
            timingFrequency = FrameTimingManager.GetCpuTimerFrequency();
        }

        private static ProfilerRecorder StartRecorder(ProfilerCategory category, string name)
        {
            try { return ProfilerRecorder.StartNew(category, name, 1); }
            catch { return default; }
        }

        private void RecordTick(uint tick, double ms, long allocated)
        {
            if (authority == null || tick < firstTick || tick >= endTick) return;
            if (tickCount >= ticks.Length) throw new InvalidOperationException("Product TDM tick capacity exceeded");
            var telemetry = authority.ProductSession.Measurement;
            ticks[tickCount++] = new TickSample
            {
                Tick = tick,
                Ms = ms,
                Allocated = allocated,
                Team1Kills = telemetry.Team1Kills,
                Team2Kills = telemetry.Team2Kills,
                Respawns = telemetry.Respawns,
                AiRays = telemetry.AiRays,
                PathRequests = telemetry.PathRequests,
                Shots = telemetry.Shots,
                DamageEvents = telemetry.DamageEvents,
                LeaseAcquires = telemetry.LeaseAcquires,
                QuerySamples = telemetry.QuerySamples
            };
        }

        private void ValidateWorkload()
        {
            if (authority == null || host == null) return;
            if (host.StartFailed) throw new InvalidOperationException(host.Failure);
            if (!host.MatchPlaying) return;
            if (QualitySettings.names[QualitySettings.GetQualityLevel()] != "Low" ||
                Screen.width != 1280 || Screen.height != 720 ||
                SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11)
                throw new InvalidOperationException("Product TDM reference configuration requires native 1280x720 Low/D3D11");
            if (QualitySettings.vSyncCount != 0 || Application.targetFrameRate != -1)
                throw new InvalidOperationException("Product TDM requires application VSync off and uncapped presentation");
            var session = authority.ProductSession;
            var telemetry = session.Measurement;
            if (!string.Equals(telemetry.WorkloadId, workload.Id, StringComparison.Ordinal))
                throw new InvalidOperationException("Captured WorkloadId missing or reset across BeginLoad; expected " + workload.Id);
            if (!string.Equals(telemetry.MapId, workload.MapId.Value, StringComparison.Ordinal))
                throw new InvalidOperationException("Captured map does not match reviewed workload identity.");
            if (!string.Equals(telemetry.Seed, workload.Seed.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                throw new InvalidOperationException("Captured seed does not match reviewed workload identity.");
            if (telemetry.TeamSize != workload.TeamSize || telemetry.Actors != workload.Actors)
                throw new InvalidOperationException("Captured roster does not match reviewed workload identity.");
            if (string.IsNullOrEmpty(telemetry.ContentManifestHash) || string.IsNullOrEmpty(telemetry.PackageRootHash))
                throw new InvalidOperationException("Content/package identity proofs absent; refusing to stamp placeholders.");
            if (telemetry.ContentBindings <= 0)
                throw new InvalidOperationException("contentBindings absent; verified package roots were not recorded.");
            if (firstTick != (uint)((preconditionSeconds + warmupSeconds) * 60) ||
                endTick != firstTick + (uint)(captureSeconds * 60))
                throw new InvalidOperationException("Sampling window identity drifted from workload durations.");
            telemetry.Profile = QualitySettings.names[QualitySettings.GetQualityLevel()];
            validated = true;
            Debug.Log("SHOOTER_TDM_BENCHMARK_VALIDATED workload=" + workload.Id +
                      " map=" + telemetry.MapId +
                      " teamSize=" + telemetry.TeamSize +
                      " actors=" + telemetry.Actors +
                      " profile=" + telemetry.Profile +
                      " warmupSeconds=" + warmupSeconds +
                      " captureSeconds=" + captureSeconds +
                      " captureStartTick=" + firstTick +
                      " contentBindings=" + telemetry.ContentBindings);
        }

        private void Update()
        {
            if (authority == null || complete || authority.Failed) return;
            try
            {
                // A stalled match would otherwise run until the wrapper timeout with no explanation (ADR-083 evidence).
                if (validated && progressWatchdog.Observe(authority.CompletedTicks, Time.realtimeSinceStartupAsDouble))
                    throw new InvalidOperationException(StallReport());
                if (host != null) host.ReportAuthorityClockTicks(authority.CompletedTicks);
                long now = Stopwatch.GetTimestamp(), allocated = AllocationMeter.TotalBytes;
                DateTime utc = DateTime.UtcNow;
                if (previousValid && previousTick >= firstTick && previousTick < endTick)
                {
                    if (QualitySettings.vSyncCount != 0 || Application.targetFrameRate != -1)
                        throw new InvalidOperationException("Presentation configuration changed during capture.");
                    presentationConfigurationChecks++;
                    if (frameCount == frames.Length)
                        throw new InvalidOperationException("Product TDM frame capacity exceeded");
                    if (!captureStarted) { captureStarted = true; startUtc = previousUtc; }
                    var mainSample = mainRecorder.Valid && mainRecorder.Count == 1 ? mainRecorder.GetSample(0) : default;
                    var renderSample = renderRecorder.Valid && renderRecorder.Count == 1 ? renderRecorder.GetSample(0) : default;
                    frames[frameCount++] = new FrameSample
                    {
                        Frame = (now - previousTimestamp) * 1000.0 / Stopwatch.Frequency,
                        Allocated = allocated < 0 || previousAllocated < 0 ? -1 : allocated - previousAllocated,
                        EndTimestamp = now,
                        Tick = previousTick,
                        RecorderMain = mainSample.Value,
                        RecorderRender = renderSample.Value,
                        MainCount = mainSample.Count,
                        RenderCount = renderSample.Count
                    };
                    ledger.Add(beforePreviousTimestamp, previousTimestamp);
                    endUtc = utc;
                    authority.ProductSession.Measurement.RenderFrames = frameCount;
                }

                PollTimings(now);
                FrameTimingManager.CaptureFrameTimings();
                bool ready = authority.Ready;
                uint tick = authority.CompletedTicks;
                beforePreviousTimestamp = previousTimestamp;
                previousTimestamp = now;
                previousAllocated = allocated;
                previousTick = tick;
                previousUtc = utc;
                previousValid = ready && validated;
                if (!authority.Ready) return;
                if (!validated) { ValidateWorkload(); return; }
                if (tick >= endTick)
                {
                    if (++drainFrames >= 64) Finish();
                    return;
                }
            }
            catch (Exception error) { authority.Fail(error); }
        }

        /// <summary>
        /// Product focus loss pauses the match (player-facing rule), which would silently stall the capture until the
        /// wrapper's timeout. Abort with an explicit reason instead; the run is invalid either way.
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus || complete || authority == null || !validated) return;
            authority.Fail(new InvalidOperationException(
                "Benchmark window lost focus during capture; the product match paused, so the timing window is invalid."));
        }

        private string StallReport()
        {
            var session = authority!.ProductSession;
            return "Benchmark match stopped advancing for " + N(progressWatchdog.StalledSeconds) + " s at tick " + authority.CompletedTicks +
                   " (frames=" + frameCount + " focused=" + Application.isFocused + " ready=" + authority.Ready +
                   " phase=" + (session != null ? session.Phase.ToString() : "none") +
                   " pause=" + (session != null ? session.PauseState.ToString() : "none") +
                   " contentRecovery=" + authority.ContentRecoveryRequired + ").";
        }

        private void PollTimings(long now)
        {
            uint count = FrameTimingManager.GetLatestTimings((uint)timings.Length, timings);
            if (timingFrequency == 0 || timingFrequency != (ulong)Stopwatch.Frequency) return;
            for (int i = 0; i < count; i++)
            {
                var t = timings[i];
                if (t.frameStartTimestamp > (ulong)now || (ulong)now - t.frameStartTimestamp > timingFrequency * 30) continue;
                if (t.cpuTimePresentCalled > (ulong)now) continue;
                ledger.Observe(t.frameStartTimestamp, t.cpuMainThreadFrameTime, t.cpuRenderThreadFrameTime, t.gpuFrameTime, t.cpuTimePresentCalled, t.firstSubmitTimestamp);
            }
        }

        private void Abort(Exception error)
        {
            if (complete) return;
            complete = true;
            enabled = false;
            DisposeRecorders();
            File.WriteAllText(Path.Combine(output, "failure.json"), JsonUtility.ToJson(new FailureMetadata
            {
                reason = error.ToString(),
                utc = DateTime.UtcNow.ToString("o"),
                diagnostic = diagnostic,
                completedTicks = authority != null ? authority.CompletedTicks : 0,
                frames = frameCount,
                ticks = tickCount
            }, true) + "\n");
            Debug.Log("SHOOTER_BENCHMARK_ABORTED");
        }

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private void Finish()
        {
            if (authority == null) throw new InvalidOperationException("Authority missing");
            if (tickCount != ticks.Length || frameCount == 0 || !captureStarted)
                throw new InvalidOperationException("Incomplete product TDM capture");
            DisposeRecorders();
            long allBefore = AllocationMeter.TotalBytes, threadBefore = AllocationMeter.ThreadBytes;
            var probe = new byte[4096];
            long endControl = allBefore < 0 ? -1 : AllocationMeter.TotalBytes - allBefore;
            long tickEndControl = threadBefore < 0 ? -1 : AllocationMeter.ThreadBytes - threadBefore;
            int mapped = 0, ambiguous = 0;
            using (var writer = new StreamWriter(Path.Combine(output, "frames.csv")))
            {
                writer.WriteLine("tick,frameMs,mainMs,renderMs,gpuMs,managedFrameBytes,nativeFrameStartTimestamp,lowerTimestamp,upperTimestamp,endTimestamp,ambiguous,recorderMainNanoseconds,recorderRenderNanoseconds,recorderMainCount,recorderRenderCount,nativePresentCalledTimestamp,nativeFirstSubmitTimestamp");
                for (int i = 0; i < frameCount; i++)
                {
                    var f = frames[i];
                    var t = ledger[i];
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
                writer.WriteLine("tick,simulationMs,managedTickBytes,team1Kills,team2Kills,respawns,aiRays,pathRequests,shots,damageEvents,leaseAcquires,querySamples");
                for (int i = 0; i < tickCount; i++)
                {
                    var t = ticks[i];
                    writer.WriteLine(t.Tick + "," + N(t.Ms) + "," + t.Allocated + "," + t.Team1Kills + "," + t.Team2Kills + "," + t.Respawns + "," +
                                     t.AiRays + "," + t.PathRequests + "," + t.Shots + "," + t.DamageEvents + "," + t.LeaseAcquires + "," + t.QuerySamples);
                }
            }

            var telemetry = authority.ProductSession.Measurement;
            if (!string.Equals(telemetry.WorkloadId, workload.Id, StringComparison.Ordinal))
                throw new InvalidOperationException("WorkloadId lost before capture finish.");
            if (telemetry.ContentBindings <= 0 ||
                string.IsNullOrEmpty(telemetry.ContentManifestHash) ||
                string.IsNullOrEmpty(telemetry.PackageRootHash))
                throw new InvalidOperationException("Refusing to emit capture with absent content counters/identities.");
            // Soak must have entered a replayed match (MatchIndex >= 2). Capture may end mid-match-2.
            if (workload.Soak && host != null && host.Coordinator.MatchIndex < 2 && !diagnostic)
                throw new InvalidOperationException("Soak capture requires finish/replay into at least a second match.");
            var metadata = new Metadata
            {
                workloadId = workload.Id,
                diagnostic = diagnostic,
                seed = telemetry.Seed,
                mapId = telemetry.MapId,
                contentManifestHash = telemetry.ContentManifestHash,
                packageRootHash = telemetry.PackageRootHash,
                teamSize = telemetry.TeamSize,
                actors = telemetry.Actors,
                profile = QualitySettings.names[QualitySettings.GetQualityLevel()],
                width = Screen.width,
                height = Screen.height,
                cpu = SystemInfo.processorType,
                gpuAdapter = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                frames = frameCount,
                ticks = tickCount,
                warmupSeconds = warmupSeconds,
                captureSeconds = captureSeconds,
                captureStartUtc = startUtc.ToString("o"),
                captureEndUtc = endUtc.ToString("o"),
                frameTimingEnabled = FrameTimingManager.IsFeatureEnabled(),
                mainRecorderAvailable = mainAvailable,
                renderRecorderAvailable = renderAvailable,
                allocationRecorderAvailable = AllocationMeter.AllThreadsAvailable,
                frameAllocationAvailable = AllocationMeter.AllThreadsAvailable && endControl >= 4096,
                tickAllocationAvailable = AllocationMeter.ThreadAvailable && tickEndControl >= 4096,
                allocationMethod = AllocationMeter.Method,
                allocationDiagnostic = AllocationMeter.Diagnostic,
                allocationMainControlBytes = AllocationMeter.MainControlBytes,
                allocationWorkerControlBytes = AllocationMeter.WorkerControlBytes,
                allocationEndControlBytes = endControl,
                tickAllocationEndControlBytes = tickEndControl,
                configuredVSyncCount = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate,
                presentationConfigurationChecks = presentationConfigurationChecks,
                preconditionSeconds = preconditionSeconds,
                captureStartTick = firstTick,
                stopwatchFrequency = Stopwatch.Frequency,
                nativeCpuFrequency = (long)timingFrequency,
                timestampFrequencyMatches = timingFrequency == (ulong)Stopwatch.Frequency,
                mappedTimings = mapped,
                ambiguousTimings = ambiguous,
                drainFrames = drainFrames,
                team1Kills = telemetry.Team1Kills,
                team2Kills = telemetry.Team2Kills,
                activeTicks = telemetry.ActiveTicks,
                respawns = telemetry.Respawns,
                aiRays = telemetry.AiRays,
                pathRequests = telemetry.PathRequests,
                shots = telemetry.Shots,
                damageEvents = telemetry.DamageEvents,
                leaseAcquires = telemetry.LeaseAcquires,
                querySamples = telemetry.QuerySamples,
                contentBindings = telemetry.ContentBindings,
                renderFrames = telemetry.RenderFrames,
                matchesCompleted = host != null ? host.Coordinator.MatchesCompleted : telemetry.MatchesCompleted,
                matchIndex = telemetry.MatchIndex,
                cumulativeActiveTicks = telemetry.PublishedCumulativeActiveTicks,
                cumulativeRespawns = telemetry.PublishedCumulativeRespawns,
                cumulativeQuerySamples = telemetry.PublishedCumulativeQuerySamples,
                cumulativeContentBindings = telemetry.PublishedCumulativeContentBindings,
                sessionEpoch = telemetry.SessionEpoch.ToString(CultureInfo.InvariantCulture)
            };
            File.WriteAllText(Path.Combine(output, "capture.json"), JsonUtility.ToJson(metadata, true) + "\n");
            complete = true;
            GC.KeepAlive(probe);
            var ai = authority.ProductSession.BotCounters;
            if (ai != null)
                Debug.Log("SHOOTER_AI_SUMMARY pathsCompleted=" + ai.PathsCompleted + " pathsPartial=" + ai.PathsPartial +
                          " pathsFailed=" + ai.PathsFailed + " stuckEvents=" + ai.StuckEvents + " shotsRequested=" + ai.ShotsRequested +
                          " stateChanges=" + ai.StateChanges);
            Debug.Log("SHOOTER_BENCHMARK_COMPLETE diagnostic=" + diagnostic + " frames=" + frameCount + " ticks=" + tickCount + " workload=" + workload.Id);
            Application.Quit(0);
        }

        private void OnDestroy()
        {
            if (authority != null)
            {
                authority.StepCompleted -= RecordTick;
                authority.SessionFailed -= Abort;
            }
            DisposeRecorders();
        }

        private void DisposeRecorders()
        {
            if (recordersDisposed) return;
            recordersDisposed = true;
            mainAvailable = mainRecorder.Valid;
            renderAvailable = renderRecorder.Valid;
            mainRecorder.Dispose();
            renderRecorder.Dispose();
        }
    }
}
