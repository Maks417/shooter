#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.VectorMath;

namespace Shooter.Generation.Environment
{
    /// <summary>Which generated military kit an authored soldier body wears (ADR-124).</summary>
    public readonly struct SoldierGearSpec
    {
        /// <summary>0 bare head, 1 helmet with goggles, 2 helmet with NVG mount and no goggles.</summary>
        public byte Helmet { get; }
        /// <summary>0 none, 1 plate carrier with magazine pouches, 2 light chest rig.</summary>
        public byte Vest { get; }
        public bool Backpack { get; }
        public bool KneePads { get; }
        public bool Belt { get; }
        public SoldierGearSpec(byte helmet, byte vest, bool backpack, bool kneePads, bool belt)
        {
            if (helmet > 2 || vest > 2) throw new ArgumentOutOfRangeException(nameof(helmet), "Unknown gear style.");
            Helmet = helmet; Vest = vest; Backpack = backpack; KneePads = kneePads; Belt = belt;
        }
    }

    /// <summary>One rigid gear mesh attached to a semantic joint; positions are relative to that joint's bind position.</summary>
    public sealed class SoldierGearPiece
    {
        public ushort Joint { get; }
        public MeshData Mesh { get; }
        public SoldierGearPiece(ushort joint, MeshData mesh) { Joint = joint; Mesh = mesh; }
    }

    /// <summary>
    /// ADR-124 generated military kit for licensed authored bodies: helmet, goggles, plate carrier or chest rig,
    /// magazine pouches, backpack, belt and knee pads. Shapes follow the soldier.loft 1.1.0 gear, rebuilt here as
    /// rigid per-joint pieces in the 1800 mm semantic bind frame (+Z forward, +Y up, character left at -X), so the
    /// presentation can parent them to the matching humanoid bones. Material slots: 0 team cover fabric, 1 webbing
    /// and pouches, 2 lens, 3 hardware. Presentation only; nothing here reaches authority or the map packages.
    /// </summary>
    public static class SoldierGearKit
    {
        public const ushort SlotCover = 0, SlotWebbing = 1, SlotLens = 2, SlotHardware = 3;
        private const ushort Pelvis = 1, Chest = 4, Head = 6, LeftCalf = 16, RightCalf = 20;

        public static IReadOnlyList<SoldierGearPiece> Build(SoldierGearSpec spec, bool detail)
        {
            int arc = detail ? 3 : 1, edge = detail ? 2 : 0, tube = detail ? 12 : 6, lathe = detail ? 24 : 10, loftRadial = detail ? 24 : 10;
            var xToZ = new Quat(0, -.70710677f, 0, .70710677f); var zToY = new Quat(-.70710677f, 0, 0, .70710677f);
            var parts = new Dictionary<ushort, MeshAssembly>();
            void Part(ushort joint, MeshData mesh, Float3 at, Quat rotation, ushort slot)
            {
                if (!parts.TryGetValue(joint, out var assembly)) parts[joint] = assembly = new MeshAssembly();
                Float3 bind = SoldierSkeleton.BindPositions[joint];
                assembly.Add(mesh, new Float3(at.X - bind.X, at.Y - bind.Y, at.Z - bind.Z), rotation, slot);
            }
            MeshData Slab(float width, float roll, params float[] zyr)
            {
                var corners = new ProfileCorner[zyr.Length / 3];
                for (int i = 0; i < corners.Length; i++) corners[i] = new ProfileCorner(zyr[3 * i], zyr[3 * i + 1], zyr[3 * i + 2]);
                return ShapeKit.Slab(corners, width, roll, arc, edge);
            }
            MeshData Band(float y0, float y1, float halfWidth, float halfDepth, int rings)
            {
                var sections = new List<ShapeKit.Section>();
                for (int i = 0; i <= rings; i++) { float y = y0 + (y1 - y0) * i / rings; sections.Add(new ShapeKit.Section(y, 0, halfWidth, halfDepth, 3)); }
                return ShapeKit.Loft(sections, loftRadial);
            }
            Float3 Arc(float a, float bz, float degrees, float y) { double r = degrees * Math.PI / 180; return new Float3((float)(a * Math.Cos(r)), y, (float)(bz * Math.Sin(r))); }
            MeshData Ring(List<Float3> path, float width, float thickness, int radial)
            {
                var radii = new Float2[path.Count]; for (int i = 0; i < radii.Length; i++) radii[i] = new Float2(width, thickness);
                return ShapeKit.Tube(path, radii, new Float3(0, 1, 0), radial, 0);
            }

            // ---- head: turned helmet shell, rails, NVG mount, goggles with strap.
            if (spec.Helmet != 0)
            {
                Part(Head, ShapeKit.Lathe(new[] { new Float2(.1170f, 0), new Float2(.1178f, .004f), new Float2(.1135f, .010f), new Float2(.1125f, .034f), new Float2(.1065f, .062f),
                    new Float2(.0925f, .088f), new Float2(.0705f, .108f), new Float2(.0425f, .120f), new Float2(.0150f, .1255f) }, lathe), new Float3(0, 1.672f, -.004f), zToY, SlotCover);
                foreach (int side in new[] { -1, 1 })
                    Part(Head, Slab(.006f, .0012f, -.034f, -.006f, .003f, .034f, -.006f, .003f, .034f, .006f, .003f, -.034f, .006f, .003f), new Float3(side * .110f, 1.703f, -.004f), Quat.Identity, SlotHardware);
                Part(Head, Slab(.040f, .003f, -.004f, -.020f, .004f, .013f, -.016f, .004f, .013f, .016f, .004f, -.004f, .020f, .004f), new Float3(0, 1.735f, .100f), Quat.Identity, SlotHardware);
                if (spec.Helmet == 2)
                    Part(Head, Slab(.030f, .004f, -.012f, -.018f, .006f, .022f, -.018f, .006f, .022f, .020f, .006f, -.012f, .020f, .006f), new Float3(0, 1.712f, .128f), Quat.Identity, SlotHardware);
                if (spec.Helmet == 1)
                {
                    var strap = new List<Float3>(); for (int d = 30; d >= -210; d -= detail ? 15 : 30) strap.Add(Arc(.088f, .104f, d, 1.652f));
                    Part(Head, Ring(strap, .0105f, .0026f, 6), new Float3(0, 0, 0), Quat.Identity, SlotWebbing);
                    var frame = new List<Float3>(); for (int d = 28; d <= 152; d += detail ? 8 : 31) frame.Add(Arc(.091f, .109f, d, 1.652f));
                    Part(Head, Ring(frame, .0190f, .0095f, tube), new Float3(0, 0, 0), Quat.Identity, SlotHardware);
                    var lens = new List<Float3>(); for (int d = 38; d <= 142; d += detail ? 8 : 26) lens.Add(Arc(.095f, .116f, d, 1.653f));
                    Part(Head, Ring(lens, .0150f, .0050f, tube), new Float3(0, 0, 0), Quat.Identity, SlotLens);
                }
            }

            // ---- torso: plate carrier (front and back plates, cummerbund, pouches, shoulder straps) or a light chest rig.
            if (spec.Vest == 1)
            {
                Part(Chest, Slab(.040f, .010f, -.150f, 1.105f, .025f, .150f, 1.105f, .025f, .150f, 1.330f, .030f, .100f, 1.405f, .020f, -.100f, 1.405f, .020f, -.150f, 1.330f, .030f),
                    new Float3(0, 0, .148f), xToZ, SlotCover);
                Part(Chest, Slab(.040f, .010f, -.150f, 1.105f, .025f, .150f, 1.105f, .025f, .150f, 1.330f, .030f, .100f, 1.405f, .020f, -.100f, 1.405f, .020f, -.150f, 1.330f, .030f),
                    new Float3(0, 0, -.150f), xToZ, SlotCover);
                Part(Chest, Band(1.095f, 1.245f, .181f, .129f, 3), new Float3(0, 0, 0), zToYInverse(), SlotCover);
                if (detail)
                    for (int k = 0; k < 3; k++)
                    {
                        float y0 = 1.272f + .040f * k;
                        Part(Chest, Slab(.005f, .0012f, -.128f, y0, .002f, .128f, y0, .002f, .128f, y0 + .018f, .002f, -.128f, y0 + .018f, .002f), new Float3(0, 0, .169f), xToZ, SlotWebbing);
                    }
                for (int k = -1; k <= 1; k++)
                {
                    Part(Chest, Slab(.046f, .010f, -.034f, 1.105f, .010f, .034f, 1.105f, .010f, .034f, 1.235f, .012f, -.034f, 1.235f, .012f), new Float3(k * .078f, 0, .190f), xToZ, SlotWebbing);
                    if (detail) Part(Chest, Slab(.050f, .006f, -.036f, 1.215f, .006f, .036f, 1.215f, .006f, .036f, 1.248f, .008f, -.036f, 1.250f, .008f), new Float3(k * .078f, 0, .192f), xToZ, SlotCover);
                }
                foreach (int side in new[] { -1, 1 })
                {
                    var path = ShapeKit.Spline(new[] { new Float3(side * .105f, 1.395f, .150f), new Float3(side * .122f, 1.500f, .095f), new Float3(side * .126f, 1.535f, 0),
                        new Float3(side * .122f, 1.500f, -.095f), new Float3(side * .105f, 1.395f, -.150f) }, detail ? 4 : 2);
                    var radii = new Float2[path.Count]; for (int i = 0; i < radii.Length; i++) radii[i] = new Float2(.026f, .0070f);
                    Part(Chest, ShapeKit.Tube(path, radii, new Float3(1, 0, 0), Math.Max(6, tube - 4), 0), new Float3(0, 0, 0), Quat.Identity, SlotWebbing);
                }
            }
            else if (spec.Vest == 2)
            {
                // Chest rig: a pouch row on a harness, no plates.
                for (int k = -1; k <= 1; k++)
                    Part(Chest, Slab(.044f, .008f, -.032f, 1.130f, .010f, .032f, 1.130f, .010f, .032f, 1.250f, .012f, -.032f, 1.250f, .012f), new Float3(k * .072f, 0, .150f), xToZ, SlotWebbing);
                Part(Chest, Slab(.020f, .006f, -.120f, 1.120f, .008f, .120f, 1.120f, .008f, .120f, 1.150f, .008f, -.120f, 1.150f, .008f), new Float3(0, 0, .130f), xToZ, SlotWebbing);
                foreach (int side in new[] { -1, 1 })
                {
                    var path = ShapeKit.Spline(new[] { new Float3(side * .080f, 1.250f, .140f), new Float3(side * .110f, 1.480f, .090f), new Float3(side * .120f, 1.530f, 0),
                        new Float3(side * .110f, 1.480f, -.090f), new Float3(side * .060f, 1.200f, -.135f) }, detail ? 4 : 2);
                    var radii = new Float2[path.Count]; for (int i = 0; i < radii.Length; i++) radii[i] = new Float2(.022f, .0060f);
                    Part(Chest, ShapeKit.Tube(path, radii, new Float3(1, 0, 0), Math.Max(6, tube - 4), 0), new Float3(0, 0, 0), Quat.Identity, SlotWebbing);
                }
            }
            if (spec.Backpack)
            {
                Part(Chest, Slab(.120f, .030f, -.150f, 1.080f, .040f, .150f, 1.080f, .040f, .155f, 1.380f, .050f, -.155f, 1.380f, .050f), new Float3(0, 0, -.215f), xToZ, SlotCover);
                if (detail) Part(Chest, Slab(.112f, .016f, -.140f, 1.355f, .030f, .140f, 1.355f, .030f, .136f, 1.402f, .022f, -.136f, 1.402f, .022f), new Float3(0, 0, -.216f), xToZ, SlotWebbing);
            }

            // ---- belt with buckle and a side pouch.
            if (spec.Belt)
            {
                Part(Pelvis, Band(.930f, .990f, .175f, .129f, 2), new Float3(0, 0, 0), zToYInverse(), SlotWebbing);
                Part(Pelvis, Slab(.010f, .002f, -.028f, .938f, .004f, .028f, .938f, .004f, .028f, .982f, .004f, -.028f, .982f, .004f), new Float3(0, 0, .131f), xToZ, SlotHardware);
                Part(Pelvis, Slab(.050f, .010f, -.050f, .880f, .012f, .050f, .880f, .012f, .050f, 1.000f, .012f, -.050f, 1.000f, .012f), new Float3(-.190f, 0, .040f), xToZ, SlotWebbing);
            }

            // ---- knee pads on the calves.
            if (spec.KneePads)
                foreach (int side in new[] { -1, 1 })
                    Part(side < 0 ? LeftCalf : RightCalf, Slab(.030f, .010f, -.045f, .410f, .020f, .045f, .410f, .020f, .048f, .540f, .022f, -.048f, .540f, .022f),
                        new Float3(side * .10f, 0, .070f), xToZ, SlotHardware);

            var pieces = new List<SoldierGearPiece>();
            foreach (var pair in parts) pieces.Add(new SoldierGearPiece(pair.Key, pair.Value.Build()));
            pieces.Sort((a, b) => a.Joint.CompareTo(b.Joint));
            return pieces;
        }

        // ShapeKit.Loft sweeps sections along +Z; bands stand upright, so turn the sweep axis onto +Y.
        private static Quat zToYInverse() => new Quat(-.70710677f, 0, 0, .70710677f);
    }
}
