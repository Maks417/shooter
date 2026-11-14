#nullable enable
using System;
using System.Collections.Generic;

namespace Shooter.Bootstrap.Release
{
    /// <summary>Reviewed FIX13 exclude/recovery constants. Packaging tooling loads the matching JSON policy.</summary>
    public static class ReleasePackagePolicy
    {
        public const string PolicyId = "shooter.release-package.v1";
        public const string ProtocolVersion = "N1-v1";
        public const string ContractVersion = "Game-v1";

        /// <summary>Stable repair message for missing/corrupt selected map content. No silent substitution.</summary>
        public const string CorruptContentRecoveryMessage =
            "Selected map content is missing or corrupt. Repair or reinstall the verified package; no silent map substitution.";

        public static readonly string[] RequiredMaps = { "training-compound", "depot-yard" };
        public static readonly string[] RequiredVisualProfiles = { "gameplay", "low", "balanced", "high" };

        public static readonly string[] ExcludeDirectoryNames =
        {
            "Shooter_BackUpThisFolder_ButDontShipItWithYourGame",
            "Shooter_BurstDebugInformation_DoNotShip",
            "Library",
            "Temp",
            "Obj",
            ".vs",
            "Logs",
            "UserSettings"
        };

        public static readonly string[] ExcludeDirectoryNameContains =
        {
            "DoNotShip",
            "BackUpThisFolder",
            "BurstDebugInformation",
            "_BackUp"
        };

        public static readonly string[] ExcludeFileExtensions =
        {
            ".pdb",
            ".mdb",
            ".log",
            ".tmp",
            ".bak"
        };

        public static readonly string[] ExcludeFileNames =
        {
            "Player.log",
            "user-settings.json",
            "user-settings.json.bak",
            "boot.config.user"
        };

        public static bool IsExcludedRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return true;
            string normalized = relativePath.Replace('\\', '/').TrimStart('/');
            string[] parts = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (DirectoryNameExcluded(parts[i])) return true;
            }

            if (parts.Length == 0) return true;
            string fileName = parts[parts.Length - 1];
            for (int i = 0; i < ExcludeFileNames.Length; i++)
            {
                if (string.Equals(fileName, ExcludeFileNames[i], StringComparison.OrdinalIgnoreCase)) return true;
            }

            string ext = System.IO.Path.GetExtension(fileName);
            for (int i = 0; i < ExcludeFileExtensions.Length; i++)
            {
                if (string.Equals(ext, ExcludeFileExtensions[i], StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        public static bool DirectoryNameExcluded(string directoryName)
        {
            if (string.IsNullOrEmpty(directoryName)) return false;
            for (int i = 0; i < ExcludeDirectoryNames.Length; i++)
            {
                if (string.Equals(directoryName, ExcludeDirectoryNames[i], StringComparison.OrdinalIgnoreCase)) return true;
            }

            for (int i = 0; i < ExcludeDirectoryNameContains.Length; i++)
            {
                if (directoryName.IndexOf(ExcludeDirectoryNameContains[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

        public static string ComputeContentClosureHash(IReadOnlyList<string> sortedMapProfileRootLines)
        {
            if (sortedMapProfileRootLines == null) throw new ArgumentNullException(nameof(sortedMapProfileRootLines));
            using var sha = System.Security.Cryptography.SHA256.Create();
            var payload = System.Text.Encoding.UTF8.GetBytes(string.Join("\n", sortedMapProfileRootLines));
            return BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>Immutable release candidate identities. Never stamp an unrelated workspace HEAD onto these.</summary>
    public readonly struct ReleasePackageIdentities
    {
        public string BinaryCommit { get; }
        public string SourceHash { get; }
        public string ToolchainHash { get; }
        public string BuildContentManifestHash { get; }
        public string ContentClosureHash { get; }
        public string ProtocolVersion { get; }
        public string ContractVersion { get; }
        public string EditorVersion { get; }
        public string Backend { get; }

        public ReleasePackageIdentities(
            string binaryCommit,
            string sourceHash,
            string toolchainHash,
            string buildContentManifestHash,
            string contentClosureHash,
            string protocolVersion,
            string contractVersion,
            string editorVersion,
            string backend)
        {
            if (string.IsNullOrWhiteSpace(binaryCommit)) throw new ArgumentException(nameof(binaryCommit));
            if (string.IsNullOrWhiteSpace(sourceHash)) throw new ArgumentException(nameof(sourceHash));
            if (string.IsNullOrWhiteSpace(toolchainHash)) throw new ArgumentException(nameof(toolchainHash));
            if (string.IsNullOrWhiteSpace(buildContentManifestHash)) throw new ArgumentException(nameof(buildContentManifestHash));
            if (string.IsNullOrWhiteSpace(contentClosureHash)) throw new ArgumentException(nameof(contentClosureHash));
            if (string.IsNullOrWhiteSpace(protocolVersion)) throw new ArgumentException(nameof(protocolVersion));
            if (string.IsNullOrWhiteSpace(contractVersion)) throw new ArgumentException(nameof(contractVersion));
            if (string.IsNullOrWhiteSpace(editorVersion)) throw new ArgumentException(nameof(editorVersion));
            if (string.IsNullOrWhiteSpace(backend)) throw new ArgumentException(nameof(backend));
            BinaryCommit = binaryCommit;
            SourceHash = sourceHash;
            ToolchainHash = toolchainHash;
            BuildContentManifestHash = buildContentManifestHash;
            ContentClosureHash = contentClosureHash;
            ProtocolVersion = protocolVersion;
            ContractVersion = contractVersion;
            EditorVersion = editorVersion;
            Backend = backend;
        }

        public ReleaseRollbackPair ToRollbackPair() =>
            new ReleaseRollbackPair(BinaryCommit, ContentClosureHash, ProtocolVersion, ContractVersion);
    }

    /// <summary>Ledger entry that preserves a matching previous binary+content pair for rollback rehearsal.</summary>
    public sealed class ReleaseRollbackLedger
    {
        readonly List<ReleaseRollbackPair> pairs = new List<ReleaseRollbackPair>();

        public int Count => pairs.Count;

        public void Record(ReleaseRollbackPair pair)
        {
            for (int i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Matches(pair)) return;
            }

            pairs.Add(pair);
        }

        public bool TryGetMatching(ReleaseRollbackPair needle, out ReleaseRollbackPair found)
        {
            for (int i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Matches(needle))
                {
                    found = pairs[i];
                    return true;
                }
            }

            found = default;
            return false;
        }

        public bool CanRollbackTo(ReleaseRollbackPair previous, ReleaseRollbackPair current) =>
            !previous.Matches(current) && TryGetMatching(previous, out _);
    }
}
