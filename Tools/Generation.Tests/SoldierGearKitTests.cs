#nullable enable
using System;
using System.Linq;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Xunit;

namespace Shooter.Generation.Tests
{
    /// <summary>ADR-124 generated gear for licensed authored bodies: rigid per-joint pieces near their joints.</summary>
    public sealed class SoldierGearKitTests
    {
        [Theory]
        [InlineData(1, 1, true, true, true)]
        [InlineData(2, 2, false, false, true)]
        [InlineData(0, 0, false, true, false)]
        public void EveryPieceSitsNearItsJoint(byte helmet, byte vest, bool backpack, bool knee, bool belt)
        {
            foreach (bool detail in new[] { true, false })
            {
                var pieces = SoldierGearKit.Build(new SoldierGearSpec(helmet, vest, backpack, knee, belt), detail);
                Assert.Equal(pieces.Select(p => p.Joint).Distinct().Count(), pieces.Count);
                foreach (var piece in pieces)
                {
                    Assert.InRange(piece.Joint, (ushort)1, (ushort)(SoldierSkeleton.JointCount - 1));
                    Assert.All(piece.Mesh.Submeshes, s => Assert.InRange(s.MaterialSlot, (ushort)0, SoldierGearKit.SlotHardware));
                    // Joint-relative: nothing farther than 0.45 m from the joint it rides on.
                    Assert.All(piece.Mesh.Positions, p => Assert.True(Math.Sqrt(p.LengthSquared) < .45, $"joint {piece.Joint} vertex {p.X},{p.Y},{p.Z}"));
                }
                Assert.Equal(helmet != 0, pieces.Any(p => p.Joint == 6));
                Assert.Equal(vest != 0 || backpack, pieces.Any(p => p.Joint == 4));
                Assert.Equal(belt, pieces.Any(p => p.Joint == 1));
                Assert.Equal(knee, pieces.Any(p => p.Joint == 16) && pieces.Any(p => p.Joint == 20));
            }
        }

        [Fact]
        public void DetailIsDeterministicAndCoarserWhenFar()
        {
            var spec = new SoldierGearSpec(1, 1, true, true, true);
            var a = SoldierGearKit.Build(spec, true); var b = SoldierGearKit.Build(spec, true); var far = SoldierGearKit.Build(spec, false);
            Assert.Equal(a.Select(p => p.Mesh.Positions.Count), b.Select(p => p.Mesh.Positions.Count));
            Assert.True(far.Sum(p => p.Mesh.Indices.Count) < a.Sum(p => p.Mesh.Indices.Count));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoldierGearSpec(3, 0, false, false, false));
        }
    }
}
