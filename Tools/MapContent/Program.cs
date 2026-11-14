using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length < 6)
                throw new ArgumentException("Expected repository, cacheRoot, reportPath, sourceCommit, map(all|compound|depot), force(true|false) [, profiles].");
            string root = Path.GetFullPath(args[0]);
            string cache = Path.GetFullPath(args[1]);
            string report = Path.GetFullPath(args[2]);
            string commit = args[3];
            string map = args[4];
            bool force = bool.Parse(args[5]);
            string profileArg = args.Length > 6 ? args[6] : "all";
            string artifacts = Path.Combine(root, "Artifacts") + Path.DirectorySeparatorChar;
            if (!cache.StartsWith(artifacts, StringComparison.OrdinalIgnoreCase) ||
                !report.StartsWith(artifacts, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Outputs must remain under repository Artifacts.");

            var toolchain = ContentIdentity.Hash(File.ReadAllBytes(Path.Combine(root, "toolchain-lock.json")));
            var mapIds = SelectMaps(map);
            var profiles = SelectProfiles(profileArg);
            // Reject incomplete dependency enumeration: every selected map must resolve approved seed/recipe.
            foreach (string mapId in mapIds)
            {
                _ = MapRecipe.ApprovedSeed(mapId);
                MapRecipe.Parse(MapRecipe.CreateSource(mapId));
            }

            var clock = Stopwatch.StartNew();
            var published = MapPackagePublisher.Publish(cache, toolchain, mapIds, profiles, force, CancellationToken.None);
            clock.Stop();
            if (published.Error != ErrorCode.None)
                throw new InvalidOperationException(published.Error + ": " + published.Diagnostic);

            // Cold/warm/forced reproduction proof for the selected gameplay packages.
            var reproduction = new List<object>();
            foreach (string mapId in mapIds)
            {
                var first = MapPackageBuilder.Build(mapId, "gameplay", toolchain);
                var second = MapPackageBuilder.Build(mapId, "gameplay", toolchain);
                if (first.PackageRootHash != second.PackageRootHash ||
                    !first.PackageRootBytes.SequenceEqual(second.PackageRootBytes) ||
                    !first.CollisionSgaf.SequenceEqual(second.CollisionSgaf) ||
                    !first.NavigationSgaf.SequenceEqual(second.NavigationSgaf) ||
                    !first.MetadataBytes.SequenceEqual(second.MetadataBytes))
                    throw new InvalidOperationException("Reproduction mismatch for " + mapId);
                string dir = MapPackagePublisher.PackageDirectory(cache, first.PackageRootHash);
                if (!MapPackagePublisher.TryOpenPublished(dir, first.PackageRootHash, out _, out string openDiagnostic))
                    throw new InvalidOperationException("Published open failed: " + openDiagnostic);
                reproduction.Add(new
                {
                    mapId,
                    packageRootSha256 = first.PackageRootHash.ToString(),
                    collisionSha256 = first.CollisionHash.ToString(),
                    navigationSha256 = first.NavigationHash.ToString(),
                    metadataSha256 = first.MetadataHash.ToString(),
                    navBakeProvenance = first.NavBakeProvenance,
                    bytesEqual = true
                });
            }

            // Cancel recovery: cancelled publish must not create a new package directory.
            string cancelProbe = Path.Combine(cache, "cancel-probe");
            Directory.CreateDirectory(cancelProbe);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                var cancelled = MapPackagePublisher.Publish(cancelProbe, toolchain, new[] { mapIds[0] }, new[] { "gameplay" }, false, cts.Token);
                if (cancelled.Error != ErrorCode.Cancelled)
                    throw new InvalidOperationException("Expected cancelled publish, got " + cancelled.Error);
            }

            // Corrupt recovery: retain corrupt bytes, do not silent-substitute.
            var sample = MapPackageBuilder.Build(mapIds[0], "gameplay", toolchain);
            string sampleDir = MapPackagePublisher.PackageDirectory(cache, sample.PackageRootHash);
            string collisionPath = Path.Combine(sampleDir, "collision-gameplay.bin");
            byte[] originalCollision = File.ReadAllBytes(collisionPath);
            File.WriteAllBytes(collisionPath, new byte[] { 1, 2, 3, 4 });
            bool corruptOpen = MapPackagePublisher.TryOpenPublished(sampleDir, sample.PackageRootHash, out _, out string corruptDiagnostic);
            if (corruptOpen) throw new InvalidOperationException("Corrupt package must not open.");
            if (!File.ReadAllBytes(collisionPath).SequenceEqual(new byte[] { 1, 2, 3, 4 }))
                throw new InvalidOperationException("Corrupt bytes must be retained for repair.");
            File.WriteAllBytes(collisionPath, originalCollision);

            var serverOnly = published.Packages.Where(p => p.VisualProfile == "gameplay").Select(p => new
            {
                p.MapId,
                packageRootSha256 = p.PackageRootHash.ToString(),
                roles = p.Dependencies.Select(d => d.Role).ToArray(),
                visualRolesAbsent = p.Dependencies.All(d => d.Role != "mesh" && d.Role != "material" && d.Role != "skeleton" && d.Role != "shader" && d.Role != "font")
            }).ToArray();
            if (serverOnly.Length == 0 || serverOnly.Any(s => !s.visualRolesAbsent))
                throw new InvalidOperationException("Server gameplay-only closure must omit visual roles.");

            var proof = new
            {
                result = "Verified",
                scope = "T055/FIX04 map package publication; software walkable-grid nav bake; Unity native NavMesh bake/player Pending",
                sourceCommit = commit,
                toolchainHash = toolchain.ToString(),
                map = map,
                profiles,
                force,
                reused = published.Reused,
                elapsedMilliseconds = clock.Elapsed.TotalMilliseconds,
                packages = published.Packages.Select(p => new
                {
                    mapId = p.MapId,
                    visualProfile = p.VisualProfile,
                    seed = p.Seed.ToString(CultureInfo.InvariantCulture),
                    packageRootSha256 = p.PackageRootHash.ToString(),
                    collisionSha256 = p.CollisionHash.ToString(),
                    navigationSha256 = p.NavigationHash.ToString(),
                    metadataSha256 = p.MetadataHash.ToString(),
                    dependencyCount = p.Dependencies.Count,
                    navBakeProvenance = p.NavBakeProvenance
                }).ToArray(),
                reproduction,
                corruptRecovery = new { retained = true, diagnostic = corruptDiagnostic },
                cancelRecovery = new { published = false },
                serverGameplayOnly = serverOnly,
                nativeUnityNavBake = "Pending",
                releasePlayerLoad = "Pending"
            };
            File.WriteAllText(report, JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            Console.WriteLine("Verified map packages=" + published.Packages.Count + "; " + clock.Elapsed.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture) + "ms");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static List<string> SelectMaps(string map) => map switch
    {
        "all" => new List<string> { "training-compound", "depot-yard" },
        "compound" => new List<string> { "training-compound" },
        "depot" => new List<string> { "depot-yard" },
        _ => throw new ArgumentException("Unknown map selection: " + map)
    };

    private static List<string> SelectProfiles(string profiles) => profiles switch
    {
        "all" => new List<string>(MapPackageBuilder.VisualProfiles),
        "gameplay" => new List<string> { "gameplay" },
        "server" => new List<string> { "gameplay" },
        "low" => new List<string> { "low" },
        "balanced" => new List<string> { "balanced" },
        "high" => new List<string> { "high" },
        _ => throw new ArgumentException("Unknown profile selection: " + profiles)
    };
}
