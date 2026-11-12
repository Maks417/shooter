#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Contracts;
using Shooter.Generation;

namespace Shooter.Content
{
    /// <summary>Bounded byte-only leases. Unity object installation/release belongs to a later main-thread adapter.</summary>
    public sealed class FileContentStore : IContentStore, IDisposable
    {
        private sealed class Loaded
        {
            public ContentManifest Manifest { get; }
            public Dictionary<ManifestArtifact, FrozenList<byte>> Payloads { get; }
            public long Bytes { get; }
            public int Leases;
            public Loaded(ContentManifest manifest, Dictionary<ManifestArtifact, FrozenList<byte>> payloads)
            { Manifest = manifest; Payloads = payloads; Bytes = payloads.Sum(p => (long)p.Value.Count); }
        }
        private sealed class Pending
        {
            public Task<Loaded> Task = null!;
            public int Waiters;
            public long ReservedBytes;
        }
        private readonly CacheFiles files;
        private readonly object sync = new object();
        private readonly long maxResidentBytes;
        private readonly int maxInflight, maxWaiters;
        private readonly Dictionary<ContentId, Pending> pending = new Dictionary<ContentId, Pending>();
        private readonly Dictionary<ContentId, Loaded> resident = new Dictionary<ContentId, Loaded>();
        private bool disposed;
        private long residentBytes;
        private long loadingBytes;
        private int waiters, activeLeases, loadsStarted;
        public long ResidentBytes { get { lock (sync) return residentBytes; } }
        public int ActiveLeases { get { lock (sync) return activeLeases; } }
        public int LoadsStarted { get { lock (sync) return loadsStarted; } }
        public int PendingLoads { get { lock (sync) return pending.Count; } }
        public int PendingWaiters { get { lock (sync) return waiters; } }
        public long ReservedBytes { get { lock (sync) return loadingBytes; } }

        public FileContentStore(CacheFiles files, long maxResidentBytes = 256L * 1024 * 1024, int maxInflight = 64, int maxWaiters = 256)
        {
            this.files = files ?? throw new ArgumentNullException(nameof(files));
            if (maxResidentBytes <= 0 || maxResidentBytes > 256L * 1024 * 1024 || maxInflight <= 0 || maxInflight > 64 || maxWaiters <= 0 || maxWaiters > 256)
                throw new ArgumentOutOfRangeException("Content store bounds.");
            this.maxResidentBytes = maxResidentBytes; this.maxInflight = maxInflight; this.maxWaiters = maxWaiters;
        }

        public Task<ContentLeaseResult> AcquireAsync(ContentId id, ContentKind kind, CancellationToken cancel)
            => Acquire(id, kind, null, cancel);

        public Task<ContentLeaseResult> AcquireVariantAsync(ContentId id, ContentKind kind, string variant, CancellationToken cancel)
        {
            if (string.IsNullOrEmpty(variant)) return System.Threading.Tasks.Task.FromResult(Failure(ErrorCode.InvalidInput, "A variant is required."));
            return Acquire(id, kind, variant, cancel);
        }

        private async Task<ContentLeaseResult> Acquire(ContentId id, ContentKind kind, string? variant, CancellationToken cancel)
        {
            if (!Enum.IsDefined(typeof(ContentKind), kind)) return Failure(ErrorCode.InvalidInput, "Unknown content kind.");
            Pending work;
            lock (sync)
            {
                if (cancel.IsCancellationRequested) return Failure(ErrorCode.Cancelled, "Content request cancelled.");
                if (disposed) return Failure(ErrorCode.Busy, "Content store is disposed.");
                if (resident.TryGetValue(id, out var existing)) return Lease(id, kind, variant, existing);
                if (waiters >= maxWaiters) return Failure(ErrorCode.Busy, "Content waiter limit reached.");
                if (!pending.TryGetValue(id, out work!))
                {
                    if (pending.Count >= maxInflight) return Failure(ErrorCode.Busy, "Content IO limit reached.");
                    work = new Pending();
                    Pending started = work;
                    work.Task = System.Threading.Tasks.Task.Run(() => Load(id, started));
                    pending.Add(id, work); loadsStarted++;
                }
                work.Waiters++; waiters++;
                _ = work.Task.ContinueWith(_ => RetireCompleted(id, work), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            try
            {
                Loaded content = await WaitForConsumer(work.Task, cancel).ConfigureAwait(false);
                lock (sync)
                {
                    if (cancel.IsCancellationRequested) return Failure(ErrorCode.Cancelled, "Content request cancelled.");
                    if (disposed) return Failure(ErrorCode.Busy, "Content store is disposed.");
                    if (!resident.TryGetValue(id, out var installed))
                    {
                        if (content.Bytes > maxResidentBytes - residentBytes - loadingBytes + work.ReservedBytes) return Failure(ErrorCode.LimitExceeded, "Content residency limit reached.");
                        ReleaseReservation(work);
                        installed = content; resident.Add(id, content); residentBytes += content.Bytes;
                    }
                    ContentLeaseResult result = Lease(id, kind, variant, installed);
                    if (installed.Leases == 0) Evict(id, installed);
                    return result;
                }
            }
            catch (OperationCanceledException) { return Failure(ErrorCode.Cancelled, "Content request cancelled."); }
            catch (CacheReadException error) { return Failure(error.Error, error.Message); }
            catch (FileNotFoundException) { return Failure(ErrorCode.MissingContent, "Content file is missing."); }
            catch (DirectoryNotFoundException) { return Failure(ErrorCode.MissingContent, "Content directory is missing."); }
            catch (Exception error) when (error is FormatException || error is ArgumentException || error is OverflowException || error is EndOfStreamException)
            { return Failure(ErrorCode.CorruptContent, "Invalid content data."); }
            catch (IOException) { return Failure(ErrorCode.CorruptContent, "Content file IO failed."); }
            catch (UnauthorizedAccessException) { return Failure(ErrorCode.CorruptContent, "Content file access failed."); }
            catch (Exception) { return Failure(ErrorCode.InternalError, "Content adapter failed."); }
            finally
            {
                lock (sync)
                {
                    work.Waiters--; waiters--;
                    if (work.Waiters == 0 && work.Task.IsCompleted) { ReleaseReservation(work); pending.Remove(id); }
                }
            }
        }

        private Loaded Load(ContentId id, Pending work)
        {
            ContentManifest manifest = files.ReadManifest(id);
            // Bound the requested bytes before payload allocations, including residency rejected loads.
            long bytes = manifest.Artifacts.Sum(a => (long)a.Bytes);
            lock (sync)
            {
                if (disposed) throw new CacheReadException(ErrorCode.Busy, "Content store is disposed.");
                if (bytes > maxResidentBytes - residentBytes - loadingBytes) throw new CacheReadException(ErrorCode.LimitExceeded, "Content exceeds store residency budget.");
                loadingBytes += bytes; work.ReservedBytes = bytes;
            }
            return new Loaded(manifest, files.ReadAll(manifest));
        }

        private ContentLeaseResult Lease(ContentId id, ContentKind kind, string? variant, Loaded content)
        {
            var matches = content.Manifest.Artifacts.Where(a => a.Kind == kind && (variant == null || a.Variant == variant)).ToArray();
            if (matches.Length == 0) return Failure(ErrorCode.MissingContent, "Requested content kind/variant is missing.");
            if (matches.Length != 1) return Failure(ErrorCode.InvalidInput, "Several variants match; request an explicit variant.");
            content.Leases++; activeLeases++;
            var lease = new ContentLease(id, kind, content.Payloads[matches[0]], () => Release(id, content));
            return new ContentLeaseResult(ErrorCode.None, lease, "");
        }

        private void Release(ContentId id, Loaded content)
        {
            lock (sync)
            {
                content.Leases--; activeLeases--;
                if (content.Leases == 0) Evict(id, content);
            }
        }
        private void Evict(ContentId id, Loaded content)
        {
            if (resident.TryGetValue(id, out var installed) && ReferenceEquals(content, installed))
            { resident.Remove(id); residentBytes -= content.Bytes; }
        }
        private void RetireCompleted(ContentId id, Pending work)
        {
            // Observe failed abandoned loads even when every consumer cancelled.
            _ = work.Task.Exception;
            lock (sync) if (work.Waiters == 0 && pending.TryGetValue(id, out var current) && ReferenceEquals(work, current)) { ReleaseReservation(work); pending.Remove(id); }
        }
        private void ReleaseReservation(Pending work) { loadingBytes -= work.ReservedBytes; work.ReservedBytes = 0; }
        private static async Task<T> WaitForConsumer<T>(Task<T> task, CancellationToken cancel)
        {
            if (!cancel.CanBeCanceled) return await task.ConfigureAwait(false);
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancel.Register(() => cancelled.TrySetResult(true)))
            {
                if (await System.Threading.Tasks.Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task) throw new OperationCanceledException(cancel);
                cancel.ThrowIfCancellationRequested();
                return await task.ConfigureAwait(false);
            }
        }
        private static ContentLeaseResult Failure(ErrorCode error, string message)
            => new ContentLeaseResult(error, null, message.Length <= 512 ? message : message.Substring(0, 512));

        public void Dispose()
        {
            lock (sync) { disposed = true; }
            // Existing leases retain their verified bytes; final Dispose releases residency normally.
        }
    }
}
