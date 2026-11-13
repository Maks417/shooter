#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Networking.Abuse
{
    /// <summary>LAN/private session abuse bounds. Public auth/encryption/deployment remain N4 (documented, not claimed).</summary>
    public sealed class AbuseLimits
    {
        public const int MaxPeers = 4;
        public const int MaxBots = 8;
        public const int MaxPacketBytes = 1200;
        public const int MaxQueueDepth = 256;
        public const int MaxCommandsPerSecondPerPeer = 90;
        public const int MaxResyncPerMinute = 30;
        public const string PublicExposureStatus = "N4-deferred: no public matchmaking/auth/encryption claim";

        private readonly DictionaryCounters peers = new DictionaryCounters();
        public int ConnectedPeers { get; private set; }
        public int Disconnects { get; private set; }

        public bool TryAdmitPeer(PeerId peer, ulong sessionToken, ulong expectedToken, out string reason)
        {
            reason = "";
            if (!peer.IsValid) { reason = "peer"; return false; }
            if (sessionToken != expectedToken) { reason = "token"; return false; }
            if (ConnectedPeers >= MaxPeers) { reason = "peer-limit"; return false; }
            ConnectedPeers++;
            return true;
        }

        public bool ValidatePacket(ReadOnlySpan<byte> payload, out string reason)
        {
            reason = "";
            if (payload.Length == 0 || payload.Length > MaxPacketBytes) { reason = "size"; return false; }
            return true;
        }

        public bool NoteMalformed()
        {
            peers.Malformed++;
            return peers.Malformed < 1024; // keep receiving; never throw out of loop
        }

        public void DisconnectPeer()
        {
            if (ConnectedPeers > 0) ConnectedPeers--;
            Disconnects++;
        }

        public string ExposureReviewSummary() =>
            $"LAN/private token sessions only; maxPeers={MaxPeers}; maxPacket={MaxPacketBytes}; {PublicExposureStatus}";

        private sealed class DictionaryCounters
        {
            public int Malformed;
        }
    }

    /// <summary>Pure B04 bandwidth/soak counters for four clients + eight bots (software gate).</summary>
    public sealed class B04SoakCounters
    {
        public long BytesUpstream { get; private set; }
        public long BytesDownstream { get; private set; }
        public int SnapshotsSent { get; private set; }
        public int CommandsAccepted { get; private set; }
        public int Resyncs { get; private set; }
        public double MaxSnapshotAgeSeconds { get; private set; }
        public bool DivergenceOverOneSecond { get; private set; }

        public void NoteUpstream(int bytes) { if (bytes > 0) BytesUpstream += bytes; }
        public void NoteDownstream(int bytes) { if (bytes > 0) BytesDownstream += bytes; }
        public void NoteSnapshot(double ageSeconds)
        {
            SnapshotsSent++;
            if (ageSeconds > MaxSnapshotAgeSeconds) MaxSnapshotAgeSeconds = ageSeconds;
            if (ageSeconds > 1.0) DivergenceOverOneSecond = true;
        }
        public void NoteCommand() => CommandsAccepted++;
        public void NoteResync() => Resyncs++;

        public double UpstreamKiBPerSecond(double durationSeconds) =>
            durationSeconds <= 0 ? 0 : (BytesUpstream / 1024.0) / durationSeconds;
        public double DownstreamKiBPerSecond(double durationSeconds) =>
            durationSeconds <= 0 ? 0 : (BytesDownstream / 1024.0) / durationSeconds;
    }
}
