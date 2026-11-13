#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Networking.Transport
{
    /// <summary>
    /// Real in-process duplex channel for dedicated-host loopback and pure tests.
    /// Not a fake: shipping local/dedicated compositions may use it; UTP wraps the same port for remote peers.
    /// </summary>
    public sealed class LoopbackNetworkChannel : INetworkChannel
    {
        private readonly object gate = new object();
        private readonly Queue<NetworkPacket> inbound = new Queue<NetworkPacket>();
        private readonly int maxQueued;
        private int dropped;

        public LoopbackNetworkChannel(int maxQueued = 256)
        {
            if (maxQueued <= 0 || maxQueued > 256) throw new ArgumentOutOfRangeException(nameof(maxQueued));
            this.maxQueued = maxQueued;
        }

        public int Dropped => dropped;
        public int Queued
        {
            get { lock (gate) return inbound.Count; }
        }

        public bool TrySend(PeerId peer, Delivery delivery, ReadOnlyMemory<byte> payload)
        {
            if (!peer.IsValid) return false;
            if (payload.Length == 0 || payload.Length > 1200) return false;
            var packet = new NetworkPacket(peer, delivery, payload.Span);
            lock (gate)
            {
                if (inbound.Count >= maxQueued)
                {
                    dropped++;
                    return false;
                }
                inbound.Enqueue(packet);
                return true;
            }
        }

        /// <summary>Inject a packet as if received from the peer (paired loopback wiring).</summary>
        public bool TryReceiveInject(PeerId peer, Delivery delivery, ReadOnlyMemory<byte> payload) =>
            TrySend(peer, delivery, payload);

        public DrainResult Poll(PacketBuffer destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            lock (gate)
            {
                int written = 0;
                int available = inbound.Count;
                int capacity = destination.Capacity;
                while (written < capacity && inbound.Count > 0)
                {
                    destination[written] = inbound.Dequeue();
                    written++;
                }
                if (available > capacity)
                    return new DrainResult(ErrorCode.LimitExceeded, 0, available);
                return new DrainResult(ErrorCode.None, written, written);
            }
        }

        public void Clear()
        {
            lock (gate) inbound.Clear();
        }
    }

    /// <summary>Wires two channels so each send lands in the peer's receive queue.</summary>
    public sealed class LoopbackPair
    {
        public LoopbackNetworkChannel Server { get; }
        public LoopbackNetworkChannel Client { get; }
        public PeerId ServerPeer { get; }
        public PeerId ClientPeer { get; }

        public LoopbackPair(PeerId serverPeer, PeerId clientPeer, int maxQueued = 256)
        {
            ServerPeer = serverPeer;
            ClientPeer = clientPeer;
            Server = new LoopbackNetworkChannel(maxQueued);
            Client = new LoopbackNetworkChannel(maxQueued);
        }

        public bool ClientToServer(Delivery delivery, ReadOnlyMemory<byte> payload) =>
            Server.TryReceiveInject(ClientPeer, delivery, payload);

        public bool ServerToClient(Delivery delivery, ReadOnlyMemory<byte> payload) =>
            Client.TryReceiveInject(ServerPeer, delivery, payload);
    }
}
