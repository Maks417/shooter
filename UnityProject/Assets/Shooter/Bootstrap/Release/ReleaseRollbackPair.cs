#nullable enable
using System;

namespace Shooter.Bootstrap.Release
{
    /// <summary>Records a matching binary/content/protocol rollback pair for M7 H03 rehearsals.</summary>
    public readonly struct ReleaseRollbackPair
    {
        public string BinaryCommit { get; }
        public string ContentManifestHash { get; }
        public string ProtocolVersion { get; }
        public string ContractVersion { get; }

        public ReleaseRollbackPair(string binaryCommit, string contentManifestHash, string protocolVersion, string contractVersion)
        {
            if (string.IsNullOrWhiteSpace(binaryCommit)) throw new ArgumentException(nameof(binaryCommit));
            if (string.IsNullOrWhiteSpace(contentManifestHash)) throw new ArgumentException(nameof(contentManifestHash));
            if (string.IsNullOrWhiteSpace(protocolVersion)) throw new ArgumentException(nameof(protocolVersion));
            if (string.IsNullOrWhiteSpace(contractVersion)) throw new ArgumentException(nameof(contractVersion));
            BinaryCommit = binaryCommit;
            ContentManifestHash = contentManifestHash;
            ProtocolVersion = protocolVersion;
            ContractVersion = contractVersion;
        }

        public bool Matches(ReleaseRollbackPair other) =>
            BinaryCommit == other.BinaryCommit
            && ContentManifestHash == other.ContentManifestHash
            && ProtocolVersion == other.ProtocolVersion
            && ContractVersion == other.ContractVersion;
    }

    public static class ReleaseHardeningDefaults
    {
        public const string MandatoryDisplayBaseline = "native720p-Low-30FPS";
        public const string StretchDisplayBaseline = "native720p-Low-60FPS-stretch";
        public const bool StretchMayFailWithoutBlockingMandatory = true;
    }
}
