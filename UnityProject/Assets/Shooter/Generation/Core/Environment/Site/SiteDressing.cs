#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// ADR-109 mesh-only dressing for site-planned maps, appended to the packaged solids mesh after the boxes and the
    /// feature dressing. It reads <see cref="CompoundPlan.Site"/>: pitched roofs with stepped gables on town and harbour
    /// buildings, parapets and roof plant on industrial ones, a church tower and spire, and an out-of-bounds backdrop per
    /// boundary side (rows of town houses, the harbour water with a moored freighter). Everything sits on or above the
    /// gameplay roofs or outside the perimeter, so collision, navigation and package identity never depend on it.
    /// </summary>
    public static class SiteDressing
    {
        private const float S = 0.70710678f;
        private static readonly Quat YUp = new Quat(-S, 0f, 0f, S);
        private const float RoofPitch = 0.52f; // radians, ~30 degrees

        public static void Append(MeshAssembly mesh, CompoundPlan plan)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var site = plan.Site;
            var arena = plan.Arena;
            if (site == null || arena == null) return;
            for (int i = 0; i < site.Buildings.Count; i++)
            {
                var b = site.Buildings[i];
                if (Pitched(b.Kind)) AddGableRoof(mesh, b.Footprint, b.Height, b.Kind == SiteBuildingKind.Church ? 0.65f : RoofPitch);
                else AddParapetRoof(mesh, b.Footprint, b.Height, i);
                if (b.Kind == SiteBuildingKind.Church) AddChurchTower(mesh, b);
            }
            AddBackdrop(mesh, arena, site.South, ArenaSide.South);
            AddBackdrop(mesh, arena, site.North, ArenaSide.North);
            AddBackdrop(mesh, arena, site.East, ArenaSide.East);
            AddBackdrop(mesh, arena, site.West, ArenaSide.West);
        }

        private static bool Pitched(SiteBuildingKind kind)
            => kind == SiteBuildingKind.House || kind == SiteBuildingKind.Shop || kind == SiteBuildingKind.Inn ||
               kind == SiteBuildingKind.TownHall || kind == SiteBuildingKind.Church || kind == SiteBuildingKind.Office ||
               kind == SiteBuildingKind.Customs || kind == SiteBuildingKind.Gatehouse;

        private static void Box(MeshAssembly mesh, Float3 c, Float3 size, MapSolidsMaterialRole role)
            => mesh.Add(PrimitiveGeometry.Box(size), c, Quat.Identity, (ushort)role);

        /// <summary>Two pitched slabs over the long axis plus crow-stepped gable ends, eaves resting on the flat roof slab.</summary>
        private static void AddGableRoof(MeshAssembly mesh, SiteRect r, float wallHeight, float pitch)
        {
            bool alongX = r.SizeX >= r.SizeZ;
            float length = (alongX ? r.SizeX : r.SizeZ) + 0.6f;
            float half = (alongX ? r.SizeZ : r.SizeX) * 0.5f + 0.3f;
            float eave = wallHeight + 0.3f;
            float rise = half * (float)Math.Tan(pitch);
            float slope = half / (float)Math.Cos(pitch);
            for (int side = -1; side <= 1; side += 2)
            {
                // Rotate about the ridge axis so the slab falls toward its own (outer) side.
                float angle = -side * pitch;
                Quat q = alongX
                    ? new Quat((float)Math.Sin(angle * 0.5), 0f, 0f, (float)Math.Cos(angle * 0.5))
                    : new Quat(0f, 0f, (float)Math.Sin(angle * 0.5), (float)Math.Cos(angle * 0.5));
                float across = side * half * 0.5f;
                var c = alongX
                    ? new Float3(r.CenterX, eave + rise * 0.5f, r.CenterZ + across)
                    : new Float3(r.CenterX + across, eave + rise * 0.5f, r.CenterZ);
                var size = alongX ? new Float3(length, 0.16f, slope) : new Float3(slope, 0.16f, length);
                mesh.Add(PrimitiveGeometry.Box(size), c, q, (ushort)MapSolidsMaterialRole.OxidePaint);
            }
            // Crow-stepped gables fill the triangle under each end of the roof.
            const int steps = 4;
            for (int end = -1; end <= 1; end += 2)
            {
                for (int k = 0; k < steps; k++)
                {
                    float w = (half - 0.3f) * 2f * (steps - k) / steps;
                    float y0 = eave + rise * k / steps, y1 = eave + rise * (k + 1) / steps;
                    float e = (length - 0.6f) * 0.5f - 0.2f;
                    var c = alongX
                        ? new Float3(r.CenterX + end * e, (y0 + y1) * 0.5f, r.CenterZ)
                        : new Float3(r.CenterX, (y0 + y1) * 0.5f, r.CenterZ + end * e);
                    Box(mesh, c, alongX ? new Float3(0.4f, y1 - y0, w) : new Float3(w, y1 - y0, 0.4f), MapSolidsMaterialRole.StructureConcrete);
                }
            }
            // Ridge cap and one chimney.
            var ridge = new Float3(r.CenterX, eave + rise + 0.05f, r.CenterZ);
            Box(mesh, ridge, alongX ? new Float3(length, 0.18f, 0.3f) : new Float3(0.3f, 0.18f, length), MapSolidsMaterialRole.Foundation);
            float cl = (alongX ? r.SizeX : r.SizeZ) * 0.25f;
            var chimney = alongX ? new Float3(r.CenterX + cl, eave + rise * 0.75f + 0.6f, r.CenterZ + half * 0.25f)
                                 : new Float3(r.CenterX + half * 0.25f, eave + rise * 0.75f + 0.6f, r.CenterZ + cl);
            Box(mesh, chimney, new Float3(0.7f, rise * 0.5f + 1.2f, 0.7f), MapSolidsMaterialRole.StructureConcrete);
        }

        /// <summary>Flat industrial roof: parapet band and a roof unit or two.</summary>
        private static void AddParapetRoof(MeshAssembly mesh, SiteRect r, float wallHeight, int index)
        {
            float y = wallHeight + 0.3f + 0.3f;
            Box(mesh, new Float3(r.CenterX, y, r.MaxZ - 0.15f), new Float3(r.SizeX, 0.6f, 0.3f), MapSolidsMaterialRole.Trim);
            Box(mesh, new Float3(r.CenterX, y, r.MinZ + 0.15f), new Float3(r.SizeX, 0.6f, 0.3f), MapSolidsMaterialRole.Trim);
            Box(mesh, new Float3(r.MinX + 0.15f, y, r.CenterZ), new Float3(0.3f, 0.6f, r.SizeZ - 0.6f), MapSolidsMaterialRole.Trim);
            Box(mesh, new Float3(r.MaxX - 0.15f, y, r.CenterZ), new Float3(0.3f, 0.6f, r.SizeZ - 0.6f), MapSolidsMaterialRole.Trim);
            float ux = r.CenterX + ((index & 1) == 0 ? -1f : 1f) * r.SizeX * 0.2f;
            Box(mesh, new Float3(ux, wallHeight + 0.3f + 0.6f, r.CenterZ), new Float3(Math.Min(2.4f, r.SizeX * 0.3f), 1.2f, Math.Min(1.6f, r.SizeZ * 0.3f)), MapSolidsMaterialRole.MetalCover);
            mesh.Add(PrimitiveGeometry.Cylinder(0.25f, 1.6f, 10), new Float3(r.CenterX, wallHeight + 0.3f + 0.8f, r.CenterZ + r.SizeZ * 0.25f), YUp,
                (ushort)MapSolidsMaterialRole.MetalCover);
        }

        /// <summary>Square bell tower rising from the west end of the church roof, with a spire.</summary>
        private static void AddChurchTower(MeshAssembly mesh, SiteBuilding b)
        {
            var r = b.Footprint;
            float side = Math.Min(4.5f, Math.Min(r.SizeX, r.SizeZ) * 0.6f);
            float cx = r.MinX + side * 0.5f + 0.5f, cz = r.CenterZ;
            float baseY = b.Height + 0.3f, top = b.Height + 10f;
            Box(mesh, new Float3(cx, (baseY + top) * 0.5f, cz), new Float3(side, top - baseY, side), MapSolidsMaterialRole.StructureConcrete);
            Box(mesh, new Float3(cx, top + 0.15f, cz), new Float3(side + 0.4f, 0.3f, side + 0.4f), MapSolidsMaterialRole.Trim);
            // Belfry openings read as dark bands on each face.
            Box(mesh, new Float3(cx, top - 1.6f, cz), new Float3(side + 0.04f, 1.4f, side * 0.4f), MapSolidsMaterialRole.Foundation);
            Box(mesh, new Float3(cx, top - 1.6f, cz), new Float3(side * 0.4f, 1.4f, side + 0.04f), MapSolidsMaterialRole.Foundation);
            mesh.Add(PrimitiveGeometry.Cone(side * 0.72f, 0.08f, 7f, 4), new Float3(cx, top + 0.3f + 3.5f, cz),
                Mul(new Quat(0f, (float)Math.Sin(Math.PI / 8), 0f, (float)Math.Cos(Math.PI / 8)), YUp), (ushort)MapSolidsMaterialRole.OxidePaint);
        }

        private static Quat Mul(Quat a, Quat b) => new Quat(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

        /// <summary>Out-of-bounds scenery behind one perimeter side (outer wall face is 1 m beyond the playable edge).</summary>
        private static void AddBackdrop(MeshAssembly mesh, ArenaLayout arena, SiteBoundaryKind kind, ArenaSide side)
        {
            float hx = arena.HalfExtentX, hz = arena.HalfExtentZ;
            bool alongX = side == ArenaSide.North || side == ArenaSide.South;
            float sign = side == ArenaSide.North || side == ArenaSide.East ? 1f : -1f;
            float edge = (alongX ? hz : hx) + 1f;
            float span = alongX ? hx + 1f : hz + 1f;
            switch (kind)
            {
                case SiteBoundaryKind.BuildingBacks:
                {
                    // A row of town houses, alternating width and height, gable roofs facing the playable area.
                    float[] widths = { 7f, 9f, 6f, 8f, 10f };
                    float[] heights = { 8f, 10.5f, 7.5f, 9.5f, 11f };
                    int k = side == ArenaSide.North ? 2 : side == ArenaSide.East ? 1 : side == ArenaSide.West ? 3 : 0;
                    for (float t = -span; t < span; k++)
                    {
                        float w = Math.Min(widths[k % widths.Length], span - t);
                        float h = heights[(k * 3) % heights.Length];
                        float depth = 7f;
                        float c = t + w * 0.5f, d = sign * (edge + depth * 0.5f);
                        var rect = alongX ? new SiteRect(c - w * 0.5f, d - depth * 0.5f, c + w * 0.5f, d + depth * 0.5f)
                                          : new SiteRect(d - depth * 0.5f, c - w * 0.5f, d + depth * 0.5f, c + w * 0.5f);
                        Box(mesh, new Float3(rect.CenterX, h * 0.5f, rect.CenterZ), new Float3(rect.SizeX, h, rect.SizeZ), MapSolidsMaterialRole.StructureConcrete);
                        AddGableRoof(mesh, rect, h - 0.3f, RoofPitch);
                        t += w;
                    }
                    break;
                }
                case SiteBoundaryKind.Water:
                {
                    // Harbour water beyond the quay wall and a moored freighter.
                    float far = 70f;
                    var wc = alongX ? new Float3(0f, -0.05f, sign * (edge + far * 0.5f)) : new Float3(sign * (edge + far * 0.5f), -0.05f, 0f);
                    Box(mesh, wc, alongX ? new Float3(span * 2f + 40f, 0.1f, far) : new Float3(far, 0.1f, span * 2f + 40f), MapSolidsMaterialRole.DepotBlue);
                    float off = sign * (edge + 9f);
                    var hull = alongX ? new Float3(6f, 2.5f, off) : new Float3(off, 2.5f, 6f);
                    Box(mesh, hull, alongX ? new Float3(56f, 6f, 11f) : new Float3(11f, 6f, 56f), MapSolidsMaterialRole.OxidePaint);
                    var deck = alongX ? new Float3(6f, 5.65f, off) : new Float3(off, 5.65f, 6f);
                    Box(mesh, deck, alongX ? new Float3(54f, 0.3f, 10f) : new Float3(10f, 0.3f, 54f), MapSolidsMaterialRole.Foundation);
                    var bridge = alongX ? new Float3(-16f, 9.5f, off) : new Float3(off, 9.5f, -16f);
                    Box(mesh, bridge, new Float3(7f, 7.5f, 7f), MapSolidsMaterialRole.StructureConcrete);
                    for (int i = 0; i < 4; i++)
                    {
                        float along = -4f + i * 7f;
                        var stack = alongX ? new Float3(along, 7.1f, off) : new Float3(off, 7.1f, along);
                        Box(mesh, stack, alongX ? new Float3(6f, 2.6f, 2.5f) : new Float3(2.5f, 2.6f, 6f), i % 2 == 0 ? MapSolidsMaterialRole.DepotBlue : MapSolidsMaterialRole.OxidePaint);
                    }
                    break;
                }
                default:
                    break; // Fences and site walls: the map's own dressing (e.g. the oil skyline) handles the backdrop.
            }
        }
    }
}
