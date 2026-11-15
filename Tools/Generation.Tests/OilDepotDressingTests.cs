#nullable enable
using System;
using System.Linq;
using Shooter.Contracts;
using Shooter.Generation.Environment;
using Xunit;

namespace Shooter.Generation.Tests
{
    /// <summary>Oil-depot visual dressing: mesh-only, round tanks enclose authoritative drum collision.</summary>
    public sealed class OilDepotDressingTests
    {
        [Fact]
        public void Dressing_does_not_change_collision_or_nav_identity()
        {
            var before = ProductMapPlanner.Generate("oil-depot");
            _ = MapSolidsMesh.Build(before, OilDepotDressing.MapId);
            var after = ProductMapPlanner.Generate("oil-depot");
            Assert.Equal(before.CollisionHash, after.CollisionHash);
            Assert.Equal(before.NavHash, after.NavHash);
            Assert.Equal(before.CollisionBoxes.Count, after.CollisionBoxes.Count);
        }

        [Fact]
        public void Oil_mesh_differs_from_map_agnostic_mesh_and_has_tall_round_skyline()
        {
            var plan = ProductMapPlanner.Generate("oil-depot");
            var plain = MapSolidsMesh.Build(plan);
            var dressed = MapSolidsMesh.Build(plan, OilDepotDressing.MapId);
            Assert.True(dressed.Positions.Count > plain.Positions.Count);
            Assert.InRange(dressed.Positions.Count, 1, MapSolidsMesh.MaxVisualVertices);
            // Flare stack reaches well above the 5 m perimeter wall.
            Assert.True(dressed.Positions.Max(p => p.Y) > 20f);
            Assert.True(plain.Positions.Max(p => p.Y) < 8f);
            var slots = dressed.Submeshes.Select(s => s.MaterialSlot).ToArray();
            Assert.Contains((ushort)MapSolidsMaterialRole.TankWhite, slots);
            Assert.Contains((ushort)MapSolidsMaterialRole.SafetyYellow, slots);
        }

        [Fact]
        public void Other_maps_ignore_oil_dressing()
        {
            var plan = ProductMapPlanner.Generate("depot-yard");
            var a = MapSolidsMesh.Build(plan);
            var b = MapSolidsMesh.Build(plan, "depot-yard");
            Assert.Equal(a.Positions.Count, b.Positions.Count);
        }

        [Fact]
        public void Round_tank_shells_enclose_every_replaced_drum_box()
        {
            var plan = ProductMapPlanner.Generate("oil-depot");
            int replacedGameplay = 0;
            foreach (var box in plan.CollisionBoxes)
            {
                if (!box.Gameplay || !OilDepotDressing.ReplacedByDressing(box)) continue;
                replacedGameplay++;
                bool enclosed = false;
                for (int t = 0; t < OilDepotDressing.TankCount && !enclosed; t++)
                {
                    var c = OilDepotDressing.TankBase(t);
                    float r = OilDepotDressing.TankRadius(t);
                    // Every XZ corner must lie inside the polygonal shell's inscribed circle.
                    float inscribed = r * (float)Math.Cos(Math.PI / 32);
                    enclosed = new[] { -1f, 1f }.All(sx => new[] { -1f, 1f }.All(sz =>
                    {
                        float dx = box.Center.X + sx * box.Size.X * .5f - c.X;
                        float dz = box.Center.Z + sz * box.Size.Z * .5f - c.Z;
                        return Math.Sqrt(dx * dx + dz * dz) <= inscribed + 1e-3;
                    })) && box.Center.Y + box.Size.Y * .5f <= OilDepotDressing.TankHeight(t) + 1e-3;
                }
                Assert.True(enclosed, "tank collision box " + box.StableId + " pokes outside its round shell");
            }
            // ADR-074: four drums (two per half) x (drum + four buttresses).
            Assert.Equal(20, replacedGameplay);
        }

        [Fact]
        public void Dressing_is_deterministic()
        {
            var plan = ProductMapPlanner.Generate("oil-depot");
            var a = GeometryCodec.EncodeMesh(MapSolidsMesh.Build(plan, OilDepotDressing.MapId));
            var b = GeometryCodec.EncodeMesh(MapSolidsMesh.Build(plan, OilDepotDressing.MapId));
            Assert.Equal(a.ToArray(), b.ToArray());
        }
    }
}
