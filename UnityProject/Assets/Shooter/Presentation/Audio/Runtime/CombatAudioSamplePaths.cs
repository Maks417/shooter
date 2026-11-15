#nullable enable
using System;
using System.IO;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Resolves Content/Audio/combat for editor, packaged player, and pure tests (T089).
    /// </summary>
    public static class CombatAudioSamplePaths
    {
        public const string RelativeCombatFolder = "Content/Audio/combat";
        public const string ManifestFileName = "manifest.json";

        public static string? ResolveCombatSampleRoot(string? explicitRoot = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitRoot) && Directory.Exists(explicitRoot))
                return Path.GetFullPath(explicitRoot);

            string? env = Environment.GetEnvironmentVariable("SHOOTER_COMBAT_AUDIO_ROOT");
            if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
                return Path.GetFullPath(env);

            foreach (var start in CandidateStarts())
            {
                string? hit = WalkForCombat(start);
                if (hit != null) return hit;
            }
            return null;
        }

        public static string? ResolveManifestPath(string? sampleRoot = null)
        {
            string? root = ResolveCombatSampleRoot(sampleRoot);
            if (root == null) return null;
            string path = Path.Combine(root, ManifestFileName);
            return File.Exists(path) ? path : null;
        }

        private static string? WalkForCombat(string start)
        {
            try
            {
                var dir = new DirectoryInfo(Path.GetFullPath(start));
                for (int i = 0; i < 10 && dir != null; i++)
                {
                    string candidate = Path.Combine(dir.FullName, "Content", "Audio", "combat");
                    if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, ManifestFileName)))
                        return candidate;
                    // StreamingAssets fallback (player copy).
                    string streaming = Path.Combine(dir.FullName, "StreamingAssets", "Audio", "combat");
                    if (Directory.Exists(streaming) && File.Exists(Path.Combine(streaming, ManifestFileName)))
                        return streaming;
                    dir = dir.Parent;
                }
            }
            catch
            {
                // Ignore IO probe failures; caller falls back to generated bank.
            }
            return null;
        }

        private static string[] CandidateStarts()
        {
            string baseDir = AppContext.BaseDirectory ?? ".";
            string cwd = Directory.GetCurrentDirectory();
            return new[] { cwd, baseDir, Path.Combine(baseDir, ".."), Path.Combine(baseDir, "..", ".."), Path.Combine(baseDir, "..", "..", "..") };
        }
    }
}
