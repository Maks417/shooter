#nullable enable
using System.Collections.Generic;
using Shooter.Bootstrap;
using Shooter.Contracts;
using Shooter.Networking.Codec;
using Shooter.Networking.Session;
using Shooter.Networking.Transport;
using Xunit;

namespace Shooter.Simulation.Tests.Network.Session
{
    public sealed class SessionCommandTests
    {
        private static ContentId ZeroHash => new ContentId("0000000000000000000000000000000000000000000000000000000000000000");

        [Fact]
        public void Mismatch_rejected_before_spawn_and_duplicate_commands_rejected()
        {
            var manifest = new MatchManifest(NetworkTransportSelection.WireProtocolVersion, "build-m5", ZeroHash, ZeroHash, 460072030UL);
            var host = new DedicatedSessionHost(manifest, sessionToken: 42);
            var good = SessionCodec.EncodeHandshake(manifest, new PeerId(9), new EntityId(1), 42);
            Assert.Equal(HandshakeStatus.Accepted, host.TryAcceptJoin(good.Span, out _));

            var badManifest = new MatchManifest(NetworkTransportSelection.WireProtocolVersion, "other-build", ZeroHash, ZeroHash, 460072030UL);
            var host2 = new DedicatedSessionHost(manifest, 42);
            var bad = SessionCodec.EncodeHandshake(badManifest, new PeerId(9), new EntityId(1), 42);
            Assert.Equal(HandshakeStatus.Rejected, host2.TryAcceptJoin(bad.Span, out var reason));
            Assert.Equal("build", reason);
            Assert.False(host2.HasJoinedPeer);

            var cmd = new InputCommand(new EntityId(1), 10, 1, 0, 1, 0, 0, InputButtons.None, 0);
            Assert.True(host.TryAcceptCommand(new PeerId(9), 10, cmd, out _));
            Assert.False(host.TryAcceptCommand(new PeerId(9), 10, cmd, out var dupReason));
            Assert.Equal("duplicate-or-stale-sequence", dupReason);

            var forged = new InputCommand(new EntityId(1), 11, 2, 0, 1, 0, 0, InputButtons.FirePressed, 0);
            Assert.False(host.TryAcceptCommand(new PeerId(99), 11, forged, out var ownReason));
            Assert.Equal("ownership", ownReason);
        }

        [Fact]
        public void Future_and_stale_ticks_rejected()
        {
            var manifest = new MatchManifest(NetworkTransportSelection.WireProtocolVersion, "build-m5", ZeroHash, ZeroHash, 1UL);
            var host = new DedicatedSessionHost(manifest, 1);
            Assert.Equal(HandshakeStatus.Accepted, host.TryAcceptJoin(
                SessionCodec.EncodeHandshake(manifest, new PeerId(1), new EntityId(1), 1).Span, out _));

            var future = new InputCommand(new EntityId(1), 100, 1, 0, 0, 0, 0, InputButtons.None, 0);
            Assert.False(host.TryAcceptCommand(new PeerId(1), 10, future, out var futureReason));
            Assert.Equal("future", futureReason);

            var stale = new InputCommand(new EntityId(1), 1, 2, 0, 0, 0, 0, InputButtons.None, 0);
            Assert.False(host.TryAcceptCommand(new PeerId(1), 30, stale, out var staleReason));
            Assert.Equal("stale", staleReason);
        }

        [Fact]
        public void Command_batcher_emits_redundancy_and_shipping_rejects_impaired_fake()
        {
            var batcher = new CommandBatcher();
            for (uint i = 1; i <= 3; i++)
                batcher.Push(new InputCommand(new EntityId(1), i, i, 0, 1, 0, 0, InputButtons.None, 0));
            Assert.True(batcher.TryBuildBatch(out List<InputCommand> batch));
            Assert.True(batch.Count >= 3);

            INetworkChannel impaired = new ImpairedNetworkSimulator(new LoopbackNetworkChannel(), 460072030UL);
            Assert.Throws<System.InvalidOperationException>(() => CompositionPolicy.RequireReal(impaired));
            CompositionPolicy.RequireReal(new LoopbackNetworkChannel());
        }
    }
}
