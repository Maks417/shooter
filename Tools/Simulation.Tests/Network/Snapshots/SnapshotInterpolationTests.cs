#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Networking.Codec;
using Shooter.Networking.Snapshots;
using Shooter.Networking.Transport;
using Xunit;

namespace Shooter.Simulation.Tests.Network.Snapshots
{
    public sealed class SnapshotInterpolationTests
    {
        [Fact]
        public void Reorder_and_loss_keep_buffer_bounded_and_resync_rate_limited()
        {
            var buffer = new RemoteInterpolationBuffer();
            var id = new EntityId(2);
            for (int i = 0; i < 80; i++)
            {
                double t = (i % 2 == 0) ? i * 0.05 : (79 - i) * 0.05; // reorder pattern
                buffer.Push(t, new RemotePose(id, new Float3(i, 0, 0), 0, 0, new Float3(1, 0, 0)));
            }
            Assert.True(buffer.Count <= RemoteInterpolationBuffer.MaxQueuedSnapshots);
            Assert.True(buffer.Dropped > 0);
            Assert.True(buffer.TrySample(2.0, out var pose));
            Assert.Equal(id, pose.Id);

            var baselines = new DeltaBaselineStore();
            var weapon = new WeaponState(new DefinitionId("weapons/rifle"), WeaponAction.Ready, 10, 10, 0, 0, 0, 0, false);
            var entity = new EntitySnapshot(id, new Float3(0, 0, 0), 0, default, Stance.Standing, 100, 1, 0, weapon, 0, 0, 1);
            var snap = new SimulationSnapshot(1, 1, MatchState.Playing, default, new[] { entity }, Array.Empty<HitVolume>());
            for (uint t = 1; t <= 20; t++)
                baselines.Store(new SimulationSnapshot(t, 1, MatchState.Playing, default, new[] { entity }, Array.Empty<HitVolume>()));
            Assert.True(baselines.TryGet(20, out _));
            Assert.False(baselines.TryGet(1, out _)); // oldest baseline evicted

            Assert.True(baselines.TryRequestResync(0, out bool allowed1));
            Assert.True(allowed1);
            Assert.True(baselines.TryRequestResync(0.5, out bool allowed2));
            Assert.False(allowed2);
        }

        [Fact]
        public void Extrapolation_caps_then_holds_and_impairment_recovers()
        {
            var buffer = new RemoteInterpolationBuffer();
            var id = new EntityId(3);
            buffer.Push(1.0, new RemotePose(id, new Float3(0, 0, 0), 0, 0, new Float3(2, 0, 0)));
            Assert.True(buffer.TrySample(1.0 + RemoteInterpolationBuffer.DefaultBufferSeconds + 0.05, out var extrapolated));
            Assert.True(extrapolated.Position.X > 0);

            // Beyond max extrapolation the pose holds at the last sample (no runaway drift).
            Assert.True(buffer.TrySample(1.0 + RemoteInterpolationBuffer.DefaultBufferSeconds + 0.5, out var held));
            Assert.Equal(0f, held.Position.X, 3);

            var pair = new LoopbackPair(new PeerId(1), new PeerId(2));
            var impaired = new ImpairedNetworkSimulator(pair.Server, 460072030UL)
            {
                DelaySeconds = 0.05,
                JitterSeconds = 0.01,
                LossProbability = 0.03
            };
            impaired.BeginOutage(0.1);
            Assert.True(impaired.TrySend(new PeerId(2), Delivery.UnreliableSequenced, new byte[] { 1, 2, 3 }));
            impaired.AdvanceTime(0.2);
            var payload = SnapshotCodec.EncodeAck(1, 2);
            Assert.True(impaired.TrySend(new PeerId(2), Delivery.ReliableOrdered, payload));
            impaired.AdvanceTime(0.2);
            var dest = new PacketBuffer();
            var drain = pair.Server.Poll(dest);
            Assert.Equal(ErrorCode.None, drain.Error);
            Assert.True(drain.WrittenCount >= 1);
        }
    }
}
