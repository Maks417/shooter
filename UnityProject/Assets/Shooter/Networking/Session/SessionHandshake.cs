#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Networking.Codec;
using Shooter.Networking.Transport;

namespace Shooter.Networking.Session
{
    public enum HandshakeStatus { Pending = 0, Accepted = 1, Rejected = 2 }

    public sealed class SessionHandshakeHost
    {
        private readonly MatchManifest required;
        private readonly ulong expectedToken;

        public SessionHandshakeHost(MatchManifest required, ulong expectedToken)
        {
            this.required = required;
            this.expectedToken = expectedToken;
        }

        public HandshakeStatus Evaluate(ReadOnlySpan<byte> payload, out PeerId peer, out EntityId actor, out string reason)
        {
            peer = default; actor = default; reason = "";
            if (!SessionCodec.TryDecodeHandshake(payload, out var manifest, out peer, out actor, out ulong token, out reason))
                return HandshakeStatus.Rejected;
            if (manifest.ProtocolVersion != required.ProtocolVersion) { reason = "protocol"; return HandshakeStatus.Rejected; }
            if (manifest.BuildCompatibility != required.BuildCompatibility) { reason = "build"; return HandshakeStatus.Rejected; }
            if (manifest.RecipeHash != required.RecipeHash || manifest.GameplayHash != required.GameplayHash)
            { reason = "content-hash"; return HandshakeStatus.Rejected; }
            if (manifest.MapSeed != required.MapSeed) { reason = "seed"; return HandshakeStatus.Rejected; }
            if (manifest.TickRate != required.TickRate || manifest.SnapshotHz != required.SnapshotHz)
            { reason = "rate"; return HandshakeStatus.Rejected; }
            if (token != expectedToken) { reason = "token"; return HandshakeStatus.Rejected; }
            reason = "";
            return HandshakeStatus.Accepted;
        }
    }

    /// <summary>Dedicated authority host: handshake gate before command spawn; no presentation dependency.</summary>
    public sealed class DedicatedSessionHost
    {
        private readonly MatchManifest manifest;
        private readonly SessionHandshakeHost handshake;
        private readonly CommandValidator validator = new CommandValidator();
        private readonly LoopbackNetworkChannel channel;
        private bool joined;
        private PeerId joinedPeer;
        private EntityId joinedActor;

        public DedicatedSessionHost(MatchManifest manifest, ulong sessionToken, LoopbackNetworkChannel? channel = null)
        {
            this.manifest = manifest;
            handshake = new SessionHandshakeHost(manifest, sessionToken);
            this.channel = channel ?? new LoopbackNetworkChannel();
        }

        public INetworkChannel Channel => channel;
        public bool HasJoinedPeer => joined;
        public MatchManifest Manifest => manifest;
        public CommandValidator Validator => validator;

        public HandshakeStatus TryAcceptJoin(ReadOnlySpan<byte> payload, out string reason)
        {
            var status = handshake.Evaluate(payload, out var peer, out var actor, out reason);
            if (status != HandshakeStatus.Accepted) return status;
            if (joined) { reason = "full"; return HandshakeStatus.Rejected; }
            joined = true;
            joinedPeer = peer;
            joinedActor = actor;
            validator.Bind(actor, peer);
            reason = "";
            return HandshakeStatus.Accepted;
        }

        public bool TryAcceptCommand(PeerId peer, uint serverTick, InputCommand command, out string reason)
        {
            if (!joined) { reason = "not-joined"; return false; }
            return validator.TryAccept(peer, serverTick, command, out reason);
        }

        public void Disconnect()
        {
            if (joined) validator.Unbind(joinedActor);
            joined = false;
            joinedPeer = default;
            joinedActor = default;
            channel.Clear();
        }
    }
}
