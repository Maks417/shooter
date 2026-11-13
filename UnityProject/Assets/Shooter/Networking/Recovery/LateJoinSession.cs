#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Networking.Codec;
using Shooter.Networking.Session;

namespace Shooter.Networking.Recovery
{
    public enum JoinPhase { Disconnected = 0, Handshaking = 1, WaitingReady = 2, Ready = 3, Spectating = 4 }

    public sealed class LateJoinSession
    {
        private readonly MatchManifest manifest;
        private readonly ulong sessionToken;
        private readonly Dictionary<PeerId, PeerJoinState> peers = new Dictionary<PeerId, PeerJoinState>();
        private ulong authoritySessionId = 1;
        private readonly HashSet<ulong> retiredSessionEventGates = new HashSet<ulong>();

        private sealed class PeerJoinState
        {
            public JoinPhase Phase;
            public EntityId Actor;
            public ulong SessionId;
            public uint BaselineTick;
            public bool FullSnapshotSent;
            public HashSet<string> Reservations = new HashSet<string>();
        }

        public LateJoinSession(MatchManifest manifest, ulong sessionToken)
        {
            this.manifest = manifest;
            this.sessionToken = sessionToken;
        }

        public ulong AuthoritySessionId => authoritySessionId;
        public int PeerCount => peers.Count;

        public HandshakeStatus BeginJoin(ReadOnlySpan<byte> handshakePayload, MatchState matchState, out PeerId peer, out string reason)
        {
            peer = default; reason = "";
            var host = new SessionHandshakeHost(manifest, sessionToken);
            var status = host.Evaluate(handshakePayload, out peer, out var actor, out reason);
            if (status != HandshakeStatus.Accepted) return status;
            if (peers.Count >= 4) { reason = "full"; return HandshakeStatus.Rejected; }

            authoritySessionId++;
            var state = new PeerJoinState
            {
                Phase = matchState == MatchState.Playing ? JoinPhase.Spectating : JoinPhase.WaitingReady,
                Actor = actor,
                SessionId = authoritySessionId,
                BaselineTick = 0,
                FullSnapshotSent = false
            };
            peers[peer] = state;
            reason = "";
            return HandshakeStatus.Accepted;
        }

        public bool TrySendFullSnapshot(PeerId peer, SimulationSnapshot snapshot, out ReadOnlyMemory<byte> payload, out string reason)
        {
            payload = default; reason = "";
            if (!peers.TryGetValue(peer, out var state)) { reason = "unknown"; return false; }
            if (state.Phase != JoinPhase.WaitingReady && state.Phase != JoinPhase.Spectating)
            { reason = "phase"; return false; }
            payload = SnapshotCodec.EncodeSnapshot(snapshot, 0, 0);
            state.FullSnapshotSent = true;
            state.BaselineTick = snapshot.Tick;
            return true;
        }

        public bool TryMarkReady(PeerId peer, uint ackedBaseline, out string reason)
        {
            reason = "";
            if (!peers.TryGetValue(peer, out var state)) { reason = "unknown"; return false; }
            if (!state.FullSnapshotSent) { reason = "no-full-snapshot"; return false; }
            if (ackedBaseline != state.BaselineTick) { reason = "baseline"; return false; }
            if (state.Phase == JoinPhase.Spectating) return true; // spectator stays non-acting
            state.Phase = JoinPhase.Ready;
            return true;
        }

        public bool CanAcceptCommands(PeerId peer) =>
            peers.TryGetValue(peer, out var state) && state.Phase == JoinPhase.Ready;

        public bool TryApplyDelta(PeerId peer, uint baselineTick, SimulationSnapshot delta, out string reason)
        {
            reason = "";
            if (!peers.TryGetValue(peer, out var state)) { reason = "unknown"; return false; }
            if (!state.FullSnapshotSent) { reason = "need-full"; return false; }
            if (baselineTick != state.BaselineTick) { reason = "baseline-mismatch"; return false; }
            state.BaselineTick = delta.Tick;
            return true;
        }

        public void Reserve(PeerId peer, string key)
        {
            if (peers.TryGetValue(peer, out var state)) state.Reservations.Add(key);
        }

        public bool Disconnect(PeerId peer, out IReadOnlyCollection<string> released)
        {
            released = Array.Empty<string>();
            if (!peers.TryGetValue(peer, out var state)) return false;
            released = new List<string>(state.Reservations);
            state.Reservations.Clear();
            retiredSessionEventGates.Add(state.SessionId);
            peers.Remove(peer);
            return true;
        }

        public bool ReconnectFresh(ReadOnlySpan<byte> handshakePayload, MatchState matchState, out PeerId peer, out ulong newSessionId, out string reason)
        {
            newSessionId = 0;
            var status = BeginJoin(handshakePayload, matchState, out peer, out reason);
            if (status != HandshakeStatus.Accepted) return false;
            newSessionId = peers[peer].SessionId;
            // Prior session event IDs cannot be replayed: gate stays retired.
            return true;
        }

        public bool IsEventSessionRetired(ulong sessionId) => retiredSessionEventGates.Contains(sessionId);

        public JoinPhase GetPhase(PeerId peer) => peers.TryGetValue(peer, out var s) ? s.Phase : JoinPhase.Disconnected;
    }
}
