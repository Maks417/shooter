#nullable enable
using System;
using System.Threading;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Presentation;
using Shooter.Presentation.Session;
using UnityEngine;

namespace Shooter.Bootstrap
{
    /// <summary>
    /// Autostarts rendered product TDM matches for measurement without shell/menu ownership (T063/T070).
    /// Implements finish → results → replay so soak can accumulate capture ticks across normal
    /// 150-kill / 54000-tick matches without extending production rules.
    /// </summary>
    public sealed class ProductTdmHost : MonoBehaviour
    {
        private IProductShellBackend? backend;
        // Measurement hooks (ActiveTicks, NotifyMatchFinished) are concrete-session only; Bootstrap may hold the concrete type.
        private OfflineGameSession? session;
        private ProductTdmWorkloads.Definition workload;
        private ProductSessionView? view;
        private CancellationTokenSource? loadCancel;
        private readonly ProductTdmCaptureCoordinator coordinator = new ProductTdmCaptureCoordinator();
        private bool bootstrapped;
        private bool startFailed;
        private bool transitionInFlight;
        private string failure = "";
        private int resultsFrames;
        private long authorityClockTicks;

        public bool MatchPlaying => coordinator.Current == ProductTdmCaptureCoordinator.Phase.Playing;
        public bool StartFailed => startFailed;
        public string Failure => failure;
        public ProductTdmWorkloads.Definition Workload => workload;
        public ProductTdmCaptureCoordinator Coordinator => coordinator;
        public ProductTdmCaptureCoordinator.Phase CapturePhase => coordinator.Current;

        public static ProductTdmHost Create(IProductShellBackend backend, OfflineGameSession session, ProductTdmWorkloads.Definition workload)
        {
            if (backend == null) throw new ArgumentNullException(nameof(backend));
            if (session == null) throw new ArgumentNullException(nameof(session));
            var host = new GameObject("Product TDM measurement host").AddComponent<ProductTdmHost>();
            host.backend = backend;
            host.session = session;
            host.workload = workload;
            host.view = ProductSessionView.Create(backend.Session, shellOwned: true);
            host.view.SetGameplayFocus(true);
            backend.InstallHumanSampler(_ => null);
            return host;
        }

        /// <summary>Updated by ProductTdmBenchmark from FoundationEntry.CompletedTicks each frame.</summary>
        public void ReportAuthorityClockTicks(long ticks) => authorityClockTicks = ticks;

        private async void Update()
        {
            if (backend == null || startFailed || transitionInFlight) return;
            try
            {
                if (!bootstrapped)
                {
                    bootstrapped = true;
                    await BeginMatchAsync(replay: false).ConfigureAwait(true);
                    return;
                }

                if (session == null) return;

                if (coordinator.Current == ProductTdmCaptureCoordinator.Phase.Playing &&
                    session.Phase == GameMatchPhase.Finished)
                {
                    var results = session.TryAcquireResults();
                    long active = session.Measurement.ActiveTicks;
                    session.NotifyMatchFinishedForMeasurement();
                    coordinator.MarkFinished(results, active);
                    coordinator.EnterResults();
                    resultsFrames = 0;
                    view?.SetGameplayFocus(false);
                    Debug.Log("SHOOTER_TDM_BENCHMARK_FINISHED match=" + coordinator.MatchesCompleted +
                              " reason=" + (results?.Reason.ToString() ?? "none") +
                              " cumulativeActiveTicks=" + coordinator.CumulativeActiveTicks);
                    return;
                }

                if (coordinator.Current == ProductTdmCaptureCoordinator.Phase.Results)
                {
                    resultsFrames++;
                    // One results frame keeps the transition observable without stretching match rules.
                    if (resultsFrames < 1) return;
                    if (!coordinator.NeedsReplay(workload.CaptureTicks, authorityClockTicks, workload.Soak))
                        return;
                    await BeginMatchAsync(replay: true).ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                startFailed = true;
                failure = ex.Message;
                Debug.LogError("SHOOTER_TDM_BENCHMARK_START_FAILED: " + ex);
            }
        }

        private async System.Threading.Tasks.Task BeginMatchAsync(bool replay)
        {
            transitionInFlight = true;
            try
            {
                var session = this.session ?? throw new InvalidOperationException("Product session missing.");
                var catalog = backend.Catalog ?? throw new InvalidOperationException("Product catalog missing.");
                var request = new SessionRequest(workload.MapId, WellKnownModes.TeamDeathmatch, workload.TeamSize, workload.Seed);

                if (replay)
                {
                    if (!coordinator.TryPrepareReplay(out var replayRequest, out string replayError))
                        throw new InvalidOperationException(replayError);
                    request = replayRequest ?? request;
                    session.Stop();
                    view?.ClearPresentation();
                }
                else if (!coordinator.TryBeginLoad(request, out string beginError))
                {
                    throw new InvalidOperationException(beginError);
                }

                if (!catalog.TryGet(workload.MapId, out var entry))
                    throw new InvalidOperationException("Unknown map for product TDM workload: " + workload.MapId.Value);
                if (entry.RecipeSeed != workload.Seed)
                    throw new InvalidOperationException("Workload seed does not match catalog map seed.");

                loadCancel?.Cancel();
                loadCancel?.Dispose();
                loadCancel = new CancellationTokenSource();
                var load = await session.BeginLoadAsync(request, loadCancel.Token).ConfigureAwait(true);
                if (!load.Succeeded)
                    throw new InvalidOperationException(string.IsNullOrEmpty(load.Diagnostic) ? load.UserFacing : load.Diagnostic);

                // WorkloadId must be applied AFTER BeginLoadAsync (ResetForNewMatch clears per-match fields).
                session.Measurement.WorkloadId = workload.Id;
                session.Measurement.MatchIndex = coordinator.MatchIndex;
                session.Measurement.MatchesCompleted = coordinator.MatchesCompleted;
                session.Measurement.CumulativeActiveTicks = coordinator.CumulativeActiveTicks;
                session.Measurement.SessionEpoch = session.SessionEpoch;

                backend.WireMapActors();
                if (!backend.TryGetVerifiedMap(out var boxes, out var metadata, out var mapId))
                    throw new InvalidOperationException("Verified map package solids missing for product TDM capture.");
                if (!backend.TryGetVerifiedVisuals(out var visuals, out string visualDiagnostic))
                    throw new InvalidOperationException("Verified visual package missing for product TDM capture: " + visualDiagnostic);
                view!.BindVerifiedMap(boxes, metadata, mapId, visuals);
                // Visual package is a second verified content binding (gameplay already recorded by session load).
                session.RecordVerifiedContentBinding();
                view.SetGameplayFocus(true);

                var ready = session.SignalReady();
                if (!ready.Succeeded) throw new InvalidOperationException(ready.Diagnostic);
                var started = session.Start();
                if (!started.Succeeded) throw new InvalidOperationException(started.Diagnostic);

                coordinator.CompleteLoadSuccess();
                Debug.Log("SHOOTER_TDM_BENCHMARK_PLAYING workload=" + workload.Id +
                          " match=" + coordinator.MatchIndex +
                          " epoch=" + session.SessionEpoch +
                          " map=" + workload.MapId.Value +
                          " teamSize=" + workload.TeamSize +
                          " seed=" + workload.Seed +
                          " replay=" + replay);
            }
            catch
            {
                coordinator.CompleteLoadFailure();
                throw;
            }
            finally
            {
                transitionInFlight = false;
            }
        }

        private void OnDestroy()
        {
            loadCancel?.Cancel();
            loadCancel?.Dispose();
            if (view != null) Destroy(view.gameObject);
        }
    }
}
