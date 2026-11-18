#nullable enable
using System;
using System.Collections.Generic;

namespace Shooter.Generation.Environment
{
    /// <summary>What a gameplay box is in the real place it imitates (ADR-109). Every gameplay box has exactly one role.</summary>
    public enum SiteRole : byte
    {
        /// <summary>Closed outer ring: backs of buildings, site fence or quay edge (dressing decides which).</summary>
        Boundary = 1,
        /// <summary>A wall, sill or lintel of a <see cref="SiteBuilding"/>.</summary>
        Wall = 2,
        /// <summary>Walkable upper-floor slab inside or on a building.</summary>
        Floor = 3,
        /// <summary>Stair tread serving an upper floor.</summary>
        Stair = 4,
        /// <summary>Prop that belongs to a host: against a facade, parked in a street, in a yard row, on a quay edge.</summary>
        Attached = 5,
        /// <summary>Named free-standing object that defines a place (fountain, crane, tank, kiosk).</summary>
        Landmark = 6
    }

    public enum SiteBuildingKind
    {
        House = 1, Shop = 2, Inn = 3, TownHall = 4, Church = 5,
        Warehouse = 10, Office = 11, Shed = 12, Customs = 13,
        PumpHouse = 20, ControlRoom = 21, Gatehouse = 22, LoadingRack = 23
    }

    public enum SiteZoneKind
    {
        Street = 1, Alley = 2, Yard = 3, Plaza = 4, Quay = 5, Staging = 6
    }

    public enum SiteBoundaryKind
    {
        /// <summary>Backs of out-of-bounds buildings (town edge).</summary>
        BuildingBacks = 1,
        /// <summary>Industrial site fence with a backdrop behind it.</summary>
        Fence = 2,
        /// <summary>Quay edge with water beyond (collision is a low-visibility barrier).</summary>
        Water = 3,
        /// <summary>Masonry site wall.</summary>
        SiteWall = 4
    }

    public enum SiteHostKind : byte { None = 0, Building = 1, Zone = 2, Boundary = 3, Landmark = 4 }

    /// <summary>Axis-aligned XZ rectangle.</summary>
    public readonly struct SiteRect
    {
        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }
        public SiteRect(float minX, float minZ, float maxX, float maxZ)
        {
            if (maxX <= minX || maxZ <= minZ) throw new ArgumentException("Empty site rectangle.");
            MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ;
        }
        public float SizeX => MaxX - MinX;
        public float SizeZ => MaxZ - MinZ;
        public float CenterX => (MinX + MaxX) * 0.5f;
        public float CenterZ => (MinZ + MaxZ) * 0.5f;
        public float Area => SizeX * SizeZ;
        public bool Contains(float x, float z, float pad = 0f)
            => x >= MinX - pad && x <= MaxX + pad && z >= MinZ - pad && z <= MaxZ + pad;

        /// <summary>XZ gap between this rectangle and a box footprint (0 when they touch or overlap).</summary>
        public float GapTo(float cx, float cz, float sx, float sz)
        {
            float dx = Math.Max(0f, Math.Max(MinX - (cx + sx * 0.5f), (cx - sx * 0.5f) - MaxX));
            float dz = Math.Max(0f, Math.Max(MinZ - (cz + sz * 0.5f), (cz - sz * 0.5f) - MaxZ));
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        public SiteRect Twin(ArenaSymmetry symmetry)
            => symmetry == ArenaSymmetry.Rotate180
                ? new SiteRect(-MaxX, -MaxZ, -MinX, -MinZ)
                : new SiteRect(MinX, -MaxZ, MaxX, -MinZ);

        public bool SameAs(SiteRect o) => Math.Abs(MinX - o.MinX) < 0.001f && Math.Abs(MaxX - o.MaxX) < 0.001f &&
                                          Math.Abs(MinZ - o.MinZ) < 0.001f && Math.Abs(MaxZ - o.MaxZ) < 0.001f;
    }

    /// <summary>One building as placed on the site (team-2 twins are separate entries).</summary>
    public sealed class SiteBuilding
    {
        public SiteBuilding(string name, SiteBuildingKind kind, SiteRect footprint, float height, int storeys, float upperFloorY, int team)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Building name required.", nameof(name));
            if (height <= 0f || storeys < 1 || storeys > 2) throw new ArgumentOutOfRangeException(nameof(storeys));
            Name = name; Kind = kind; Footprint = footprint; Height = height; Storeys = storeys; UpperFloorY = upperFloorY; Team = team;
        }
        public string Name { get; }
        public SiteBuildingKind Kind { get; }
        public SiteRect Footprint { get; }
        public float Height { get; }
        public int Storeys { get; }
        /// <summary>Top of the walkable upper floor (0 for single-storey buildings).</summary>
        public float UpperFloorY { get; }
        /// <summary>1 or 2 for the half it belongs to, 0 when the building is its own symmetry image.</summary>
        public int Team { get; }
    }

    /// <summary>A named open area (street, yard, plaza, quay, staging). Carries no geometry.</summary>
    public sealed class SiteZone
    {
        public SiteZone(string name, SiteZoneKind kind, SiteRect area, int team)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Zone name required.", nameof(name));
            Name = name; Kind = kind; Area = area; Team = team;
        }
        public string Name { get; }
        public SiteZoneKind Kind { get; }
        public SiteRect Area { get; }
        public int Team { get; }
        /// <summary>Plazas and quays are the only areas allowed to be wide open (ADR-109 void and sightline rules).</summary>
        public bool DeclaredOpen => Kind == SiteZoneKind.Plaza || Kind == SiteZoneKind.Quay;
    }

    /// <summary>Semantic tag of one gameplay box (keyed by stable id).</summary>
    public readonly struct SiteBoxInfo
    {
        public SiteBoxInfo(ulong stableId, SiteRole role, SiteHostKind hostKind, string host, SiteRect hostArea, int team)
        {
            StableId = stableId; Role = role; HostKind = hostKind; Host = host; HostArea = hostArea; Team = team;
        }
        public ulong StableId { get; }
        public SiteRole Role { get; }
        public SiteHostKind HostKind { get; }
        public string Host { get; }
        public SiteRect HostArea { get; }
        public int Team { get; }
    }

    /// <summary>Named landmark (free-standing by design).</summary>
    public sealed class SiteLandmark
    {
        public SiteLandmark(string name, SiteRect footprint, float height, int team)
        {
            Name = name; Footprint = footprint; Height = height; Team = team;
        }
        public string Name { get; }
        public SiteRect Footprint { get; }
        public float Height { get; }
        public int Team { get; }
    }

    /// <summary>
    /// Semantic site description carried next to the gameplay boxes of a site-planned map (ADR-109): buildings, open
    /// zones, landmarks, boundary kinds and the role of every gameplay box. Validation (<see cref="SiteRules"/>), the
    /// top-down review render and the mesh dressing read it; collision and navigation never depend on it.
    /// </summary>
    public sealed class SiteLayout
    {
        private readonly Dictionary<ulong, SiteBoxInfo> boxes;

        public SiteLayout(
            string name,
            IReadOnlyList<SiteBuilding> buildings,
            IReadOnlyList<SiteZone> zones,
            IReadOnlyList<SiteLandmark> landmarks,
            IReadOnlyList<SiteBoxInfo> boxInfos,
            SiteBoundaryKind south, SiteBoundaryKind north, SiteBoundaryKind east, SiteBoundaryKind west)
        {
            Name = name;
            Buildings = new List<SiteBuilding>(buildings);
            Zones = new List<SiteZone>(zones);
            Landmarks = new List<SiteLandmark>(landmarks);
            BoxInfos = new List<SiteBoxInfo>(boxInfos);
            boxes = new Dictionary<ulong, SiteBoxInfo>();
            for (int i = 0; i < boxInfos.Count; i++) boxes[boxInfos[i].StableId] = boxInfos[i];
            South = south; North = north; East = east; West = west;
        }

        public string Name { get; }
        public IReadOnlyList<SiteBuilding> Buildings { get; }
        public IReadOnlyList<SiteZone> Zones { get; }
        public IReadOnlyList<SiteLandmark> Landmarks { get; }
        public IReadOnlyList<SiteBoxInfo> BoxInfos { get; }
        public SiteBoundaryKind South { get; }
        public SiteBoundaryKind North { get; }
        public SiteBoundaryKind East { get; }
        public SiteBoundaryKind West { get; }

        public bool TryGetBox(ulong stableId, out SiteBoxInfo info) => boxes.TryGetValue(stableId, out info);
    }
}
