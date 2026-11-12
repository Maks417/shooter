using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;

public sealed class CompilerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "shooter-compiler-" + Guid.NewGuid().ToString("N"));
    private readonly ContentId toolchain = new ContentId(new string('1', 64));
    private CacheFiles Files => new CacheFiles(root, toolchain);
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private static byte[] Source(string id = "diagnostics/box", string[]? dependencies = null) => Encoding.UTF8.GetBytes(
        "{\"recipeId\":\"" + id + "\",\"schemaId\":\"shooter.recipe.box-collision.v1\",\"generatorId\":\"collision.box\",\"generatorVersion\":\"1.0.0\",\"seed\":\"460072030\",\"parameters\":{\"widthMm\":1000,\"heightMm\":1000,\"depthMm\":1000,\"bevelMm\":0},\"dependencies\":[" + string.Join(",", (dependencies ?? Array.Empty<string>()).Select(d => "\"" + d + "\"")) + "]}");
    private CompilationNode Node(IAssetGenerator? generator = null, string id = "diagnostics/box", string[]? deps = null) =>
        new CompilationNode(Source(id, deps), "windows64", "gameplay", toolchain, generator ?? new CpuBoxCollisionGenerator());

    [Fact]
    public async Task ColdWarmAndForcedCompilationPreserveExactImmutableBytes()
    {
        var generator = new CountingGenerator(); var node = Node(generator); var compiler = new ContentCompiler(Files);
        var cold = await compiler.CompileAsync(new[] { node }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.None, cold.Error); Assert.False(Assert.Single(cold.Entries).Reused);
        string manifestPath = Path.Combine(Files.ContentDirectory(node.Id), "manifest.json"); byte[] original = File.ReadAllBytes(manifestPath);
        var warm = await compiler.CompileAsync(new[] { node }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.None, warm.Error); Assert.True(Assert.Single(warm.Entries).Reused); Assert.Equal(1, generator.Calls);
        var force = await compiler.CompileAsync(new[] { node }, true, CancellationToken.None);
        Assert.Equal(ErrorCode.None, force.Error); Assert.True(Assert.Single(force.Entries).Reused); Assert.Equal(2, generator.Calls);
        Assert.Equal(original, File.ReadAllBytes(manifestPath));
        Assert.Empty(Directory.GetDirectories(root, "*.tmp", SearchOption.AllDirectories));
        Assert.Equal(node.Id.ToString(), JsonValue.Parse(File.ReadAllBytes(Path.Combine(root, "index.json"))).Object["contentIds"].Array[0].String);
    }
    [Fact]
    public async Task ConcurrentCompilersDeduplicateUnderOsKeyLock()
    {
        var generator = new CountingGenerator(); var node = Node(generator);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => new ContentCompiler(Files).CompileAsync(new[] { node }, false, CancellationToken.None))));
        Assert.All(results, r => Assert.Equal(ErrorCode.None, r.Error)); Assert.Equal(1, generator.Calls);
        Assert.Single(results, r => !r.Entries[0].Reused);
        Files.VerifyContent(Files.ReadManifest(node.Id));
    }
    [Fact]
    public async Task ForceRejectsSameKeyDifferentBytesWithoutOverwrite()
    {
        var generator = new CountingGenerator(); var node = Node(generator); var compiler = new ContentCompiler(Files);
        Assert.Equal(ErrorCode.None, (await compiler.CompileAsync(new[] { node }, false, CancellationToken.None)).Error);
        byte[] original = File.ReadAllBytes(Path.Combine(Files.ContentDirectory(node.Id), "manifest.json"));
        generator.Alternate = true;
        var result = await compiler.CompileAsync(new[] { node }, true, CancellationToken.None);
        Assert.Equal(ErrorCode.CorruptContent, result.Error); Assert.Contains("Reproducibility", result.Diagnostic);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(Files.ContentDirectory(node.Id), "manifest.json")));
    }
    [Fact]
    public async Task CorruptWarmCacheFailsWithoutRegeneratingOrDeleting()
    {
        var generator = new CountingGenerator(); var node = Node(generator); var compiler = new ContentCompiler(Files);
        Assert.Equal(ErrorCode.None, (await compiler.CompileAsync(new[] { node }, false, CancellationToken.None)).Error);
        string payload = Path.Combine(Files.ContentDirectory(node.Id), "collision-main.bin"); File.WriteAllBytes(payload, new byte[] { 1, 2, 3 });
        Assert.Equal(ErrorCode.CorruptContent, (await compiler.CompileAsync(new[] { node }, false, CancellationToken.None)).Error);
        Assert.Equal(1, generator.Calls); Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(payload));
    }
    [Fact]
    public async Task CancellationDoesNotPublishOrDeleteAnotherCompilersStage()
    {
        var node = Node(new CancelGenerator()); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Equal(ErrorCode.Cancelled, (await new ContentCompiler(Files).CompileAsync(new[] { node }, false, cancel.Token)).Error);
        Assert.False(Directory.Exists(Files.ContentDirectory(node.Id)));
        var otherStage = Path.Combine(root, "aa", ".other.tmp"); Files.CreateDirectories(otherStage); File.WriteAllText(Path.Combine(otherStage, "keep"), "unrelated");
        Assert.Equal(ErrorCode.Cancelled, (await new ContentCompiler(Files).CompileAsync(new[] { node }, false, CancellationToken.None)).Error);
        Assert.True(File.Exists(Path.Combine(otherStage, "keep"))); Assert.False(File.Exists(Path.Combine(root, "index.json")));
    }
    [Fact]
    public async Task DagPublishesDependenciesBeforeConsumersFromReversedInput()
    {
        var sequence = new List<string>(); var generator = new CountingGenerator(sequence);
        var first = Node(generator, "diagnostics/first"); var second = Node(generator, "diagnostics/second", new[] { first.Id.ToString() });
        var result = await new ContentCompiler(Files).CompileAsync(new[] { second, first }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.None, result.Error); Assert.Equal(new[] { "diagnostics/first", "diagnostics/second" }, sequence);
        Assert.Equal(new[] { first.Id, second.Id }, result.Entries.Select(e => e.Manifest.ContentId));
        // Already compiled dependencies need not be regenerated in a subsequent request.
        Assert.Equal(ErrorCode.None, (await new ContentCompiler(Files).CompileAsync(new[] { second }, false, CancellationToken.None)).Error);
    }
    [Fact]
    public async Task MissingDependencyAndDuplicateNodesRejectBeforeGeneration()
    {
        var generator = new CountingGenerator(); var missing = Node(generator, deps: new[] { new string('0', 64) });
        Assert.Equal(ErrorCode.MissingContent, (await new ContentCompiler(Files).CompileAsync(new[] { missing }, false, CancellationToken.None)).Error);
        var node = Node(generator);
        Assert.Equal(ErrorCode.InvalidInput, (await new ContentCompiler(Files).CompileAsync(new[] { node, node }, false, CancellationToken.None)).Error);
        Assert.Equal(0, generator.Calls);
    }
    [Fact]
    public async Task CacheCapacityFailureLeavesNoPartialContentOrIndex()
    {
        var node = Node(); var result = await new ContentCompiler(Files, maxCacheBytes: 1).CompileAsync(new[] { node }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.LimitExceeded, result.Error); Assert.False(Directory.Exists(Files.ContentDirectory(node.Id)));
        Assert.Empty(Directory.GetDirectories(root, "*.tmp", SearchOption.AllDirectories)); Assert.False(File.Exists(Path.Combine(root, "index.json")));
    }
    [Fact]
    public async Task InvalidGeneratedBoundsRejectBeforePublication()
    {
        var node = Node(new CountingGenerator { BadBounds = true });
        var result = await new ContentCompiler(Files).CompileAsync(new[] { node }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.CorruptContent, result.Error); Assert.False(Directory.Exists(Files.ContentDirectory(node.Id)));
    }
    [Fact]
    public async Task BusyKeyLockCanTimeOutOrCancelWithoutPublication()
    {
        var node = Node(); string locks = Path.Combine(root, ".locks"); Files.CreateDirectories(locks);
        using var held = new FileStream(Path.Combine(locks, node.Id + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var compiler = new ContentCompiler(Files, TimeSpan.FromMilliseconds(50));
        Assert.Equal(ErrorCode.Busy, (await compiler.CompileAsync(new[] { node }, false, CancellationToken.None)).Error);
        using var cancelled = new CancellationTokenSource(25);
        Assert.Equal(ErrorCode.Cancelled, (await compiler.CompileAsync(new[] { node }, false, cancelled.Token)).Error);
        Assert.False(Directory.Exists(Files.ContentDirectory(node.Id)));
    }
    private sealed class CancelGenerator : IAssetGenerator
    { public GenerationResult Generate(GenerationRequest request, CancellationToken cancel) => throw new OperationCanceledException(); }
    private sealed class CountingGenerator : IAssetGenerator
    {
        public int Calls; public bool Alternate, BadBounds; private readonly List<string>? sequence;
        public CountingGenerator(List<string>? sequence = null) { this.sequence = sequence; }
        public GenerationResult Generate(GenerationRequest request, CancellationToken cancel)
        {
            Interlocked.Increment(ref Calls); sequence?.Add(request.Recipe.RecipeId.ToString()); cancel.ThrowIfCancellationRequested();
            float side = Alternate ? 2 : 1;
            var collision = new CollisionData(Array.Empty<TaggedCapsule>(), new[] { new TaggedBox(new Box(new Float3(0, 0, 0), new Float3(side, side, side), Quat.Identity), 0) }, Array.Empty<TaggedHeightGrid>());
            byte[] payload = ArtifactCodec.Encode(ContentKind.Collision, GeometryCodec.EncodeCollision(collision), GeneratorFingerprint.Compute("collision.box", "1.0.0"), request.ToolchainHash);
            var artifact = new GeneratedArtifact(ContentKind.Collision, "main", 1, payload, ContentIdentity.Hash(payload));
            var bounds = BadBounds ? new Aabb(new Float3(0, 0, 0), new Float3(0, 0, 0)) : new Aabb(new Float3(-side / 2, -side / 2, -side / 2), new Float3(side / 2, side / 2, side / 2));
            return new GenerationResult(ErrorCode.None, new[] { artifact }, bounds, Array.Empty<Diagnostic>());
        }
    }
}
