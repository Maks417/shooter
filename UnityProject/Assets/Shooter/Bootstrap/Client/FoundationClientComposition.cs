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
                    if (gallery.Length == 0) FoundationView.Create();
                    else
                    {
                        if (FoundationEntry.CommandLine("-shooterBenchmark", "").Length != 0)
                            throw new System.ArgumentException("M1-content-gallery-v1 is a diagnostic, not a B01 benchmark substitution.");
                        var hash = new Shooter.Contracts.ContentId(FoundationEntry.CommandLine("-shooterContentToolchainHash", ""));
                        var view = GeneratedGallery.Create(gallery, hash, Shader.Find("Shooter/ProceduralSurface"));
                        view.CaptureDiagnosticFrame(FoundationEntry.CommandLine("-shooterGalleryCapture", ""));
                    }
                    if (FoundationEntry.CommandLine("-shooterBenchmark", "").Length > 0)
                        new GameObject("Benchmark recorder").AddComponent<FoundationBenchmark>().Initialize(authority);
                });
            }
        }
    }
}
