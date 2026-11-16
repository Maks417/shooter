#nullable enable
using System;
using System.Collections.Generic;

namespace Shooter.World.Streaming
{
    public enum ChunkRequestState { Queued = 0, Loading = 1, Ready = 2, Cancelled = 3 }

    public readonly struct ChunkKey : IEquatable<ChunkKey>
    {
        public int X { get; }
        public int Z { get; }
        public ChunkKey(int x, int z) { X = x; Z = z; }
        public bool Equals(ChunkKey other) => X == other.X && Z == other.Z;
        public override bool Equals(object? obj) => obj is ChunkKey k && Equals(k);
        public override int GetHashCode() => (X * 397) ^ Z;
        public override string ToString() => X + "," + Z;
    }

    public sealed class ChunkHandle : IDisposable
    {
        private readonly Action? release;
        private bool disposed;
        public ChunkKey Key { get; }
        public long Bytes { get; }
        public int RefCount { get; private set; }
        public ChunkHandle(ChunkKey key, long bytes, Action? release = null)
        {
            if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
            Key = key; Bytes = bytes; this.release = release; RefCount = 1;
        }
        public void AddRef()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ChunkHandle));
            RefCount++;
        }
        public void Dispose()
        {
            if (disposed) return;
            if (--RefCount > 0) return;
            disposed = true;
            release?.Invoke();
        }
    }

    /// <summary>Visual prefetch with shared handles, upload budget, cancellation and teleport fence. Gameplay map stays resident.</summary>
    public sealed class VisualStreamingScheduler
    {
        public const double UploadBudgetMs = 2.0;
        public const long DefaultMaxResidentBytes = 256L * 1024 * 1024;
        public const int PrefetchRadius = 1; // 3×3 including center

        private struct Request
        {
            public ChunkKey Key;
            public int Priority;
            public uint Generation;
        }

        private readonly long maxResidentBytes;
        private readonly Dictionary<ChunkKey, ChunkHandle> resident = new Dictionary<ChunkKey, ChunkHandle>();
        private readonly List<Request> queue = new List<Request>();
        private readonly HashSet<ChunkKey> queued = new HashSet<ChunkKey>();
        private readonly Dictionary<ChunkKey, ChunkRequestState> states = new Dictionary<ChunkKey, ChunkRequestState>();
        private long residentBytes;
        private uint generation = 1;
        private bool teleportFence;
        private int uploadsThisTick;
        private double uploadMsThisTick;
        private int handleLeaks;
        private int missingCollision;
        private ChunkKey focusKey;

        public long ResidentBytes => residentBytes;
        public int ResidentCount => resident.Count;
        public int QueueDepth => queue.Count;
        public int UploadsThisTick => uploadsThisTick;
        public double UploadMsThisTick => uploadMsThisTick;
        public bool TeleportFenceActive => teleportFence;
        public int HandleLeakCount => handleLeaks;
        public int MissingCollisionCount => missingCollision;
        public uint Generation => generation;

        public VisualStreamingScheduler(long maxResidentBytes = DefaultMaxResidentBytes)
        {
            if (maxResidentBytes <= 0 || maxResidentBytes > DefaultMaxResidentBytes)
                throw new ArgumentOutOfRangeException(nameof(maxResidentBytes));
            this.maxResidentBytes = maxResidentBytes;
        }

        public void BeginTeleportFence() => teleportFence = true;

        public void CompleteTeleportFence() => teleportFence = false;

        public void NoteFocusKey(ChunkKey key) => focusKey = key;

        public void SetFocus(int chunkX, int chunkZ, int lookAheadX = 0, int lookAheadZ = 0)
        {
            generation++;
            focusKey = new ChunkKey(chunkX + lookAheadX, chunkZ + lookAheadZ);
            var keep = new HashSet<ChunkKey>();
            for (int dz = -PrefetchRadius; dz <= PrefetchRadius; dz++)
            for (int dx = -PrefetchRadius; dx <= PrefetchRadius; dx++)
                keep.Add(new ChunkKey(chunkX + dx + lookAheadX, chunkZ + dz + lookAheadZ));

            for (int i = queue.Count - 1; i >= 0; i--)
            {
                if (!keep.Contains(queue[i].Key))
                {
                    states[queue[i].Key] = ChunkRequestState.Cancelled;
                    queued.Remove(queue[i].Key);
                    queue.RemoveAt(i);
                }
            }

            foreach (var key in keep)
            {
                if (resident.ContainsKey(key) || queued.Contains(key)) continue;
                int priority = Math.Abs(key.X - chunkX) + Math.Abs(key.Z - chunkZ);
                queue.Add(new Request { Key = key, Priority = priority, Generation = generation });
                queued.Add(key);
                states[key] = ChunkRequestState.Queued;
            }
            queue.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        /// <summary>Split uploads under 2 ms cap. Callers must provide ≤2 ms work units.</summary>
        public void TickUpload(Func<ChunkKey, long> sizeForChunk, Func<ChunkKey, double> uploadCostMs, bool gameplayCollisionPresent = true)
        {
            if (sizeForChunk == null || uploadCostMs == null) throw new ArgumentNullException(nameof(sizeForChunk));
            uploadsThisTick = 0;
            uploadMsThisTick = 0;
            if (teleportFence && !gameplayCollisionPresent)
            {
                missingCollision++;
                return;
            }
            while (queue.Count > 0)
            {
                var req = queue[0];
                double cost = uploadCostMs(req.Key);
                if (cost < 0 || double.IsNaN(cost) || double.IsInfinity(cost))
                    throw new InvalidOperationException("Invalid upload cost.");
                if (uploadsThisTick > 0 && uploadMsThisTick + cost > UploadBudgetMs) break;
                if (uploadsThisTick == 0 && cost > UploadBudgetMs)
                    throw new InvalidOperationException("Upload unit exceeds 2 ms cap; split required.");

                queue.RemoveAt(0);
                queued.Remove(req.Key);
                long bytes = sizeForChunk(req.Key);
                if (bytes < 0) throw new InvalidOperationException("Negative chunk size.");
                if (residentBytes + bytes > maxResidentBytes)
                {
                    EvictUntil(bytes);
                    if (residentBytes + bytes > maxResidentBytes)
                    {
                        states[req.Key] = ChunkRequestState.Cancelled;
                        continue;
                    }
                }

                states[req.Key] = ChunkRequestState.Loading;
                ChunkKey key = req.Key;
                long capturedBytes = bytes;
                var handle = new ChunkHandle(key, bytes, () => ReleaseResident(key, capturedBytes));
                resident[key] = handle;
                residentBytes += bytes;
                states[key] = ChunkRequestState.Ready;
                uploadsThisTick++;
                uploadMsThisTick += cost;
                if (uploadMsThisTick >= UploadBudgetMs) break;
            }

            if (teleportFence && (resident.ContainsKey(focusKey) || GetState(focusKey) == ChunkRequestState.Ready))
                CompleteTeleportFence();
        }

        public bool TryGetHandle(ChunkKey key, out ChunkHandle handle)
        {
            if (resident.TryGetValue(key, out handle!))
            {
                handle.AddRef();
                return true;
            }
            handle = null!;
            return false;
        }

        public ChunkRequestState GetState(ChunkKey key)
            => states.TryGetValue(key, out var s) ? s : ChunkRequestState.Cancelled;

        public void ReleaseHandle(ChunkHandle handle)
        {
            if (handle == null) throw new ArgumentNullException(nameof(handle));
            int before = handle.RefCount;
            handle.Dispose();
            if (before <= 0) handleLeaks++;
        }

        public int RunTraversal(IReadOnlyList<ChunkKey> path, Func<ChunkKey, long> sizeForChunk, Func<ChunkKey, double> uploadCostMs)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            int transitions = 0;
            for (int i = 0; i < path.Count; i++)
            {
                var key = path[i];
                NoteFocusKey(key);
                SetFocus(key.X, key.Z);
                // Drain queue within budget across micro-ticks so traversal always completes residency for focus.
                int guard = 0;
                while (!resident.ContainsKey(key) && queue.Count > 0 && guard++ < 64)
                    TickUpload(sizeForChunk, uploadCostMs, gameplayCollisionPresent: true);
                if (!resident.ContainsKey(key)) missingCollision++;
                transitions++;
            }
            return transitions;
        }

        private void EvictUntil(long needed)
        {
            var keys = new List<ChunkKey>(resident.Keys);
            keys.Sort((a, b) => (Math.Abs(a.X) + Math.Abs(a.Z)).CompareTo(Math.Abs(b.X) + Math.Abs(b.Z)));
            for (int i = keys.Count - 1; i >= 0 && residentBytes + needed > maxResidentBytes; i--)
            {
                var key = keys[i];
                if (!resident.TryGetValue(key, out var h)) continue;
                if (h.RefCount > 1) continue;
                h.Dispose();
            }
        }

        private void ReleaseResident(ChunkKey key, long bytes)
        {
            if (resident.Remove(key))
            {
                residentBytes -= bytes;
                if (residentBytes < 0) residentBytes = 0;
            }
        }
    }
}
