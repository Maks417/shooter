#nullable enable
using System;
using System.Collections.Generic;
using Shooter.World.Streaming;
using Xunit;

namespace Shooter.Generation.Tests
{
    public sealed class StreamingTests
    {
        [Fact]
        public void Prefetch_is_3x3_and_uploads_split_under_2ms()
        {
            var scheduler = new VisualStreamingScheduler();
            scheduler.NoteFocusKey(new ChunkKey(3, 3));
            scheduler.SetFocus(3, 3);
            Assert.Equal(9, scheduler.QueueDepth);
            scheduler.TickUpload(_ => 1024, _ => 0.75);
            Assert.True(scheduler.UploadMsThisTick <= VisualStreamingScheduler.UploadBudgetMs + 1e-9);
            Assert.True(scheduler.UploadsThisTick >= 1);
            Assert.True(scheduler.UploadsThisTick <= 3); // 0.75*3=2.25 would exceed after 2; allow 2 or 3 depending on split rule
            Assert.True(scheduler.ResidentCount >= 1);
        }

        [Fact]
        public void Cancellation_drops_superseded_chunks()
        {
            var scheduler = new VisualStreamingScheduler();
            scheduler.SetFocus(0, 0);
            Assert.Equal(9, scheduler.QueueDepth);
            scheduler.SetFocus(5, 5);
            Assert.Equal(9, scheduler.QueueDepth);
            Assert.Equal(ChunkRequestState.Queued, scheduler.GetState(new ChunkKey(5, 5)));
            Assert.Equal(ChunkRequestState.Cancelled, scheduler.GetState(new ChunkKey(0, 0)));
        }

        [Fact]
        public void Shared_handles_refcount_and_100_traversals_do_not_leak()
        {
            var scheduler = new VisualStreamingScheduler();
            var path = new List<ChunkKey>();
            for (int i = 0; i < 100; i++)
                path.Add(new ChunkKey(i % 8, (i / 8) % 8));
            int transitions = scheduler.RunTraversal(path, _ => 2048, _ => 0.5);
            Assert.Equal(100, transitions);
            Assert.Equal(0, scheduler.HandleLeakCount);
            Assert.True(scheduler.ResidentBytes <= VisualStreamingScheduler.DefaultMaxResidentBytes);
            if (scheduler.TryGetHandle(new ChunkKey(0, 0), out var handle))
            {
                int refs = handle.RefCount;
                scheduler.ReleaseHandle(handle);
                Assert.True(refs >= 1);
            }
        }

        [Fact]
        public void Teleport_fence_holds_until_focus_ready()
        {
            var scheduler = new VisualStreamingScheduler();
            scheduler.BeginTeleportFence();
            Assert.True(scheduler.TeleportFenceActive);
            scheduler.NoteFocusKey(new ChunkKey(2, 2));
            scheduler.SetFocus(2, 2);
            scheduler.TickUpload(_ => 100, _ => 0.1, gameplayCollisionPresent: true);
            Assert.False(scheduler.TeleportFenceActive);
        }

        [Fact]
        public void Residency_bounded_evicts_under_pressure()
        {
            var scheduler = new VisualStreamingScheduler(maxResidentBytes: 10_000);
            for (int i = 0; i < 20; i++)
            {
                scheduler.NoteFocusKey(new ChunkKey(i, 0));
                scheduler.SetFocus(i, 0);
                scheduler.TickUpload(_ => 3000, _ => 0.2);
            }
            Assert.True(scheduler.ResidentBytes <= 10_000);
        }
    }
}
