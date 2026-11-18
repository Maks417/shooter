#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// Visual-only petroleum dressing for <c>oil-depot</c>, appended to the packaged solids mesh.
    /// Never touches <see cref="CompoundPlan.CollisionBoxes"/>: collision, navigation and route hashes are
    /// unchanged. Ground-touching pieces sit inside or on top of existing gameplay solids, against the
    /// perimeter walls, or outside the playable rectangle; free spans hang above jump height.
    /// </summary>
    public static class OilDepotDressing
    {
        public const string MapId = "oil-depot";

        /// <summary>Gameplay tank drums from <see cref="OilDepotSitePlanner.Tanks"/> and their z-mirror twins (centre x/z, planner radius, height).</summary>
        private static readonly float[,] Tanks = BuildTankTable();

        private static float[,] BuildTankTable()
        {
            int n = OilDepotSitePlanner.Tanks.GetLength(0);
            var t = new float[n * 2, 4];
            for (int i = 0; i < n; i++)
            for (int m = 0; m < 2; m++)
            {
                t[i * 2 + m, 0] = OilDepotSitePlanner.Tanks[i, 0];
                t[i * 2 + m, 1] = m == 0 ? OilDepotSitePlanner.Tanks[i, 1] : -OilDepotSitePlanner.Tanks[i, 1];
                t[i * 2 + m, 2] = OilDepotSitePlanner.Tanks[i, 2];
                t[i * 2 + m, 3] = OilDepotSitePlanner.Tanks[i, 3];
            }
            return t;
        }

        /// <summary>Visual shell radius encloses the square drum corners (0.85 r × √2).</summary>
        public const float ShellRadiusFactor = 1.21f;

        private const float S = 0.70710678f;
        private static readonly Quat YUp = new Quat(-S, 0f, 0f, S);
        private static readonly Quat AlongX = new Quat(0f, S, 0f, S);
        private static readonly Quat AlongZ = Quat.Identity;

        public static int TankCount => Tanks.GetLength(0);

        public static float TankRadius(int i) => Tanks[i, 2] * ShellRadiusFactor;
        public static Float3 TankBase(int i) => new Float3(Tanks[i, 0], 0f, Tanks[i, 1]);
        public static float TankHeight(int i) => Tanks[i, 3];

        /// <summary>
        /// True for boxes the round tank shells replace visually: the planner's square drum/buttress boxes and
        /// the legacy square crown slabs. Their collision stays authoritative inside the round shell.
        /// </summary>
        public static bool ReplacedByDressing(CollisionBox box)
        {
            if (!box.Gameplay && Math.Abs(box.Size.Y - 0.12f) < 0.005f &&
                Math.Abs(box.Size.X - box.Size.Z) < 0.01f && box.Size.X > 6f && box.Center.X < -20f)
                return true;
            if (!box.Gameplay) return false;
            float top = box.Center.Y + box.Size.Y * 0.5f;
            for (int i = 0; i < Tanks.GetLength(0); i++)
            {
                float r = Tanks[i, 2];
                if (Math.Abs(top - Tanks[i, 3]) > 0.01f) continue;
                if (Math.Abs(box.Center.X - Tanks[i, 0]) > r || Math.Abs(box.Center.Z - Tanks[i, 1]) > r) continue;
                if (box.Size.X > r * 1.7f + 0.01f || box.Size.Z > r * 1.7f + 0.01f) continue;
                return true;
            }
            return false;
        }

        public static void Append(MeshAssembly mesh, CompoundPlan plan)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var layout = plan.Arena ?? throw new ArgumentException("Arena plan required.", nameof(plan));
            var site = plan.Site;
            for (int i = 0; i < Tanks.GetLength(0); i++)
                AddStorageTank(mesh, Tanks[i, 0], Tanks[i, 1], TankRadius(i), Tanks[i, 3], ladderTowardNegativeX: true);
            AddTankHeaders(mesh, layout);
            AddWallPipeRacks(mesh, layout, site);
            AddOverheadRacks(mesh, plan);
            AddPumpHouseRoofPlant(mesh);
            AddSkyline(mesh, layout);
        }

        private static void Box(MeshAssembly mesh, Float3 center, Float3 size, MapSolidsMaterialRole role)
            => mesh.Add(PrimitiveGeometry.Box(size), center, Quat.Identity, (ushort)role);

        private static void BoxY(MeshAssembly mesh, Float3 center, Float3 size, float yaw, MapSolidsMaterialRole role)
            => mesh.Add(PrimitiveGeometry.Box(size), center,
                new Quat(0f, (float)Math.Sin(yaw * 0.5), 0f, (float)Math.Cos(yaw * 0.5)), (ushort)role);

        private static void Vertical(MeshAssembly mesh, float x, float y0, float y1, float z, float radius, int segments,
            MapSolidsMaterialRole role)
            => mesh.Add(PrimitiveGeometry.Cylinder(radius, y1 - y0, segments), new Float3(x, (y0 + y1) * 0.5f, z), YUp, (ushort)role);

        private static void PipeX(MeshAssembly mesh, float x0, float x1, float y, float z, float radius, MapSolidsMaterialRole role)
            => mesh.Add(PrimitiveGeometry.Cylinder(radius, Math.Abs(x1 - x0), 10), new Float3((x0 + x1) * 0.5f, y, z), AlongX, (ushort)role);

        private static void PipeZ(MeshAssembly mesh, float x, float y, float z0, float z1, float radius, MapSolidsMaterialRole role)
            => mesh.Add(PrimitiveGeometry.Cylinder(radius, Math.Abs(z1 - z0), 10), new Float3(x, y, (z0 + z1) * 0.5f), AlongZ, (ushort)role);

        private static void AddStorageTank(MeshAssembly mesh, float cx, float cz, float radius, float height, bool ladderTowardNegativeX)
        {
            // Welded shell on a dark footing ring, two strake bands and a low conical roof.
            Vertical(mesh, cx, 0f, height, cz, radius, 32, MapSolidsMaterialRole.TankWhite);
            Vertical(mesh, cx, 0f, 0.32f, cz, radius + 0.1f, 32, MapSolidsMaterialRole.Foundation);
            Vertical(mesh, cx, height * 0.36f - 0.05f, height * 0.36f + 0.05f, cz, radius + 0.03f, 32, MapSolidsMaterialRole.Foundation);
            Vertical(mesh, cx, height * 0.70f - 0.05f, height * 0.70f + 0.05f, cz, radius + 0.03f, 32, MapSolidsMaterialRole.Foundation);
            mesh.Add(PrimitiveGeometry.Cone(radius + 0.06f, radius * 0.22f, 0.85f, 32),
                new Float3(cx, height + 0.425f, cz), YUp, (ushort)MapSolidsMaterialRole.TankWhite);
            Vertical(mesh, cx, height + 0.85f, height + 1.05f, cz, radius * 0.22f, 16, MapSolidsMaterialRole.MetalCover);

            // Roof-edge guard rail: posts plus tangent rail segments.
            const int railSegments = 16;
            float railR = radius - 0.12f;
            float chord = 2f * railR * (float)Math.Sin(Math.PI / railSegments);
            for (int i = 0; i < railSegments; i++)
            {
                double a = 2 * Math.PI * i / railSegments;
                float px = cx + railR * (float)Math.Cos(a), pz = cz + railR * (float)Math.Sin(a);
                Box(mesh, new Float3(px, height + 0.55f, pz), new Float3(0.05f, 1.1f, 0.05f), MapSolidsMaterialRole.SafetyYellow);
                double m = a + Math.PI / railSegments;
                float mx = cx + railR * (float)Math.Cos(Math.PI / railSegments) * (float)Math.Cos(m);
                float mz = cz + railR * (float)Math.Cos(Math.PI / railSegments) * (float)Math.Sin(m);
                BoxY(mesh, new Float3(mx, height + 1.08f, mz), new Float3(0.05f, 0.05f, chord), (float)-m, MapSolidsMaterialRole.SafetyYellow);
            }

            // Caged access ladder on the wall-facing side.
            float side = ladderTowardNegativeX ? -1f : 1f;
            float lx = cx + side * (radius + 0.22f);
            float top = height + 1.1f;
            foreach (float off in new[] { -0.24f, 0.24f })
                Box(mesh, new Float3(lx, top * 0.5f, cz + off), new Float3(0.06f, top, 0.06f), MapSolidsMaterialRole.SafetyYellow);
            for (float y = 0.4f; y < height; y += 0.35f)
                Box(mesh, new Float3(lx, y, cz), new Float3(0.04f, 0.04f, 0.48f), MapSolidsMaterialRole.MetalCover);
            for (float y = 2.4f; y <= height + 0.6f; y += 0.8f)
            {
                Box(mesh, new Float3(lx + side * 0.36f, y, cz), new Float3(0.04f, 0.06f, 0.72f), MapSolidsMaterialRole.SafetyYellow);
                Box(mesh, new Float3(lx + side * 0.18f, y, cz - 0.36f), new Float3(0.36f, 0.06f, 0.04f), MapSolidsMaterialRole.SafetyYellow);
                Box(mesh, new Float3(lx + side * 0.18f, y, cz + 0.36f), new Float3(0.36f, 0.06f, 0.04f), MapSolidsMaterialRole.SafetyYellow);
            }
        }

        /// <summary>Overhead outlet pipes from each farm tank to the west-wall header, above jump height.</summary>
        private static void AddTankHeaders(MeshAssembly mesh, ArenaLayout layout)
        {
            float wallFace = -layout.HalfExtentX;
            for (int i = 0; i < Tanks.GetLength(0); i++)
            {
                float cx = Tanks[i, 0], cz = Tanks[i, 1], r = TankRadius(i);
                if (!HeaderRunClear(i)) continue;
                float y = 4.05f;
                PipeX(mesh, wallFace + 0.25f, cx - r + 0.05f, y, cz + 0.9f, 0.16f, MapSolidsMaterialRole.MetalCover);
                // Gate valve body + handwheel where the outlet leaves the shell.
                Box(mesh, new Float3(cx - r - 0.45f, y, cz + 0.9f), new Float3(0.42f, 0.42f, 0.42f), MapSolidsMaterialRole.OxidePaint);
                Vertical(mesh, cx - r - 0.45f, y + 0.21f, y + 0.55f, cz + 0.9f, 0.03f, 6, MapSolidsMaterialRole.MetalCover);
                Vertical(mesh, cx - r - 0.45f, y + 0.55f, y + 0.6f, cz + 0.9f, 0.22f, 12, MapSolidsMaterialRole.OxidePaint);
            }
        }

        /// <summary>Wall-mounted product lines on all four perimeter walls, brackets every four metres.</summary>
        /// <summary>True when the header from tank <paramref name="i"/> to the west wall passes no other tank.</summary>
        private static bool HeaderRunClear(int i)
        {
            float z = Tanks[i, 1] + 0.9f;
            for (int j = 0; j < Tanks.GetLength(0); j++)
            {
                if (j == i || Tanks[j, 0] >= Tanks[i, 0]) continue;
                if (Math.Abs(z - Tanks[j, 1]) <= TankRadius(j) + 0.3f) return false;
            }
            return true;
        }

        /// <summary>True when the wall-face point (x, z) is the back wall of a site building (no exterior pipes inside rooms).</summary>
        private static bool BehindBuilding(SiteLayout? site, float x, float z)
        {
            if (site == null) return false;
            for (int i = 0; i < site.Buildings.Count; i++)
                if (site.Buildings[i].Footprint.Contains(x, z, 0.6f)) return true;
            return false;
        }

        /// <summary>Pipe along a perimeter wall, split wherever a site building backs onto that wall.</summary>
        private static void PipeRun(MeshAssembly mesh, SiteLayout? site, bool alongX, float fixedCoord, float from, float to, float y, float radius,
            MapSolidsMaterialRole role)
        {
            float start = float.NaN;
            const float step = 0.5f;
            for (float t = from; ; t += step)
            {
                bool done = t >= to;
                float tt = Math.Min(t, to);
                bool blocked = BehindBuilding(site, alongX ? tt : fixedCoord, alongX ? fixedCoord : tt);
                if (!blocked && float.IsNaN(start)) start = tt;
                if ((blocked || done) && !float.IsNaN(start))
                {
                    float end = blocked ? tt - step : tt;
                    if (end - start >= 1f)
                    {
                        if (alongX) PipeX(mesh, start, end, y, fixedCoord, radius, role);
                        else PipeZ(mesh, fixedCoord, y, start, end, radius, role);
                    }
                    start = float.NaN;
                }
                if (done) break;
            }
        }

        private static void AddWallPipeRacks(MeshAssembly mesh, ArenaLayout layout, SiteLayout? site)
        {
            float hx = layout.HalfExtentX, hz = layout.HalfExtentZ;
            float[] heights = { 3.25f, 3.7f, 4.15f };
            float[] radii = { 0.16f, 0.11f, 0.2f };
            MapSolidsMaterialRole[] roles = { MapSolidsMaterialRole.OxidePaint, MapSolidsMaterialRole.SafetyYellow, MapSolidsMaterialRole.MetalCover };
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * (hx - 0.3f);
                for (int p = 0; p < heights.Length; p++)
                    PipeRun(mesh, site, false, x, -hz + 0.2f, hz - 0.2f, heights[p], radii[p], roles[p]);
                for (float z = -hz + 2f; z < hz - 1f; z += 4f)
                    if (!BehindBuilding(site, x, z)) Box(mesh, new Float3(sx * (hx - 0.22f), 3.7f, z), new Float3(0.44f, 1.3f, 0.08f), MapSolidsMaterialRole.Foundation);
            }
            foreach (float sz in new[] { -1f, 1f })
            {
                float z = sz * (hz - 0.3f);
                for (int p = 0; p < heights.Length; p++)
                    PipeRun(mesh, site, true, z, -hx + 0.6f, hx - 0.6f, heights[p], radii[p], roles[p]);
                for (float x = -hx + 2f; x < hx - 1f; x += 4f)
                    if (!BehindBuilding(site, x, z)) Box(mesh, new Float3(x, 3.7f, sz * (hz - 0.22f)), new Float3(0.08f, 1.3f, 0.44f), MapSolidsMaterialRole.Foundation);
            }
        }

        /// <summary>
        /// Pipes laid on every overhead site run (pipe racks wholly above head height, hosted by a zone): three lines on
        /// the beam. Nothing is added below the beam, so nothing visible stands where players walk.
        /// </summary>
        private static void AddOverheadRacks(MeshAssembly mesh, CompoundPlan plan)
        {
            if (plan.Site == null) return;
            MapSolidsMaterialRole[] roles = { MapSolidsMaterialRole.TankWhite, MapSolidsMaterialRole.OxidePaint, MapSolidsMaterialRole.SafetyYellow };
            float[] offs = { -0.35f, 0f, 0.35f };
            for (int i = 0; i < plan.CollisionBoxes.Count; i++)
            {
                var b = plan.CollisionBoxes[i];
                float bottom = b.Center.Y - b.Size.Y * 0.5f;
                if (!b.Gameplay || bottom < CompoundConnectivity.AgentHeightMeters + 0.5f) continue;
                if (!plan.Site.TryGetBox(b.StableId, out var info) || info.HostKind != SiteHostKind.Zone) continue;
                bool alongX = b.Size.X >= b.Size.Z;
                float half = (alongX ? b.Size.X : b.Size.Z) * 0.5f;
                float top = b.Center.Y + b.Size.Y * 0.5f;
                for (int p = 0; p < 3; p++)
                {
                    if (alongX) PipeX(mesh, b.Center.X - half, b.Center.X + half, top + 0.2f, b.Center.Z + offs[p], 0.14f, roles[p]);
                    else PipeZ(mesh, b.Center.X + offs[p], top + 0.2f, b.Center.Z - half, b.Center.Z + half, 0.14f, roles[p]);
                }
            }
        }

        /// <summary>Pump-house roof plant: header manifold, vents and a motor housing (roof top ≈ 4.15 m).</summary>
        private static void AddPumpHouseRoofPlant(MeshAssembly mesh)
        {
            const float roof = 4.8f;
            PipeZ(mesh, -5f, roof + 0.35f, -5.8f, 5.8f, 0.22f, MapSolidsMaterialRole.TankWhite);
            PipeZ(mesh, -4.3f, roof + 0.3f, -5.8f, 5.8f, 0.15f, MapSolidsMaterialRole.OxidePaint);
            for (float z = -5f; z <= 5f; z += 2.5f)
                Box(mesh, new Float3(-4.65f, roof + 0.12f, z), new Float3(1.2f, 0.24f, 0.2f), MapSolidsMaterialRole.Foundation);
            Box(mesh, new Float3(3f, roof + 0.7f, 2.5f), new Float3(3.2f, 1.4f, 2.2f), MapSolidsMaterialRole.MetalCover);
            Box(mesh, new Float3(3f, roof + 1.45f, 2.5f), new Float3(3.4f, 0.1f, 2.4f), MapSolidsMaterialRole.Foundation);
            foreach (float vx in new[] { 1f, 5f })
            {
                Vertical(mesh, vx, roof, roof + 2.6f, -3.5f, 0.18f, 12, MapSolidsMaterialRole.MetalCover);
                mesh.Add(PrimitiveGeometry.Cone(0.32f, 0.05f, 0.3f, 12), new Float3(vx, roof + 2.75f, -3.5f), YUp,
                    (ushort)MapSolidsMaterialRole.MetalCover);
            }
        }

        /// <summary>Background refinery skyline outside the perimeter: large tanks and a banded flare stack.</summary>
        private static void AddSkyline(MeshAssembly mesh, ArenaLayout layout)
        {
            float wx = layout.HalfExtentX;
            AddStorageTank(mesh, -wx - 15f, -20f, 7.5f, 11f, ladderTowardNegativeX: false);
            AddStorageTank(mesh, -wx - 17f, 6f, 6.5f, 9.5f, ladderTowardNegativeX: false);
            AddStorageTank(mesh, -wx - 14f, 30f, 7f, 12f, ladderTowardNegativeX: false);
            AddStorageTank(mesh, wx + 14f, -26f, 6f, 9f, ladderTowardNegativeX: true);
            AddStorageTank(mesh, wx + 15f, 22f, 6.5f, 10f, ladderTowardNegativeX: true);

            // Flare stack with alternating aviation bands, platform and tip.
            float fx = -wx - 9f, fz = -42f;
            const float stackHeight = 26f;
            const int bands = 8;
            for (int i = 0; i < bands; i++)
            {
                float y0 = stackHeight * i / bands, y1 = stackHeight * (i + 1) / bands;
                Vertical(mesh, fx, y0, y1, fz, 0.55f, 16, i % 2 == 0 ? MapSolidsMaterialRole.TankWhite : MapSolidsMaterialRole.OxidePaint);
            }
            Vertical(mesh, fx, stackHeight - 3f, stackHeight - 2.85f, fz, 1.6f, 16, MapSolidsMaterialRole.Foundation);
            Vertical(mesh, fx, stackHeight, stackHeight + 1.2f, fz, 0.7f, 16, MapSolidsMaterialRole.Foundation);
            Vertical(mesh, fx, 0f, 0.6f, fz, 1.4f, 16, MapSolidsMaterialRole.Foundation);

            // Background pipe bridge linking the skyline tanks along the west boundary.
            PipeZ(mesh, -wx - 6f, 7.5f, -40f, 40f, 0.3f, MapSolidsMaterialRole.TankWhite);
            PipeZ(mesh, -wx - 6f, 7.5f + 0.7f, -40f, 40f, 0.22f, MapSolidsMaterialRole.OxidePaint);
            for (float z = -38f; z <= 38f; z += 8f)
            {
                Box(mesh, new Float3(-wx - 6f, 3.75f, z), new Float3(0.35f, 7.5f, 0.35f), MapSolidsMaterialRole.Foundation);
                Box(mesh, new Float3(-wx - 6f, 7.2f, z), new Float3(1.4f, 0.25f, 0.3f), MapSolidsMaterialRole.Foundation);
            }
        }
    }
}
