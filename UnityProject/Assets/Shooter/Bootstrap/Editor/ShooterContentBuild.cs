using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Shooter.Content;
using Shooter.Contracts;
using Shooter.Generation;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Only explicit production adapters are composed here. No test generator enters an editor/player composition.
public static class ShooterContentBuild
{
    [Serializable] public sealed class CompileProof
    {
        public string result = "Verified", recipeSet, profile, contentProfile = "gameplay", toolchainHash, contentId, manifestSha256;
        public string cacheRoot, generatorId = "collision.box", generatorVersion = "1.0.0", seed = "460072030", sourceHash, sourceCommit;
        public string scope = "P02 CPU collision diagnostic only; no render mesh, slice compilation or reference hardware gate";
        public bool reused, force, dryRun;
        public double elapsedMilliseconds;
        public int generatedBytes;
        public ShooterBuild.FileProof[] sources;
    }
    public static void Compile()
    {
        ShooterBuild.CheckToolchain();
        string recipeSet = ShooterBuild.Argument("-shooterRecipeSet", "box-collision-v1");
        string profile = ShooterBuild.Argument("-shooterProfile", "Low");
        if (recipeSet != "box-collision-v1") throw new NotSupportedException("P02 supports box-collision-v1 only; render/slice recipes await P03-P05.");
        if (profile != "Low" && profile != "Balanced" && profile != "High") throw new ArgumentException("Unknown profile.");
        string root = Path.GetFullPath(ShooterBuild.Argument("-shooterCacheRoot", Path.Combine(ShooterBuild.RepositoryRoot, "Artifacts/content-cache")));
        string allowed = Path.Combine(ShooterBuild.RepositoryRoot, "Artifacts") + Path.DirectorySeparatorChar;
        if (!root.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Compiler cache must stay under repository Artifacts.");
        bool force = Flag("-shooterForce"), dry = Flag("-shooterDryRun");
        var toolchain = new ContentId(ShooterBuild.HashFile(ShooterBuild.LockPath));
        string sourcePath = Path.Combine(Application.dataPath, "Shooter/Generation/Recipes/box-collision.json");
        var node = new CompilationNode(File.ReadAllBytes(sourcePath), "windows64", "gameplay", toolchain, new CpuBoxCollisionGenerator());
        var files = new CacheFiles(root, toolchain);
        var clock = Stopwatch.StartNew();
        ContentManifest manifest = null; bool reused = false;
        if (!dry)
        {
            var result = new ContentCompiler(files).CompileAsync(new[] { node }, force, CancellationToken.None).GetAwaiter().GetResult();
            if (result.Error != ErrorCode.None) throw new InvalidOperationException("Content compilation " + result.Error + ": " + result.Diagnostic);
            manifest = result.Entries.Single().Manifest; reused = result.Entries.Single().Reused;
            // Exercise the real byte lease adapter from disk; no Unity object installation is claimed.
            using var store = new FileContentStore(files);
            var loaded = store.AcquireAsync(manifest.ContentId, ContentKind.Collision, CancellationToken.None).GetAwaiter().GetResult();
            if (loaded.Error != ErrorCode.None || loaded.Lease == null) throw new InvalidOperationException("Content lease verification failed: " + loaded.Diagnostic);
            using (loaded.Lease)
            {
                byte[] payload = new byte[loaded.Lease.Length]; loaded.Lease.CopyTo(payload);
                var collision = GeometryCodec.DecodeCollision(ArtifactCodec.Decode(payload, ContentKind.Collision, manifest.GeneratorHash, toolchain));
                if (collision.Boxes.Count != 1) throw new InvalidOperationException("CPU diagnostic output must contain one numeric collision box.");
            }
        }
        clock.Stop();
        var sources = Directory.GetFiles(Path.Combine(Application.dataPath, "Shooter/Generation"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(Application.dataPath, "Shooter/Content"), "*.cs", SearchOption.AllDirectories))
            .Concat(new[] { sourcePath, Path.Combine(Application.dataPath, "Shooter/Bootstrap/Editor/ShooterContentBuild.cs") })
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => new ShooterBuild.FileProof { path = "UnityProject/" + p.Substring(Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Length + 1).Replace('\\', '/'), sha256 = ShooterBuild.HashFile(p) }).ToArray();
        ShooterBuild.WriteJson(ShooterBuild.Argument("-shooterReport"), new CompileProof
        {
            recipeSet = recipeSet, profile = profile, toolchainHash = toolchain.ToString(), cacheRoot = root,
            contentId = node.Id.ToString(), sourceHash = node.Source.SourceHash.ToString(), manifestSha256 = manifest == null ? "" : ContentIdentity.Hash(manifest.Encode()).ToString(),
            reused = reused, force = force, dryRun = dry, elapsedMilliseconds = clock.Elapsed.TotalMilliseconds,
            generatedBytes = manifest == null ? 0 : manifest.Artifacts.Sum(a => a.Bytes), sources = sources,
            sourceCommit = SourceCommit()
        });
        Debug.Log("SHOOTER_CONTENT_VERIFIED " + node.Id + (dry ? " dry-run" : reused ? " warm" : " cold"));
    }
    private static bool Flag(string name)
    {
        string value = ShooterBuild.Argument(name, "false");
        if (!bool.TryParse(value, out bool result)) throw new ArgumentException("Expected true/false for " + name);
        return result;
    }
    private static string SourceCommit()
    {
        var start = new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = ShooterBuild.RepositoryRoot, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        using var process = Process.Start(start); string result = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Cannot determine compiler source commit.");
        return result;
    }
}
