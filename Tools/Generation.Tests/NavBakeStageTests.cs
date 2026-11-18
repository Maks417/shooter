using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Xunit;

/// <summary>
/// Two-stage native publication seam: an externally baked (editor) NVB1 is injected into the pure .NET publication.
/// A software grid stands in for the native triangulation; only the staging, binding and provenance rules are under test.
/// </summary>
[Collection("NativeNavProvider")]
public sealed class NavBakeStageTests
{
    private const string Identity = "unity=6000.3.28f1;revision=8f082d2fe476;com.unity.ai.navigation=2.0.0";
    private readonly ContentId toolchain = ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("nav-bake-stage-test"));

    private static IReadOnlyList<CollisionBox> GameplayBoxes(string mapId)
    {
        return ProductMapPlanner.Generate(mapId).CollisionBoxes;
    }

    private const float R = 0.32f, H = 1.8f, Step = 0.25f, Slope = 45f, Margin = 0.05f;

    private static string KeyFor(IReadOnlyList<CollisionBox> boxes) =>
        NavBakeStage.InputKey(boxes, CompoundPlan.MapHalfExtent, R, H, Step, Slope, Margin);

    private NavBakeStage.Entry StageFor(string mapId, string? provenance = null, bool repeat = true, byte[]? bytes = null, string identity = Identity)
    {
        var boxes = GameplayBoxes(mapId);
        var grid = CleanNavBake.Build(boxes, CompoundPlan.MapHalfExtent, 2f);
        var gameplay = MapPackageBuilder.Build(mapId, "gameplay", toolchain);
        return new NavBakeStage.Entry(mapId, KeyFor(boxes), ContentIdentity.Hash(gameplay.CollisionSgaf),
            provenance ?? NavBakePublication.NativeProvenance, identity, repeat, bytes ?? UnityNavMeshBakeCodec.Encode(grid));
    }

    private static T WithProvider<T>(Func<IReadOnlyList<CollisionBox>, float, float, float, float, float, float, NavBakePublication.NativeBake?> provider, Func<T> body)
    {
        var previous = NavBakePublication.NativeProvider;
        var previousIdentity = NavBakePublication.NativeToolIdentity;
        try
        {
            NavBakePublication.NativeProvider = provider;
            NavBakePublication.NativeToolIdentity = Identity;
            return body();
        }
        finally
        {
            NavBakePublication.NativeProvider = previous;
            NavBakePublication.NativeToolIdentity = previousIdentity;
        }
    }

    [Fact]
    public void InjectedBakeIsPublishedNativeAndEveryNonNavigationPayloadMatchesSoftwarePublication()
    {
        foreach (string mapId in new[] { "training-compound", "depot-yard" })
        {
            var software = MapPackageBuilder.Build(mapId, "low", toolchain);
            var entry = StageFor(mapId);
            var injected = WithProvider(NavBakeStage.CreateProvider(new[] { entry }), () => MapPackageBuilder.Build(mapId, "low", toolchain));

            Assert.Equal(NavBakePublication.NativeProvenance, injected.NavBakeProvenance);
            Assert.DoesNotContain("software-walkable-grid", injected.NavBakeProvenance, StringComparison.Ordinal);
            Assert.Equal(software.CollisionHash, injected.CollisionHash);
            Assert.Equal(software.MetadataHash, injected.MetadataHash);
            Assert.Equal(software.Dependencies.Count, injected.Dependencies.Count);
            for (int i = 0; i < software.Dependencies.Count; i++)
            {
                var a = software.Dependencies[i];
                var b = injected.Dependencies[i];
                Assert.Equal(a.Path, b.Path);
                if (a.Role == "navigation")
                {
                    Assert.NotEqual(a.Sha256, b.Sha256);
                    continue;
                }
                Assert.Equal(a.Sha256, b.Sha256);
                Assert.True(a.Bytes.AsSpan().SequenceEqual(b.Bytes), a.Path);
            }

            // The published navigation payload carries exactly the staged NVB1 bytes.
            var fingerprint = GeneratorFingerprint.Compute(MapRecipe.GeneratorIdFor(mapId), MapRecipe.GeneratorVersion);
            var nav = GeometryCodec.DecodeNavigation(ArtifactCodec.Decode(injected.NavigationSgaf, ContentKind.Navigation, fingerprint, toolchain));
            Assert.True(nav.Blob.SequenceEqual(entry.BakeBytes));
        }
    }

    [Fact]
    public void BoxesRecoveredFromPublishedCollisionProduceTheSameInputKeyAsTheGeneratorPlan()
    {
        foreach (string mapId in new[] { "training-compound", "depot-yard" })
        {
            var gameplay = MapPackageBuilder.Build(mapId, "gameplay", toolchain);
            var fingerprint = GeneratorFingerprint.Compute(MapRecipe.GeneratorIdFor(mapId), MapRecipe.GeneratorVersion);
            var collision = GeometryCodec.DecodeCollision(ArtifactCodec.Decode(gameplay.CollisionSgaf, ContentKind.Collision, fingerprint, toolchain));
            var recovered = NavBakeStage.BoxesFromCollision(collision);
            Assert.Equal(KeyFor(GameplayBoxes(mapId)), KeyFor(recovered));
        }
        Assert.NotEqual(KeyFor(GameplayBoxes("training-compound")), KeyFor(GameplayBoxes("depot-yard")));
    }

    [Fact]
    public void PublisherWritesNativeSidecarForInjectedBakesAndRefusesMismatchedMapBake()
    {
        string root = Path.Combine(Path.GetTempPath(), "shooter-nav-stage-" + Guid.NewGuid().ToString("N"));
        try
        {
            var compound = StageFor("training-compound");
            var depot = StageFor("depot-yard");
            var ok = WithProvider(NavBakeStage.CreateProvider(new[] { compound, depot }),
                () => MapPackagePublisher.Publish(root, toolchain, new[] { "training-compound", "depot-yard" }, new[] { "gameplay" }, false, CancellationToken.None));
            Assert.Equal(ErrorCode.None, ok.Error);
            Assert.All(ok.Packages, p => Assert.True(NavBakePublication.IsNativeProvenance(p.NavBakeProvenance)));
            Assert.True(NavBakeProvenanceDocument.TryValidate(root, true, out int count, out bool allNative, out string diagnostic), diagnostic);
            Assert.Equal(2, count);
            Assert.True(allNative);
            Assert.Contains(Identity, File.ReadAllText(NavBakeProvenanceDocument.SidecarPath(root)), StringComparison.Ordinal);

            // A stage that only knows the depot bake must fail loudly for the compound, never fall back to the software grid.
            string rejected = Path.Combine(Path.GetTempPath(), "shooter-nav-stage-" + Guid.NewGuid().ToString("N"));
            try
            {
                var result = WithProvider(NavBakeStage.CreateProvider(new[] { depot }),
                    () => MapPackagePublisher.Publish(rejected, toolchain, new[] { "training-compound" }, new[] { "gameplay" }, false, CancellationToken.None));
                Assert.NotEqual(ErrorCode.None, result.Error);
                Assert.Contains("native-stage-has-no-bake-for-collision-input", result.Diagnostic, StringComparison.Ordinal);
                Assert.False(File.Exists(NavBakeProvenanceDocument.SidecarPath(rejected)));
            }
            finally { TryDelete(rejected); }
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void StageDirectoryRoundTripsAndRejectsTamperingMissingEntriesAndMapSwaps()
    {
        string dir = Path.Combine(Path.GetTempPath(), "shooter-nav-stage-dir-" + Guid.NewGuid().ToString("N"));
        try
        {
            var compound = StageFor("training-compound");
            NavBakeStage.Write(dir, compound);
            var loaded = NavBakeStage.Load(dir, new[] { "training-compound" });
            Assert.Equal(compound.InputKey, loaded[0].InputKey);
            Assert.Equal(compound.CollisionSha256, loaded[0].CollisionSha256);
            Assert.Equal(Identity, loaded[0].ToolIdentity);
            Assert.True(compound.BakeBytes.AsSpan().SequenceEqual(loaded[0].BakeBytes));

            Assert.Throws<FormatException>(() => NavBakeStage.Load(dir, new[] { "depot-yard" })); // missing entry
            Assert.Throws<FormatException>(() => NavBakeStage.Load(Path.Combine(dir, "nope"), new[] { "training-compound" }));

            // Tampered bake bytes no longer match the descriptor hash.
            byte[] tampered = (byte[])compound.BakeBytes.Clone();
            tampered[tampered.Length / 2] ^= 0x5a;
            Assert.Throws<FormatException>(() => NavBakeStage.Parse(NavBakeStage.EncodeDescriptor(compound), tampered));

            // A compound descriptor/bake renamed to the depot slot is refused (map id inside the descriptor wins).
            File.Copy(Path.Combine(dir, "training-compound" + NavBakeStage.DescriptorSuffix), Path.Combine(dir, "depot-yard" + NavBakeStage.DescriptorSuffix));
            File.Copy(Path.Combine(dir, "training-compound" + NavBakeStage.BakeSuffix), Path.Combine(dir, "depot-yard" + NavBakeStage.BakeSuffix));
            Assert.Throws<FormatException>(() => NavBakeStage.Load(dir, new[] { "depot-yard" }));
        }
        finally { TryDelete(dir); }
    }

    [Fact]
    public void EntryRejectsMalformedNonNativeNonReproducibleAndNonCanonicalBakes()
    {
        var boxes = GameplayBoxes("depot-yard");
        string key = KeyFor(boxes);
        var collision = ContentIdentity.Hash(new byte[] { 1 });
        byte[] good = UnityNavMeshBakeCodec.Encode(CleanNavBake.Build(boxes, CompoundPlan.MapHalfExtent, 2f));

        _ = new NavBakeStage.Entry("depot-yard", key, collision, NavBakePublication.NativeProvenance, Identity, true, good);
        Assert.Throws<FormatException>(() => new NavBakeStage.Entry("depot-yard", key, collision, NavBakePublication.NativeProvenance, Identity, true, new byte[] { 1, 2, 3 }));
        Assert.Throws<FormatException>(() => new NavBakeStage.Entry("depot-yard", key, collision, NavBakePublication.NativeProvenance, Identity, true, Array.Empty<byte>()));
        Assert.Throws<FormatException>(() => new NavBakeStage.Entry("depot-yard", key, collision, NavBakePublication.SoftwarePendingProvenance, Identity, true, good));
        Assert.Throws<FormatException>(() => new NavBakeStage.Entry("depot-yard", key, collision, "unity-ai-navigation-2.0.0;pinned-physx-collision-bake;extra", Identity, true, good));
        Assert.Throws<FormatException>(() => new NavBakeStage.Entry("depot-yard", key, collision, NavBakePublication.NativeProvenance, " ", true, good));
        Assert.Throws<FormatException>(() => new NavBakeStage.Entry("depot-yard", key, collision, NavBakePublication.NativeProvenance, Identity, false, good));
        Assert.Throws<FormatException>(() => new NavBakeStage.Entry("unknown-map", key, collision, NavBakePublication.NativeProvenance, Identity, true, good));
        Assert.Throws<FormatException>(() => new NavBakeStage.Entry("depot-yard", "XYZ", collision, NavBakePublication.NativeProvenance, Identity, true, good));
        var trailing = good.Concat(new byte[] { 0 }).ToArray();
        Assert.Throws<FormatException>(() => new NavBakeStage.Entry("depot-yard", key, collision, NavBakePublication.NativeProvenance, Identity, true, trailing));

        Assert.Throws<FormatException>(() => NavBakeStage.Parse(new byte[] { (byte)'{', (byte)'}' }, good));
        Assert.Throws<FormatException>(() => NavBakeStage.Parse(System.Text.Encoding.UTF8.GetBytes("not json"), good));
    }

    [Fact]
    public void ProviderRejectsDuplicateKeysAndEmptyStage()
    {
        var entry = StageFor("depot-yard");
        Assert.Throws<ArgumentException>(() => NavBakeStage.CreateProvider(Array.Empty<NavBakeStage.Entry>()));
        Assert.Throws<ArgumentException>(() => NavBakeStage.CreateProvider(new[] { entry, entry }));
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch (IOException) { /* temp */ }
    }
}
