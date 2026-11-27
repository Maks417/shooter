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
                    var args = ClientArgs.Read();
                    // T195: the first matching mode installs the view (same precedence as the former if/else chain).
                    for (int i = 0; i < Modes.Length; i++)
                    {
                        if (!Modes[i].Matches(authority, args)) continue;
                        Modes[i].Install(authority, args);
                        if (Modes[i].AddsBenchmarkRecorder && args.Benchmark.Length > 0)
                            new GameObject("Benchmark recorder").AddComponent<FoundationBenchmark>().Initialize(authority);
                        return;
                    }
                });
            }
        }

        /// <summary>The command-line selections the client modes choose between.</summary>
        private sealed class ClientArgs
        {
            public string Gallery = "", Soldier = "", Benchmark = "";

            public static ClientArgs Read() => new ClientArgs
            {
                Gallery = FoundationEntry.CommandLine("-shooterContentGallery", ""),
                Soldier = FoundationEntry.CommandLine("-shooterSoldierGallery", ""),
                Benchmark = FoundationEntry.CommandLine("-shooterBenchmark", "")
            };
        }

        /// <summary>T195: one client view mode: when it applies, how it installs, and whether a B01 benchmark recorder rides along.</summary>
        private sealed class ClientMode
        {
            public string Name = "";
            public Func<FoundationEntry, ClientArgs, bool> Matches = (_, __) => false;
            public Action<FoundationEntry, ClientArgs> Install = (_, __) => { };
            public bool AddsBenchmarkRecorder;
        }

        /// <summary>Ordered client modes; a new view is one entry here instead of another branch in <see cref="Compose"/>.</summary>
        private static readonly ClientMode[] Modes =
        {
            new ClientMode { Name = "product-tdm-benchmark", Matches = (a, c) => ProductTdmWorkloads.TryGet(c.Benchmark, out _), Install = InstallTdmBenchmark },
            new ClientMode
            {
                Name = "product-shell",
                Matches = (a, c) => a.ProductMode && c.Soldier.Length == 0 && c.Gallery.Length == 0 && c.Benchmark.Length == 0,
                Install = InstallProductShell
            },
            new ClientMode { Name = "soldier-gallery", Matches = (a, c) => c.Soldier.Length != 0, Install = InstallSoldierGallery, AddsBenchmarkRecorder = true },
            new ClientMode { Name = "foundation-or-material", Matches = (a, c) => c.Gallery.Length == 0, Install = InstallFoundationView, AddsBenchmarkRecorder = true },
            new ClientMode { Name = "content-gallery", Matches = (a, c) => true, Install = InstallContentGallery, AddsBenchmarkRecorder = true }
        };

        /// <summary>Mode names in precedence order (diagnostics and tests).</summary>
        public static IReadOnlyList<string> ModeNames => Array.ConvertAll(Modes, m => m.Name);

        private static void InstallTdmBenchmark(FoundationEntry authority, ClientArgs args)
        {
            ProductTdmWorkloads.TryGet(args.Benchmark, out var tdmWorkload);
            if (!authority.ProductMode)
                throw new System.ArgumentException("Product TDM workloads require product composition, not the diagnostic floor/box path.");
            if (authority.ContentRecoveryRequired)
                throw new System.InvalidOperationException("Product TDM workload cannot start: " + authority.ContentRecoveryDetail);
            var backend = new EntryShellBackend(authority);
            var host = ProductTdmHost.Create(backend, authority.ProductSession, tdmWorkload);
            new GameObject("Product TDM benchmark recorder").AddComponent<ProductTdmBenchmark>()
                .Initialize(authority, host, tdmWorkload);
        }

        private static void InstallProductShell(FoundationEntry authority, ClientArgs args)
        {
            // Paint a briefing insertion screen before Main so cold start is not a blank hitch.
            var boot = BootLoadingHost.Create();
            if (authority.ContentRecoveryRequired)
            {
                UnityEngine.Object.Destroy(boot.gameObject);
                ContentRecoveryHost? recovery = null;
                recovery = ContentRecoveryHost.Create(
                    ReleasePackagePolicy.CorruptContentRecoveryMessage,
                    authority.ContentRecoveryDetail,
                    onRetry: () =>
                    {
                        if (!authority.TryRecoverProductContent(out _)) return;
                        if (recovery != null) UnityEngine.Object.Destroy(recovery.gameObject);
                        GameShellMenu.Create(new EntryShellBackend(authority), PreferredMap(), PreferredTeamSize());
                    });
                return;
            }
            string preferredMap = PreferredMap();
            byte preferredSize = PreferredTeamSize();
            // Frame-time summaries in the player log while a match plays (diagnostic; the benchmark mode records its own).
            FrameTimingSummary.Attach(authority);
            // T059: shell owns start/stop; CLI/PlayerPrefs are setup defaults only.
            boot.ContinueAfterPaint(() =>
                GameShellMenu.Create(new EntryShellBackend(authority), preferredMap, preferredSize));
        }

        private static string PreferredMap() => FoundationEntry.CommandLine("-shooterMap",
            PlayerPrefs.GetString("shooter.map", WellKnownMaps.TrainingCompound.Value));

        private static byte PreferredTeamSize()
        {
            byte size = byte.Parse(
                FoundationEntry.CommandLine("-shooterTeamSize", PlayerPrefs.GetInt("shooter.teamSize", 5).ToString()),
                System.Globalization.CultureInfo.InvariantCulture);
            return Shooter.Game.Contracts.GameContractVersion.IsAllowedTeamSize(size) ? size : (byte)5;
        }

        private static void InstallSoldierGallery(FoundationEntry authority, ClientArgs args)
        {
            if (args.Gallery.Length != 0 || args.Benchmark.Length != 0)
                throw new System.ArgumentException("Soldier diagnostics require their own view, separate from gallery and benchmark.");
            var hash = new Shooter.Contracts.ContentId(FoundationEntry.CommandLine("-shooterContentToolchainHash", ""));
            string report = FoundationEntry.CommandLine("-shooterRigReport", "");
            var view = SoldierGallery.Create(args.Soldier, hash, report.Length == 0 ? 1 : 8);
            view.Scenario = FoundationEntry.CommandLine("-shooterRigScenario", "walk");
            if (report.Length != 0) new GameObject("Eight-rig timing diagnostic").AddComponent<RigTimingDiagnostic>().Initialize(view, report,
                FoundationEntry.CommandLine("-shooterSourceCommit", ""), hash.ToString());
        }

        private static void InstallFoundationView(FoundationEntry authority, ClientArgs args)
        {
            if (args.Benchmark == MaterialBenchmarkRecipe.Workload)
                MaterialBenchmarkView.Create(FoundationEntry.CommandLine("-shooterMaterialCache", ""),
                    new Shooter.Contracts.ContentId(FoundationEntry.CommandLine("-shooterContentToolchainHash", "")), Shader.Find("Shooter/ProceduralSurface"));
            else FoundationView.Create();
        }

        private static void InstallContentGallery(FoundationEntry authority, ClientArgs args)
        {
            if (args.Benchmark.Length != 0)
                throw new System.ArgumentException("M1-content-gallery-v1 is a diagnostic, not a B01 benchmark substitution.");
            var hash = new Shooter.Contracts.ContentId(FoundationEntry.CommandLine("-shooterContentToolchainHash", ""));
            GeneratedGallery.Create(args.Gallery, hash, Shader.Find("Shooter/ProceduralSurface"));
        }

        private sealed class EntryShellBackend : IProductShellBackend, IProductMenuVisualSource
        {
            private readonly FoundationEntry entry;

            public EntryShellBackend(FoundationEntry entry) => this.entry = entry ?? throw new ArgumentNullException(nameof(entry));

            public IGameSessionControl Session => entry.ProductSession;
            public IArmoryCatalogView Armory => ArmoryCatalogView.Instance;
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
