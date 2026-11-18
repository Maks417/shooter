#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>Door or window in one facade of a <see cref="SiteBuilder.Building"/>.</summary>
    public readonly struct SiteOpening
    {
        public const float DoorHead = 2.5f;

        public SiteOpening(ArenaSide side, float offset, float width, float sill, float head)
        {
            if (width <= 0f) throw new ArgumentOutOfRangeException(nameof(width));
            if (sill < 0f || head <= sill) throw new ArgumentOutOfRangeException(nameof(head));
            Side = side; Offset = offset; Width = width; Sill = sill; Head = head;
        }

        public ArenaSide Side { get; }
        /// <summary>Centre along the facade, relative to the building centre (X for N/S facades, Z for E/W).</summary>
        public float Offset { get; }
        public float Width { get; }
        /// <summary>Bottom of the opening (0 for a door).</summary>
        public float Sill { get; }
        /// <summary>Top of the opening.</summary>
        public float Head { get; }
        public bool IsDoor => Sill <= 0.001f;

        public static SiteOpening Door(ArenaSide side, float offset, float width = 2.4f) => new SiteOpening(side, offset, width, 0f, DoorHead);

        /// <summary>Ground-floor window: 1.05 m sill hides a standing body below the chest, 2.2 m head.</summary>
        public static SiteOpening Window(ArenaSide side, float offset, float width = 1.6f) => new SiteOpening(side, offset, width, 1.05f, 2.2f);

        /// <summary>Upper-floor window over a floor whose top is <paramref name="floorY"/> (sill = floor + 0.95 m).</summary>
        public static SiteOpening UpperWindow(ArenaSide side, float offset, float floorY, float width = 1.6f)
            => new SiteOpening(side, offset, width, floorY + 0.95f, floorY + 2.15f);
    }

    /// <summary>Handle returned by <see cref="SiteBuilder.Building"/> so floors, stairs and attachments can name their host.</summary>
    public sealed class SiteBuildingRef
    {
        internal SiteBuildingRef(string name, SiteRect rect, float height, float wall, bool mirror)
        {
            Name = name; Rect = rect; Height = height; Wall = wall; Mirror = mirror;
        }
        public string Name { get; }
        public SiteRect Rect { get; }
        public float Height { get; }
        public float Wall { get; }
        public bool Mirror { get; }
        internal float UpperFloorY { get; set; }
    }

    /// <summary>
    /// ADR-109 site planner front end. Planners describe a place (boundary, buildings with doors and windows, upper
    /// floors and stairs, open zones, props attached to a host, a few named landmarks) in team-1 coordinates; this
    /// builder compiles it to <see cref="ArenaBoxBuilder"/> gameplay boxes (symmetry twins included) and records the
    /// semantic role of every box in a <see cref="SiteLayout"/>. There is no way to add an unowned box.
    /// </summary>
    public sealed class SiteBuilder
    {
        private const float MinHeaderMeters = 0.5f;
        private const float RailHeight = 1.1f;

        private readonly ArenaBoxBuilder b;
        private readonly List<SiteBuilding> buildings = new List<SiteBuilding>();
        private readonly List<SiteZone> zones = new List<SiteZone>();
        private readonly Dictionary<string, SiteZone> zoneByName = new Dictionary<string, SiteZone>(StringComparer.Ordinal);
        private readonly List<SiteLandmark> landmarks = new List<SiteLandmark>();
        private readonly List<SiteBoxInfo> infos = new List<SiteBoxInfo>();
        private SiteBoundaryKind south = SiteBoundaryKind.SiteWall, north = SiteBoundaryKind.SiteWall,
            east = SiteBoundaryKind.SiteWall, west = SiteBoundaryKind.SiteWall;

        public SiteBuilder(string name, ulong idBase, ArenaLayout layout)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            b = new ArenaBoxBuilder(idBase, layout.Symmetry);
        }

        public string Name { get; }
        public ArenaLayout Layout { get; }
        public ArenaSymmetry Symmetry => Layout.Symmetry;
        /// <summary>Raw builder, for dressing-only feature records. Gameplay boxes must go through the typed methods.</summary>
        internal ArenaBoxBuilder Boxes => b;

        // ---------------------------------------------------------------- boundary and zones

        /// <summary>Closed outer ring (≥ 4 m) just outside the playable rectangle; kinds pick the dressing per side.</summary>
        public void Boundary(SiteBoundaryKind southKind, SiteBoundaryKind northKind, SiteBoundaryKind eastKind, SiteBoundaryKind westKind, float height = 6f)
        {
            south = southKind; north = northKind; east = eastKind; west = westKind;
            var area = new SiteRect(-Layout.HalfExtentX - 2f, -Layout.HalfExtentZ - 2f, Layout.HalfExtentX + 2f, Layout.HalfExtentZ + 2f);
            Track(SiteRole.Boundary, SiteHostKind.Boundary, "boundary", area, false, () => b.Perimeter(Layout, height, 1f));
        }

        /// <summary>Declares an open area (no geometry). Mirrored zones get "<paramref name="name"/>'" as the twin's name.</summary>
        public SiteZone Zone(string name, SiteZoneKind kind, SiteRect area, bool mirror = true)
        {
            var z = new SiteZone(name, kind, area, mirror ? 1 : 0);
            zones.Add(z); zoneByName.Add(name, z);
            if (mirror)
            {
                var t = new SiteZone(name + "'", kind, area.Twin(Symmetry), 2);
                zones.Add(t); zoneByName.Add(t.Name, t);
            }
            return z;
        }

        // ---------------------------------------------------------------- buildings

        /// <summary>
        /// Rectangular building with walls of <paramref name="wall"/> thickness, door and window openings and a visual
        /// roof. Doors leave a lintel above <see cref="SiteOpening.DoorHead"/>; windows leave a sill and a lintel.
        /// </summary>
        public SiteBuildingRef Building(string name, SiteBuildingKind kind, SiteRect rect, float height, IReadOnlyList<SiteOpening> openings,
            bool mirror = true, float wall = 0.4f, float upperFloorY = 0f, bool roof = true)
        {
            if (openings == null) throw new ArgumentNullException(nameof(openings));
            var r = new SiteBuildingRef(name, rect, height, wall, mirror) { UpperFloorY = upperFloorY };
            int storeys = upperFloorY > 0f ? 2 : 1;
            buildings.Add(new SiteBuilding(name, kind, rect, height, storeys, upperFloorY, mirror ? 1 : 0));
            if (mirror) buildings.Add(new SiteBuilding(name + "'", kind, rect.Twin(Symmetry), height, storeys, upperFloorY, 2));

            Track(SiteRole.Wall, SiteHostKind.Building, name, rect, mirror, () =>
            {
                float x0 = rect.MinX, x1 = rect.MaxX, z0 = rect.MinZ, z1 = rect.MaxZ, cx = rect.CenterX, cz = rect.CenterZ;
                FacadeAlongX(z1 - wall * 0.5f, x0, x1, cx, wall, height, openings, ArenaSide.North, mirror);
                FacadeAlongX(z0 + wall * 0.5f, x0, x1, cx, wall, height, openings, ArenaSide.South, mirror);
                FacadeAlongZ(x1 - wall * 0.5f, z0 + wall, z1 - wall, cz, wall, height, openings, ArenaSide.East, mirror);
                FacadeAlongZ(x0 + wall * 0.5f, z0 + wall, z1 - wall, cz, wall, height, openings, ArenaSide.West, mirror);
                if (roof)
                    b.Add(new Float3(cx, height + 0.15f, cz), new Float3(rect.SizeX, 0.3f, rect.SizeZ), ArenaBoxBuilder.TagSolid, mirror, gameplay: false);
            });
            return r;
        }

        /// <summary>
        /// Open elevated structure (catwalk, gantry, loading platform) recorded as a landmark; use <see cref="Floor"/>,
        /// <see cref="Stair"/> and <see cref="Rail"/> with the returned host to build its deck.
        /// </summary>
        public SiteBuildingRef Platform(string name, SiteRect footprint, float deckY, bool mirror)
        {
            landmarks.Add(new SiteLandmark(name, footprint, deckY, mirror ? 1 : 0));
            if (mirror) landmarks.Add(new SiteLandmark(name + "'", footprint.Twin(Symmetry), deckY, 2));
            return new SiteBuildingRef(name, footprint, deckY, 0f, mirror) { UpperFloorY = deckY };
        }

        /// <summary>Support post of a platform or overhead pipe rack (thin, full height to the deck).</summary>
        public void Post(SiteBuildingRef host, float x, float z, float size = 0.35f, float height = 0f, bool? mirror = null)
        {
            float h = height > 0f ? height : host.UpperFloorY - 0.3f;
            bool m = mirror ?? host.Mirror;
            Track(SiteRole.Attached, SiteHostKind.Landmark, host.Name, host.Rect, m, () =>
                b.AddGrounded(x, z, size, size, h, ArenaBoxBuilder.TagSolid, m));
        }

        /// <summary>Interior partition wall of a building (along X at z, between x0 and x1) with door gaps.</summary>
        public void Partition(SiteBuildingRef host, bool alongX, float at, float from, float to, IReadOnlyList<ArenaGap>? doors, float height = 0f, float thickness = 0.3f)
        {
            float h = height > 0f ? height : host.Height;
            Track(SiteRole.Wall, SiteHostKind.Building, host.Name, host.Rect, host.Mirror, () =>
            {
                if (alongX) b.WallAlongX(at, from, to, thickness, h, doors, ArenaBoxBuilder.TagSolid, host.Mirror);
                else b.WallAlongZ(at, from, to, thickness, h, doors, ArenaBoxBuilder.TagSolid, host.Mirror);
            });
        }

        /// <summary>Walkable upper-floor slab (top at the building's upper floor height).</summary>
        public void Floor(SiteBuildingRef host, SiteRect slab, float thickness = 0.25f)
        {
            if (host.UpperFloorY <= 0f) throw new InvalidOperationException(host.Name + " has no upper floor.");
            float top = host.UpperFloorY;
            Track(SiteRole.Floor, SiteHostKind.Building, host.Name, host.Rect, host.Mirror, () =>
                b.Add(new Float3(slab.CenterX, top - thickness * 0.5f, slab.CenterZ), new Float3(slab.SizeX, thickness, slab.SizeZ), ArenaBoxBuilder.TagSolid, host.Mirror));
        }

        /// <summary>Stair climbing along Z to the host's upper floor (1 m treads, ≤ 0.25 m risers).</summary>
        public void Stair(SiteBuildingRef host, float x, float baseZ, float width, bool ascendingPositiveZ)
        {
            if (host.UpperFloorY <= 0f) throw new InvalidOperationException(host.Name + " has no upper floor.");
            Track(SiteRole.Stair, SiteHostKind.Building, host.Name, host.Rect, host.Mirror, () =>
                b.AddStairRun(x, baseZ, width, host.UpperFloorY, 1f, ascendingPositiveZ, host.Mirror));
        }

        /// <summary>1.1 m balustrade standing on the host's upper floor (half cover for defenders up there).</summary>
        public void Rail(SiteBuildingRef host, float cx, float cz, float sizeX, float sizeZ)
        {
            float y = host.UpperFloorY + RailHeight * 0.5f;
            Track(SiteRole.Attached, SiteHostKind.Building, host.Name, host.Rect, host.Mirror, () =>
                b.Add(new Float3(cx, y, cz), new Float3(sizeX, RailHeight, sizeZ), ArenaBoxBuilder.TagCover, host.Mirror));
        }

        // ---------------------------------------------------------------- attachments

        /// <summary>
        /// Prop standing against a facade (outside unless <paramref name="inside"/>): crates at a loading dock, bins,
        /// a counter, steps, a stall. <paramref name="along"/> is the centre along the facade relative to the building centre.
        /// </summary>
        public void AgainstFacade(SiteBuildingRef host, ArenaSide side, float along, float length, float depth, float height,
            bool inside = false, ArenaFeatureKind? feature = null, float facing = 1f, bool? mirror = null)
        {
            var rc = host.Rect;
            float x, z, sx, sz;
            float outward = inside ? -1f : 1f;
            switch (side)
            {
                case ArenaSide.North: x = rc.CenterX + along; z = (inside ? rc.MaxZ - host.Wall : rc.MaxZ) + outward * depth * 0.5f; sx = length; sz = depth; break;
                case ArenaSide.South: x = rc.CenterX + along; z = (inside ? rc.MinZ + host.Wall : rc.MinZ) - outward * depth * 0.5f; sx = length; sz = depth; break;
                case ArenaSide.East: z = rc.CenterZ + along; x = (inside ? rc.MaxX - host.Wall : rc.MaxX) + outward * depth * 0.5f; sx = depth; sz = length; break;
                default: z = rc.CenterZ + along; x = (inside ? rc.MinX + host.Wall : rc.MinX) - outward * depth * 0.5f; sx = depth; sz = length; break;
            }
            AddProp(SiteHostKind.Building, host.Name, rc, mirror ?? host.Mirror, x, z, sx, sz, height, feature, facing);
        }

        /// <summary>Prop placed inside a named zone: a car at the curb, a container on a terminal row, cargo on a quay.</summary>
        public void InZone(string zone, float x, float z, float sizeX, float sizeZ, float height, ArenaFeatureKind? feature = null, float facing = 1f)
        {
            if (!zoneByName.TryGetValue(zone, out var zn)) throw new ArgumentException("Unknown zone " + zone, nameof(zone));
            AddProp(SiteHostKind.Zone, zn.Name, zn.Area, zn.Team == 1, x, z, sizeX, sizeZ, height, feature, facing);
        }

        /// <summary>Overhead run (pipe rack, gantry beam) inside a zone: entirely above head height, so it neither blocks movement nor eye lines.</summary>
        public void Overhead(string zone, float x, float z, float sizeX, float sizeZ, float bottomY, float topY)
        {
            if (!zoneByName.TryGetValue(zone, out var zn)) throw new ArgumentException("Unknown zone " + zone, nameof(zone));
            if (bottomY < CompoundConnectivity.AgentHeightMeters + 0.5f || topY - bottomY <= CompoundConnectivity.WalkableSurfaceMaxThickness)
                throw new ArgumentOutOfRangeException(nameof(bottomY), "Overhead runs stay above head and thicker than a walkable slab.");
            Track(SiteRole.Attached, SiteHostKind.Zone, zn.Name, zn.Area, zn.Team == 1, () =>
                b.Add(new Float3(x, (bottomY + topY) * 0.5f, z), new Float3(sizeX, topY - bottomY, sizeZ), ArenaBoxBuilder.TagSolid, zn.Team == 1));
        }

        /// <summary>Parked vehicle in a street or yard zone, its long axis along the zone's long axis.</summary>
        public void Park(string zone, ArenaFeatureKind kind, float x, float z, float length, float width, float height, float facing = 1f)
        {
            if (!zoneByName.TryGetValue(zone, out var zn)) throw new ArgumentException("Unknown zone " + zone, nameof(zone));
            bool alongX = zn.Area.SizeX >= zn.Area.SizeZ;
            float sx = alongX ? length : width, sz = alongX ? width : length;
            AddProp(SiteHostKind.Zone, zn.Name, zn.Area, zn.Team == 1, x, z, sx, sz, height, kind, facing);
        }

        /// <summary>Prop against the outer boundary (inside face), e.g. a lean-to, a stacked pallet bay, a pipe rack on the fence.</summary>
        public void AgainstBoundary(float x, float z, float sizeX, float sizeZ, float height, bool mirror = true, ArenaFeatureKind? feature = null, float facing = 1f)
        {
            var area = new SiteRect(-Layout.HalfExtentX, -Layout.HalfExtentZ, Layout.HalfExtentX, Layout.HalfExtentZ);
            // Host area is the ring outside the playable rectangle; the gap test uses the inner playable edge.
            AddProp(SiteHostKind.Boundary, "boundary", area, mirror, x, z, sizeX, sizeZ, height, feature, facing);
        }

        /// <summary>Named free-standing landmark built from one or more boxes (tank, fountain, crane base, kiosk).</summary>
        public void Landmark(string name, SiteRect footprint, float height, bool mirror, Action<ArenaBoxBuilder> build)
        {
            landmarks.Add(new SiteLandmark(name, footprint, height, mirror ? 1 : 0));
            if (mirror) landmarks.Add(new SiteLandmark(name + "'", footprint.Twin(Symmetry), height, 2));
            Track(SiteRole.Landmark, SiteHostKind.Landmark, name, footprint, mirror, () => build(b));
        }

        /// <summary>Simple single-box landmark.</summary>
        public void Landmark(string name, float x, float z, float sizeX, float sizeZ, float height, bool mirror = true, ArenaFeatureKind? feature = null, float facing = 1f)
        {
            var fp = new SiteRect(x - sizeX * 0.5f, z - sizeZ * 0.5f, x + sizeX * 0.5f, z + sizeZ * 0.5f);
            Landmark(name, fp, height, mirror, bb =>
            {
                bb.AddGrounded(x, z, sizeX, sizeZ, height, height >= 2f ? ArenaBoxBuilder.TagSolid : ArenaBoxBuilder.TagCover, mirror);
                if (feature.HasValue) bb.RecordFeature(feature.Value, x, z, sizeX, height, sizeZ, facing, mirror);
            });
        }

        // ---------------------------------------------------------------- output

        public CompoundPlan Build(string generatorId, string generatorVersion, ulong seed)
        {
            var site = new SiteLayout(Name, buildings, zones, landmarks, infos, south, north, east, west);
            return ArenaGeometry.CreatePlan(generatorId, generatorVersion, seed, Layout, b, site);
        }

        // ---------------------------------------------------------------- internals

        private void AddProp(SiteHostKind hostKind, string host, SiteRect hostArea, bool mirror, float x, float z, float sx, float sz, float height,
            ArenaFeatureKind? feature, float facing)
        {
            Track(SiteRole.Attached, hostKind, host, hostArea, mirror, () =>
            {
                ushort tag = height >= 2f ? ArenaBoxBuilder.TagSolid : ArenaBoxBuilder.TagCover;
                if (height < 2f)
                {
                    var threat = new Float3(x, 0f, 0f);
                    b.AddCover(x, z, sx, height, sz, threat, mirror, tag);
                }
                else b.AddGrounded(x, z, sx, sz, height, tag, mirror);
                if (feature.HasValue) b.RecordFeature(feature.Value, x, z, sx, height, sz, facing, mirror);
            });
        }

        /// <summary>Runs <paramref name="emit"/> and tags every gameplay box it added. Twins get the twin host area.</summary>
        private void Track(SiteRole role, SiteHostKind hostKind, string host, SiteRect hostArea, bool mirror, Action emit)
        {
            int start = b.Boxes.Count;
            emit();
            var twin = hostArea.Twin(Symmetry);
            for (int i = start; i < b.Boxes.Count; i++)
            {
                var box = b.Boxes[i];
                bool isTwin = mirror && !twin.SameAs(hostArea) &&
                              twin.GapTo(box.Center.X, box.Center.Z, box.Size.X, box.Size.Z) < hostArea.GapTo(box.Center.X, box.Center.Z, box.Size.X, box.Size.Z);
                infos.Add(new SiteBoxInfo(box.StableId, role, hostKind, isTwin ? host + "'" : host, isTwin ? twin : hostArea, mirror ? (isTwin ? 2 : 1) : 0));
            }
        }

        private void FacadeAlongX(float z, float x0, float x1, float center, float wall, float height, IReadOnlyList<SiteOpening> openings, ArenaSide side, bool mirror)
        {
            var gaps = new List<ArenaGap>();
            for (int i = 0; i < openings.Count; i++)
            {
                var o = openings[i];
                if (o.Side != side) continue;
                float c = center + o.Offset;
                gaps.Add(new ArenaGap(c, o.Width));
                OpeningInfill(c, z, o.Width, wall, height, o, true, mirror);
            }
            b.WallAlongX(z, x0, x1, wall, height, gaps, ArenaBoxBuilder.TagSolid, mirror);
        }

        private void FacadeAlongZ(float x, float z0, float z1, float center, float wall, float height, IReadOnlyList<SiteOpening> openings, ArenaSide side, bool mirror)
        {
            var gaps = new List<ArenaGap>();
            for (int i = 0; i < openings.Count; i++)
            {
                var o = openings[i];
                if (o.Side != side) continue;
                float c = center + o.Offset;
                gaps.Add(new ArenaGap(c, o.Width));
                OpeningInfill(x, c, o.Width, wall, height, o, false, mirror);
            }
            b.WallAlongZ(x, z0, z1, wall, height, gaps, ArenaBoxBuilder.TagSolid, mirror);
        }

        /// <summary>Sill below and lintel above an opening; both stay thicker than a walkable slab.</summary>
        private void OpeningInfill(float x, float z, float width, float wall, float height, SiteOpening o, bool alongX, bool mirror)
        {
            float sx = alongX ? width : wall, sz = alongX ? wall : width;
            if (o.Sill > 0.001f)
            {
                float sill = Math.Max(o.Sill, MinHeaderMeters);
                b.Add(new Float3(x, sill * 0.5f, z), new Float3(sx, sill, sz), sill >= 2f ? ArenaBoxBuilder.TagSolid : ArenaBoxBuilder.TagCover, mirror);
            }
            float head = Math.Min(o.Head, height - MinHeaderMeters);
            if (head > o.Sill + 0.5f && height - head >= MinHeaderMeters - 1e-4f)
                b.Add(new Float3(x, (head + height) * 0.5f, z), new Float3(sx, height - head, sz), ArenaBoxBuilder.TagSolid, mirror);
        }
    }
}
