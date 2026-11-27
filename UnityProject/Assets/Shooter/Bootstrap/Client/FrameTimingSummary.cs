#nullable enable
using Shooter.Game.Contracts;
using UnityEngine;

namespace Shooter.Bootstrap
{
    /// <summary>
    /// Product player: while a match is playing and not paused, samples each frame's interval and the native
    /// FrameTimingManager CPU main thread, CPU render thread and GPU times, and every 10 s of play logs
    /// <c>SHOOTER_FRAME_SUMMARY tick=... session=... seconds=... frames=... fps=... frameMs=p50/p95/max mainMs=... renderMs=...
    /// gpuMs=... slowFrames=...</c>. Ordinary play logs can then tell CPU from GPU stalls without a benchmark run (the
    /// benchmark gate aborts on the first slow frame). Diagnostic only: it never changes the simulation or rendering.
    /// </summary>
    public sealed class FrameTimingSummary : MonoBehaviour
    {
        public const float WindowSeconds = 10f;
        private readonly FrameTimingWindow window = new FrameTimingWindow(4096);
        private readonly FrameTiming[] latest = new FrameTiming[1];
        private FoundationEntry? authority;
        private bool timingEnabled;

        public static FrameTimingSummary Attach(FoundationEntry authority)
        {
            var summary = new GameObject("Frame timing summary").AddComponent<FrameTimingSummary>();
            summary.authority = authority;
            summary.timingEnabled = FrameTimingManager.IsFeatureEnabled();
            Debug.Log("SHOOTER_FRAME_SUMMARY_READY frameTiming=" + (summary.timingEnabled ? 1 : 0) + " windowSeconds=" + WindowSeconds);
            return summary;
        }

        private void Update()
        {
            var session = authority != null && !authority.Failed ? authority.ProductSession : null;
            bool playing = session != null && session.Phase == GameMatchPhase.Playing && session.PauseState == SessionPauseState.Running;
            if (!playing)
            {
                // Menus, loading, pause and results are not gameplay frames; a partial window is dropped.
                window.Reset();
                return;
            }

            double main = -1, render = -1, gpu = -1;
            if (timingEnabled)
            {
                // The latest native timings lag a few frames; over a 10 s window that only shifts which frames are counted.
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, latest) == 1)
                {
                    main = latest[0].cpuMainThreadFrameTime;
                    render = latest[0].cpuRenderThreadFrameTime;
                    gpu = latest[0].gpuFrameTime;
                }
            }
            window.Add(Time.unscaledDeltaTime * 1000.0, main, render, gpu);
            if (window.Seconds < WindowSeconds) return;
            // The session id matches the SHOOTER_SHELL screen=Playing line, which names the map and team size.
            Debug.Log("SHOOTER_FRAME_SUMMARY tick=" + authority!.CompletedTicks + " session=" + session!.AuthoritySessionId + " " + window.Summary());
            window.Reset();
        }
    }
}
