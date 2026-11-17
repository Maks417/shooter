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
            Assert.Equal(1f, low.AoVolumeStrength); // Low relies on the baked AO volume alone (ADR-081).
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
            Assert.InRange(balanced.AoVolumeStrength, 0.5f, 1f);
            Assert.InRange(high.AoVolumeStrength, 0.5f, 1f);
        }

        [Fact]
        public void QualityUpliftScalesAntiAliasingTexturesFogAndScopeLens()
        {
            var low = PostProcessingTier.For(0);
            var balanced = PostProcessingTier.For(1);
            var high = PostProcessingTier.For(2);
            // ADR-103: Low keeps the gate budget (no MSAA, cheap FXAA); higher tiers resolve edges with MSAA.
            Assert.Equal(1, low.MsaaSamples);
            Assert.True(low.Fxaa);
            Assert.Equal(2, balanced.MsaaSamples);
            Assert.Equal(4, high.MsaaSamples);
            Assert.False(high.Fxaa);
            Assert.True(high.AnisoLevel > balanced.AnisoLevel && balanced.AnisoLevel > low.AnisoLevel);
            Assert.Equal(1f, low.FogDistanceScale);
            Assert.True(high.FogDistanceScale > balanced.FogDistanceScale && balanced.FogDistanceScale > 1f);
            Assert.True(high.ScopeLensTextureSize >= 1024);
            Assert.True(balanced.ScopeLensTextureSize > low.ScopeLensTextureSize);
        }

        [Fact]
        public void UnknownTierIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PostProcessingTier.For(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => PostProcessingTier.For(-1));
        }
    }
}
