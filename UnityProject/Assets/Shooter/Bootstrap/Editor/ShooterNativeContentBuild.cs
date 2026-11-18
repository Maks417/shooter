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
/// Two-stage native navigation publication (T067/C01, supersedes the whole-package Mono publication).
/// The editor ONLY bakes navigation: it reads the authoritative gameplay collision from packages already published by
/// the pure .NET MapContent publisher (never regenerated in Mono, whose float rounding differs from .NET/IL2CPP),
/// runs the pinned AI Navigation bake through <see cref="UnityNavMeshBakeEditorBridge"/> and writes one
/// <see cref="NavBakeStage"/> entry (NVB1 bytes + provenance) per map. The pure publisher then publishes the final
/// packages with these bakes, so every non-navigation payload is byte-identical to the software path.
/// Never labels a software grid native: staging fails unless the provider returned a real native bake.
/// </summary>
public static class ShooterNativeContentBuild
{
    [Serializable] public sealed class StagedMapProof
    {
        public string mapId = "", sourcePackageRootSha256 = "", collisionSha256 = "", inputKey = "", bakeSha256 = "";
        public int gameplayBoxCount, polygonCount, vertexCount;
        public double milliseconds;
        public bool repeatIdentical;
    }

    [Serializable] public sealed class NavStageProof
    {
        public string result = "Staged", scope = "T067 native Unity NavMesh bake staging (batchmode editor); content is published by the pure .NET publisher";
        public string toolchainHash = "", editorVersion = "", editorRevision = "", aiNavigationVersion = "", nativeToolIdentity = "";
        public string packageCacheRoot = "", stageDirectory = "";
        public StagedMapProof[] maps = Array.Empty<StagedMapProof>();
    }

    // Locked shooter.nav-settings.v1 SliceDefault; must equal the constants in MapPackageBuilder.Build (the .NET
    // stage lookup is keyed on them, so any drift fails loudly instead of publishing a mismatched bake).
    private const float AgentRadius = 0.32f, AgentHeight = 1.8f, MaxStep = 0.25f, MaxSlope = 45f, ClearanceMargin = 0.05f;

    public static void Compile()
    {
        var toolchain = ShooterBuild.CheckToolchain();
        string allowed = Path.Combine(ShooterBuild.RepositoryRoot, "Artifacts") + Path.DirectorySeparatorChar;
        string cacheRoot = Path.GetFullPath(ShooterBuild.Argument("-shooterPackageCache"));
        string stage = Path.GetFullPath(ShooterBuild.Argument("-shooterNavStage"));
        if (!cacheRoot.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || !stage.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Package cache and nav stage must stay under repository Artifacts.");
        string reportPath = ShooterBuild.Argument("-shooterReport");
        // "mapId=packageRootSha256,mapId=packageRootSha256": one published .NET package per map (any profile; collision is shared).
        var sources = new List<KeyValuePair<string, string>>();
        foreach (string item in ShooterBuild.Argument("-shooterPackages").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = item.IndexOf('=');
            if (eq <= 0) throw new ArgumentException("Bad -shooterPackages item: " + item);
            sources.Add(new KeyValuePair<string, string>(item.Substring(0, eq), item.Substring(eq + 1)));
        }
        if (sources.Count == 0) throw new ArgumentException("No source packages supplied.");

        // Ensure the editor bridge registered its provider (InitializeOnLoad normally already did).
        RuntimeHelpers.RunClassConstructor(typeof(UnityNavMeshBakeEditorBridge).TypeHandle);
        var inner = NavBakePublication.NativeProvider ?? throw new InvalidOperationException("Native NavMesh provider is not registered.");
        string identity = "unity=" + toolchain.editorVersion + ";revision=" + toolchain.editorRevision + ";com.unity.ai.navigation=" + toolchain.aiNavigationVersion;
        var toolchainHash = new ContentId(ShooterBuild.HashFile(ShooterBuild.LockPath));
        if (Directory.Exists(stage)) Directory.Delete(stage, true);
        Directory.CreateDirectory(stage);

        var proofs = new List<StagedMapProof>();
        foreach (var source in sources)
        {
            string mapId = source.Key;
            var rootHash = new ContentId(source.Value);
            string dir = MapPackagePublisher.PackageDirectory(cacheRoot, rootHash);
            if (!MapPackagePublisher.TryOpenPublished(dir, rootHash, out var opened, out string diagnostic))
                throw new InvalidOperationException("Source .NET package open failed for " + mapId + ": " + diagnostic);
            if (opened.MapId != mapId) throw new InvalidOperationException("Source package " + source.Value + " is map " + opened.MapId + ", expected " + mapId);
            var fingerprint = GeneratorFingerprint.Compute(MapRecipe.GeneratorIdFor(mapId), MapRecipe.GeneratorVersion);
            var collision = GeometryCodec.DecodeCollision(ArtifactCodec.Decode(opened.CollisionSgaf, ContentKind.Collision, fingerprint, toolchainHash));
            var boxes = NavBakeStage.BoxesFromCollision(collision);

            var clock = Stopwatch.StartNew();
            var first = inner(boxes, CompoundPlan.MapHalfExtent, AgentRadius, AgentHeight, MaxStep, MaxSlope, ClearanceMargin);
            clock.Stop();
            if (first == null || !NavBakePublication.IsNativeProvenance(first.Provenance))
                throw new InvalidOperationException("Native bake failed for " + mapId);
            byte[] firstBytes = UnityNavMeshBakeCodec.Encode(first.Bake);
            // Determinism probe: a second independent native bake must encode to identical NVB1 bytes.
            var second = inner(boxes, CompoundPlan.MapHalfExtent, AgentRadius, AgentHeight, MaxStep, MaxSlope, ClearanceMargin);
            bool repeat = second != null && firstBytes.AsSpan().SequenceEqual(UnityNavMeshBakeCodec.Encode(second.Bake));
            if (!repeat) throw new InvalidOperationException("Native bake for " + mapId + " is not reproducible (repeat bake bytes differ); not publishable as release content.");
            // ADR-114: no walkable surface inside a solid, no nav vertex closer to a solid than the agent radius.
            var audit = NavCollisionAudit.ThrowIfLeaky(mapId, first.Bake, boxes, AgentRadius, AgentHeight, MaxStep);
            Debug.Log("SHOOTER_NAV_COLLISION_AUDIT map=" + mapId + " " + audit.Summary());

            string key = NavBakeStage.InputKey(boxes, CompoundPlan.MapHalfExtent, AgentRadius, AgentHeight, MaxStep, MaxSlope, ClearanceMargin);
            var collisionHash = ContentIdentity.Hash(opened.CollisionSgaf);
            var entry = new NavBakeStage.Entry(mapId, key, collisionHash, first.Provenance, identity, repeat, firstBytes);
            NavBakeStage.Write(stage, entry);
            proofs.Add(new StagedMapProof
            {
                mapId = mapId, sourcePackageRootSha256 = rootHash.ToString(), collisionSha256 = collisionHash.ToString(), inputKey = key,
                bakeSha256 = ContentIdentity.Hash(firstBytes).ToString(), gameplayBoxCount = boxes.Count,
                polygonCount = first.Bake.Polygons.Count, vertexCount = first.Bake.Vertices.Count,
                milliseconds = clock.Elapsed.TotalMilliseconds, repeatIdentical = repeat
            });
        }

        ShooterBuild.WriteJson(reportPath, new NavStageProof
        {
            toolchainHash = toolchainHash.ToString(),
            editorVersion = toolchain.editorVersion, editorRevision = toolchain.editorRevision, aiNavigationVersion = toolchain.aiNavigationVersion,
            nativeToolIdentity = identity, packageCacheRoot = cacheRoot, stageDirectory = stage, maps = proofs.ToArray()
        });
        Debug.Log("SHOOTER_NATIVE_NAV_STAGED maps=" + proofs.Count);
    }
}
