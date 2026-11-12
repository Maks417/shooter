using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;

public sealed class ContentStoreTests
{
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "shooter-content-test-" + Guid.NewGuid().ToString("N"));
        public ContentId Toolchain { get; } = new ContentId(new string('7', 64));
        public CacheFiles Files { get; }
        public Fixture() { Files = new CacheFiles(Root, Toolchain); Files.CreateDirectories(Root); }
        public ContentManifest Publish(ulong seed = 0, bool variants = false)
        {
            byte[] source = Encoding.UTF8.GetBytes("{\"schemaId\":\"shooter.recipe.box-collision.v1\",\"recipeId\":\"tests/collision\",\"generatorId\":\"collision.box\",\"generatorVersion\":\"1.0.0\",\"seed\":\"" + seed.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\",\"dependencies\":[],\"parameters\":{\"widthMm\":1000,\"heightMm\":1000,\"depthMm\":1000,\"bevelMm\":0}}");
            var recipe = BoxRecipe.Parse(source);
            var request = new GenerationRequest(recipe.Document, "windows64", "gameplay", Toolchain);
            var result = new CpuBoxCollisionGenerator().Generate(request, CancellationToken.None);
            if (variants)
            {
                var original = result.Artifacts[0];
                result = new GenerationResult(ErrorCode.None, new[] { original, new GeneratedArtifact(original.Kind, "secondary", 1, original.Payload, original.Sha256) }, result.Bounds, Array.Empty<Diagnostic>());
            }
            var manifest = ContentManifest.Create(request, recipe.SourceHash, result);
            string dir = Files.ContentDirectory(manifest.ContentId); Files.CreateDirectories(dir);
            File.WriteAllBytes(Path.Combine(dir, "manifest.json"), manifest.Encode());
            foreach (var item in result.Artifacts)
                File.WriteAllBytes(Path.Combine(dir, manifest.Artifacts.Single(a => a.Variant == item.Variant).Path), item.Payload.ToArray());
            return manifest;
        }
        public string ManifestPath(ContentManifest manifest) => Path.Combine(Files.ContentDirectory(manifest.ContentId), "manifest.json");
        public string ArtifactPath(ContentManifest manifest) => Path.Combine(Files.ContentDirectory(manifest.ContentId), manifest.Artifacts[0].Path);
        public void Mutate(ContentManifest manifest, Action<JsonObject> change)
        {
            JsonObject json = JsonNode.Parse(File.ReadAllText(ManifestPath(manifest)))!.AsObject();
            change(json); File.WriteAllText(ManifestPath(manifest), json.ToJsonString());
        }
        public void Dispose()
        {
            // Tests own this exact unique temp directory, never a content ID provided by untrusted input.
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
    private sealed class GateFiles : CacheFiles, IDisposable
    {
        public ManualResetEventSlim Started { get; } = new ManualResetEventSlim();
        public ManualResetEventSlim Continue { get; } = new ManualResetEventSlim();
        public int ArtifactReads;
        public GateFiles(Fixture f) : base(f.Root, f.Toolchain) { }
        public override ContentManifest ReadManifest(ContentId id)
        {
            Started.Set();
            if (!Continue.Wait(TimeSpan.FromSeconds(10))) throw new IOException("Test IO gate timeout.");
            return base.ReadManifest(id);
        }
        public override byte[] ReadArtifact(ContentManifest manifest, ManifestArtifact artifact)
        { Interlocked.Increment(ref ArtifactReads); return base.ReadArtifact(manifest, artifact); }
        public void Dispose() { Continue.Set(); Started.Dispose(); Continue.Dispose(); }
    }

    [Fact]
    public async Task LeasesShareVerifiedBytesAndReleaseIndependently()
    {
        using var f = new Fixture(); var manifest = f.Publish();
        using var store = new FileContentStore(f.Files);
        var first = await store.AcquireAsync(manifest.ContentId, ContentKind.Collision, CancellationToken.None);
        var second = await store.AcquireAsync(manifest.ContentId, ContentKind.Collision, CancellationToken.None);
        Assert.Equal(ErrorCode.None, first.Error); Assert.Equal(ErrorCode.None, second.Error);
        Assert.Equal(2, store.ActiveLeases); Assert.Equal(manifest.Artifacts[0].Bytes, store.ResidentBytes); Assert.Equal(1, store.LoadsStarted);
        var one = first.Lease!; var two = second.Lease!;
        Assert.Equal((byte)'S', one[0]);
        one.Dispose(); one.Dispose(); Assert.Throws<ObjectDisposedException>(() => one[0]);
        Assert.Equal((byte)'S', two[0]); Assert.Equal(1, store.ActiveLeases);
        two.Dispose(); Assert.Equal(0, store.ActiveLeases); Assert.Equal(0, store.ResidentBytes);
        var again = await store.AcquireAsync(manifest.ContentId, ContentKind.Collision, CancellationToken.None);
        Assert.Equal(2, store.LoadsStarted); again.Lease!.Dispose();
    }

    [Fact]
    public async Task ConsumerCancellationDoesNotCancelCoalescedPeer()
    {
        using var f = new Fixture(); var manifest = f.Publish(); using var files = new GateFiles(f);
        using var store = new FileContentStore(files); using var cancel = new CancellationTokenSource();
        Task<ContentLeaseResult> cancelled = store.AcquireAsync(manifest.ContentId, ContentKind.Collision, cancel.Token);
        Assert.True(files.Started.Wait(TimeSpan.FromSeconds(5)));
        var peer = store.AcquireAsync(manifest.ContentId, ContentKind.Collision, CancellationToken.None);
        cancel.Cancel(); var failure = await cancelled;
        Assert.Equal(ErrorCode.Cancelled, failure.Error); Assert.Null(failure.Lease); Assert.False(peer.IsCompleted);
        files.Continue.Set(); var result = await peer;
        Assert.Equal(ErrorCode.None, result.Error); Assert.Equal(1, store.LoadsStarted); Assert.Equal(1, files.ArtifactReads);
        result.Lease!.Dispose(); Assert.Equal(0, store.ResidentBytes); Assert.Equal(0, store.PendingWaiters);
    }

    [Fact]
    public async Task AbandonedLoadRetiresAndCanBeRequestedAgain()
    {
        using var f = new Fixture(); var manifest = f.Publish(); using var files = new GateFiles(f);
        using var store = new FileContentStore(files); using var cancel = new CancellationTokenSource();
        var task = store.AcquireAsync(manifest.ContentId, ContentKind.Collision, cancel.Token);
        Assert.True(files.Started.Wait(TimeSpan.FromSeconds(5))); cancel.Cancel(); Assert.Equal(ErrorCode.Cancelled, (await task).Error);
        files.Continue.Set(); Assert.True(SpinWait.SpinUntil(() => store.PendingLoads == 0, TimeSpan.FromSeconds(5)));
        Assert.Equal(0, store.ReservedBytes); Assert.Equal(0, store.ResidentBytes); Assert.Equal(0, store.ActiveLeases);
        var result = await store.AcquireAsync(manifest.ContentId, ContentKind.Collision, CancellationToken.None);
        Assert.Equal(ErrorCode.None, result.Error); result.Lease!.Dispose(); Assert.Equal(2, store.LoadsStarted);
    }

    [Fact]
    public async Task WaiterAndInflightBoundsReturnBusyWithoutDroppingPeer()
    {
        using var f = new Fixture(); var first = f.Publish(); var second = f.Publish(1); using var files = new GateFiles(f);
        using var store = new FileContentStore(files, maxInflight: 1, maxWaiters: 1);
        var task = store.AcquireAsync(first.ContentId, ContentKind.Collision, CancellationToken.None);
        Assert.True(files.Started.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(ErrorCode.Busy, (await store.AcquireAsync(second.ContentId, ContentKind.Collision, CancellationToken.None)).Error);
        Assert.Equal(ErrorCode.Busy, (await store.AcquireAsync(first.ContentId, ContentKind.Collision, CancellationToken.None)).Error);
        files.Continue.Set(); var result = await task; Assert.Equal(ErrorCode.None, result.Error); result.Lease!.Dispose();
        Assert.Equal(0, store.PendingWaiters); Assert.Equal(0, store.PendingLoads);
    }

    [Fact]
    public async Task ResidencyIsBoundedBeforeArtifactReadAndReclaimedAfterLastLease()
    {
        using var f = new Fixture(); var first = f.Publish(); var second = f.Publish(1); using var files = new GateFiles(f); files.Continue.Set();
        using (var tiny = new FileContentStore(files, 1))
        {
            Assert.Equal(ErrorCode.LimitExceeded, (await tiny.AcquireAsync(first.ContentId, ContentKind.Collision, CancellationToken.None)).Error);
            Assert.Equal(0, files.ArtifactReads); Assert.Equal(0, tiny.ReservedBytes);
        }
        using var store = new FileContentStore(files, first.Artifacts[0].Bytes);
        var a = await store.AcquireAsync(first.ContentId, ContentKind.Collision, CancellationToken.None);
        Assert.Equal(ErrorCode.None, a.Error);
        var b = await store.AcquireAsync(second.ContentId, ContentKind.Collision, CancellationToken.None);
        Assert.Equal(ErrorCode.LimitExceeded, b.Error); Assert.Null(b.Lease); Assert.Equal(1, files.ArtifactReads);
        a.Lease!.Dispose(); b = await store.AcquireAsync(second.ContentId, ContentKind.Collision, CancellationToken.None);
        Assert.Equal(ErrorCode.None, b.Error); b.Lease!.Dispose();
    }

    [Fact]
    public async Task KindOnlyLookupRejectsAmbiguityAndExplicitVariantSucceeds()
    {
        using var f = new Fixture(); var manifest = f.Publish(variants: true); using var store = new FileContentStore(f.Files);
        var ambiguous = await store.AcquireAsync(manifest.ContentId, ContentKind.Collision, CancellationToken.None);
        Assert.Equal(ErrorCode.InvalidInput, ambiguous.Error); Assert.Null(ambiguous.Lease); Assert.Equal(0, store.ResidentBytes);
        var explicitResult = await store.AcquireVariantAsync(manifest.ContentId, ContentKind.Collision, "secondary", CancellationToken.None);
        Assert.Equal(ErrorCode.None, explicitResult.Error); explicitResult.Lease!.Dispose();
        Assert.Equal(ErrorCode.MissingContent, (await store.AcquireVariantAsync(manifest.ContentId, ContentKind.Collision, "absent", CancellationToken.None)).Error);
    }

    [Fact]
    public async Task DisposeKeepsExistingLeaseValidAndRejectsNewAcquisition()
    {
        using var f = new Fixture(); var manifest = f.Publish(); var store = new FileContentStore(f.Files);
        var result = await store.AcquireAsync(manifest.ContentId, ContentKind.Collision, CancellationToken.None);
        store.Dispose(); Assert.Equal((byte)'S', result.Lease![0]);
        Assert.Equal(ErrorCode.Busy, (await store.AcquireAsync(manifest.ContentId, ContentKind.Collision, CancellationToken.None)).Error);
        result.Lease.Dispose(); Assert.Equal(0, store.ResidentBytes); Assert.Equal(0, store.ActiveLeases);
    }

    [Fact]
    public async Task MissingCorruptOversizedAndUnsupportedContentReturnExplicitFailures()
    {
        using var f = new Fixture(); using var store = new FileContentStore(f.Files);
        Assert.Equal(ErrorCode.MissingContent, (await store.AcquireAsync(new ContentId(new string('0', 64)), ContentKind.Collision, CancellationToken.None)).Error);
        var manifest = f.Publish(); byte[] original = File.ReadAllBytes(f.ArtifactPath(manifest)); original[^1] ^= 1;
        File.WriteAllBytes(f.ArtifactPath(manifest), original);
        Assert.Equal(ErrorCode.CorruptContent, (await store.AcquireAsync(manifest.ContentId, ContentKind.Collision, CancellationToken.None)).Error);
        using (var file = new FileStream(f.ArtifactPath(manifest), FileMode.Open, FileAccess.Write)) file.SetLength((long)ContractVersion.MaxArtifactBytes + 1);
        Assert.Equal(ErrorCode.LimitExceeded, (await store.AcquireAsync(manifest.ContentId, ContentKind.Collision, CancellationToken.None)).Error);
        manifest = f.Publish(); f.Mutate(manifest, json => json["manifestSchema"] = "shooter.content.manifest.v99");
        Assert.Equal(ErrorCode.UnsupportedVersion, (await store.AcquireAsync(manifest.ContentId, ContentKind.Collision, CancellationToken.None)).Error);
        Assert.Equal(0, store.ActiveLeases); Assert.Equal(0, store.ResidentBytes); Assert.Equal(0, store.ReservedBytes);
    }

    [Fact]
    public void ReaderRejectsWrongToolchainPathsCountsAndBounds()
    {
        using var f = new Fixture(); var manifest = f.Publish();
        var wrong = new CacheFiles(f.Root, new ContentId(new string('8', 64)));
        Assert.Equal(ErrorCode.UnsupportedVersion, Assert.Throws<CacheReadException>(() => wrong.ReadManifest(manifest.ContentId)).Error);
        Assert.Throws<CacheReadException>(() => f.Files.AssertSafePath(f.Root + "-outside"));
        f.Mutate(manifest, json => json["artifacts"]![0]!["path"] = "../escape.bin");
        Assert.Equal(ErrorCode.CorruptContent, Assert.Throws<CacheReadException>(() => f.Files.ReadManifest(manifest.ContentId)).Error);
        manifest = f.Publish(); f.Mutate(manifest, json => json["counts"]!["vertices"] = 1);
        Assert.Throws<CacheReadException>(() => f.Files.VerifyContent(f.Files.ReadManifest(manifest.ContentId)));
        manifest = f.Publish(); f.Mutate(manifest, json => { json["bounds"]!["minMm"] = new JsonArray(0, 0, 0); json["bounds"]!["maxMm"] = new JsonArray(1, 1, 1); });
        Assert.Throws<CacheReadException>(() => f.Files.VerifyContent(f.Files.ReadManifest(manifest.ContentId)));
    }

    [Fact]
    public async Task PrecancelledAndInvalidRequestsCreateNoIO()
    {
        using var f = new Fixture(); var manifest = f.Publish(); using var store = new FileContentStore(f.Files);
        Assert.Equal(ErrorCode.Cancelled, (await store.AcquireAsync(manifest.ContentId, ContentKind.Collision, new CancellationToken(true))).Error);
        Assert.Equal(ErrorCode.InvalidInput, (await store.AcquireAsync(manifest.ContentId, (ContentKind)99, CancellationToken.None)).Error);
        Assert.Equal(ErrorCode.InvalidInput, (await store.AcquireVariantAsync(manifest.ContentId, ContentKind.Collision, "", CancellationToken.None)).Error);
        Assert.Equal(0, store.LoadsStarted);
    }

    [Fact]
    public void ReaderRejectsReparseDirectoriesIncludingCacheRootAncestors()
    {
        using var f = new Fixture(); string target = f.Root + "-target"; string link = Path.Combine(f.Root, "link");
        Directory.CreateDirectory(target);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (string argument in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start)!;
                string stdout = process.StandardOutput.ReadToEnd(), stderr = process.StandardError.ReadToEnd(); process.WaitForExit();
                Assert.True(process.ExitCode == 0, stdout + stderr);
            }
            else Directory.CreateSymbolicLink(link, target);
            Assert.Throws<CacheReadException>(() => f.Files.AssertSafePath(Path.Combine(link, "artifact.bin")));
            Assert.Throws<CacheReadException>(() => new CacheFiles(Path.Combine(link, "nested-cache"), f.Toolchain));
        }
        finally
        {
            // Remove only the exact known link, without recursive traversal; then the empty test-owned target.
            if (Directory.Exists(link)) Directory.Delete(link);
            Directory.Delete(target);
        }
    }
}
