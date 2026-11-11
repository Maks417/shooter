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
                    FoundationView.Create();
                    if (FoundationEntry.CommandLine("-shooterBenchmark", "").Length > 0)
                        new GameObject("Benchmark recorder").AddComponent<FoundationBenchmark>().Initialize(authority);
                });
            }
        }
    }
}
