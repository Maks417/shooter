#nullable enable
using NUnit.Framework;
using Shooter.Generation;
using Shooter.Generation.Noise;

namespace Shooter.Tests
{
    /// <summary>
    /// ADR-075 / T092 cross-lane determinism: the editor's Mono runtime must reproduce the exact goldens frozen in
    /// Tools/Generation.Tests/NoiseFieldTests.cs (.NET lane). Keep the two tables identical.
    /// </summary>
    public sealed class NoiseFieldEditModeTests
    {
        private static readonly object[] Goldens =
        {
            new object[] { NoiseBasis.Gradient, new[] { 0d, -0.5491059903256589, 0.24189669620474832 }, new[] { 0.5, 0.46066652163662747, 0.5129477091207834 } },
            new object[] { NoiseBasis.Cellular, new[] { -0.5629840500086463, -0.8366196448390766, -0.38779146749814875 }, new[] { 0.4855732777681709, 0.5244052993552571, 0.47416897202472497 } },
            new object[] { NoiseBasis.RidgedFbm, new[] { 1d, -0.35797266622167784, 0.23951416196941114 }, new[] { 1d, 0.5189413954625768, 0.5178291899823476 } },
            new object[] { NoiseBasis.DomainWarpedFbm, new[] { 0d, 0.23474330857145864, 0.18089531348765306 }, new[] { 0.5, 0.6676345888356159, 0.4783284670636132 } },
        };

        [TestCaseSource(nameof(Goldens))]
        public void EditorRuntimeMatchesToolsLaneGoldens(NoiseBasis basis, double[] raw, double[] periodic)
        {
            var field = NoiseField.Create(basis, NamedRandomStream.Derive(460072030UL, "materials/noise-golden", "noise/" + basis), 8, 4);
            Assert.AreEqual(raw[0], field.Raw(0, 0), 0);
            Assert.AreEqual(raw[1], field.Raw(1.25, 3.5), 0);
            Assert.AreEqual(raw[2], field.Raw(7.75, 2.125), 0);
            Assert.AreEqual(periodic[0], field.SamplePeriodic(0, 0), 0);
            Assert.AreEqual(periodic[1], field.SamplePeriodic(.3, .7), 0);
            Assert.AreEqual(periodic[2], field.SamplePeriodic(.999, .5), 0);
        }
    }
}
