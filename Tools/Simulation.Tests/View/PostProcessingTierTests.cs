using System;
using Shooter.Presentation.View;
using Xunit;

namespace Shooter.Simulation.Tests.View
{
    /// <summary>ADR-080: Low keeps the reference-gate budget; Balanced/High add HDR, tonemapping, bloom and SSAO.</summary>
    public sealed class PostProcessingTierTests
    {
        [Fact]
        public void LowStaysLdrWithoutBloomOrSsao()
        {
            var low = PostProcessingTier.For(0);
            Assert.False(low.Hdr);
            Assert.False(low.NeutralTonemapping);
            Assert.False(low.Bloom);
            Assert.False(low.Ssao);
            Assert.Equal(0f, low.PostExposure);
        }

        [Fact]
        public void HigherTiersAddHdrTonemappingBloomAndSsao()
        {
            var balanced = PostProcessingTier.For(1);
            var high = PostProcessingTier.For(2);
            foreach (var tier in new[] { balanced, high })
            {
                Assert.True(tier.Hdr);
                Assert.True(tier.NeutralTonemapping);
                Assert.True(tier.Bloom);
                Assert.True(tier.Ssao);
                Assert.InRange(tier.BloomThreshold, 0.8f, 1.5f);
            }
            Assert.True(high.BloomIntensity >= balanced.BloomIntensity);
            Assert.True(high.BloomHighQualityFiltering);
            Assert.False(balanced.BloomHighQualityFiltering);
        }

        [Fact]
        public void UnknownTierIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PostProcessingTier.For(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => PostProcessingTier.For(-1));
        }
    }
}
