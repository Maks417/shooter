#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Bootstrap;
using Shooter.Contracts;
using Shooter.Simulation.Motor;

namespace Shooter.Simulation.Tests.Motor
{
    /// <summary>
    /// Test-only fake with an optional inclined ground plane (y = tan(angle) * z) and solid boxes. Capsule sweeps are
    /// evaluated as exact sphere-versus-primitive distance on the bottom and top spheres (marching plus bisection),
    /// so box edges produce rounded, non-horizontal contact normals like a real physics backend.
    /// </summary>
    internal sealed class SlopeWorld : IWorldQueries, IFakeAdapter
    {
        private readonly List<(Float3 Min, Float3 Max, ulong Id)> boxes = new List<(Float3, Float3, ulong)>();
        private readonly bool hasPlane;
        private readonly float tan;
        private readonly Float3 planeNormal;

        public SlopeWorld(float angleDegrees = 0f, bool plane = true)
        {
            hasPlane = plane;
            double a = angleDegrees * Math.PI / 180.0;
            tan = (float)Math.Tan(a);
            planeNormal = new Float3(0, (float)Math.Cos(a), -(float)Math.Sin(a));
        }

        /// <summary>Resting feet height for a capsule of radius 0.32 standing at z (the sphere centre sits one radius off the plane along its normal).</summary>
        public float GroundHeightAt(float z) => tan * z + 0.32f * (1f / planeNormal.Y - 1f);
        public void AddBox(Float3 min, Float3 max, ulong id) => boxes.Add((min, max, id));

        private bool Nearest(Float3 c, out float dist, out Float3 normal, out ulong id)
        {
            dist = float.MaxValue; normal = new Float3(0, 1, 0); id = 1;
            bool any = false;
            if (hasPlane)
            {
                dist = c.X * planeNormal.X + c.Y * planeNormal.Y + c.Z * planeNormal.Z;
                normal = planeNormal; any = true;
            }
            foreach (var b in boxes)
            {
                float px = Math.Min(Math.Max(c.X, b.Min.X), b.Max.X);
                float py = Math.Min(Math.Max(c.Y, b.Min.Y), b.Max.Y);
                float pz = Math.Min(Math.Max(c.Z, b.Min.Z), b.Max.Z);
                float dx = c.X - px, dy = c.Y - py, dz = c.Z - pz;
                float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (d < 1e-6f) { if (!any || -1f < dist) { dist = -1f; normal = new Float3(0, 1, 0); id = b.Id; any = true; } continue; }
                if (!any || d < dist) { dist = d; normal = new Float3(dx / d, dy / d, dz / d); id = b.Id; any = true; }
            }
            return any;
        }

        private bool SphereSweep(Float3 start, Float3 dir, float length, float radius, out float t, out Float3 normal, out ulong id)
        {
            t = 0;
            Nearest(start, out float d0, out normal, out id);
            if (d0 - radius < -1e-4f) return true;
            if (d0 - radius <= 1e-4f && dir.X * normal.X + dir.Y * normal.Y + dir.Z * normal.Z < -1e-6f) return true;
            const float step = 0.004f;
            float prev = 0f;
            for (float s = step; ; s += step)
            {
                float cur = Math.Min(s, length);
                var p = new Float3(start.X + dir.X * cur, start.Y + dir.Y * cur, start.Z + dir.Z * cur);
                Nearest(p, out float d, out _, out _);
                if (d - radius < -1e-4f)
                {
                    float lo = prev, hi = cur;
                    for (int i = 0; i < 24; i++)
                    {
                        float mid = 0.5f * (lo + hi);
                        var q = new Float3(start.X + dir.X * mid, start.Y + dir.Y * mid, start.Z + dir.Z * mid);
                        Nearest(q, out float dm, out _, out _);
                        if (dm - radius < -1e-4f) hi = mid; else lo = mid;
                    }
                    t = hi;
                    var h = new Float3(start.X + dir.X * hi, start.Y + dir.Y * hi, start.Z + dir.Z * hi);
                    Nearest(h, out _, out normal, out id);
                    return true;
                }
                prev = cur;
                if (cur >= length) return false;
            }
        }

        public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter)
        {
            if (!capsule.IsValid) return new SweepHit(QueryStatus.InvalidInput);
            float len = (float)Math.Sqrt(displacement.LengthSquared);
            if (len == 0) return new SweepHit(QueryStatus.NoHit);
            var dir = new Float3(displacement.X / len, displacement.Y / len, displacement.Z / len);
            bool hit = false; float best = float.MaxValue; Float3 bn = default; ulong bid = 0;
            foreach (var c in new[] { capsule.BottomSphereCenter, capsule.TopSphereCenter })
            {
                if (SphereSweep(c, dir, len, capsule.Radius, out float t, out Float3 n, out ulong id) && t < best)
                { hit = true; best = t; bn = n; bid = id; }
            }
            if (!hit) return new SweepHit(QueryStatus.NoHit);
            var qh = new QueryHit(best, default, bn, default, new SurfaceId(bid + 100), bid, 1, 1);
            return new SweepHit(QueryStatus.Hit, qh);
        }

        public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter)
        {
            if (!SphereSweep(origin, unitDirection, maxMeters, 0f, out float t, out Float3 n, out ulong id))
                return new RayHit(QueryStatus.NoHit);
            var qh = new QueryHit(t, default, n, default, new SurfaceId(id + 100), id, 1, 1);
            return new RayHit(QueryStatus.Hit, qh);
        }

        public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
    }
}
