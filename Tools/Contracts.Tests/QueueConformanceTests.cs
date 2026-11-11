using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Xunit;

namespace Shooter.Contracts.Tests
{
    internal sealed class FixtureCommandSource : ICommandSource
    {
        private readonly Queue<InputCommand> queue = new Queue<InputCommand>();
        public bool TryEnqueue(InputCommand command)
        { if (!command.IsValid || queue.Count == 256) return false; queue.Enqueue(command); return true; }
        public DrainResult Drain(uint tick, CommandBuffer destination)
        {
            int count = queue.Count;
            if (count > destination.Capacity) return new DrainResult(ErrorCode.LimitExceeded, 0, count);
            for (int i = 0; i < count; i++) destination[i] = queue.Dequeue();
            return new DrainResult(ErrorCode.None, count, count);
        }
    }
    internal sealed class FixtureNetwork : INetworkChannel
    {
        private readonly Queue<NetworkPacket> queue = new Queue<NetworkPacket>();
        public bool TrySend(PeerId peer, Delivery delivery, ReadOnlyMemory<byte> payload)
        {
            if (!peer.IsValid || (delivery != Delivery.ReliableOrdered && delivery != Delivery.UnreliableSequenced) || payload.Length > 1200 || queue.Count >= 256) return false;
            queue.Enqueue(new NetworkPacket(peer, delivery, payload.Span)); return true;
        }
        public DrainResult Poll(PacketBuffer destination)
        {
            int count = queue.Count;
            if (count > destination.Capacity) return new DrainResult(ErrorCode.LimitExceeded, 0, count);
            for (int i = 0; i < count; i++) destination[i] = queue.Dequeue();
            return new DrainResult(ErrorCode.None, count, count);
        }
    }
    public sealed class QueueConformanceTests
    {
        [Fact]
        public void CommandOverflowAllowsBoundedRetryWithoutSilentlyDroppingInputs()
        {
            var source = new FixtureCommandSource();
            for (int i = 0; i < 256; i++) Assert.True(source.TryEnqueue(ValueAndFixtureTests.Command()));
            Assert.False(source.TryEnqueue(ValueAndFixtureTests.Command()));
            var failed = source.Drain(0, new CommandBuffer(1)); Assert.Equal(ErrorCode.LimitExceeded, failed.Error); Assert.Equal(0, failed.WrittenCount); Assert.Equal(256, failed.RequiredCount);
            var retry = new CommandBuffer(); var success = source.Drain(0, retry); Assert.Equal(256, success.WrittenCount); Assert.True(retry[255].IsValid);
            Assert.Equal(0, source.Drain(0, retry).WrittenCount);
        }
        [Fact]
        public void NetworkCopiesAcceptedPayloadAndAppliesBoundedBackpressure()
        {
            var channel = new FixtureNetwork(); byte[] payload = { 1, 2, 3 };
            Assert.True(channel.TrySend(new PeerId(1), Delivery.ReliableOrdered, payload)); payload[0] = 99;
            for (int i = 1; i < 256; i++) Assert.True(channel.TrySend(new PeerId(1), Delivery.UnreliableSequenced, payload));
            Assert.False(channel.TrySend(new PeerId(1), Delivery.ReliableOrdered, payload));
            var failed = channel.Poll(new PacketBuffer(1)); Assert.Equal(ErrorCode.LimitExceeded, failed.Error); Assert.Equal(0, failed.WrittenCount); Assert.Equal(256, failed.RequiredCount);
            var packets = new PacketBuffer(); Assert.Equal(256, channel.Poll(packets).WrittenCount); Assert.Equal(1, packets[0].Payload[0]); Assert.Equal(99, packets[1].Payload[0]);
            Assert.Equal(0, channel.Poll(packets).WrittenCount);
            Assert.False(channel.TrySend(new PeerId(1), Delivery.ReliableOrdered, new byte[1201])); Assert.False(channel.TrySend(default, Delivery.ReliableOrdered, payload));
        }
    }
}
