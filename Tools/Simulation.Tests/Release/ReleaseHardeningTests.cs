#nullable enable
using System.IO;
using Shooter.Bootstrap.Release;
using Xunit;

namespace Shooter.Simulation.Tests.Release
{
    public sealed class ReleaseHardeningTests
    {
        [Fact]
        public void Packaged_content_defaults_are_executable_relative()
        {
            string playerData = Path.GetFullPath(Path.Combine("C:\\", "Games", "ShooterOffline", "Shooter_Data"));
            string install = PackagedContentLayout.InstallRootFromDataPath(playerData, editorPlayer: false);
            Assert.Equal(Path.GetFullPath(Path.Combine("C:\\", "Games", "ShooterOffline")), install);
            Assert.Equal(Path.GetFullPath(Path.Combine(install, "Content", "map-packages")), PackagedContentLayout.DefaultMapCache(install));
            Assert.Equal(Path.GetFullPath(Path.Combine(install, "toolchain-lock.json")), PackagedContentLayout.DefaultToolchainLock(install));

            string editorData = Path.GetFullPath(Path.Combine("C:\\", "Job", "shooter", "UnityProject", "Assets"));
            string repo = PackagedContentLayout.InstallRootFromDataPath(editorData, editorPlayer: true);
            Assert.Equal(Path.GetFullPath(Path.Combine("C:\\", "Job", "shooter")), repo);
            Assert.Equal(Path.GetFullPath(Path.Combine(repo, "Artifacts", "map-packages")), PackagedContentLayout.EditorArtifactsMapCache(repo));
        }

        [Fact]
        public void Rollback_pair_matches_only_identical_binary_content_protocol()
        {
            var a = new ReleaseRollbackPair("abc", "hash1", "N1-v1", "C0-v1");
            var b = new ReleaseRollbackPair("abc", "hash1", "N1-v1", "C0-v1");
            var c = new ReleaseRollbackPair("abc", "hash2", "N1-v1", "C0-v1");
            Assert.True(a.Matches(b));
            Assert.False(a.Matches(c));
            Assert.Equal("native720p-Low-30FPS", ReleaseHardeningDefaults.MandatoryDisplayBaseline);
            Assert.True(ReleaseHardeningDefaults.StretchMayFailWithoutBlockingMandatory);
        }

        [Fact]
        public void Exclude_policy_rejects_backup_debug_pdb_and_user_files()
        {
            Assert.True(ReleasePackagePolicy.IsExcludedRelativePath("Shooter_BackUpThisFolder_ButDontShipItWithYourGame/il2cppOutput/x.c"));
            Assert.True(ReleasePackagePolicy.IsExcludedRelativePath("Shooter_BurstDebugInformation_DoNotShip/temp.txt"));
            Assert.True(ReleasePackagePolicy.IsExcludedRelativePath("Shooter_Data/Plugins/x.pdb"));
            Assert.True(ReleasePackagePolicy.IsExcludedRelativePath("Player.log"));
            Assert.True(ReleasePackagePolicy.IsExcludedRelativePath("user-settings.json"));
            Assert.False(ReleasePackagePolicy.IsExcludedRelativePath("Shooter.exe"));
            Assert.False(ReleasePackagePolicy.IsExcludedRelativePath("Shooter_Data/boot.config"));
            Assert.Equal(ReleasePackagePolicy.PolicyId, "shooter.release-package.v1");
            Assert.Equal(ReleasePackagePolicy.ProtocolVersion, "N1-v1");
            Assert.Equal(ReleasePackagePolicy.ContractVersion, "Game-v1");
        }

        [Fact]
        public void Corrupt_content_recovery_message_is_stable_and_non_substituting()
        {
            Assert.Contains("Repair or reinstall", ReleasePackagePolicy.CorruptContentRecoveryMessage);
            Assert.Contains("no silent map substitution", ReleasePackagePolicy.CorruptContentRecoveryMessage);
        }

        [Fact]
        public void Package_identities_feed_matching_rollback_pair_without_head_stamp()
        {
            var identities = new ReleasePackageIdentities(
                "deadbeef",
                "sourcehash",
                "toolchainhash",
                "buildcontent",
                "closurehash",
                ReleasePackagePolicy.ProtocolVersion,
                ReleasePackagePolicy.ContractVersion,
                "6000.3.28f1",
                "IL2CPP");
            var pair = identities.ToRollbackPair();
            Assert.Equal("deadbeef", pair.BinaryCommit);
            Assert.Equal("closurehash", pair.ContentManifestHash);
            Assert.Equal(ReleasePackagePolicy.ProtocolVersion, pair.ProtocolVersion);
            Assert.Equal(ReleasePackagePolicy.ContractVersion, pair.ContractVersion);
        }

        [Fact]
        public void Rollback_ledger_rehearses_matching_previous_pair()
        {
            var previous = new ReleaseRollbackPair("aaa", "content-a", "N1-v1", "Game-v1");
            var current = new ReleaseRollbackPair("bbb", "content-b", "N1-v1", "Game-v1");
            var ledger = new ReleaseRollbackLedger();
            ledger.Record(previous);
            ledger.Record(current);
            Assert.True(ledger.TryGetMatching(previous, out var found));
            Assert.True(found.Matches(previous));
            Assert.True(ledger.CanRollbackTo(previous, current));
            Assert.False(ledger.CanRollbackTo(current, current));
        }

        [Fact]
        public void Content_closure_hash_is_order_sensitive_to_sorted_lines()
        {
            var a = ReleasePackagePolicy.ComputeContentClosureHash(new[] { "a:1", "b:2" });
            var b = ReleasePackagePolicy.ComputeContentClosureHash(new[] { "a:1", "b:2" });
            var c = ReleasePackagePolicy.ComputeContentClosureHash(new[] { "a:1", "b:3" });
            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
            Assert.Equal(64, a.Length);
        }
    }
}
