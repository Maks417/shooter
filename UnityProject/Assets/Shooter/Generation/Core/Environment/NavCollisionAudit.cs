#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// Nav-vs-collision publication gate. A published walkable surface must never lie inside a solid gameplay box:
    /// every nav triangle at a height where a non-walkable box taller than the step would block a standing agent is
    /// clipped against that box's footprint (shrunk by <see cref="FootprintShrinkMeters"/>), and any covered area is
    /// a leak. Every nav vertex must also keep at least agent radius minus <see cref="ClearanceToleranceMeters"/>
    /// from such a box. Pure and deterministic; runs on the bake that is about to be published.
    /// </summary>
    public static class NavCollisionAudit
    {
        public const float FootprintShrinkMeters = 0.05f;
        public const float ClearanceToleranceMeters = 0.05f;
        /// <summary>Clipped overlap below this XZ area (m²) is a touching edge, not coverage.</summary>
        public const float MinLeakAreaSquareMeters = 1e-3f;

        public sealed class Report
        {
            public int BlockingBoxes { get; }
            public int LeakingBoxes { get; }
            public int LeakingTriangles { get; }
            public float LeakAreaSquareMeters { get; }
            public int ClearanceViolations { get; }
            public float MinVertexClearanceMeters { get; }
            public string FirstFinding { get; }
            public bool Passed => LeakingBoxes == 0 && ClearanceViolations == 0;

            public Report(int blockingBoxes, int leakingBoxes, int leakingTriangles, float leakArea, int clearanceViolations, float minClearance, string firstFinding)
            {
                BlockingBoxes = blockingBoxes; LeakingBoxes = leakingBoxes; LeakingTriangles = leakingTriangles;
                LeakAreaSquareMeters = leakArea; ClearanceViolations = clearanceViolations;
                MinVertexClearanceMeters = minClearance; FirstFinding = firstFinding;
            }

            public string Summary() => string.Format(CultureInfo.InvariantCulture,
                "blocking={0};leakingBoxes={1};leakingTriangles={2};leakArea={3:0.###};clearanceViolations={4};minVertexClearance={5:0.###};first={6}",
                BlockingBoxes, LeakingBoxes, LeakingTriangles, LeakAreaSquareMeters, ClearanceViolations, MinVertexClearanceMeters, FirstFinding);
        }

        /// <summary>
        /// True when a non-walkable gameplay box taller than <paramref name="maxStep"/> blocks an agent standing at
        /// <paramref name="surfaceY"/>: the box reaches above the step and starts below the agent's head.
        /// </summary>
        public static bool Blocks(CollisionBox box, float surfaceY, float agentHeight, float maxStep)
        {
            if (!IsSolid(box, maxStep)) return false;
            float minY = box.Center.Y - box.Size.Y * 0.5f;
            float maxY = box.Center.Y + box.Size.Y * 0.5f;
            return maxY > surfaceY + maxStep && minY < surfaceY + agentHeight;
        }

        /// <summary>True when some standing height in [<paramref name="lowY"/>, <paramref name="highY"/>] is blocked by the box.</summary>
        public static bool BlocksRange(CollisionBox box, float lowY, float highY, float agentHeight, float maxStep)
        {
            if (!IsSolid(box, maxStep)) return false;
            float minY = box.Center.Y - box.Size.Y * 0.5f;
            float maxY = box.Center.Y + box.Size.Y * 0.5f;
            return lowY < maxY - maxStep && highY > minY - agentHeight;
        }

        /// <summary>Gameplay boxes the navigation must route around (not stair treads or catwalk decks).</summary>
        public static bool IsSolid(CollisionBox box, float maxStep) =>
            box.Gameplay && !CompoundConnectivity.IsWalkableSurface(box) && box.Size.Y > maxStep;

        public static Report Run(UnityNavMeshBakeCodec.Bake bake, IReadOnlyList<CollisionBox> boxes, float agentRadius, float agentHeight, float maxStep)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            if (agentRadius <= 0f || agentHeight <= 0f || maxStep < 0f) throw new ArgumentOutOfRangeException(nameof(agentRadius));

            var solid = new List<CollisionBox>();
            for (int i = 0; i < boxes.Count; i++) if (IsSolid(boxes[i], maxStep)) solid.Add(boxes[i]);

            var leakingBoxes = new HashSet<int>();
            var leakingTriangles = new HashSet<long>();
            float leakArea = 0f;
            string first = "none";
            var clipA = new List<Float3>(8);
            var clipB = new List<Float3>(8);

            for (int p = 0; p < bake.Polygons.Count; p++)
            {
                var poly = bake.Polygons[p].Indices;
                for (int t = 1; t + 1 < poly.Length; t++)
                {
                    var a = bake.Vertices[(int)poly[0]];
                    var b = bake.Vertices[(int)poly[t]];
                    var c = bake.Vertices[(int)poly[t + 1]];
                    float tminX = Math.Min(a.X, Math.Min(b.X, c.X)), tmaxX = Math.Max(a.X, Math.Max(b.X, c.X));
                    float tminZ = Math.Min(a.Z, Math.Min(b.Z, c.Z)), tmaxZ = Math.Max(a.Z, Math.Max(b.Z, c.Z));
                    float tminY = Math.Min(a.Y, Math.Min(b.Y, c.Y)), tmaxY = Math.Max(a.Y, Math.Max(b.Y, c.Y));
                    for (int s = 0; s < solid.Count; s++)
                    {
                        var box = solid[s];
                        float hx = box.Size.X * 0.5f - FootprintShrinkMeters;
                        float hz = box.Size.Z * 0.5f - FootprintShrinkMeters;
                        if (hx <= 0f || hz <= 0f) continue;
                        float x0 = box.Center.X - hx, x1 = box.Center.X + hx;
                        float z0 = box.Center.Z - hz, z1 = box.Center.Z + hz;
                        if (tmaxX <= x0 || tminX >= x1 || tmaxZ <= z0 || tminZ >= z1) continue;
                        // Cheap vertical reject before clipping: no height on this triangle can be blocked.
                        if (!BlocksRange(box, tminY, tmaxY, agentHeight, maxStep)) continue;

                        clipA.Clear(); clipA.Add(a); clipA.Add(b); clipA.Add(c);
                        ClipRect(clipA, clipB, x0, x1, z0, z1);
                        if (clipA.Count < 3) continue;
                        // Heights over the covered region come from the clipped polygon (plane interpolated on edges).
                        float cminY = clipA[0].Y, cmaxY = clipA[0].Y;
                        for (int k = 1; k < clipA.Count; k++)
                        {
                            cminY = Math.Min(cminY, clipA[k].Y);
                            cmaxY = Math.Max(cmaxY, clipA[k].Y);
                        }
                        if (!BlocksRange(box, cminY, cmaxY, agentHeight, maxStep)) continue;
                        float area = AreaXZ(clipA);
                        if (area < MinLeakAreaSquareMeters) continue;
                        leakArea += area;
                        leakingTriangles.Add(((long)p << 16) | (uint)t);
                        if (leakingBoxes.Add(s) && first == "none")
                            first = string.Format(CultureInfo.InvariantCulture, "leak:box({0:0.##},{1:0.##},{2:0.##} size {3:0.##}x{4:0.##}x{5:0.##}) area {6:0.###}",
                                box.Center.X, box.Center.Y, box.Center.Z, box.Size.X, box.Size.Y, box.Size.Z, area);
                    }
                }
            }

            int violations = 0;
            float minClearance = float.PositiveInfinity;
            float required = agentRadius - ClearanceToleranceMeters;
            for (int v = 0; v < bake.Vertices.Count; v++)
            {
                var vertex = bake.Vertices[v];
                for (int s = 0; s < solid.Count; s++)
                {
                    var box = solid[s];
                    float dx = Math.Max(0f, Math.Abs(vertex.X - box.Center.X) - box.Size.X * 0.5f);
                    float dz = Math.Max(0f, Math.Abs(vertex.Z - box.Center.Z) - box.Size.Z * 0.5f);
                    if (dx >= required || dz >= required) continue;
                    if (!Blocks(box, vertex.Y, agentHeight, maxStep)) continue;
                    float d = MathF.Sqrt(dx * dx + dz * dz);
                    if (d < minClearance) minClearance = d;
                    if (d >= required) continue;
                    violations++;
                    if (first == "none")
                        first = string.Format(CultureInfo.InvariantCulture, "clearance:vertex({0:0.##},{1:0.##},{2:0.##}) {3:0.###}m from box({4:0.##},{5:0.##},{6:0.##})",
                            vertex.X, vertex.Y, vertex.Z, d, box.Center.X, box.Center.Y, box.Center.Z);
                }
            }
            if (float.IsPositiveInfinity(minClearance)) minClearance = required;
            return new Report(solid.Count, leakingBoxes.Count, leakingTriangles.Count, leakArea, violations, minClearance, first);
        }

        /// <summary>Throws when the bake would let agents walk inside, or brush through, solid gameplay collision.</summary>
        public static Report ThrowIfLeaky(string mapId, UnityNavMeshBakeCodec.Bake bake, IReadOnlyList<CollisionBox> boxes, float agentRadius, float agentHeight, float maxStep)
        {
            var report = Run(bake, boxes, agentRadius, agentHeight, maxStep);
            if (!report.Passed)
                throw new InvalidOperationException("nav-collision-audit-failed:" + mapId + ":" + report.Summary());
            return report;
        }

        private static void ClipRect(List<Float3> poly, List<Float3> scratch, float x0, float x1, float z0, float z1)
        {
            ClipAxis(poly, scratch, true, x0, true);
            ClipAxis(poly, scratch, true, x1, false);
            ClipAxis(poly, scratch, false, z0, true);
            ClipAxis(poly, scratch, false, z1, false);
        }

        /// <summary>Sutherland–Hodgman against one axis-aligned half-plane; Y is interpolated along clipped edges.</summary>
        private static void ClipAxis(List<Float3> poly, List<Float3> scratch, bool xAxis, float bound, bool keepGreater)
        {
            scratch.Clear();
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var cur = poly[i];
                var prev = poly[(i + n - 1) % n];
                float cv = xAxis ? cur.X : cur.Z, pv = xAxis ? prev.X : prev.Z;
                bool cin = keepGreater ? cv >= bound : cv <= bound;
                bool pin = keepGreater ? pv >= bound : pv <= bound;
                if (cin != pin)
                {
                    float t = (bound - pv) / (cv - pv);
                    scratch.Add(new Float3(prev.X + (cur.X - prev.X) * t, prev.Y + (cur.Y - prev.Y) * t, prev.Z + (cur.Z - prev.Z) * t));
                }
                if (cin) scratch.Add(cur);
            }
            poly.Clear();
            poly.AddRange(scratch);
        }

        private static float AreaXZ(List<Float3> poly)
        {
            float sum = 0f;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                sum += poly[j].X * poly[i].Z - poly[i].X * poly[j].Z;
            return Math.Abs(sum) * 0.5f;
        }
    }
}
