#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    public enum ArenaSide { North, South, East, West }

    public readonly struct ArenaGap
    {
        public float Center { get; }
        public float Width { get; }
        public ArenaGap(float center, float width)
        {
            if (width <= 0f) throw new ArgumentOutOfRangeException(nameof(width));
            Center = center; Width = width;
        }
    }

    public readonly struct ArenaDoor
    {
        public ArenaSide Side { get; }
        /// <summary>Door centre along the wall, relative to the shell centre (X for N/S walls, Z for E/W walls).</summary>
        public float Offset { get; }
        public float Width { get; }
        public ArenaDoor(ArenaSide side, float offset, float width)
        {
            if (width <= 0f) throw new ArgumentOutOfRangeException(nameof(width));
            Side = side; Offset = offset; Width = width;
        }
    }

    /// <summary>
    /// Deterministic box/cover list builder for arena planners. Stable ids are assigned sequentially from a
    /// base so planners never hand-pick ids; optional z-mirror twins are emitted directly after the original.
    /// </summary>
    public sealed class ArenaBoxBuilder
    {
        public const ushort TagSolid = 1;
        public const ushort TagCover = 2;
        public const float StandingEyeMeters = 1.65f;
        public const float CrouchEyeMeters = 1.05f;

        private readonly List<CollisionBox> boxes = new List<CollisionBox>();
        private readonly List<CoverProxy> covers = new List<CoverProxy>();
        private ulong nextId;

        public ArenaBoxBuilder(ulong idBase)
        {
            if (idBase == 0) throw new ArgumentOutOfRangeException(nameof(idBase));
            nextId = idBase;
        }

        public IReadOnlyList<CollisionBox> Boxes => boxes;
        public IReadOnlyList<CoverProxy> Covers => covers;
        public ulong NextId => nextId;

        /// <summary>
        /// Advances the stable-id cursor to a higher base (e.g. visual-detail ranges). Rejects rewind so
        /// gameplay ids already emitted cannot collide with later visual appends.
        /// </summary>
        public void JumpIdsTo(ulong id)
        {
            if (id < nextId) throw new ArgumentOutOfRangeException(nameof(id), "Cannot rewind stable ids.");
            nextId = id;
        }

        /// <summary>Adds one box (centre/size) and, when <paramref name="mirror"/> is set and the centre is off z = 0, its z-mirror twin.</summary>
        public void Add(Float3 center, Float3 size, ushort tag = TagSolid, bool mirror = true, bool gameplay = true)
        {
            AddOne(center, size, tag, gameplay);
            if (mirror && Math.Abs(center.Z) > 0.0005f)
                AddOne(ArenaLayout.MirrorZ(center), size, tag, gameplay);
        }

        /// <summary>Adds a ground-standing box described by its XZ footprint centre and height.</summary>
        public void AddGrounded(float x, float z, float sizeX, float sizeZ, float height, ushort tag = TagSolid, bool mirror = true)
            => Add(new Float3(x, height * 0.5f, z), new Float3(sizeX, height, sizeZ), tag, mirror);

        /// <summary>Wall running along X at the given z between x0 and x1, with gaps (centre/width along X).</summary>
        public void WallAlongX(float z, float x0, float x1, float thickness, float height, IReadOnlyList<ArenaGap>? gaps = null, ushort tag = TagSolid, bool mirror = true)
        {
            foreach (var seg in Segments(x0, x1, gaps))
                AddGrounded((seg.a + seg.b) * 0.5f, z, seg.b - seg.a, thickness, height, tag, mirror);
        }

        /// <summary>Wall running along Z at the given x between z0 and z1, with gaps (centre/width along Z).</summary>
        public void WallAlongZ(float x, float z0, float z1, float thickness, float height, IReadOnlyList<ArenaGap>? gaps = null, ushort tag = TagSolid, bool mirror = true)
        {
            foreach (var seg in Segments(z0, z1, gaps))
                AddGrounded(x, (seg.a + seg.b) * 0.5f, thickness, seg.b - seg.a, height, tag, mirror);
        }

        /// <summary>Closed perimeter wall just outside the playable rectangle (inner face on the boundary).</summary>
        public void Perimeter(ArenaLayout layout, float height = 5f, float thickness = 1f)
        {
            float hx = layout.HalfExtentX, hz = layout.HalfExtentZ;
            // North/south walls (mirror twins) span the full width including the corners.
            AddGrounded(0f, hz + thickness * 0.5f, (hx + thickness) * 2f, thickness, height, TagSolid, true);
            // East/west walls are centred on z = 0.
            AddGrounded(hx + thickness * 0.5f, 0f, thickness, hz * 2f, height, TagSolid, false);
            AddGrounded(-hx - thickness * 0.5f, 0f, thickness, hz * 2f, height, TagSolid, false);
        }

        /// <summary>
        /// Rectangular shell centred at (cx, cz) with door openings on chosen sides. A roof is emitted as a
        /// non-gameplay box (ADR-049 visual roof; walkable decks use <see cref="AddCatwalkDeck"/> instead).
        /// With <paramref name="mirror"/> the whole shell is also emitted at -cz.
        /// </summary>
        public void Shell(float cx, float cz, float sizeX, float sizeZ, float thickness, float height, IReadOnlyList<ArenaDoor>? doors, bool roof = false, bool mirror = true)
        {
            float x0 = cx - sizeX * 0.5f, x1 = cx + sizeX * 0.5f;
            float z0 = cz - sizeZ * 0.5f, z1 = cz + sizeZ * 0.5f;
            WallAlongX(z1 - thickness * 0.5f, x0, x1, thickness, height, SideGaps(doors, ArenaSide.North, cx), TagSolid, mirror);
            WallAlongX(z0 + thickness * 0.5f, x0, x1, thickness, height, SideGaps(doors, ArenaSide.South, cx), TagSolid, mirror);
            WallAlongZ(x1 - thickness * 0.5f, z0 + thickness, z1 - thickness, thickness, height, SideGaps(doors, ArenaSide.East, cz), TagSolid, mirror);
            WallAlongZ(x0 + thickness * 0.5f, z0 + thickness, z1 - thickness, thickness, height, SideGaps(doors, ArenaSide.West, cz), TagSolid, mirror);
            if (roof)
                Add(new Float3(cx, height + 0.15f, cz), new Float3(sizeX, 0.3f, sizeZ), TagSolid, mirror, gameplay: false);
        }

        /// <summary>
        /// Stair of thin walkable treads (≤ <see cref="CompoundConnectivity.MaxStepMeters"/> risers) climbing along +Z
        /// from <paramref name="baseZ"/> toward higher Z. Deck top of the last tread equals <paramref name="deckTopY"/>.
        /// </summary>
        public void AddStairRun(
            float x,
            float baseZ,
            float widthX,
            float deckTopY,
            float treadDepthZ,
            bool ascendingPositiveZ = true,
            bool mirror = true,
            float riser = CompoundConnectivity.MaxStepMeters,
            float treadThickness = 0.2f)
        {
            if (deckTopY <= 0f) throw new ArgumentOutOfRangeException(nameof(deckTopY));
            if (treadDepthZ < CompoundConnectivity.CellSize - 1e-3f)
                throw new ArgumentOutOfRangeException(nameof(treadDepthZ), "Tread depth must be ≥ path cell size so climb steps align to the grid.");
            if (riser <= 0f || riser > CompoundConnectivity.MaxStepMeters + 1e-3f)
                throw new ArgumentOutOfRangeException(nameof(riser));
            if (treadThickness <= 0f || treadThickness > CompoundConnectivity.WalkableSurfaceMaxThickness)
                throw new ArgumentOutOfRangeException(nameof(treadThickness));
            int steps = Math.Max(1, (int)Math.Ceiling(deckTopY / riser - 1e-4f));
            float stepRiser = deckTopY / steps;
            float dir = ascendingPositiveZ ? 1f : -1f;
            for (int i = 0; i < steps; i++)
            {
                float top = stepRiser * (i + 1);
                float cz = baseZ + dir * (treadDepthZ * (i + 0.5f));
                Add(new Float3(x, top - treadThickness * 0.5f, cz), new Float3(widthX, treadThickness, treadDepthZ), TagSolid, mirror);
            }
        }

        /// <summary>
        /// Thin walkable catwalk deck (pass-under when deck underside ≥ agent height) with optional 1.1 m rail cover
        /// on both long edges. Stairs are added separately via <see cref="AddStairRun"/>.
        /// </summary>
        public void AddCatwalkDeck(
            float cx,
            float cz,
            float sizeX,
            float sizeZ,
            float deckTopY,
            bool rails = true,
            bool mirror = true,
            float deckThickness = 0.25f)
        {
            if (deckTopY < CompoundConnectivity.AgentHeightMeters)
                throw new ArgumentOutOfRangeException(nameof(deckTopY), "Deck must clear agent height for underpass.");
            if (deckThickness <= 0f || deckThickness > CompoundConnectivity.WalkableSurfaceMaxThickness)
                throw new ArgumentOutOfRangeException(nameof(deckThickness));
            if (sizeX < 3f || sizeZ < 3f) throw new ArgumentOutOfRangeException(nameof(sizeX), "Catwalk corridor ≥ 3 m.");
            Add(new Float3(cx, deckTopY - deckThickness * 0.5f, cz), new Float3(sizeX, deckThickness, sizeZ), TagSolid, mirror);
            if (!rails) return;
            const float railH = 1.1f;
            float railY = deckTopY + railH * 0.5f;
            // Rails along the long Z edges when sizeZ >= sizeX, otherwise along X.
            if (sizeZ >= sizeX)
            {
                float halfX = sizeX * 0.5f;
                Add(new Float3(cx - halfX + 0.1f, railY, cz), new Float3(0.2f, railH, sizeZ), TagCover, mirror);
                Add(new Float3(cx + halfX - 0.1f, railY, cz), new Float3(0.2f, railH, sizeZ), TagCover, mirror);
            }
            else
            {
                float halfZ = sizeZ * 0.5f;
                Add(new Float3(cx, railY, cz - halfZ + 0.1f), new Float3(sizeX, railH, 0.2f), TagCover, mirror);
                Add(new Float3(cx, railY, cz + halfZ - 0.1f), new Float3(sizeX, railH, 0.2f), TagCover, mirror);
            }
        }

        /// <summary>
        /// Adds a cover box (gameplay) plus its <see cref="CoverProxy"/>. Peek points are on the side facing away
        /// from <paramref name="threat"/> (where a defender stands behind the cover): standing eye 1.65 m, crouch eye 1.05 m.
        /// </summary>
        public void AddCover(float x, float z, float sizeX, float sizeY, float sizeZ, Float3 threat, bool mirror = true, ushort tag = TagCover)
        {
            AddCoverOne(x, z, sizeX, sizeY, sizeZ, threat, tag);
            if (mirror && Math.Abs(z) > 0.0005f)
                AddCoverOne(x, -z, sizeX, sizeY, sizeZ, ArenaLayout.MirrorZ(threat), tag);
        }

        private void AddCoverOne(float x, float z, float sizeX, float sizeY, float sizeZ, Float3 threat, ushort tag)
        {
            var center = new Float3(x, sizeY * 0.5f, z);
            AddOne(center, new Float3(sizeX, sizeY, sizeZ), tag, true);
            float dx = x - threat.X, dz = z - threat.Z;
            float px = x, pz = z;
            if (Math.Abs(dx) >= Math.Abs(dz)) px += (dx >= 0f ? 1f : -1f) * (sizeX * 0.5f + 0.6f);
            else pz += (dz >= 0f ? 1f : -1f) * (sizeZ * 0.5f + 0.6f);
            ulong id = nextId++;
            covers.Add(new CoverProxy(id, center, sizeX, sizeY, sizeZ,
                new Float3(px, StandingEyeMeters, pz), new Float3(px, CrouchEyeMeters, pz), sizeY >= 2f));
        }

        private void AddOne(Float3 center, Float3 size, ushort tag, bool gameplay)
        {
            ulong id = nextId++;
            boxes.Add(new CollisionBox(id, id, tag, center, size, gameplay));
        }

        private static IReadOnlyList<ArenaGap>? SideGaps(IReadOnlyList<ArenaDoor>? doors, ArenaSide side, float centerAlong)
        {
            if (doors == null) return null;
            var list = new List<ArenaGap>();
            for (int i = 0; i < doors.Count; i++)
                if (doors[i].Side == side) list.Add(new ArenaGap(centerAlong + doors[i].Offset, doors[i].Width));
            return list;
        }

        private static List<(float a, float b)> Segments(float from, float to, IReadOnlyList<ArenaGap>? gaps)
        {
            if (to <= from) throw new ArgumentException("Wall span is empty.");
            var sorted = new List<ArenaGap>();
            if (gaps != null) sorted.AddRange(gaps);
            sorted.Sort((p, q) => p.Center.CompareTo(q.Center));
            var result = new List<(float a, float b)>();
            float cursor = from;
            for (int i = 0; i < sorted.Count; i++)
            {
                float gl = sorted[i].Center - sorted[i].Width * 0.5f;
                float gr = sorted[i].Center + sorted[i].Width * 0.5f;
                if (gl - cursor > 0.05f) result.Add((cursor, Math.Min(gl, to)));
                cursor = Math.Max(cursor, gr);
            }
            if (to - cursor > 0.05f) result.Add((cursor, to));
            return result;
        }
    }

    /// <summary>Shared plan assembly and hashing for arena planners.</summary>
    public static class ArenaGeometry
    {
        /// <summary>Collision/nav identity derived from the actual gameplay box list (not seed-only).</summary>
        public static ContentId HashGameplay(string generatorId, string generatorVersion, ulong seed, IReadOnlyList<CollisionBox> boxes, string kind)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.UTF8, true);
            w.Write(generatorId);
            w.Write(generatorVersion);
            w.Write(kind);
            w.Write(seed);
            int gameplay = 0;
            for (int i = 0; i < boxes.Count; i++) if (boxes[i].Gameplay) gameplay++;
            w.Write(gameplay);
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (!b.Gameplay) continue;
                w.Write(b.StableId); w.Write(b.SurfaceId); w.Write(b.SurfaceTag);
                w.Write(b.Center.X); w.Write(b.Center.Y); w.Write(b.Center.Z);
                w.Write(b.Size.X); w.Write(b.Size.Y); w.Write(b.Size.Z);
            }
            w.Flush();
            return ContentIdentity.Hash(ms.ToArray());
        }

        /// <summary>Assembles the immutable arena plan (anchors, bot spawns, route, hashes) from a finished builder.</summary>
        public static CompoundPlan CreatePlan(string generatorId, string generatorVersion, ulong seed, ArenaLayout layout, ArenaBoxBuilder builder)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var boxes = new List<CollisionBox>(builder.Boxes);
            var covers = new List<CoverProxy>(builder.Covers);
            float clearance = CompoundConnectivity.RequiredClearance;

            var bots = new List<Float3>();
            var r = layout.StagingRect(2);
            for (int iz = 0; iz < 3 && bots.Count < 12; iz++)
            for (int ix = 0; ix < 4 && bots.Count < 12; ix++)
            {
                float x = layout.Team2Anchor.X + (ix - 1.5f) * 6f;
                float z = r.minZ + 3f + iz * 4f;
                if (!layout.StagingContains(2, x, z)) continue;
                if (CompoundConnectivity.IsBlocked(boxes, x, z, clearance)) continue;
                bots.Add(new Float3(x, 0f, z));
            }
            if (bots.Count == 0) bots.Add(layout.Team2Anchor);

            var route = new List<Float3>();
            CompoundConnectivity.TryFindPath(boxes, layout.Team1Anchor, layout.Team2Anchor, route, clearance);

            var collisionHash = HashGameplay(generatorId, generatorVersion, seed, boxes, "collision");
            var navHash = HashGameplay(generatorId, generatorVersion, seed, boxes, "navigation");
            return new CompoundPlan(
                seed, 1, 1, collisionHash, navHash,
                layout.Team1Anchor, layout.Team2Anchor, 8f, bots,
                Array.Empty<BuildingFootprint>(), Array.Empty<PropPlacement>(), covers, boxes, route,
                new float[2, 2], layout);
        }
    }
}
