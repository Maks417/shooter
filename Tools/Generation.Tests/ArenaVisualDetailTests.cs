#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Shooter.Contracts;
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

        // ADR-109: the ADR-074 arena planners and their gameplay fingerprints are retired; product maps are site plans whose
        // collision/nav goldens live in MapRegistryTests. These checks keep the visual-only (roof) boxes honest.

        [Theory]
        [InlineData("training-compound")]
        [InlineData("depot-yard")]
        [InlineData("oil-depot")]
        public void Arena_rules_still_pass_after_visual_detail(string mapId)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            var report = ArenaRules.Validate(plan);
            output.WriteLine(mapId + "\n" + report.ToText());
            Assert.True(report.Passed, report.ToText());
            Assert.True(report.GameplayBoxCount <= ArenaRules.MaxGameplayBoxes);
        }

        [Theory]
        [InlineData("training-compound")]
        [InlineData("depot-yard")]
        [InlineData("oil-depot")]
        public void Visual_boxes_off_z0_have_symmetry_twins(string mapId)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            var arena = plan.Arena!;
            int checkedOffAxis = 0;
            foreach (var a in plan.CollisionBoxes)
            {
                if (a.Gameplay) continue;
                var t = arena.Twin(a.Center);
                if (Math.Abs(t.X - a.Center.X) <= 0.0005f && Math.Abs(t.Z - a.Center.Z) <= 0.0005f) continue;
                checkedOffAxis++;
                bool twin = false;
                foreach (var b in plan.CollisionBoxes)
                {
                    if (b.Gameplay) continue;
                    if (Math.Abs(t.X - b.Center.X) < 0.001f &&
                        Math.Abs(t.Y - b.Center.Y) < 0.001f &&
                        Math.Abs(t.Z - b.Center.Z) < 0.001f &&
                        Math.Abs(a.Size.X - b.Size.X) < 0.001f &&
                        Math.Abs(a.Size.Y - b.Size.Y) < 0.001f &&
                        Math.Abs(a.Size.Z - b.Size.Z) < 0.001f)
                    {
                        twin = true;
                        break;
                    }
                }
                Assert.True(twin, "visual box " + a.StableId + " at (" + a.Center.X + "," + a.Center.Z + ") lacks its " + arena.Symmetry + " twin");
            }
            Assert.True(checkedOffAxis > 0, "expected off-axis visual boxes");
            output.WriteLine(mapId + " off-axis visual boxes checked=" + checkedOffAxis);
        }

        [Theory]
        [InlineData("training-compound", 3.5f)]
        [InlineData("depot-yard", 4.5f)]
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
        public void Solids_mesh_splits_concrete_metal_and_trim_submeshes(string mapId)
        {
            var plan = ProductMapPlanner.Generate(mapId);
            var mesh = MapSolidsMesh.Build(plan);
            Assert.True(mesh.Submeshes.Count >= 2, mapId + " expected multi-material solids, got " + mesh.Submeshes.Count);
            var slots = new HashSet<ushort>();
            uint indexCoverage = 0;
            for (int i = 0; i < mesh.Submeshes.Count; i++)
            {
                Assert.True(mesh.Submeshes[i].IndexCount > 0 && mesh.Submeshes[i].IndexCount % 3 == 0);
                slots.Add(mesh.Submeshes[i].MaterialSlot);
                indexCoverage += mesh.Submeshes[i].IndexCount;
                Assert.InRange(mesh.Submeshes[i].MaterialSlot, (ushort)0, (ushort)MapSolidsMaterialRole.Foundation);
            }
            Assert.Equal((uint)mesh.Indices.Count, indexCoverage);
            Assert.Contains((ushort)MapSolidsMaterialRole.StructureConcrete, slots);
            Assert.Contains((ushort)MapSolidsMaterialRole.MetalCover, slots);
            Assert.Contains((ushort)MapSolidsMaterialRole.Trim, slots);
            output.WriteLine(mapId + " solids submeshes=" + mesh.Submeshes.Count + " slots=" + string.Join(",", slots));
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
