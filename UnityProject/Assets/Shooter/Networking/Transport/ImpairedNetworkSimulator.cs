#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Bootstrap;
using Shooter.Contracts;

namespace Shooter.Networking.Transport
{
    /// <summary>
    /// Seeded delay/jitter/loss/reorder/outage simulator for impairment matrices.
    /// Test/harness only; marked fake so shipping composition rejects it as the production channel.
    /// </summary>
    public sealed class ImpairedNetworkSimulator : INetworkChannel, IFakeAdapter
    {
        private readonly INetworkChannel inner;
        private readonly Random rng;
        private readonly Queue<(double deliverAt, NetworkPacket packet)> delayed = new Queue<(double, NetworkPacket)>();
        private readonly List<(double deliverAt, NetworkPacket packet)> hold = new List<(double, NetworkPacket)>();
        private double nowSeconds;
        private double outageUntil;

        public double DelaySeconds { get; set; }
        public double JitterSeconds { get; set; }
        public double LossProbability { get; set; }
        public double ReorderProbability { get; set; }
        public int MaxQueued { get; }
        public int Dropped { get; private set; }
        public int Lost { get; private set; }

        public ImpairedNetworkSimulator(INetworkChannel inner, ulong seed, int maxQueued = 256)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (maxQueued <= 0 || maxQueued > 256) throw new ArgumentOutOfRangeException(nameof(maxQueued));
            MaxQueued = maxQueued;
            rng = new Random(unchecked((int)(seed ^ (seed >> 32))));
        }

        public void AdvanceTime(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            nowSeconds += deltaSeconds;
            FlushReady();
        }

        public void BeginOutage(double durationSeconds)
        {
            if (durationSeconds < 0) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            outageUntil = Math.Max(outageUntil, nowSeconds + durationSeconds);
        }

        public bool TrySend(PeerId peer, Delivery delivery, ReadOnlyMemory<byte> payload)
        {
            if (!peer.IsValid || payload.Length == 0 || payload.Length > 1200) return false;
            if (nowSeconds < outageUntil) { Lost++; return true; }
            if (LossProbability > 0 && rng.NextDouble() < LossProbability) { Lost++; return true; }

            double delay = DelaySeconds;
            if (JitterSeconds > 0) delay += (rng.NextDouble() * 2 - 1) * JitterSeconds;
            if (delay < 0) delay = 0;
            if (ReorderProbability > 0 && rng.NextDouble() < ReorderProbability)
                delay += 0.005 + rng.NextDouble() * 0.02;

            var packet = new NetworkPacket(peer, delivery, payload.Span);
            if (hold.Count + delayed.Count >= MaxQueued) { Dropped++; return false; }
            hold.Add((nowSeconds + delay, packet));
            hold.Sort((a, b) => a.deliverAt.CompareTo(b.deliverAt));
            FlushReady();
            return true;
        }

        public DrainResult Poll(PacketBuffer destination) => inner.Poll(destination);

        private void FlushReady()
        {
            for (int i = 0; i < hold.Count;)
            {
                if (hold[i].deliverAt > nowSeconds) break;
                var packet = hold[i].packet;
                hold.RemoveAt(i);
                if (!inner.TrySend(packet.Peer, packet.Delivery, packet.Payload.ToArray()))
                    Dropped++;
            }
        }
    }
}
