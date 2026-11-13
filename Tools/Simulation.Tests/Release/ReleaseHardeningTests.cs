#nullable enable
using Shooter.Bootstrap.Release;
using Xunit;

namespace Shooter.Simulation.Tests.Release
{
    public sealed class ReleaseHardeningTests
    {
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
    }
}
