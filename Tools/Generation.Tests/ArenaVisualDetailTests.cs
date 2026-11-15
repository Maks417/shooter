#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Xunit;
using Xunit.Abstractions;

namespace Shooter.Generation.Tests
{
    /// <summary>T078 slice A: visual enrichment must leave gameplay collision byte-identical and render roofs.</summary>
    public sealed class ArenaVisualDetailTests
    {
        private readonly ITestOutputHelper output;
        public ArenaVisualDetailTests(ITestOutputHelper output) { this.output = output; }

        // Captured from planners at base 3c6f640 before ArenaVisualDetail append (gameplay-only fingerprint).
        private const string CompoundGameplayFingerprint = "d02fd17e2b98461c0c9446ff67e7368b4648565335d6cd2e115ce45ac470d533";
        private const string DepotGameplayFingerprint = "7a722f67180a2bb51e0fc927749345b719d180f058afa43ee9919377af76090a";
        private const string CompoundCollisionHash = "8860d3e60dab3c25fd8182a1b12cba0eef414e6e9e50c06a54b96f2a96f817ca";
        private const string DepotCollisionHash = "bc408febb8b18dc4434c5af5904537e79f580297e00ad669973436d054089acf";
        private const int CompoundGameplayCount = 130;
        private const int DepotGameplayCount = 116;

        [Theory]
        [InlineData("training-compound")]
        [InlineData("depot-yard")]
        public void Arena_rules_still_pass_after_visual_detail(string mapId)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            var report = ArenaRules.Validate(plan);
            output.WriteLine(mapId + "\n" + report.ToText());
            Assert.True(report.Passed, report.ToText());
            Assert.True(report.GameplayBoxCount <= ArenaRules.MaxGameplayBoxes);
        }

        [Fact]
        public void Compound_gameplay_boxes_match_pre_visual_fingerprint()
        {
            var plan = CompoundArenaPlanner.Generate(MapRecipe.ApprovedSeed("training-compound"));
            Assert.Equal(CompoundGameplayCount, CountGameplay(plan));
            Assert.Equal(CompoundGameplayFingerprint, FingerprintGameplay(plan));
            Assert.Equal(CompoundCollisionHash, plan.CollisionHash.ToString());
            int visual = CountVisual(plan);
            output.WriteLine("compound visual boxes=" + visual);
            Assert.True(visual > 0);
            Assert.True(visual < ArenaVisualDetail.MaxVisualExtras);
            AssertVisualIdsInRange(plan, ArenaVisualDetail.CompoundVisualIdBase);
        }

        [Fact]
        public void Depot_gameplay_boxes_match_pre_visual_fingerprint()
        {
            var plan = DepotArenaPlanner.Generate(MapRecipe.ApprovedSeed("depot-yard"));
            Assert.Equal(DepotGameplayCount, CountGameplay(plan));
            Assert.Equal(DepotGameplayFingerprint, FingerprintGameplay(plan));
            Assert.Equal(DepotCollisionHash, plan.CollisionHash.ToString());
            int visual = CountVisual(plan);
            output.WriteLine("depot visual boxes=" + visual);
            Assert.True(visual > 0);
            Assert.True(visual < ArenaVisualDetail.MaxVisualExtras);
            AssertVisualIdsInRange(plan, ArenaVisualDetail.DepotVisualIdBase);
        }

        [Theory]
        [InlineData("training-compound")]
        [InlineData("depot-yard")]
        public void Visual_boxes_off_z0_have_mirror_twins(string mapId)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            int checkedOffAxis = 0;
            foreach (var a in plan.CollisionBoxes)
            {
                if (a.Gameplay) continue;
                if (Math.Abs(a.Center.Z) <= 0.0005f) continue;
                checkedOffAxis++;
                bool twin = false;
                foreach (var b in plan.CollisionBoxes)
                {
                    if (b.Gameplay) continue;
                    if (Math.Abs(a.Center.X - b.Center.X) < 0.001f &&
                        Math.Abs(a.Center.Y - b.Center.Y) < 0.001f &&
                        Math.Abs(a.Center.Z + b.Center.Z) < 0.001f &&
                        Math.Abs(a.Size.X - b.Size.X) < 0.001f &&
                        Math.Abs(a.Size.Y - b.Size.Y) < 0.001f &&
                        Math.Abs(a.Size.Z - b.Size.Z) < 0.001f)
                    {
                        twin = true;
                        break;
                    }
                }
                Assert.True(twin, "visual box " + a.StableId + " at z=" + a.Center.Z + " lacks mirror twin");
            }
            Assert.True(checkedOffAxis > 0, "expected off-axis visual boxes");
            output.WriteLine(mapId + " off-axis visual boxes checked=" + checkedOffAxis);
        }

        [Theory]
        [InlineData("training-compound", 3.5f)]
        [InlineData("depot-yard", 5.0f)]
        public void Solids_mesh_includes_non_gameplay_roof_above_building_height(string mapId, float buildingHeight)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            bool hasRoofBox = false;
            float maxRoofTop = 0f;
            foreach (var box in plan.CollisionBoxes)
            {
                if (box.Gameplay) continue;
                float top = box.Center.Y + box.Size.Y * 0.5f;
                // Roof slab / eaves sit at or above wall height.
                if (box.Center.Y >= buildingHeight - 0.05f && box.Size.Y <= 0.4f &&
                    box.Size.X >= 8f && box.Size.Z >= 8f)
                {
                    hasRoofBox = true;
                    if (top > maxRoofTop) maxRoofTop = top;
                }
            }
            Assert.True(hasRoofBox, "expected a non-gameplay roof/eave slab");

            var mesh = MapSolidsMesh.Build(plan);
            float maxY = float.NegativeInfinity;
            for (int i = 0; i < mesh.Positions.Count; i++)
                if (mesh.Positions[i].Y > maxY) maxY = mesh.Positions[i].Y;
            output.WriteLine(mapId + " mesh maxY=" + maxY + " roofTop=" + maxRoofTop);
            Assert.True(maxY > buildingHeight, "solids mesh maxY " + maxY + " should exceed building height " + buildingHeight);
            Assert.True(maxY >= maxRoofTop - 0.001f, "mesh should include roof vertices");
        }

        [Theory]
        [InlineData("training-compound")]
        [InlineData("depot-yard")]
        public void Solids_mesh_splits_structure_metal_and_trim_submeshes(string mapId)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            var mesh = MapSolidsMesh.Build(plan);
            Assert.True(mesh.Submeshes.Count >= 2, "expected at least structure + trim (or metal)");
            var slots = new HashSet<ushort>();
            for (int i = 0; i < mesh.Submeshes.Count; i++)
            {
                var sub = mesh.Submeshes[i];
                Assert.True(sub.IndexCount > 0 && sub.IndexCount % 3 == 0);
                slots.Add(sub.MaterialSlot);
            }
            Assert.Contains((ushort)MapSolidsMaterialRole.StructureConcrete, slots);
            Assert.Contains((ushort)MapSolidsMaterialRole.Trim, slots);
            Assert.Contains((ushort)MapSolidsMaterialRole.MetalCover, slots);
            output.WriteLine(mapId + " submeshes=" + mesh.Submeshes.Count + " slots=" + string.Join(",", slots));
        }

        [Theory]
        [InlineData("training-compound")]
        [InlineData("depot-yard")]
        public void Visual_props_include_lamp_ridge_and_fence_extras(string mapId)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            int lamps = 0, ridges = 0, posts = 0;
            foreach (var box in plan.CollisionBoxes)
            {
                if (box.Gameplay) continue;
                // Lamp shaft: tall thin post.
                if (box.Size.Y >= 3.5f && box.Size.X <= 0.25f && box.Size.Z <= 0.25f) lamps++;
                // Ridge cap: thin high strip.
                if (box.Size.Y <= 0.2f && box.Center.Y >= 3.4f &&
                    ((box.Size.X >= 8f && box.Size.Z <= 0.4f) || (box.Size.Z >= 8f && box.Size.X <= 0.4f)))
                    ridges++;
                // Fence post: short thin upright.
                if (box.Size.Y >= 1.2f && box.Size.Y <= 1.5f && box.Size.X <= 0.2f && box.Size.Z <= 0.2f) posts++;
            }
            output.WriteLine(mapId + " lamps=" + lamps + " ridges=" + ridges + " fencePosts=" + posts);
            Assert.True(lamps >= 6, "expected mirrored lamp shafts");
            Assert.True(ridges >= 1, "expected roof ridge caps");
            Assert.True(posts >= 8, "expected fence posts");
        }

        [Fact]
        public void Gameplay_box_order_and_identity_unchanged_relative_to_visual_append_point()
        {
            // Visual ids start at high bases; every gameplay id stays below those bases and ordered as before.
            var compound = CompoundArenaPlanner.Generate(MapRecipe.ApprovedSeed("training-compound"));
            var depot = DepotArenaPlanner.Generate(MapRecipe.ApprovedSeed("depot-yard"));
            AssertGameplayBelowVisualBase(compound, ArenaVisualDetail.CompoundVisualIdBase);
            AssertGameplayBelowVisualBase(depot, ArenaVisualDetail.DepotVisualIdBase);
        }

        private static void AssertGameplayBelowVisualBase(CompoundPlan plan, ulong visualBase)
        {
            ulong lastGameplay = 0;
            foreach (var b in plan.CollisionBoxes)
            {
                if (!b.Gameplay) continue;
                Assert.True(b.StableId < visualBase, "gameplay id " + b.StableId + " invaded visual range");
                Assert.True(b.StableId > lastGameplay);
                lastGameplay = b.StableId;
            }
        }

        private static void AssertVisualIdsInRange(CompoundPlan plan, ulong visualBase)
        {
            int inRange = 0;
            foreach (var b in plan.CollisionBoxes)
            {
                if (b.Gameplay) continue;
                if (b.StableId >= visualBase) inRange++;
            }
            Assert.True(inRange > 0, "expected visual ids >= " + visualBase);
        }

        private static int CountGameplay(CompoundPlan plan)
        {
            int n = 0;
            for (int i = 0; i < plan.CollisionBoxes.Count; i++)
                if (plan.CollisionBoxes[i].Gameplay) n++;
            return n;
        }

        private static int CountVisual(CompoundPlan plan)
        {
            int n = 0;
            for (int i = 0; i < plan.CollisionBoxes.Count; i++)
                if (!plan.CollisionBoxes[i].Gameplay) n++;
            return n;
        }

        private static string FingerprintGameplay(CompoundPlan plan)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
            for (int i = 0; i < plan.CollisionBoxes.Count; i++)
            {
                var b = plan.CollisionBoxes[i];
                if (!b.Gameplay) continue;
                w.Write(b.StableId); w.Write(b.SurfaceId); w.Write(b.SurfaceTag);
                w.Write(b.Center.X); w.Write(b.Center.Y); w.Write(b.Center.Z);
                w.Write(b.Size.X); w.Write(b.Size.Y); w.Write(b.Size.Z);
            }
            w.Flush();
            return ToHex(SHA256.HashData(ms.ToArray()));
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }

    }
}
