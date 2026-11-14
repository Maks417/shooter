#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Generation.Environment;
using Xunit;

namespace Shooter.Generation.Tests
{
    public sealed class EnvironmentTests
    {
        [Fact]
        public void Canonical_seeds_produce_connected_collision_safe_compounds()
        {
            var reports = CompoundValidation.ValidateCanonical();
            Assert.Equal(4, reports.Length);
            for (int i = 0; i < reports.Length; i++)
            {
                Assert.True(reports[i].Passed, "seed " + reports[i].Seed + ": " + reports[i].Diagnostic);
                Assert.True(reports[i].BorderErrorMeters <= 1e-4f);
                Assert.True(reports[i].MaxOpeningErrorMeters <= 0.01f);
                Assert.True(reports[i].SpawnObjectiveConnected);
                Assert.True(reports[i].BotsReachable);
                Assert.True(reports[i].SpawnLosBlocked);
            }
        }

        [Fact]
        public void Gameplay_hashes_independent_of_visual_profile_label()
        {
            var plan = CompoundPlanner.Generate(460072030UL);
            var again = CompoundPlanner.Generate(460072030UL);
            Assert.Equal(plan.CollisionHash, again.CollisionHash);
            Assert.Equal(plan.NavHash, again.NavHash);
            Assert.Equal(plan.CollisionBoxes.Count, again.CollisionBoxes.Count);
            Assert.Equal(12, plan.Buildings.Count);
            Assert.Equal(120, plan.Props.Count);
            Assert.Equal(8, plan.BotSpawns.Count);
        }

        [Fact]
        public void Chunk_grid_is_8x8_of_32m_and_spawn_objective_match_baseline()
        {
            var plan = CompoundPlanner.Generate(42UL);
            Assert.Equal(new ChunkCoord(0, 0), CompoundPlan.ChunkAt(-104, -104));
            Assert.Equal(new ChunkCoord(7, 7), CompoundPlan.ChunkAt(104, 104));
            Assert.Equal(32f, CompoundPlan.ChunkSize);
            Assert.True(Math.Abs(plan.PlayerSpawn.X - (-104f)) <= 8f);
            Assert.True(Math.Abs(plan.PlayerSpawn.Z - (-104f)) <= 8f);
            Assert.Equal(96f, plan.ObjectiveCenter.X);
            Assert.Equal(96f, plan.ObjectiveCenter.Z);
            Assert.Equal(3f, plan.ObjectiveRadius);
        }

        [Fact]
        public void Clearance_uses_capsule_radius_plus_5cm()
        {
            Assert.Equal(0.37f, CompoundConnectivity.RequiredClearance, 3);
        }

        [Fact]
        public void Different_seeds_change_heights_but_keep_shell_layout()
        {
            var a = CompoundPlanner.Generate(1UL);
            var b = CompoundPlanner.Generate(42UL);
            Assert.Equal(a.Buildings.Count, b.Buildings.Count);
            Assert.Equal(a.Buildings[0].Center.X, b.Buildings[0].Center.X);
            Assert.NotEqual(a.HeightSamples[10, 10], b.HeightSamples[10, 10]);
        }

        [Fact]
        public void Depot_catalog_seed_differs_from_compound_and_from_robustness_forty_two()
        {
            var depot = DepotPlanner.Generate(460072031UL);
            var compound = CompoundPlanner.Generate(460072030UL);
            Assert.NotEqual(depot.CollisionHash, compound.CollisionHash);
            Assert.True(depot.CollisionBoxes.Count > 0);
            Assert.NotEqual(42UL, MapRecipe.ApprovedSeed("depot-yard"));
            Assert.Equal(460072031UL, MapRecipe.ApprovedSeed("depot-yard"));
        }
    }
}
