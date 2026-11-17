using System;
using System.Collections.Generic;
using Shooter.Bootstrap.Release;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Generation.Environment;
using Shooter.Presentation;
using Shooter.Presentation.Session;
using Shooter.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Bootstrap
{
    public static class FoundationClientComposition
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            UnityEngine.Rendering.DebugManager.instance.enableRuntimeUI = false;
            SceneManager.sceneLoaded += Compose;
        }
        private static void Compose(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var entry = root.GetComponent<FoundationEntry>();
                if (entry != null) entry.InstallView(authority =>
                {
                    string profile = FoundationEntry.CommandLine("-shooterProfile", "Low");
                    int tier = System.Array.IndexOf(QualitySettings.names, profile);
                    if (tier < 0) throw new System.ArgumentException("Unknown quality profile: " + profile);
                    QualitySettings.SetQualityLevel(tier, true);
                    string gallery = FoundationEntry.CommandLine("-shooterContentGallery", "");
                    string soldier = FoundationEntry.CommandLine("-shooterSoldierGallery", "");
                    string benchmark = FoundationEntry.CommandLine("-shooterBenchmark", "");
                    if (ProductTdmWorkloads.TryGet(benchmark, out var tdmWorkload))
                    {
                        if (!authority.ProductMode)
                            throw new System.ArgumentException("Product TDM workloads require product composition, not the diagnostic floor/box path.");
                        if (authority.ContentRecoveryRequired)
                            throw new System.InvalidOperationException("Product TDM workload cannot start: " + authority.ContentRecoveryDetail);
                        var backend = new EntryShellBackend(authority);
                        var host = ProductTdmHost.Create(backend, tdmWorkload);
                        new GameObject("Product TDM benchmark recorder").AddComponent<ProductTdmBenchmark>()
                            .Initialize(authority, host, tdmWorkload);
                        return;
                    }
                    if (authority.ProductMode && soldier.Length == 0 && gallery.Length == 0
                        && benchmark.Length == 0)
                    {
                        // Paint a briefing insertion screen before Main so cold start is not a blank hitch.
                        var boot = BootLoadingHost.Create();
                        if (authority.ContentRecoveryRequired)
                        {
                            UnityEngine.Object.Destroy(boot.gameObject);
                            ContentRecoveryHost.Create(
                                ReleasePackagePolicy.CorruptContentRecoveryMessage,
                                authority.ContentRecoveryDetail,
                                onRetry: () =>
                                {
                                    if (!authority.TryRecoverProductContent(out _)) return;
                                    UnityEngine.Object.Destroy(GameObject.Find("Content recovery"));
                                    string map = FoundationEntry.CommandLine("-shooterMap",
                                        PlayerPrefs.GetString("shooter.map", WellKnownMaps.TrainingCompound.Value));
                                    byte size = byte.Parse(
                                        FoundationEntry.CommandLine("-shooterTeamSize",
                                            PlayerPrefs.GetInt("shooter.teamSize", 5).ToString()),
                                        System.Globalization.CultureInfo.InvariantCulture);
                                    if (!Shooter.Game.Contracts.GameContractVersion.IsAllowedTeamSize(size)) size = 5;
                                    GameShellMenu.Create(new EntryShellBackend(authority), map, size);
                                });
                            return;
                        }
                        string preferredMap = FoundationEntry.CommandLine("-shooterMap",
                            PlayerPrefs.GetString("shooter.map", WellKnownMaps.TrainingCompound.Value));
                        byte preferredSize = byte.Parse(
                            FoundationEntry.CommandLine("-shooterTeamSize",
                                PlayerPrefs.GetInt("shooter.teamSize", 5).ToString()),
                            System.Globalization.CultureInfo.InvariantCulture);
                        if (!Shooter.Game.Contracts.GameContractVersion.IsAllowedTeamSize(preferredSize)) preferredSize = 5;
                        // T059: shell owns start/stop; CLI/PlayerPrefs are setup defaults only.
                        boot.ContinueAfterPaint(() =>
                            GameShellMenu.Create(new EntryShellBackend(authority), preferredMap, preferredSize));
                        return;
                    }
                    if (soldier.Length != 0)
                    {
                        if (gallery.Length != 0 || FoundationEntry.CommandLine("-shooterBenchmark", "").Length != 0)
                            throw new System.ArgumentException("Soldier diagnostics require their own view, separate from gallery and benchmark.");
                        var hash = new Shooter.Contracts.ContentId(FoundationEntry.CommandLine("-shooterContentToolchainHash", ""));
                        string report = FoundationEntry.CommandLine("-shooterRigReport", "");
                        var view = SoldierGallery.Create(soldier,hash,report.Length == 0 ? 1 : 8);
                        view.Scenario = FoundationEntry.CommandLine("-shooterRigScenario","walk");
                        if (report.Length != 0) new GameObject("Eight-rig timing diagnostic").AddComponent<RigTimingDiagnostic>().Initialize(view,report,
                            FoundationEntry.CommandLine("-shooterSourceCommit",""),hash.ToString());
                    }
                    else if (gallery.Length == 0)
                    {
                        if (FoundationEntry.CommandLine("-shooterBenchmark", "") == MaterialBenchmarkRecipe.Workload)
                            MaterialBenchmarkView.Create(FoundationEntry.CommandLine("-shooterMaterialCache", ""),
                                new Shooter.Contracts.ContentId(FoundationEntry.CommandLine("-shooterContentToolchainHash", "")), Shader.Find("Shooter/ProceduralSurface"));
                        else FoundationView.Create();
                    }
                    else
                    {
                        if (FoundationEntry.CommandLine("-shooterBenchmark", "").Length != 0)
                            throw new System.ArgumentException("M1-content-gallery-v1 is a diagnostic, not a B01 benchmark substitution.");
                        var hash = new Shooter.Contracts.ContentId(FoundationEntry.CommandLine("-shooterContentToolchainHash", ""));
                        GeneratedGallery.Create(gallery, hash, Shader.Find("Shooter/ProceduralSurface"));
                    }
                    if (FoundationEntry.CommandLine("-shooterBenchmark", "").Length > 0)
                        new GameObject("Benchmark recorder").AddComponent<FoundationBenchmark>().Initialize(authority);
                });
            }
        }

        private sealed class EntryShellBackend : IProductShellBackend, IProductMenuVisualSource
        {
            private readonly FoundationEntry entry;

            public EntryShellBackend(FoundationEntry entry) => this.entry = entry ?? throw new ArgumentNullException(nameof(entry));

            public OfflineGameSession Session => entry.ProductSession;
            public IMapCatalog Catalog => entry.ProductCatalog;
            public GameOperationResult Pause() => entry.PauseProduct();
            public GameOperationResult Resume() => entry.ResumeProduct();
            public void WireMapActors() => entry.WireProductMapActors();
            public void InstallHumanSampler(Func<uint, LocalPlayerCommand?> sampler) => entry.InstallProductHumanSampler(sampler);

            public bool TryGetVerifiedMap(out IReadOnlyList<CollisionBox> boxes, out byte[]? metadataBytes, out string mapId)
            {
                CompoundMapRuntime? map = entry.ProductMap;
                if (map?.PackagedBoxes == null || map.PackagedBoxes.Count == 0)
                {
                    boxes = Array.Empty<CollisionBox>();
                    metadataBytes = null;
                    mapId = "";
                    return false;
                }
                boxes = map.PackagedBoxes;
                metadataBytes = map.LoadedPackage?.MetadataBytes;
                mapId = map.LoadedPackage?.MapId ?? "unknown";
                return true;
            }

            public bool TryGetVerifiedVisuals(out PackagedVisualPayloads visuals, out string diagnostic) =>
                entry.TryGetVerifiedVisuals(out visuals, out diagnostic);
            public bool TryLoadMenuVisuals(MapId map, out PackagedVisualPayloads visuals, out string diagnostic) =>
                entry.TryLoadMenuVisuals(map, out visuals, out diagnostic);
        }
    }
}
