#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// T078/T079 VD5 software: non-gameplay architectural trim (eaves, door frames, windows, baseboards,
    /// lane curbs, lamp posts, ridge caps, fence posts, industrial extras). Appends after gameplay
    /// geometry; never mutates gameplay boxes. Stable ids use high bases that do not collide with
    /// compound (10000+) / depot (20000+) gameplay.
    /// </summary>
    public static class ArenaVisualDetail
    {
        public const ulong CompoundVisualIdBase = 50000;
        public const ulong DepotVisualIdBase = 60000;
        public const int MaxVisualExtras = 800;

        private const float FrameDepth = 0.12f;
        private const float FrameWidth = 0.14f;
        private const float EaveOverhang = 0.35f;
        private const float EaveThickness = 0.12f;
        private const float TrimDepth = 0.08f;
        private const float BaseboardHeight = 0.18f;
        private const float CapHeight = 0.12f;
        private const float DoorClearHeight = 2.35f;
        private const float WindowSill = 1.05f;
        private const float WindowHeight = 1.1f;
        private const float WindowWidth = 1.4f;
        private const float CurbHeight = 0.18f;
        private const float CurbWidth = 0.35f;
        private const float LampPostHeight = 4.2f;
        private const float LampPostWidth = 0.16f;
        private const float LampHeadSize = 0.45f;
        private const float RidgeCapHeight = 0.14f;
        private const float RidgeCapWidth = 0.28f;
        private const float FencePostHeight = 1.35f;
        private const float FencePostWidth = 0.14f;

        /// <summary>Appends training-compound visual-only boxes. Call after all gameplay geometry.</summary>
        public static void AppendCompound(ArenaBoxBuilder b, ArenaLayout layout)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            b.JumpIdsTo(CompoundVisualIdBase);
            int start = b.Boxes.Count;

            var hqDoors = new[]
            {
                new ArenaDoor(ArenaSide.North, 0f, 2.4f), new ArenaDoor(ArenaSide.South, 0f, 2.4f),
                new ArenaDoor(ArenaSide.East, 0f, 2.4f), new ArenaDoor(ArenaSide.West, 0f, 2.4f)
            };
            DetailShell(b, 0f, 0f, 16f, 12f, 0.4f, 3.5f, hqDoors, mirror: false, longWallWindows: true);

            var barracksDoors = new[]
            {
                new ArenaDoor(ArenaSide.South, -3f, 2.4f),
                new ArenaDoor(ArenaSide.North, 3f, 2.4f),
                new ArenaDoor(ArenaSide.West, 0f, 2.4f)
            };
            DetailShell(b, 32.2f, -19f, 12f, 22f, 0.4f, 3.5f, barracksDoors, mirror: true, longWallWindows: true);

            // Barracks porch steps (visual only ≤ 0.15 m) at the west (mid-facing) door.
            AddPorchStep(b, 32.2f - 6f - 0.45f, -19f, 0.9f, 1.8f, 0.14f, mirror: true);

            // HQ plaza edge trim (inner ring around the 16×12 shell).
            AddPlazaEdge(b, 0f, 0f, 18f, 14f, 0.2f, mirror: false);

            // Range berm lips along the staggered concrete blocks (team-1 half + mirror).
            AddBermLip(b, -37.5f, -20f, 13f, 1.5f);
            AddBermLip(b, -27.5f, -6.75f, 13f, 1.5f);

            // Staging apron / lane curbs: low strips inside the spawn wall, gaps at the three gates.
            AddStagingCurbs(b, layout, gateXs: new[] { -30f, 0f, 30f }, gateWidth: 4f, wallZ: -37f);

            // Lane separator curb lips at x = ±18 (team-1 half + mirror via builder).
            AddLaneCurbAlongZ(b, 18f, -36f, -2f);
            AddLaneCurbAlongZ(b, -18f, -36f, -2f);

            // Gate lamp posts + short fence stubs along the staging apron (team-1 + mirror).
            AddGateLamps(b, gateXs: new[] { -30f, 0f, 30f }, wallZ: -37f, gateWidth: 4f);
            AddFenceStubs(b, layout, wallZ: -37f, spacing: 8f);

            // Roof ridge caps on HQ / barracks shells.

            // Ridge caps along HQ / barracks roof spines.
            AddRidgeCap(b, 0f, 0f, 16f, alongX: true, wallHeight: 3.5f, mirror: false);
            AddRidgeCap(b, 32.2f, -19f, 22f, alongX: false, wallHeight: 3.5f, mirror: true);

            // Lamp posts at plaza / staging / range approaches (mirrored across z = 0).
            AddLampPost(b, -10f, -8f, mirror: true);
            AddLampPost(b, 10f, -8f, mirror: true);
            AddLampPost(b, -22f, -34f, mirror: true);
            AddLampPost(b, 22f, -34f, mirror: true);
            AddLampPost(b, 0f, -34f, mirror: true);
            AddLampPost(b, 28f, -12f, mirror: true);

            // Fence posts just outside the staging wall, gaps preserved at the three gates.
            AddFencePostRow(b, -37.55f, -42f, -32f, spacing: 4f, mirror: true);
            AddFencePostRow(b, -37.55f, -28f, -2f, spacing: 4.5f, mirror: true);
            AddFencePostRow(b, -37.55f, 2f, 28f, spacing: 4.5f, mirror: true);
            AddFencePostRow(b, -37.55f, 32f, 42f, spacing: 4f, mirror: true);

            EnsureBudget(b.Boxes.Count - start);
        }

        /// <summary>Appends depot-yard visual-only boxes. Call after all gameplay geometry.</summary>
        public static void AppendDepot(ArenaBoxBuilder b, ArenaLayout layout)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            b.JumpIdsTo(DepotVisualIdBase);
            int start = b.Boxes.Count;

            var warehouseDoors = new[]
            {
                new ArenaDoor(ArenaSide.North, -3f, 3f), new ArenaDoor(ArenaSide.North, 3f, 3f),
                new ArenaDoor(ArenaSide.South, -3f, 3f), new ArenaDoor(ArenaSide.South, 3f, 3f),
                new ArenaDoor(ArenaSide.East, 0f, 2.4f), new ArenaDoor(ArenaSide.West, 0f, 2.4f)
            };
            DetailShell(b, 0f, 0f, 22f, 26f, 0.5f, 5f, warehouseDoors, mirror: false, longWallWindows: true);

            // Loading canopy lips outside N/S warehouse faces (above dock height, visual only).
            b.Add(new Float3(0f, 3.2f, -13.9f), new Float3(14f, 0.15f, 1.2f), ArenaBoxBuilder.TagSolid, mirror: true, gameplay: false);
            // Corner posts at warehouse outer corners.
            AddCornerPosts(b, 0f, 0f, 22f, 26f, 5.1f, mirror: false);

            // Container edge rails on a few maze stacks (thin top lips).
            AddContainerRail(b, 23.25f, 0f, 6f, 2.5f, 2.6f, mirror: false);
            AddContainerRail(b, 33.25f, 0f, 6f, 2.5f, 5.2f, mirror: false);
            AddContainerRail(b, 27.5f, -6.5f, 6f, 2.5f, 2.6f, mirror: true);
            AddContainerRail(b, 37f, -13f, 6f, 2.5f, 5.2f, mirror: true);
            AddContainerRail(b, -9f, -30f, 6f, 2.5f, 5.2f, mirror: true);
            AddContainerRail(b, 9f, -30f, 6f, 2.5f, 5.2f, mirror: true);

            // Staging apron curbs inside the gatehouse, gaps at funnelled gates.
            AddStagingCurbs(b, layout, gateXs: new[] { -28f, 0f, 26f }, gateWidth: 4f, wallZ: -42f);

            // Lane separator curb lips at x = ±20.
            AddLaneCurbAlongZ(b, 20f, -40f, -2f, mirror: false);
            AddLaneCurbAlongZ(b, -20f, -40f, -2f, mirror: false);
            // Mirror the curb on the +Z half explicitly (separators span full Z with mirror:false).
            AddLaneCurbAlongZ(b, 20f, 2f, 40f, mirror: false);
            AddLaneCurbAlongZ(b, -20f, 2f, 40f, mirror: false);

            AddGateLamps(b, gateXs: new[] { -28f, 0f, 26f }, wallZ: -42f, gateWidth: 4f);
            AddFenceStubs(b, layout, wallZ: -42f, spacing: 9f);
            // Container corner castings on a subset of maze stacks (metal-prop heuristic).
            AddContainerCorners(b, 23.25f, 0f, 6f, 2.5f, 2.6f, mirror: false);
            AddContainerCorners(b, 33.25f, 0f, 6f, 2.5f, 5.2f, mirror: false);
            AddContainerCorners(b, 27.5f, -6.5f, 6f, 2.5f, 2.6f, mirror: true);
            AddContainerCorners(b, -9f, -30f, 6f, 2.5f, 5.2f, mirror: true);

            // Warehouse roof ridge + lamp posts on loading apron / maze approaches.
            AddRidgeCap(b, 0f, 0f, 26f, alongX: false, wallHeight: 5f, mirror: false);
            AddLampPost(b, -14f, -16f, mirror: true);
            AddLampPost(b, 14f, -16f, mirror: true);
            AddLampPost(b, -30f, -38f, mirror: true);
            AddLampPost(b, 24f, -38f, mirror: true);
            AddLampPost(b, 0f, -38f, mirror: true);
            AddLampPost(b, 30f, -8f, mirror: true);

            // Fence posts just outside the gatehouse apron between funnelled gates.
            AddFencePostRow(b, -42.55f, -40f, -30f, spacing: 3.5f, mirror: true);
            AddFencePostRow(b, -42.55f, -26f, -2f, spacing: 4f, mirror: true);
            AddFencePostRow(b, -42.55f, 2f, 24f, spacing: 4f, mirror: true);
            AddFencePostRow(b, -42.55f, 28f, 40f, spacing: 3.5f, mirror: true);

            EnsureBudget(b.Boxes.Count - start);
        }

        private static void DetailShell(
            ArenaBoxBuilder b,
            float cx, float cz, float sizeX, float sizeZ, float thickness, float height,
            IReadOnlyList<ArenaDoor> doors, bool mirror, bool longWallWindows)
        {
            AddEaves(b, cx, cz, sizeX, sizeZ, height, mirror);
            AddBaseboardAndCap(b, cx, cz, sizeX, sizeZ, thickness, height, mirror);
            for (int i = 0; i < doors.Count; i++)
                AddDoorFrame(b, cx, cz, sizeX, sizeZ, thickness, height, doors[i], mirror);
            if (longWallWindows)
                AddWindowFrames(b, cx, cz, sizeX, sizeZ, thickness, height, doors, mirror);
        }

        private static void AddGateLamps(ArenaBoxBuilder b, float[] gateXs, float wallZ, float gateWidth)
        {
            const float postH = 3.2f;
            const float post = 0.18f;
            const float head = 0.28f;
            float z = wallZ + (wallZ < 0f ? 0.9f : -0.9f);
            float y = postH * 0.5f;
            for (int i = 0; i < gateXs.Length; i++)
            {
                float half = gateWidth * 0.5f + 0.55f;
                // Stem + small head box; stem hits metal-prop heuristic.
                b.Add(new Float3(gateXs[i] - half, y, z), new Float3(post, postH, post), ArenaBoxBuilder.TagSolid, true, false);
                b.Add(new Float3(gateXs[i] - half, postH + head * 0.5f, z), new Float3(head, head, head), ArenaBoxBuilder.TagSolid, true, false);
                b.Add(new Float3(gateXs[i] + half, y, z), new Float3(post, postH, post), ArenaBoxBuilder.TagSolid, true, false);
                b.Add(new Float3(gateXs[i] + half, postH + head * 0.5f, z), new Float3(head, head, head), ArenaBoxBuilder.TagSolid, true, false);
            }
        }

        private static void AddFenceStubs(ArenaBoxBuilder b, ArenaLayout layout, float wallZ, float spacing)
        {
            float z = wallZ + (wallZ < 0f ? 1.4f : -1.4f);
            float x0 = layout.StagingMinX + 2f;
            float x1 = layout.StagingMaxX - 2f;
            for (float x = x0; x <= x1 + 0.01f; x += spacing)
            {
                // Skip near gate centers (±2 m around typical mid-map gates).
                if (Math.Abs(x) < 3f || Math.Abs(x - 28f) < 3f || Math.Abs(x + 28f) < 3f ||
                    Math.Abs(x - 30f) < 3f || Math.Abs(x + 30f) < 3f || Math.Abs(x - 26f) < 3f)
                    continue;
                b.Add(new Float3(x, 0.55f, z), new Float3(0.12f, 1.1f, 0.12f), ArenaBoxBuilder.TagSolid, true, false);
            }
        }

        private static void AddContainerCorners(ArenaBoxBuilder b, float x, float z, float sizeX, float sizeZ, float height, bool mirror)
        {
            // Uniform corner castings (TagCover → metal). Same size on ±Z so mirror-twin checks hold.
            const float cast = 0.28f;
            float hx = sizeX * 0.5f, hz = sizeZ * 0.5f;
            float y = height + cast * 0.5f;
            b.Add(new Float3(x + hx, y, z + hz), new Float3(cast, cast, cast), ArenaBoxBuilder.TagCover, mirror, false);
            b.Add(new Float3(x - hx, y, z + hz), new Float3(cast, cast, cast), ArenaBoxBuilder.TagCover, mirror, false);
            b.Add(new Float3(x + hx, y, z - hz), new Float3(cast, cast, cast), ArenaBoxBuilder.TagCover, mirror, false);
            b.Add(new Float3(x - hx, y, z - hz), new Float3(cast, cast, cast), ArenaBoxBuilder.TagCover, mirror, false);
        }

        private static void AddEaves(ArenaBoxBuilder b, float cx, float cz, float sizeX, float sizeZ, float height, bool mirror)
        {
            float y = height + 0.06f;
            float ox = sizeX * 0.5f + EaveOverhang * 0.5f;
            float oz = sizeZ * 0.5f + EaveOverhang * 0.5f;
            // Four perimeter lips extending past the existing roof slab.
            b.Add(new Float3(cx, y, cz + oz), new Float3(sizeX + EaveOverhang * 2f, EaveThickness, EaveOverhang), ArenaBoxBuilder.TagSolid, mirror, false);
            b.Add(new Float3(cx, y, cz - oz), new Float3(sizeX + EaveOverhang * 2f, EaveThickness, EaveOverhang), ArenaBoxBuilder.TagSolid, mirror, false);
            b.Add(new Float3(cx + ox, y, cz), new Float3(EaveOverhang, EaveThickness, sizeZ), ArenaBoxBuilder.TagSolid, mirror, false);
            b.Add(new Float3(cx - ox, y, cz), new Float3(EaveOverhang, EaveThickness, sizeZ), ArenaBoxBuilder.TagSolid, mirror, false);
        }

        private static void AddBaseboardAndCap(
            ArenaBoxBuilder b, float cx, float cz, float sizeX, float sizeZ, float thickness, float height, bool mirror)
        {
            float x0 = cx - sizeX * 0.5f, x1 = cx + sizeX * 0.5f;
            float z0 = cz - sizeZ * 0.5f, z1 = cz + sizeZ * 0.5f;
            float outer = thickness * 0.5f + TrimDepth * 0.5f;
            // Proud of outer wall faces: N/S along X, E/W along Z.
            AddTrimBand(b, cx, z1 + outer, sizeX, TrimDepth, BaseboardHeight * 0.5f, BaseboardHeight, mirror);
            AddTrimBand(b, cx, z0 - outer, sizeX, TrimDepth, BaseboardHeight * 0.5f, BaseboardHeight, mirror);
            AddTrimBand(b, x1 + outer, cz, TrimDepth, sizeZ - thickness * 2f, BaseboardHeight * 0.5f, BaseboardHeight, mirror);
            AddTrimBand(b, x0 - outer, cz, TrimDepth, sizeZ - thickness * 2f, BaseboardHeight * 0.5f, BaseboardHeight, mirror);

            float capY = height - CapHeight * 0.5f;
            AddTrimBand(b, cx, z1 + outer, sizeX, TrimDepth, capY, CapHeight, mirror);
            AddTrimBand(b, cx, z0 - outer, sizeX, TrimDepth, capY, CapHeight, mirror);
            AddTrimBand(b, x1 + outer, cz, TrimDepth, sizeZ - thickness * 2f, capY, CapHeight, mirror);
            AddTrimBand(b, x0 - outer, cz, TrimDepth, sizeZ - thickness * 2f, capY, CapHeight, mirror);
        }

        private static void AddTrimBand(ArenaBoxBuilder b, float x, float z, float sx, float sz, float centerY, float height, bool mirror)
        {
            if (sx <= 0.05f || sz <= 0.05f) return;
            b.Add(new Float3(x, centerY, z), new Float3(sx, height, sz), ArenaBoxBuilder.TagSolid, mirror, false);
        }

        private static void AddDoorFrame(
            ArenaBoxBuilder b, float cx, float cz, float sizeX, float sizeZ, float thickness, float height,
            ArenaDoor door, bool mirror)
        {
            float lintelBottom = Math.Min(DoorClearHeight, height - 0.25f);
            float lintelH = Math.Max(0.18f, height - lintelBottom);
            float lintelY = lintelBottom + lintelH * 0.5f;
            float jambH = lintelBottom;
            float jambY = jambH * 0.5f;
            float proud = thickness * 0.5f + FrameDepth * 0.5f;

            switch (door.Side)
            {
                case ArenaSide.North:
                case ArenaSide.South:
                {
                    float wallZ = door.Side == ArenaSide.North
                        ? cz + sizeZ * 0.5f - thickness * 0.5f + proud
                        : cz - sizeZ * 0.5f + thickness * 0.5f - proud;
                    float openL = cx + door.Offset - door.Width * 0.5f;
                    float openR = cx + door.Offset + door.Width * 0.5f;
                    // Jambs entirely outside the clear opening.
                    b.Add(new Float3(openL - FrameWidth * 0.5f, jambY, wallZ), new Float3(FrameWidth, jambH, FrameDepth), ArenaBoxBuilder.TagSolid, mirror, false);
                    b.Add(new Float3(openR + FrameWidth * 0.5f, jambY, wallZ), new Float3(FrameWidth, jambH, FrameDepth), ArenaBoxBuilder.TagSolid, mirror, false);
                    b.Add(new Float3(cx + door.Offset, lintelY, wallZ), new Float3(door.Width + FrameWidth * 2f, lintelH, FrameDepth), ArenaBoxBuilder.TagSolid, mirror, false);
                    break;
                }
                case ArenaSide.East:
                case ArenaSide.West:
                {
                    float wallX = door.Side == ArenaSide.East
                        ? cx + sizeX * 0.5f - thickness * 0.5f + proud
                        : cx - sizeX * 0.5f + thickness * 0.5f - proud;
                    float openL = cz + door.Offset - door.Width * 0.5f;
                    float openR = cz + door.Offset + door.Width * 0.5f;
                    b.Add(new Float3(wallX, jambY, openL - FrameWidth * 0.5f), new Float3(FrameDepth, jambH, FrameWidth), ArenaBoxBuilder.TagSolid, mirror, false);
                    b.Add(new Float3(wallX, jambY, openR + FrameWidth * 0.5f), new Float3(FrameDepth, jambH, FrameWidth), ArenaBoxBuilder.TagSolid, mirror, false);
                    b.Add(new Float3(wallX, lintelY, cz + door.Offset), new Float3(FrameDepth, lintelH, door.Width + FrameWidth * 2f), ArenaBoxBuilder.TagSolid, mirror, false);
                    break;
                }
            }
        }

        private static void AddWindowFrames(
            ArenaBoxBuilder b, float cx, float cz, float sizeX, float sizeZ, float thickness, float height,
            IReadOnlyList<ArenaDoor> doors, bool mirror)
        {
            // Long walls = larger horizontal span. Place 2 window frames per long side, skipping door bands.
            bool longIsX = sizeX >= sizeZ;
            float proud = thickness * 0.5f + FrameDepth * 0.5f;
            float winY = WindowSill + WindowHeight * 0.5f;
            if (winY + WindowHeight * 0.5f > height - 0.2f) return;

            if (longIsX)
            {
                float[] xs = { cx - sizeX * 0.28f, cx + sizeX * 0.28f };
                PlaceWindowsOnWall(b, xs, cz + sizeZ * 0.5f - thickness * 0.5f + proud, true, ArenaSide.North, cx, doors, winY, mirror);
                PlaceWindowsOnWall(b, xs, cz - sizeZ * 0.5f + thickness * 0.5f - proud, true, ArenaSide.South, cx, doors, winY, mirror);
            }
            else
            {
                float[] zs = { cz - sizeZ * 0.28f, cz + sizeZ * 0.28f };
                PlaceWindowsOnWall(b, zs, cx + sizeX * 0.5f - thickness * 0.5f + proud, false, ArenaSide.East, cz, doors, winY, mirror);
                PlaceWindowsOnWall(b, zs, cx - sizeX * 0.5f + thickness * 0.5f - proud, false, ArenaSide.West, cz, doors, winY, mirror);
            }
        }

        private static void PlaceWindowsOnWall(
            ArenaBoxBuilder b, float[] along, float face, bool alongX, ArenaSide side, float shellCenterAlong,
            IReadOnlyList<ArenaDoor> doors, float winY, bool mirror)
        {
            for (int i = 0; i < along.Length; i++)
            {
                float c = along[i];
                if (OverlapsDoor(doors, side, shellCenterAlong, c, WindowWidth + 0.4f)) continue;
                AddWindowRecess(b, c, face, alongX, winY, mirror);
            }
        }

        private static bool OverlapsDoor(IReadOnlyList<ArenaDoor> doors, ArenaSide side, float shellCenterAlong, float along, float pad)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                if (doors[i].Side != side) continue;
                float doorAlong = shellCenterAlong + doors[i].Offset;
                if (Math.Abs(along - doorAlong) < doors[i].Width * 0.5f + pad) return true;
            }
            return false;
        }

        /// <summary>Thin rectangular frame (sill/header/jambs) that reads as a punched window recess.</summary>
        private static void AddWindowRecess(ArenaBoxBuilder b, float along, float face, bool alongX, float winY, bool mirror)
        {
            float halfW = WindowWidth * 0.5f;
            float halfH = WindowHeight * 0.5f;
            const float bar = 0.1f;
            const float sillDepth = 0.22f;
            const float sillHeight = 0.08f;
            if (alongX)
            {
                b.Add(new Float3(along - halfW - bar * 0.5f, winY, face), new Float3(bar, WindowHeight, FrameDepth), ArenaBoxBuilder.TagSolid, mirror, false);
                b.Add(new Float3(along + halfW + bar * 0.5f, winY, face), new Float3(bar, WindowHeight, FrameDepth), ArenaBoxBuilder.TagSolid, mirror, false);
                b.Add(new Float3(along, winY - halfH - bar * 0.5f, face), new Float3(WindowWidth + bar * 2f, bar, FrameDepth), ArenaBoxBuilder.TagSolid, mirror, false);
                b.Add(new Float3(along, winY + halfH + bar * 0.5f, face), new Float3(WindowWidth + bar * 2f, bar, FrameDepth), ArenaBoxBuilder.TagSolid, mirror, false);
                // Proud sill ledge under the frame.
                float sillY = winY - halfH - bar - sillHeight * 0.5f;
                float sillFace = face + (face >= 0f ? sillDepth * 0.35f : -sillDepth * 0.35f);
                b.Add(new Float3(along, sillY, sillFace), new Float3(WindowWidth + bar * 2f, sillHeight, sillDepth), ArenaBoxBuilder.TagSolid, mirror, false);
            }
            else
            {
                b.Add(new Float3(face, winY, along - halfW - bar * 0.5f), new Float3(FrameDepth, WindowHeight, bar), ArenaBoxBuilder.TagSolid, mirror, false);
                b.Add(new Float3(face, winY, along + halfW + bar * 0.5f), new Float3(FrameDepth, WindowHeight, bar), ArenaBoxBuilder.TagSolid, mirror, false);
                b.Add(new Float3(face, winY - halfH - bar * 0.5f, along), new Float3(FrameDepth, bar, WindowWidth + bar * 2f), ArenaBoxBuilder.TagSolid, mirror, false);
                b.Add(new Float3(face, winY + halfH + bar * 0.5f, along), new Float3(FrameDepth, bar, WindowWidth + bar * 2f), ArenaBoxBuilder.TagSolid, mirror, false);
                float sillY = winY - halfH - bar - sillHeight * 0.5f;
                float sillFace = face + (face >= 0f ? sillDepth * 0.35f : -sillDepth * 0.35f);
                b.Add(new Float3(sillFace, sillY, along), new Float3(sillDepth, sillHeight, WindowWidth + bar * 2f), ArenaBoxBuilder.TagSolid, mirror, false);
            }
        }

        private static void AddPorchStep(ArenaBoxBuilder b, float x, float z, float sizeX, float sizeZ, float height, bool mirror)
        {
            if (height > 0.15f) throw new ArgumentOutOfRangeException(nameof(height));
            b.Add(new Float3(x, height * 0.5f, z), new Float3(sizeX, height, sizeZ), ArenaBoxBuilder.TagSolid, mirror, false);
        }

        private static void AddPlazaEdge(ArenaBoxBuilder b, float cx, float cz, float sizeX, float sizeZ, float strip, bool mirror)
        {
            float y = CurbHeight * 0.5f;
            b.Add(new Float3(cx, y, cz + sizeZ * 0.5f), new Float3(sizeX, CurbHeight, strip), ArenaBoxBuilder.TagSolid, mirror, false);
            b.Add(new Float3(cx, y, cz - sizeZ * 0.5f), new Float3(sizeX, CurbHeight, strip), ArenaBoxBuilder.TagSolid, mirror, false);
            b.Add(new Float3(cx + sizeX * 0.5f, y, cz), new Float3(strip, CurbHeight, sizeZ - strip * 2f), ArenaBoxBuilder.TagSolid, mirror, false);
            b.Add(new Float3(cx - sizeX * 0.5f, y, cz), new Float3(strip, CurbHeight, sizeZ - strip * 2f), ArenaBoxBuilder.TagSolid, mirror, false);
        }

        private static void AddBermLip(ArenaBoxBuilder b, float x, float z, float sizeX, float sizeZ)
        {
            // Thin raised lip on the top outer edge of a range block (team-1 + mirror).
            float top = 2.4f;
            b.Add(new Float3(x, top + 0.08f, z), new Float3(sizeX + 0.2f, 0.16f, sizeZ + 0.2f), ArenaBoxBuilder.TagSolid, true, false);
        }

        private static void AddStagingCurbs(ArenaBoxBuilder b, ArenaLayout layout, float[] gateXs, float gateWidth, float wallZ)
        {
            // Low curb just inside the spawn/gate wall on the playable side, with gaps at gates.
            float z = wallZ + (wallZ < 0f ? 0.55f : -0.55f);
            float x0 = layout.StagingMinX + 1f;
            float x1 = layout.StagingMaxX - 1f;
            var gaps = new ArenaGap[gateXs.Length];
            for (int i = 0; i < gateXs.Length; i++)
                gaps[i] = new ArenaGap(gateXs[i], gateWidth + 1.5f);
            // Manual segmented curb (cannot use WallAlongX — that emits gameplay boxes).
            AddGappedCurbAlongX(b, z, x0, x1, gaps, mirror: true);
        }

        private static void AddGappedCurbAlongX(ArenaBoxBuilder b, float z, float x0, float x1, ArenaGap[] gaps, bool mirror)
        {
            var sorted = new List<ArenaGap>(gaps);
            sorted.Sort((p, q) => p.Center.CompareTo(q.Center));
            float cursor = x0;
            for (int i = 0; i < sorted.Count; i++)
            {
                float gl = sorted[i].Center - sorted[i].Width * 0.5f;
                float gr = sorted[i].Center + sorted[i].Width * 0.5f;
                if (gl - cursor > 0.4f)
                {
                    float mid = (cursor + Math.Min(gl, x1)) * 0.5f;
                    float len = Math.Min(gl, x1) - cursor;
                    b.Add(new Float3(mid, CurbHeight * 0.5f, z), new Float3(len, CurbHeight, CurbWidth), ArenaBoxBuilder.TagSolid, mirror, false);
                }
                cursor = Math.Max(cursor, gr);
            }
            if (x1 - cursor > 0.4f)
            {
                float mid = (cursor + x1) * 0.5f;
                b.Add(new Float3(mid, CurbHeight * 0.5f, z), new Float3(x1 - cursor, CurbHeight, CurbWidth), ArenaBoxBuilder.TagSolid, mirror, false);
            }
        }

        private static void AddLaneCurbAlongZ(ArenaBoxBuilder b, float x, float z0, float z1, bool mirror = true)
        {
            if (z1 <= z0) return;
            float mid = (z0 + z1) * 0.5f;
            float len = z1 - z0;
            // Offset slightly toward lane centre so the separator wall remains the gameplay blocker.
            float curbX = x + (x >= 0f ? -0.45f : 0.45f);
            b.Add(new Float3(curbX, CurbHeight * 0.5f, mid), new Float3(CurbWidth, CurbHeight, len), ArenaBoxBuilder.TagSolid, mirror, false);
        }

        private static void AddCornerPosts(ArenaBoxBuilder b, float cx, float cz, float sizeX, float sizeZ, float height, bool mirror)
        {
            const float post = 0.28f;
            float hx = sizeX * 0.5f + 0.15f, hz = sizeZ * 0.5f + 0.15f;
            float y = height * 0.5f;
            b.Add(new Float3(cx + hx, y, cz + hz), new Float3(post, height, post), ArenaBoxBuilder.TagSolid, mirror, false);
            b.Add(new Float3(cx - hx, y, cz + hz), new Float3(post, height, post), ArenaBoxBuilder.TagSolid, mirror, false);
            b.Add(new Float3(cx + hx, y, cz - hz), new Float3(post, height, post), ArenaBoxBuilder.TagSolid, mirror, false);
            b.Add(new Float3(cx - hx, y, cz - hz), new Float3(post, height, post), ArenaBoxBuilder.TagSolid, mirror, false);
        }

        private static void AddContainerRail(ArenaBoxBuilder b, float x, float z, float sizeX, float sizeZ, float height, bool mirror)
        {
            float y = height + 0.06f;
            const float rail = 0.08f;
            b.Add(new Float3(x, y, z + sizeZ * 0.5f), new Float3(sizeX, rail, rail), ArenaBoxBuilder.TagCover, mirror, false);
            b.Add(new Float3(x, y, z - sizeZ * 0.5f), new Float3(sizeX, rail, rail), ArenaBoxBuilder.TagCover, mirror, false);
            b.Add(new Float3(x + sizeX * 0.5f, y, z), new Float3(rail, rail, sizeZ), ArenaBoxBuilder.TagCover, mirror, false);
            b.Add(new Float3(x - sizeX * 0.5f, y, z), new Float3(rail, rail, sizeZ), ArenaBoxBuilder.TagCover, mirror, false);
        }

        /// <summary>
        /// Thin ridge along the longer roof axis so eaves read as a capped roof rather than a flat slab.
        /// </summary>
        private static void AddRidgeCap(ArenaBoxBuilder b, float cx, float cz, float length, bool alongX, float wallHeight, bool mirror)
        {
            float y = wallHeight + EaveThickness + 0.08f + RidgeCapHeight * 0.5f;
            if (alongX)
                b.Add(new Float3(cx, y, cz), new Float3(length + EaveOverhang, RidgeCapHeight, RidgeCapWidth), ArenaBoxBuilder.TagSolid, mirror, false);
            else
                b.Add(new Float3(cx, y, cz), new Float3(RidgeCapWidth, RidgeCapHeight, length + EaveOverhang), ArenaBoxBuilder.TagSolid, mirror, false);
        }

        private static void AddLampPost(ArenaBoxBuilder b, float x, float z, bool mirror)
        {
            float postY = LampPostHeight * 0.5f;
            b.Add(new Float3(x, postY, z), new Float3(LampPostWidth, LampPostHeight, LampPostWidth), ArenaBoxBuilder.TagSolid, mirror, false);
            // Cross-arm + head so the prop reads as a yard light, not a lone pole.
            b.Add(new Float3(x + 0.35f, LampPostHeight - 0.08f, z), new Float3(0.7f, 0.1f, 0.1f), ArenaBoxBuilder.TagSolid, mirror, false);
            b.Add(new Float3(x + 0.55f, LampPostHeight - 0.28f, z), new Float3(LampHeadSize, 0.2f, LampHeadSize), ArenaBoxBuilder.TagSolid, mirror, false);
        }

        /// <summary>Posts along X at fixed z (team-1 half); builder mirror fills the opposite staging wall.</summary>
        private static void AddFencePostRow(ArenaBoxBuilder b, float z, float x0, float x1, float spacing, bool mirror)
        {
            if (x1 <= x0 || spacing <= 0.2f) return;
            float y = FencePostHeight * 0.5f;
            for (float x = x0; x <= x1 + 0.001f; x += spacing)
                b.Add(new Float3(x, y, z), new Float3(FencePostWidth, FencePostHeight, FencePostWidth), ArenaBoxBuilder.TagSolid, mirror, false);
        }

        private static void EnsureBudget(int added)
        {
            if (added <= 0) throw new InvalidOperationException("ArenaVisualDetail produced no visual boxes.");
            if (added > MaxVisualExtras)
                throw new InvalidOperationException("ArenaVisualDetail exceeded visual budget: " + added + " > " + MaxVisualExtras);
        }
    }
}
