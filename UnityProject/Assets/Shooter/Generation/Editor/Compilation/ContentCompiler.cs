#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>Only schema-validated recipes enter compilation; generators are explicitly injected.</summary>
    public sealed class CompilationNode
    {
        public ValidatedRecipe Source { get; }
        public GenerationRequest Request { get; }
        public IAssetGenerator Generator { get; }
        public ContentId Id { get; }
        public FrozenList<string> MaterialLayerIds { get; }
        public CompilationNode(ReadOnlySpan<byte> source, string platform, string profile, ContentId toolchainHash, IAssetGenerator generator)
            : this(ValidatedRecipe.FromBox(source), platform, profile, toolchainHash, generator) { }
        public CompilationNode(ValidatedRecipe source, string platform, string profile, ContentId toolchainHash, IAssetGenerator generator, IReadOnlyList<string>? materialLayerIds = null)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Request = new GenerationRequest(Source.Document, platform, profile, toolchainHash);
            MaterialLayerIds = new FrozenList<string>(materialLayerIds ?? Array.Empty<string>(), 256);
            Generator = generator ?? throw new ArgumentNullException(nameof(generator));
            Id = ContentIdentity.ContentKey(Source.SourceHash, Source.Document.GeneratorId, Source.Document.GeneratorVersion,
                Source.Document.SeedValue, Source.Document.Dependencies, platform, profile, 1, toolchainHash);
        }
    }
    public sealed class CompilationEntry
    {
        public ContentManifest Manifest { get; }
        public bool Reused { get; }
        public double ElapsedMilliseconds { get; }
        public CompilationEntry(ContentManifest manifest, bool reused, double elapsedMilliseconds)
        { Manifest = manifest; Reused = reused; ElapsedMilliseconds = elapsedMilliseconds; }
    }
    public sealed class CompilationResult
    {
        public ErrorCode Error { get; }
        public string Diagnostic { get; }
        public FrozenList<CompilationEntry> Entries { get; }
        internal CompilationResult(ErrorCode error, string diagnostic, List<CompilationEntry> entries)
        { Error = error; Diagnostic = diagnostic.Length <= 512 ? diagnostic : diagnostic.Substring(0, 512); Entries = new FrozenList<CompilationEntry>(entries, DependencyOrder.MaxNodes); }
    }

    /// <summary>Editor/tools-only CPU orchestration. Never called by gameplay or the runtime store.</summary>
    public sealed class ContentCompiler
    {
        private sealed class Failure : Exception
        {
            public ErrorCode Error { get; }
            public Failure(ErrorCode error, string message) : base(message) { Error = error; }
        }
        private readonly CacheFiles files;
        private readonly TimeSpan lockTimeout;
        private readonly long maxCacheBytes;
        public ContentCompiler(CacheFiles files, TimeSpan? lockTimeout = null, long maxCacheBytes = 8L * 1024 * 1024 * 1024)
        {
            this.files = files ?? throw new ArgumentNullException(nameof(files));
            this.lockTimeout = lockTimeout ?? TimeSpan.FromSeconds(60);
            if (this.lockTimeout <= TimeSpan.Zero || maxCacheBytes <= 0) throw new ArgumentOutOfRangeException(nameof(lockTimeout));
            this.maxCacheBytes = maxCacheBytes;
        }

        public async Task<CompilationResult> CompileAsync(IReadOnlyList<CompilationNode> nodes, bool force, CancellationToken cancel)
        {
            var entries = new List<CompilationEntry>();
            try
            {
                cancel.ThrowIfCancellationRequested();
                if (nodes == null || nodes.Count == 0 || nodes.Count > DependencyOrder.MaxNodes) throw new ArgumentException("Expected 1..4096 compilation nodes.");
                var byId = new Dictionary<ContentId, CompilationNode>();
                var graph = new Dictionary<ContentId, IReadOnlyList<ContentId>>();
                foreach (var node in nodes)
                {
                    if (node == null || byId.ContainsKey(node.Id)) throw new ArgumentException("Null/duplicate compilation node.");
                    if (node.Request.ToolchainHash != files.ExpectedToolchainHash) throw new Failure(ErrorCode.UnsupportedVersion, "Compiler request uses a different toolchain than the cache.");
                    byId.Add(node.Id, node); graph.Add(node.Id, node.Request.Recipe.Dependencies);
                }
                foreach (var node in nodes)
                    foreach (var dependency in node.Request.Recipe.Dependencies)
                        if (!graph.ContainsKey(dependency))
                        {
                            if (graph.Count >= DependencyOrder.MaxNodes) throw new Failure(ErrorCode.LimitExceeded, "Dependency graph node limit.");
                            var external = files.ReadManifest(dependency); files.VerifyContent(external);
                            // External content must already be a completely published dependency closure.
                            ValidateExternal(external, new HashSet<ContentId>(), cancel);
                            graph.Add(dependency, Array.Empty<ContentId>());
                        }
                foreach (ContentId id in DependencyOrder.Sort(graph))
                {
                    cancel.ThrowIfCancellationRequested();
                    if (byId.TryGetValue(id, out var node)) entries.Add(await CompileOne(node, force, cancel).ConfigureAwait(false));
                }
                cancel.ThrowIfCancellationRequested();
                await PublishIndex(entries, cancel).ConfigureAwait(false);
                return new CompilationResult(ErrorCode.None, "", entries);
            }
            catch (OperationCanceledException) { return new CompilationResult(ErrorCode.Cancelled, "Compilation cancelled; no incomplete content or index published.", entries); }
            catch (Failure error) { return new CompilationResult(error.Error, error.Message, entries); }
            catch (CacheReadException error) { return new CompilationResult(error.Error, error.Message, entries); }
            catch (TimeoutException error) { return new CompilationResult(ErrorCode.Busy, error.Message, entries); }
            catch (FileNotFoundException error) { return new CompilationResult(ErrorCode.MissingContent, error.Message, entries); }
            catch (DirectoryNotFoundException error) { return new CompilationResult(ErrorCode.MissingContent, error.Message, entries); }
            catch (FormatException error) { return new CompilationResult(ErrorCode.CorruptContent, error.Message, entries); }
            catch (ArgumentException error) { return new CompilationResult(ErrorCode.InvalidInput, error.Message, entries); }
            catch (IOException error) { return new CompilationResult(ErrorCode.CorruptContent, error.Message, entries); }
            catch (UnauthorizedAccessException error) { return new CompilationResult(ErrorCode.InvalidInput, error.Message, entries); }
            catch (Exception error) { return new CompilationResult(ErrorCode.InternalError, error.GetType().Name + ": " + error.Message, entries); }
        }

        private void ValidateExternal(ContentManifest manifest, HashSet<ContentId> visited, CancellationToken cancel)
        {
            // Iterative closure walk avoids stack exhaustion on malformed manifests.
            var pending = new Stack<ContentManifest>(); pending.Push(manifest);
            while (pending.Count != 0)
            {
                cancel.ThrowIfCancellationRequested();
                var next = pending.Pop();
                if (!visited.Add(next.ContentId)) continue;
                if (visited.Count > DependencyOrder.MaxNodes) throw new ArgumentException("Dependency closure limit.");
                foreach (var id in next.Dependencies)
                {
                    var dependency = files.ReadManifest(id); files.VerifyContent(dependency);
                    pending.Push(dependency);
                }
            }
        }

        private async Task<CompilationEntry> CompileOne(CompilationNode node, bool force, CancellationToken cancel)
        {
            var clock = Stopwatch.StartNew();
            using var keyLock = await Lock(node.Id.ToString(), cancel).ConfigureAwait(false);
            string destination = files.ContentDirectory(node.Id);
            files.AssertSafePath(destination);
            bool exists = Directory.Exists(destination);
            ContentManifest? previous = null;
            if (exists)
            {
                previous = files.ReadManifest(node.Id); files.VerifyContent(previous);
                if (!force) return new CompilationEntry(previous, true, clock.Elapsed.TotalMilliseconds);
            }
            cancel.ThrowIfCancellationRequested();
            GenerationResult result = node.Generator.Generate(node.Request, cancel);
            cancel.ThrowIfCancellationRequested();
            if (result.Error != ErrorCode.None) throw new Failure(result.Error, "Generator rejected recipe: " + result.Error);
            var manifest = ContentManifest.Create(node.Request, node.Source.SourceHash, result, node.MaterialLayerIds);
            if (manifest.ContentId != node.Id) throw new FormatException("Compiler identity mismatch.");
            byte[] manifestBytes = manifest.Encode();
            if (previous != null)
            {
                if (!previous.Encode().AsSpan().SequenceEqual(manifestBytes)) throw new FormatException("Reproducibility failure: same key produced a different manifest.");
                foreach (var artifact in manifest.Artifacts)
                {
                    var output = result.Artifacts.Single(a => a.Kind == artifact.Kind && a.Variant == artifact.Variant);
                    var priorArtifact = previous.Artifacts.Single(a => a.Path == artifact.Path);
                    if (!output.Payload.SequenceEqual(files.ReadArtifact(previous, priorArtifact))) throw new FormatException("Reproducibility failure: same key produced different bytes.");
                }
                return new CompilationEntry(previous, true, clock.Elapsed.TotalMilliseconds);
            }
            string parent = Path.GetDirectoryName(destination)!;
            files.CreateDirectories(parent);
            string stage = Path.Combine(parent, "." + node.Id + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                files.CreateDirectories(stage);
                foreach (var artifact in manifest.Artifacts)
                {
                    cancel.ThrowIfCancellationRequested();
                    var output = result.Artifacts.Single(a => a.Kind == artifact.Kind && a.Variant == artifact.Variant);
                    WriteDurable(Path.Combine(stage, artifact.Path), output.Payload.ToArray());
                }
                WriteDurable(Path.Combine(stage, "manifest.json"), manifestBytes);
                // Fresh bytes are re-read/hash-checked before a same-volume directory rename.
                foreach (var artifact in manifest.Artifacts)
                {
                    string path = Path.Combine(stage, artifact.Path); files.AssertSafePath(path);
                    using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (input.Length != artifact.Bytes || input.Length > ContractVersion.MaxArtifactBytes) throw new FormatException("Staged artifact size mismatch.");
                    byte[] payload = new byte[artifact.Bytes];
                    int offset = 0;
                    while (offset < payload.Length)
                    {
                        int read = input.Read(payload, offset, payload.Length - offset);
                        if (read == 0) throw new FormatException("Truncated staged artifact.");
                        offset += read;
                    }
                    if (input.ReadByte() != -1) throw new FormatException("Staged artifact grew during verification.");
                    if (ContentIdentity.Hash(payload) != artifact.Sha256 || payload.Length != artifact.Bytes) throw new FormatException("Staged artifact verification failed.");
                }
                using var publication = await Lock("publication", cancel).ConfigureAwait(false);
                if (CacheBytes() > maxCacheBytes) throw new Failure(ErrorCode.LimitExceeded, "Local cache capacity exceeded; no automatic deletion is performed.");
                cancel.ThrowIfCancellationRequested();
                files.AssertSafePath(stage); files.AssertSafePath(destination);
                Directory.Move(stage, destination);
                return new CompilationEntry(manifest, false, clock.Elapsed.TotalMilliseconds);
            }
            finally
            {
                // This unique, compiler-created staging path is the only recursive cleanup target.
                if (Directory.Exists(stage)) { files.AssertSafePath(stage); AssertOwnedStage(stage, parent, node.Id); Directory.Delete(stage, true); }
            }
        }

        private async Task<FileStream> Lock(string key, CancellationToken cancel)
        {
            string directory = Path.Combine(files.Root, ".locks"); files.CreateDirectories(directory);
            string path = Path.Combine(directory, key + ".lock"); var elapsed = Stopwatch.StartNew();
            while (true)
            {
                cancel.ThrowIfCancellationRequested(); files.AssertSafePath(path);
                try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException) when (elapsed.Elapsed < lockTimeout) { await Task.Delay(25, cancel).ConfigureAwait(false); }
                catch (IOException) { throw new TimeoutException("Cache lock timeout: " + key); }
            }
            // OS exclusive handles are released on process exit. Persistent empty lock files are never treated as stale ownership or deleted.
        }

        private async Task PublishIndex(List<CompilationEntry> entries, CancellationToken cancel)
        {
            using var indexLock = await Lock("index", cancel).ConfigureAwait(false);
            string target = Path.Combine(files.Root, "index.json");
            string temp = Path.Combine(files.Root, ".index-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                foreach (var entry in entries) files.VerifyContent(files.ReadManifest(entry.Manifest.ContentId));
                string json = "{\"schema\":\"shooter.content.index.v1\",\"contentIds\":[" + string.Join(",", entries.Select(e => e.Manifest.ContentId).OrderBy(id => id).Select(id => "\"" + id + "\"")) + "]}";
                WriteDurable(temp, CanonicalJson.Encode(Encoding.UTF8.GetBytes(json)));
                cancel.ThrowIfCancellationRequested(); files.AssertSafePath(target);
                if (File.Exists(target)) File.Replace(temp, target, null); else File.Move(temp, target);
            }
            finally { if (File.Exists(temp)) { files.AssertSafePath(temp); File.Delete(temp); } }
        }

        private void WriteDurable(string path, byte[] bytes)
        {
            files.AssertSafePath(path);
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
        }
        private long CacheBytes()
        {
            long bytes = 0; int visited = 0; var pending = new Stack<string>(); pending.Push(files.Root);
            while (pending.Count != 0)
            {
                string dir = pending.Pop(); files.AssertSafePath(dir);
                foreach (string path in Directory.EnumerateFileSystemEntries(dir))
                {
                    if (++visited > 1000000) throw new ArgumentException("Cache entry-count limit.");
                    files.AssertSafePath(path);
                    if (Directory.Exists(path)) pending.Push(path); else bytes = checked(bytes + new FileInfo(path).Length);
                    if (bytes > maxCacheBytes) return bytes;
                }
            }
            return bytes;
        }
        private static void AssertOwnedStage(string stage, string parent, ContentId id)
        {
            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(stage)), Path.GetFullPath(parent), StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(stage).StartsWith("." + id + ".", StringComparison.Ordinal) || !stage.EndsWith(".tmp", StringComparison.Ordinal))
                throw new IOException("Refusing cleanup outside owned staging directory.");
        }
    }
}
