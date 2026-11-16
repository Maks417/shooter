using NUnit.Framework;
using Shooter.Generation;
using Shooter.Generation.Environment;

namespace Shooter.Tests
{
    /// <summary>
    /// ADR-081 cross-lane determinism: Unity's Mono must bake the same AO volume bytes as the .NET tools lane.
    /// The hashes are the same goldens as Tools/Generation.Tests/AmbientOcclusionVolumeTests.cs.
    /// </summary>
    public sealed class AmbientOcclusionVolumeEditModeTests
    {
        [TestCase("training-compound", "3d42eb63d9196b0f6df9df2e6857d8a54ad656f26a14f8a4596adab2a1f92378")]
        [TestCase("depot-yard", "e556cad88d5c63b5b10b95f6398f3875f9ea3f84fa2d7be50e55ec65ad40c732")]
        [TestCase("oil-depot", "8284297853928a9c722af9a55383cc31ac6a6a006994c1e4841c8572c083667b")]
        public void MonoBakeMatchesToolsLaneGolden(string mapId, string sha256)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            byte[] volume = MapAmbientOcclusionVolume.BakeUncached(plan.CollisionBoxes);
            Assert.That(ContentIdentity.Hash(volume).ToString(), Is.EqualTo(sha256));
        }
    }
}
