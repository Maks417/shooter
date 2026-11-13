using Shooter.Presentation;
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
    }
}
