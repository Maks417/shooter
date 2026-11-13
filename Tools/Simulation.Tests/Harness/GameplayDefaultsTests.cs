#nullable enable
using System.Text.Json;
using Shooter.Simulation;
using Shooter.Simulation.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Harness
{
    public sealed class GameplayDefaultsTests
    {
        [Fact]
        public void FixtureCollisionLayersMatchIntegratorMatrix()
        {
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "fixtures", "gameplay-defaults.json"));
            using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(path));
            var layers = doc.RootElement.GetProperty("collisionLayers");
            Assert.Equal(GameplayDefaults.WorldSolidLayer, layers.GetProperty("WorldSolid").GetInt32());
            Assert.Equal(GameplayDefaults.ActorMotorLayer, layers.GetProperty("ActorMotor").GetInt32());
            Assert.Equal(GameplayDefaults.ActorHitLayer, layers.GetProperty("ActorHit").GetInt32());
            Assert.Equal(CollisionLayers.WorldSolid, GameplayDefaults.WorldSolidLayer);
            Assert.Equal(GameplayDefaults.TickRate, doc.RootElement.GetProperty("tickRate").GetInt32());
            Assert.Equal("shooter.gameplay.v1", doc.RootElement.GetProperty("schemaId").GetString());
        }
    }
}
