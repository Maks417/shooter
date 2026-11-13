#nullable enable
namespace Shooter.Networking.Transport
{
    /// <summary>
    /// N01 decision surface. Unity Transport is the pinned shipping adapter behind <see cref="Contracts.INetworkChannel"/>.
    /// NGO anticipation is not treated as FPS rollback/replay. Exact package version is recorded when the locked editor verifies it.
    /// </summary>
    public static class NetworkTransportSelection
    {
        public const string SelectedAdapter = "Unity.Transport (UTP) behind INetworkChannel";
        public const string RejectedAlternative = "Netcode for GameObjects anticipation API (not FPS rollback/replay)";
        public const string WireProtocolVersion = "N1-v1";
        public const string DecisionId = "ADR-029";
        /// <summary>Null until locked editor verifies the UTP package against toolchain-lock.</summary>
        public const string? PinnedUnityTransportVersion = null;
        public const string LoopbackAdapter = "Shooter.Networking.Transport.LoopbackNetworkChannel";
        public const bool CustomPredictionRequired = true;
    }
}
