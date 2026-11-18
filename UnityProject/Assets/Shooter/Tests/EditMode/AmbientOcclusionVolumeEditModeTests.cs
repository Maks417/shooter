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
        [TestCase("training-compound", "e59f18c6edef2494f1ee767f72f58fae55233b9f5da973c71a1ac94c582c210d")]
        [TestCase("depot-yard", "f7de906b6c16ac62a9b0bedadedc7885975c7ae08708d610c58feeab7888c284")]
        [TestCase("oil-depot", "d4b5cdf773e7bf71e53da4a02431dbc6b576e9de8bad023090cbf15cc6dfbf89")]
        public void MonoBakeMatchesToolsLaneGolden(string mapId, string sha256)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            byte[] volume = MapAmbientOcclusionVolume.BakeUncached(plan.CollisionBoxes);
            Assert.That(ContentIdentity.Hash(volume).ToString(), Is.EqualTo(sha256));
        }
    }
}
