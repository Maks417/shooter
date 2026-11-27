#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Combat;
using Shooter.Simulation.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    /// <summary>
    /// ADR-129: authoritative grenade flight against world geometry. The reported hit point never places the grenade,
    /// so a PhysX start contact (distance 0, point (0,0,0)) cannot teleport it to the map origin.
    /// </summary>
    public sealed class GrenadeSimulatorTests
    {
        /// <summary>
        /// Exact sphere sweeps against the y = 0 ground and solid boxes. With <c>startContact</c> it reproduces the PhysX
        /// multi-hit cast: a sphere already within the contact offset of any surface reports distance 0, point zero and
        /// normal = -direction, whichever way it moves.
        /// </summary>
        private sealed class SphereWorld : IWorldQueries
        {
            public const float ContactOffset = 0.01f;
            private readonly bool startContact;
            private readonly List<(Float3 Min, Float3 Max, ulong Id)> boxes = new List<(Float3, Float3, ulong)>();
            public SphereWorld(bool startContact = false) { this.startContact = startContact; }
            public void AddBox(Float3 min, Float3 max, ulong id) => boxes.Add((min, max, id));

            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter) => new RayHit(QueryStatus.NoHit);

            private static QueryHit Hit(float distance, Float3 point, Float3 normal, ulong id) =>
                new QueryHit(distance, point, normal, default, new SurfaceId((uint)id), id, 1, 1);

            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter)
            {
                var c = capsule.BottomSphereCenter; float r = capsule.Radius;
                float len = (float)Math.Sqrt(displacement.LengthSquared);
                var dir = new Float3(displacement.X / len, displacement.Y / len, displacement.Z / len);
                if (startContact && Gap(c, r) < ContactOffset)
                    return new SweepHit(QueryStatus.Hit, Hit(0f, default, new Float3(-dir.X, -dir.Y, -dir.Z), 100));
                float best = float.MaxValue; Float3 normal = default; ulong id = 0;
                if (dir.Y < -1e-6f)
                {
                    float t = Math.Max(0f, (c.Y - r) / -dir.Y);
                    if (t <= len) { best = t; normal = new Float3(0, 1, 0); id = 100; }
                }
                foreach (var box in boxes)
                {
                    var min = new Float3(box.Min.X - r, box.Min.Y - r, box.Min.Z - r);
                    var max = new Float3(box.Max.X + r, box.Max.Y + r, box.Max.Z + r);
                    if (RayBox(c, dir, len, min, max, out float t, out var n) && t < best) { best = t; normal = n; id = box.Id; }
                }
                if (id == 0) return new SweepHit(QueryStatus.NoHit);
                var centre = new Float3(c.X + dir.X * best, c.Y + dir.Y * best, c.Z + dir.Z * best);
                var point = new Float3(centre.X - normal.X * r, centre.Y - normal.Y * r, centre.Z - normal.Z * r);
                return new SweepHit(QueryStatus.Hit, Hit(best, point, normal, id));
            }

            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned)
            {
                var c = capsule.BottomSphereCenter; float r = capsule.Radius;
                int n = 0;
                if (c.Y < r) callerOwned[n++] = Hit(r - c.Y, new Float3(c.X, 0, c.Z), new Float3(0, 1, 0), 100);
                foreach (var box in boxes)
                {
                    var q = Closest(c, box.Min, box.Max);
                    float d = Distance(c, q);
                    if (d >= r) continue;
                    var normal = d > 1e-6f ? new Float3((c.X - q.X) / d, (c.Y - q.Y) / d, (c.Z - q.Z) / d) : new Float3(0, 1, 0);
                    callerOwned[n++] = Hit(r - d, q, normal, box.Id);
                }
                var result = new OverlapResult(n == 0 ? QueryStatus.NoHit : QueryStatus.Hit, n, n);
                if (n > 0) callerOwned.SortComplete(result);
                return result;
            }

            private float Gap(Float3 c, float r)
            {
                float gap = c.Y - r;
                foreach (var box in boxes) gap = Math.Min(gap, Distance(c, Closest(c, box.Min, box.Max)) - r);
                return gap;
            }

            private static Float3 Closest(Float3 c, Float3 min, Float3 max) => new Float3(
                Math.Clamp(c.X, min.X, max.X), Math.Clamp(c.Y, min.Y, max.Y), Math.Clamp(c.Z, min.Z, max.Z));

            private static bool RayBox(Float3 o, Float3 d, float len, Float3 min, Float3 max, out float t, out Float3 normal)
            {
                t = 0f; normal = default;
                float tmin = 0f, tmax = len;
                float[] oo = { o.X, o.Y, o.Z }, dd = { d.X, d.Y, d.Z }, lo = { min.X, min.Y, min.Z }, hi = { max.X, max.Y, max.Z };
                int axis = -1; float sign = 0f;
                for (int a = 0; a < 3; a++)
                {
                    if (Math.Abs(dd[a]) < 1e-8f) { if (oo[a] < lo[a] || oo[a] > hi[a]) return false; continue; }
                    float t1 = (lo[a] - oo[a]) / dd[a], t2 = (hi[a] - oo[a]) / dd[a];
                    float s = -1f;
                    if (t1 > t2) { (t1, t2) = (t2, t1); s = 1f; }
                    if (t1 > tmin) { tmin = t1; axis = a; sign = s; }
                    tmax = Math.Min(tmax, t2);
                    if (tmin > tmax) return false;
                }
                if (axis < 0) return false; // starts inside the expanded box: left to the start-contact path
                t = tmin;
                normal = axis == 0 ? new Float3(sign, 0, 0) : axis == 1 ? new Float3(0, sign, 0) : new Float3(0, 0, sign);
                return true;
            }
        }

        private const float Dt = 1f / 60f;

        private static (GrenadeSimulator sim, LiveGrenade g) Throw(Float3 origin, Float3 aim, uint fuseTicks = 120)
        {
            var sim = new GrenadeSimulator();
            Assert.True(sim.TrySpawn(new EntityId(7), 1, origin, aim, WeaponDefinition.Grenade, fuseTicks, 0, 1, out var g));
            return (sim, g);
        }

        private static GrenadeExplosion RunToBlast(GrenadeSimulator sim, LiveGrenade g, IWorldQueries world, Action<Float3>? each = null)
        {
            var blasts = new List<GrenadeExplosion>();
            for (uint tick = 1; tick < 400; tick++)
            {
                var before = g.Position;
                sim.Step(tick, Dt, world, blasts);
                if (blasts.Count > 0) return blasts[0];
                float jump = Distance(before, g.Position);
                Assert.True(jump < 1f, $"tick {tick}: grenade jumped {jump} m from {F(before)} to {F(g.Position)}");
                each?.Invoke(g.Position);
            }
            throw new InvalidOperationException("grenade never exploded");
        }

        private static string F(Float3 p) => $"({p.X:0.###}, {p.Y:0.###}, {p.Z:0.###})";

        private static float Distance(Float3 a, Float3 b) =>
            (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

        [Fact]
        public void FloorBounceExplodesWhereTheGrenadeRolledNotAtTheOrigin()
        {
            var world = new SphereWorld();
            var (sim, g) = Throw(new Float3(20, 1.5f, 20), new Float3(0, 0, 1));
            var blast = RunToBlast(sim, g, world, p => Assert.True(p.Y >= GrenadeSimulator.Radius - 1e-4f, $"below the floor at {F(p)}"));
            Assert.True(blast.Position.Z > 24f, $"blast at {F(blast.Position)}");
            Assert.InRange(blast.Position.X, 19.9f, 20.1f);
            Assert.InRange(blast.Position.Y, GrenadeSimulator.Radius, GrenadeSimulator.Radius + .1f);
        }

        [Fact]
        public void BouncesThatStartTouchingTheFloorNeverTeleportTheGrenade()
        {
            // Every sweep that starts within the contact offset of the floor reports point zero; the grenade must keep
            // rolling where it landed.
            var world = new SphereWorld(startContact: true);
            var (sim, g) = Throw(new Float3(30, 1.5f, -30), new Float3(0, 0, -1));
            var blast = RunToBlast(sim, g, world);
            Assert.True(Distance(blast.Position, default) > 20f, $"blast at {F(blast.Position)}");
            Assert.True(blast.Position.Z < -32f, $"blast at {F(blast.Position)}");
            Assert.True(blast.Position.Y > 0f);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WallBounceReflectsAndExplodesInFrontOfTheWall(bool startContact)
        {
            var world = new SphereWorld(startContact);
            world.AddBox(new Float3(-5, 0, 3), new Float3(5, 4, 3.3f), 42);
            var (sim, g) = Throw(new Float3(0, 1.5f, 0), new Float3(0, 0, 1));
            float maxZ = float.MinValue;
            var blast = RunToBlast(sim, g, world, p => maxZ = Math.Max(maxZ, p.Z));
            Assert.True(maxZ <= 3f - GrenadeSimulator.Radius + 1e-3f, $"entered the wall: max z {maxZ}");
            Assert.True(blast.Position.Z < 3f, $"blast at {F(blast.Position)}");
            Assert.True(blast.Position.Z > -6f, $"blast at {F(blast.Position)}");
        }

        [Fact]
        public void ThrowAgainstAWallStartsOutsideIt()
        {
            var world = new SphereWorld();
            world.AddBox(new Float3(-5, 0, .34f), new Float3(5, 4, .64f), 42);
            var sim = new GrenadeSimulator();
            var eye = new Float3(0, 1.6f, 0);
            var muzzle = new Float3(.18f, 1.45f, .45f);
            var origin = sim.SafeLaunchOrigin(eye, muzzle, world);
            Assert.True(origin.Z + GrenadeSimulator.Radius <= .34f, $"origin {F(origin)} is inside the wall");
            Assert.Equal(muzzle, sim.SafeLaunchOrigin(eye, muzzle, new SphereWorld()));
        }
    }
}
