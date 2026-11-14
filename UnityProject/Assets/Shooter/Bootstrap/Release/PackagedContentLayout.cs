#nullable enable
using System;
using System.IO;

namespace Shooter.Bootstrap.Release
{
    /// <summary>
    /// Executable-relative packaged content discovery (T067 / FIX07).
    /// Player install root is the directory beside the executable (Application.dataPath parent).
    /// Editor install root is the repository root (Application.dataPath/../..) so local Content/ and Artifacts/ layouts resolve.
    /// </summary>
    public static class PackagedContentLayout
    {
        public const string MapPackagesFolder = "Content";
        public const string MapPackagesLeaf = "map-packages";
        public const string ToolchainLockFileName = "toolchain-lock.json";

        public static string InstallRootFromDataPath(string dataPath, bool editorPlayer)
        {
            if (string.IsNullOrWhiteSpace(dataPath)) throw new ArgumentException("data path required", nameof(dataPath));
            string data = Path.GetFullPath(dataPath);
            if (editorPlayer)
                return Path.GetFullPath(Path.Combine(data, "..", ".."));
            return Path.GetFullPath(Path.Combine(data, ".."));
        }

        public static string DefaultMapCache(string installRoot) =>
            Path.GetFullPath(Path.Combine(installRoot, MapPackagesFolder, MapPackagesLeaf));

        public static string DefaultToolchainLock(string installRoot) =>
            Path.GetFullPath(Path.Combine(installRoot, ToolchainLockFileName));

        public static string EditorArtifactsMapCache(string repositoryRoot) =>
            Path.GetFullPath(Path.Combine(repositoryRoot, "Artifacts", MapPackagesLeaf));

        /// <summary>
        /// Prefer packaged Content/map-packages; in editor, fall back to Artifacts/map-packages when the packaged folder is absent.
        /// </summary>
        public static string ResolveMapCache(string installRoot, bool allowEditorArtifactsFallback)
        {
            string packaged = DefaultMapCache(installRoot);
            if (Directory.Exists(packaged)) return packaged;
            if (allowEditorArtifactsFallback)
            {
                string artifacts = EditorArtifactsMapCache(installRoot);
                if (Directory.Exists(artifacts)) return artifacts;
            }
            return packaged;
        }

        public static string ResolveToolchainLock(string installRoot, bool allowEditorRepoFallback)
        {
            string packaged = DefaultToolchainLock(installRoot);
            if (File.Exists(packaged)) return packaged;
            if (allowEditorRepoFallback && File.Exists(packaged)) return packaged;
            return packaged;
        }
    }
}
