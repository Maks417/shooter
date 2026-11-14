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
    /// Autostarts a rendered product TDM match for measurement without shell/menu ownership (T063).
    /// Presentation/Menus remain untouched; this host binds ProductSessionView only.
    /// </summary>
    public sealed class ProductTdmHost : MonoBehaviour
    {
        private IProductShellBackend? backend;
        private ProductTdmWorkloads.Definition workload;
        private ProductSessionView? view;
        private CancellationTokenSource? loadCancel;
        private bool startRequested;
        private bool startFailed;
        private string failure = "";

        public bool MatchPlaying { get; private set; }
        public bool StartFailed => startFailed;
        public string Failure => failure;
        public ProductTdmWorkloads.Definition Workload => workload;

        public static ProductTdmHost Create(IProductShellBackend backend, ProductTdmWorkloads.Definition workload)
        {
            if (backend == null) throw new ArgumentNullException(nameof(backend));
            var host = new GameObject("Product TDM measurement host").AddComponent<ProductTdmHost>();
            host.backend = backend;
            host.workload = workload;
            host.view = ProductSessionView.Create(backend.Session, shellOwned: true);
            host.view.SetGameplayFocus(true);
            backend.InstallHumanSampler(_ => null);
            return host;
        }

        private async void Update()
        {
            if (backend == null || startRequested || startFailed) return;
            startRequested = true;
            try
            {
                await StartWorkloadAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                startFailed = true;
                failure = ex.Message;
                Debug.LogError("SHOOTER_TDM_BENCHMARK_START_FAILED: " + ex);
            }
        }

        private async System.Threading.Tasks.Task StartWorkloadAsync()
        {
            var session = backend!.Session ?? throw new InvalidOperationException("Product session missing.");
            var catalog = backend.Catalog ?? throw new InvalidOperationException("Product catalog missing.");
            if (!catalog.TryGet(workload.MapId, out var entry))
                throw new InvalidOperationException("Unknown map for product TDM workload: " + workload.MapId.Value);
            if (entry.RecipeSeed != workload.Seed)
                throw new InvalidOperationException("Workload seed does not match catalog map seed.");

            var request = new SessionRequest(workload.MapId, WellKnownModes.TeamDeathmatch, workload.TeamSize, workload.Seed);
            session.Measurement.WorkloadId = workload.Id;
            loadCancel = new CancellationTokenSource();
            var load = await session.BeginLoadAsync(request, loadCancel.Token).ConfigureAwait(true);
            if (!load.Succeeded)
                throw new InvalidOperationException(string.IsNullOrEmpty(load.Diagnostic) ? load.UserFacing : load.Diagnostic);

            backend.WireMapActors();
            if (!backend.TryGetVerifiedMap(out var boxes, out var metadata, out var mapId))
                throw new InvalidOperationException("Verified map package solids missing for product TDM capture.");
            view!.BindVerifiedMap(boxes, metadata, mapId);

            var ready = session.SignalReady();
            if (!ready.Succeeded) throw new InvalidOperationException(ready.Diagnostic);
            var started = session.Start();
            if (!started.Succeeded) throw new InvalidOperationException(started.Diagnostic);

            MatchPlaying = true;
            Debug.Log("SHOOTER_TDM_BENCHMARK_PLAYING workload=" + workload.Id + " map=" + workload.MapId.Value +
                      " teamSize=" + workload.TeamSize + " seed=" + workload.Seed);
        }

        private void OnDestroy()
        {
            loadCancel?.Cancel();
            loadCancel?.Dispose();
            if (view != null) Destroy(view.gameObject);
        }
    }
}
