#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Shooter.Build;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// T067/C01: publishes both product maps with the pinned Unity AI Navigation bake registered as
/// <see cref="NavBakePublication.NativeProvider"/>. Writes the same ADR-035/038 package/catalog layout as pure
/// MapContent plus the nav-provenance sidecar. Never labels a software grid native: every package must report
/// <see cref="NavBakePublication.NativeProvenance"/> or the batch fails.
/// </summary>
public static class ShooterNativeContentBuild
{
    [Serializable] public sealed class PackageProof
    {
        public string mapId = "", visualProfile = "", seed = "", packageRootSha256 = "", navigationSha256 = "", navSettingsSha256 = "";
        public string navBakeProvenance = "";
        public int polygonCount, vertexCount;
        public bool pathSpawnToObjective;
    }

    [Serializable] public sealed class BakeTiming
    {
        public string key = "";
        public int polygonCount, vertexCount;
        public double milliseconds;
        public bool repeatIdentical;
    }

    [Serializable] public sealed class NativeCompileProof
    {
        public string result = "Verified", scope = "T067 native Unity NavMesh publication (batchmode editor); not a release-player load or manual gameplay acceptance";
        public string sourceCommit = "", toolchainHash = "", editorVersion = "", editorRevision = "", aiNavigationVersion = "";
        public string nativeToolIdentity = "", cacheRoot = "", map = "", profile = "";
        public string nativeUnityNavBake = "Pending", releasePlayerLoad = "Pending";
        public bool force, allNative, sidecarValidated;
        public double elapsedMilliseconds;
        public int nativeBakeInvocations, nativeBakeCacheHits;
        public BakeTiming[] bakes = Array.Empty<BakeTiming>();
        public PackageProof[] packages = Array.Empty<PackageProof>();
    }

    public static void Compile()
    {
        var toolchain = ShooterBuild.CheckToolchain();
        string map = ShooterBuild.Argument("-shooterMap", "all");
        string profile = ShooterBuild.Argument("-shooterProfile", "all");
        bool force = bool.Parse(ShooterBuild.Argument("-shooterForce", "false"));
        string root = Path.GetFullPath(ShooterBuild.Argument("-shooterCacheRoot", Path.Combine(ShooterBuild.RepositoryRoot, "Artifacts/map-packages-native")));
        string allowed = Path.Combine(ShooterBuild.RepositoryRoot, "Artifacts") + Path.DirectorySeparatorChar;
        if (!root.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Native map cache must stay under repository Artifacts.");
        string reportPath = ShooterBuild.Argument("-shooterReport");

        List<string> mapIds;
        switch (map)
        {
            case "all": mapIds = new List<string> { "training-compound", "depot-yard" }; break;
            case "compound": mapIds = new List<string> { "training-compound" }; break;
            case "depot": mapIds = new List<string> { "depot-yard" }; break;
            default: throw new ArgumentException("Unknown map selection: " + map);
        }
        List<string> profiles;
        switch (profile)
        {
            case "all": profiles = new List<string>(MapPackageBuilder.VisualProfiles); break;
            case "gameplay": case "server": profiles = new List<string> { "gameplay" }; break;
            case "low": case "balanced": case "high": profiles = new List<string> { profile }; break;
            default: throw new ArgumentException("Unknown profile selection: " + profile);
        }

        // Ensure the editor bridge registered its provider (InitializeOnLoad normally already did).
        RuntimeHelpers.RunClassConstructor(typeof(UnityNavMeshBakeEditorBridge).TypeHandle);
        var inner = NavBakePublication.NativeProvider ?? throw new InvalidOperationException("Native NavMesh provider is not registered.");
        var timings = new List<BakeTiming>();
        var cache = new Dictionary<string, NavBakePublication.NativeBake?>(StringComparer.Ordinal);
        int invocations = 0, hits = 0;
        NavBakePublication.NativeProvider = (boxes, half, radius, height, step, slope, margin) =>
        {
            string key = BakeKey(boxes, half, radius, height, step, slope, margin);
            if (cache.TryGetValue(key, out var cached)) { hits++; return cached; }
            invocations++;
            var clock = Stopwatch.StartNew();
            var first = inner(boxes, half, radius, height, step, slope, margin);
            clock.Stop();
            var timing = new BakeTiming { key = key.Substring(0, 16), milliseconds = clock.Elapsed.TotalMilliseconds };
            if (first != null)
            {
                timing.polygonCount = first.Bake.Polygons.Count;
                timing.vertexCount = first.Bake.Vertices.Count;
                // Determinism probe: a second independent native bake must encode to identical NVB1 bytes.
                var second = inner(boxes, half, radius, height, step, slope, margin);
                timing.repeatIdentical = second != null &&
                    UnityNavMeshBakeCodec.Encode(first.Bake).AsSpan().SequenceEqual(UnityNavMeshBakeCodec.Encode(second.Bake));
            }
            timings.Add(timing);
            cache[key] = first;
            return first;
        };

        string identity = "unity=" + toolchain.editorVersion + ";revision=" + toolchain.editorRevision + ";com.unity.ai.navigation=" + toolchain.aiNavigationVersion;
        NavBakePublication.NativeToolIdentity = identity;
        var toolchainHash = new ContentId(ShooterBuild.HashFile(ShooterBuild.LockPath));
        var total = Stopwatch.StartNew();
        MapPackagePublishResult published;
        try
        {
            published = MapPackagePublisher.Publish(root, toolchainHash, mapIds, profiles, force, CancellationToken.None);
        }
        finally
        {
            NavBakePublication.NativeProvider = inner;
        }
        total.Stop();
        if (published.Error != ErrorCode.None) throw new InvalidOperationException(published.Error + ": " + published.Diagnostic);
        if (published.Packages.Count == 0) throw new InvalidOperationException("No packages published.");

        var proofs = new List<PackageProof>();
        foreach (var package in published.Packages)
        {
            if (!NavBakePublication.IsNativeProvenance(package.NavBakeProvenance))
                throw new InvalidOperationException("Package " + package.MapId + "/" + package.VisualProfile + " is not native: " + package.NavBakeProvenance);
            string dir = MapPackagePublisher.PackageDirectory(root, package.PackageRootHash);
            if (!MapPackagePublisher.TryOpenPublished(dir, package.PackageRootHash, out var opened, out string diagnostic))
                throw new InvalidOperationException("Published open failed: " + diagnostic);
            var fingerprint = GeneratorFingerprint.Compute(MapRecipe.GeneratorIdFor(package.MapId), MapRecipe.GeneratorVersion);
            var nav = GeometryCodec.DecodeNavigation(ArtifactCodec.Decode(opened.NavigationSgaf, ContentKind.Navigation, fingerprint, toolchainHash));
            if (nav.Format != UnityNavMeshBakeCodec.AdapterFormat) throw new InvalidOperationException("Unexpected navigation format " + nav.Format);
            var bake = UnityNavMeshBakeCodec.Decode(nav.Blob);
            var corners = new List<Float3>();
            bool path = UnityNavMeshBakeCodec.TryFindPath(bake, package.Plan.PlayerSpawn, package.Plan.ObjectiveCenter, corners);
            if (!path) throw new InvalidOperationException("Native bake has no spawn-to-objective path for " + package.MapId);
            proofs.Add(new PackageProof
            {
                mapId = package.MapId, visualProfile = package.VisualProfile,
                seed = package.Seed.ToString(CultureInfo.InvariantCulture),
                packageRootSha256 = package.PackageRootHash.ToString(),
                navigationSha256 = package.NavigationHash.ToString(),
                navSettingsSha256 = package.NavSettingsHash.ToString(),
                navBakeProvenance = package.NavBakeProvenance,
                polygonCount = bake.Polygons.Count, vertexCount = bake.Vertices.Count, pathSpawnToObjective = path
            });
        }
        if (!NavBakeProvenanceDocument.TryValidate(root, true, out _, out bool allNative, out string provenanceDiagnostic))
            throw new InvalidOperationException("nav-provenance sidecar invalid: " + provenanceDiagnostic);
        if (!allNative) throw new InvalidOperationException("Sidecar reports non-native packages.");
        if (timings.Any(t => !t.repeatIdentical))
            throw new InvalidOperationException("Native bake is not reproducible (repeat bake bytes differ); not publishable as release content.");

        ShooterBuild.WriteJson(reportPath, new NativeCompileProof
        {
            sourceCommit = SourceCommit(), toolchainHash = toolchainHash.ToString(),
            editorVersion = toolchain.editorVersion, editorRevision = toolchain.editorRevision, aiNavigationVersion = toolchain.aiNavigationVersion,
            nativeToolIdentity = identity, cacheRoot = root, map = map, profile = profile, force = force,
            allNative = allNative, sidecarValidated = true,
            nativeUnityNavBake = "PASS", releasePlayerLoad = "Pending",
            elapsedMilliseconds = total.Elapsed.TotalMilliseconds,
            nativeBakeInvocations = invocations, nativeBakeCacheHits = hits,
            bakes = timings.ToArray(), packages = proofs.ToArray()
        });
        Debug.Log("SHOOTER_NATIVE_CONTENT_VERIFIED packages=" + proofs.Count + " bakes=" + invocations);
    }

    private static string BakeKey(IReadOnlyList<CollisionBox> boxes, float half, float radius, float height, float step, float slope, float margin)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        sb.Append(half.ToString("R", inv)).Append('|').Append(radius.ToString("R", inv)).Append('|').Append(height.ToString("R", inv)).Append('|')
          .Append(step.ToString("R", inv)).Append('|').Append(slope.ToString("R", inv)).Append('|').Append(margin.ToString("R", inv));
        for (int i = 0; i < boxes.Count; i++)
        {
            var b = boxes[i];
            sb.Append(';').Append(b.Gameplay ? 'g' : 'v')
              .Append(b.Center.X.ToString("R", inv)).Append(',').Append(b.Center.Y.ToString("R", inv)).Append(',').Append(b.Center.Z.ToString("R", inv)).Append(',')
              .Append(b.Size.X.ToString("R", inv)).Append(',').Append(b.Size.Y.ToString("R", inv)).Append(',').Append(b.Size.Z.ToString("R", inv));
        }
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "").ToLowerInvariant();
    }

    private static string SourceCommit()
    {
        var start = new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = ShooterBuild.RepositoryRoot, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        using var process = Process.Start(start)!; string result = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Cannot determine source commit.");
        return result;
    }
}
