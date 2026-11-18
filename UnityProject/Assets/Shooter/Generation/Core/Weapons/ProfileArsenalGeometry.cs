#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>
    /// ADR-106 geometry.weapon 1.3.0: the ADR-102 arsenal archetypes rebuilt from rounded side profiles
    /// (<see cref="ShapeKit.Slab"/>), turned parts (<see cref="ShapeKit.Lathe"/>) and superellipse lofts
    /// (<see cref="ShapeKit.Loft"/>) instead of bevelled boxes. Sockets, collision, envelopes and material slots are the
    /// 1.2.0 ones (<see cref="ArsenalGeometry"/>), so gameplay and hand placement are unchanged.
    /// Weapon space: +Z forward along the bore, +Y up, origin at the trigger-hand grip.
    /// Material slots: 0 coated metal, 1 polymer, 2 machined steel, 3 recess/rubber, 5 oiled wood, 6 polished gold,
    /// 11 clear sight glass (ADR-115, mounted sights only).
    /// </summary>
    internal static class ProfileArsenalGeometry
    {
        private const ushort Metal = 0, Polymer = 1, Steel = 2, Recess = 3, Wood = 5, Gold = 6, Glass = 11;

        public static WeaponBuild Build(string kind, ArsenalParts parts)
        {
            var lods = new MeshData[4];
            for (int i = 0; i < 4; i++) lods[i] = new Builder(kind, parts, i).Main();
            var magazine = new Builder(kind, parts, 0).Magazine();
            var cycle = new Builder(kind, parts, 0).Cycle();
            var bounds = ArsenalGeometry.BoundsFor(kind, parts);
            foreach (var mesh in new[] { lods[0], lods[1], lods[2], lods[3], magazine, cycle })
                foreach (var p in mesh.Positions)
                    if (p.X < bounds.Min.X || p.X > bounds.Max.X || p.Y < bounds.Min.Y || p.Y > bounds.Max.Y || p.Z < bounds.Min.Z || p.Z > bounds.Max.Z)
                        throw new InvalidOperationException("Profile arsenal construction escaped its declared envelope (" + kind + ").");
            return new WeaponBuild(lods, magazine, cycle, ArsenalGeometry.Collision(kind, parts), bounds, ArsenalGeometry.Sockets(kind, parts));
        }

        /// <summary>
        /// ADR-107: LOD0..3 of <paramref name="sight"/> built on <paramref name="kind"/>'s sight rail, in that gun's weapon
        /// space (the mesh parents to the gun at its origin). The scope bodies are the 1.3.0 optic constructions unchanged.
        /// </summary>
        public static MeshData[] BuildSight(string kind, string sight)
        {
            if (!ArsenalGeometry.IsMountKind(kind)) throw new ArgumentException("Kind takes no mounted sight: " + kind, nameof(kind));
            if (Array.IndexOf(ArsenalGeometry.MountedSights, sight) < 0) throw new ArgumentException("Unknown mounted sight: " + sight, nameof(sight));
            if (!ArsenalGeometry.SightHasMesh(kind, sight)) throw new ArgumentException("The " + kind + " irons are part of the body.", nameof(sight));
            var parts = new ArsenalParts(kind, ArsenalParts.BarrelRange(kind).min, "black", "none", "mount", kind == "ak" ? "wood" : "fixed");
            var bounds = ArsenalGeometry.SightBoundsFor(kind);
            var lods = new MeshData[4];
            for (int i = 0; i < 4; i++)
            {
                lods[i] = new Builder(kind, parts, i).Sight(sight);
                foreach (var p in lods[i].Positions)
                    if (p.X < bounds.Min.X || p.X > bounds.Max.X || p.Y < bounds.Min.Y || p.Y > bounds.Max.Y || p.Z < bounds.Min.Z || p.Z > bounds.Max.Z)
                        throw new InvalidOperationException("Mounted sight escaped its declared envelope (" + sight + " on " + kind + ").");
            }
            return lods;
        }

        private static readonly Quat ZToY = new Quat(-.70710677f, 0, 0, .70710677f);
        private static readonly Quat ZToX = new Quat(0, .70710677f, 0, .70710677f);

        private static Quat AboutZ(double degrees)
        { double h = degrees * Math.PI / 360; return new Quat(0, 0, (float)Math.Sin(h), (float)Math.Cos(h)); }

        private sealed class Builder
        {
            private readonly string kind;
            private readonly ArsenalParts p;
            private readonly MeshAssembly a = new MeshAssembly();
            private readonly int arc, edge, radial, loftRadial;
            private readonly ushort metal, furniture, grip;
            private readonly bool detail, fine;

            public Builder(string kind, ArsenalParts parts, int lod)
            {
                this.kind = kind; p = parts;
                arc = new[] { 4, 2, 1, 1 }[lod];
                edge = new[] { 2, 1, 0, 0 }[lod];
                radial = new[] { 32, 16, 10, 6 }[lod];
                loftRadial = new[] { 28, 16, 10, 8 }[lod];
                detail = lod < 2; fine = lod == 0;
                metal = parts.Furniture == "gold" ? Gold : Metal;
                furniture = parts.Furniture == "wood" ? Wood : parts.Furniture == "gold" ? Gold : Polymer;
                // Gold pistols keep dark grip panels so the hand reads against the finish.
                grip = parts.Furniture == "gold" ? Polymer : furniture;
            }

            // ------------------------------------------------------------ primitives

            /// <summary>Side-view outline given as (z, y, fillet) triples, extruded across X and centred on <paramref name="x"/>.</summary>
            private void Slab(ushort material, float x, float width, float roll, params float[] zyr)
            {
                if (zyr.Length % 3 != 0) throw new ArgumentException("Slab corners are (z, y, radius) triples.");
                var corners = new ProfileCorner[zyr.Length / 3];
                for (int i = 0; i < corners.Length; i++) corners[i] = new ProfileCorner(zyr[3 * i], zyr[3 * i + 1], zyr[3 * i + 2]);
                a.Add(ShapeKit.Slab(corners, width, roll, arc, edge), new Float3(x, 0, 0), Quat.Identity, material);
            }

            /// <summary>As <see cref="Slab"/>, with the outline pitched by <paramref name="degrees"/> about a (z, y) pivot.</summary>
            private void SlabPitched(ushort material, float x, float width, float roll, float pivotZ, float pivotY, float degrees, params float[] zyr)
            {
                double t = degrees * Math.PI / 180; double c = Math.Cos(t), s = Math.Sin(t);
                var turned = new float[zyr.Length];
                for (int i = 0; i < zyr.Length; i += 3)
                {
                    double z = zyr[i], y = zyr[i + 1];
                    turned[i] = (float)(pivotZ + y * s + z * c);
                    turned[i + 1] = (float)(pivotY + y * c - z * s);
                    turned[i + 2] = zyr[i + 2];
                }
                Slab(material, x, width, roll, turned);
            }

            private void Plate(ushort material, float x, float width, float z0, float z1, float y0, float y1, float radius)
            {
                // Fillets never take more than a third of the short side, so the rounded rectangle keeps clean flats.
                radius = Math.Min(radius, .3f * Math.Min(z1 - z0, y1 - y0));
                Slab(material, x, width, Math.Min(Math.Min(.0008f, width * .3f), radius * .9f), z0, y0, radius, z1, y0, radius, z1, y1, radius, z0, y1, radius);
            }

            /// <summary>Turned part around the bore-parallel axis through (x, y); (radius, z) pairs from rear to front.</summary>
            private void Turn(ushort material, float x, float y, int segments, params float[] rz)
                => a.Add(ShapeKit.Lathe(Profile(rz), segments), new Float3(x, y, 0), Quat.Identity, material);

            private void TurnAbout(ushort material, Float3 origin, Quat rotation, int segments, params float[] rz)
                => a.Add(ShapeKit.Lathe(Profile(rz), segments), origin, rotation, material);

            private static Float2[] Profile(float[] rz)
            {
                var profile = new Float2[rz.Length / 2];
                for (int i = 0; i < profile.Length; i++) profile[i] = new Float2(rz[2 * i], rz[2 * i + 1]);
                return profile;
            }

            private void Loft(ushort material, params ShapeKit.Section[] sections)
                => a.Add(ShapeKit.Loft(sections, loftRadial), new Float3(0, 0, 0), Quat.Identity, material);

            private static ShapeKit.Section S(float z, float y, float halfWidth, float halfHeight, float squareness = 3.2f)
                => new ShapeKit.Section(z, y, halfWidth, halfHeight, squareness);

            /// <summary>Raised strip running along +Z on a round part, at <paramref name="degrees"/> around the axis through (0, y).</summary>
            private void Strip(ushort material, float y, float radius, float degrees, float z0, float z1, float halfWidth, float halfHeight)
                => a.Add(ShapeKit.Loft(new[] { S(z0, radius, halfWidth, halfHeight, 4), S(z1, radius, halfWidth, halfHeight, 4) }, 8),
                    new Float3(0, y, 0), AboutZ(degrees), material);

            /// <summary>
            /// Moulded pistol grip: a loft down the grip axis with a palm swell at the back and finger ridges at the front,
            /// pitched backwards by <paramref name="pitch"/> degrees about its top centre (y, z).
            /// </summary>
            private void Grip(ushort material, float y, float z, float pitch, float length = .105f, float depth = .042f, float width = .030f, int ridges = 3)
            {
                float h = length / 2, d = depth / 2, w = width / 2;
                int stations = fine ? 26 : detail ? 13 : 6;
                int count = detail ? ridges : 0;
                var sections = new List<ShapeKit.Section>();
                for (int i = 0; i <= stations; i++)
                {
                    double t = (double)i / stations;
                    // Palm swell bulging the back, finger ridges on the lower front, a slight flare at the heel.
                    double swell = .0034 * Math.Exp(-Math.Pow((t - .30) / .17, 2));
                    double ridge = count > 0 && t > .40 && t < .96 ? .0012 * (1 - Math.Cos(2 * Math.PI * count * (t - .40) / .56)) / 2 : 0;
                    double flare = t > .90 ? .0016 * (t - .90) / .10 : 0;
                    double front = .0008 + ridge + flare, back = swell + flare * .5;
                    float halfDepth = (float)(d + (front + back) / 2), centre = (float)((front - back) / 2);
                    float halfWidth = (float)(w * (1 + .05 * swell / .0034) * (t < .04 ? .96 + t : 1));
                    sections.Add(S((float)(-h + length * t), centre, halfWidth, halfDepth, 2.7f));
                }
                double angle = (90 + pitch) * Math.PI / 360;
                a.Add(ShapeKit.Loft(sections, Math.Max(12, loftRadial)), new Float3(0, y, z), new Quat((float)Math.Sin(angle), 0, 0, (float)Math.Cos(angle)), material);
            }

            /// <summary>U-shaped trigger guard strip from the grip (z0) to the receiver (z1).</summary>
            private void Guard(ushort material, float z0, float z1, float bottom, float top, float thickness, float width, float frontRadius = .008f)
            {
                float t = thickness;
                Slab(material, 0, width, Math.Min(.0012f, width * .2f),
                    z0, top, .0012f, z0, bottom, .007f, z1, bottom, frontRadius, z1, top, .0012f,
                    z1 - t, top - .0015f, .0012f, z1 - t, bottom + t, Math.Max(.0015f, frontRadius - t), z0 + t, bottom + t, .0045f, z0 + t, top - .0025f, .0012f);
            }

            private void Trigger(float z, float top)
                => Slab(Steel, 0, .006f, .0012f, z - .006f, top, .0012f, z + .003f, top, .0012f, z + .005f, top - .016f, .004f, z, top - .025f, .003f,
                    z - .002f, top - .021f, .0015f, z - .001f, top - .010f, .003f);

            // ------------------------------------------------------------ shared long-gun parts

            private void Barrel(float y, float rear, float front, float radius, float shoulderZ = float.NaN, float shoulder = 0)
            {
                if (float.IsNaN(shoulderZ) || !detail)
                    Turn(metal, 0, y, radial, radius, rear, radius, front);
                else
                    Turn(metal, 0, y, radial, radius, rear, radius, shoulderZ - .012f, shoulder, shoulderZ - .010f, shoulder, shoulderZ + .010f,
                        radius, shoulderZ + .012f, radius, front);
            }

            private void MuzzleDevice(float y, float front)
            {
                float length = ArsenalGeometry.MuzzleDeviceLength(p.Muzzle);
                float end = front + length;
                switch (p.Muzzle)
                {
                    case "slant":
                        // AK-47 slant brake: turned body, open bore, the angled cut face and a top gas port.
                        Turn(metal, 0, y, radial, .0092f, front - .006f, .0125f, front - .002f, .0130f, front + .004f, .0130f, end - .004f,
                            .0122f, end, .0055f, end, .0055f, end - .016f);
                        if (detail)
                        {
                            SlabPitched(Recess, .0090f, .006f, .0006f, end - .018f, y + .0095f, 30, -.010f, -.003f, .002f, .010f, -.003f, .002f, .010f, .003f, .002f, -.010f, .003f, .002f);
                            Plate(Recess, 0, .006f, front + .022f, front + .036f, y + .0112f, y + .0136f, .0008f);
                        }
                        break;
                    case "flash":
                        Turn(metal, 0, y, radial, .0090f, front - .004f, .0105f, front, .0128f, end - .010f, .0128f, end, .0065f, end, .0065f, end - .012f);
                        if (detail) for (int side = -1; side <= 1; side += 2) Plate(Recess, side * .0118f, .0016f, front + .010f, end - .012f, y - .002f, y + .002f, .0012f);
                        break;
                    case "birdcage":
                        Turn(metal, 0, y, radial, .0094f, front - .003f, .0108f, front, .0108f, end - .004f, .0100f, end, .0055f, end, .0055f, end - .014f);
                        if (detail)
                            for (int i = 0; i < 4; i++)
                                Strip(Recess, y, .0103f, 45 + 90 * i, front + .012f, end - .010f, .0019f, .0011f);
                        break;
                    case "brake":
                        Slab(metal, 0, .038f, .004f, front - .002f, y - .0150f, .004f, end - .010f, y - .0150f, .005f, end - .010f, y + .0150f, .005f, front - .002f, y + .0150f, .004f);
                        Turn(metal, 0, y, radial, .0150f, end - .014f, .0150f, end, .0075f, end, .0075f, end - .010f);
                        if (detail)
                            for (int i = 0; i < 3; i++)
                                for (int side = -1; side <= 1; side += 2)
                                    Plate(Recess, side * .0189f, .0012f, front + .010f + i * .018f, front + .020f + i * .018f, y - .010f, y + .010f, .002f);
                        break;
                    default:
                        Turn(metal, 0, y, radial, .0110f, front - .014f, .0110f, front, .0045f, front, .0045f, front - .006f);
                        break;
                }
            }

            private void Optic(float railY, float railZ) => Optic(p.Optic, railY, railZ);

            private void Optic(string optic, float railY, float railZ)
            {
                switch (optic)
                {
                    case "scope2":
                    {
                        float cy = railY + .035f;
                        // Prism body with a sloped front, objective and ocular housings.
                        Slab(metal, 0, .034f, .0035f, railZ - .055f, railY + .016f, .004f, railZ + .055f, railY + .016f, .004f, railZ + .055f, railY + .040f, .006f,
                            railZ + .040f, railY + .050f, .008f, railZ - .052f, railY + .049f, .008f);
                        Turn(metal, 0, cy, radial, .0190f, railZ + .050f, .0210f, railZ + .056f, .0210f, railZ + .100f, .0182f, railZ + .100f, .0182f, railZ + .096f);
                        Turn(Recess, 0, cy, radial, .0178f, railZ + .0961f, .0178f, railZ + .0969f);
                        Turn(metal, 0, cy, radial, .0150f, railZ - .085f, .0170f, railZ - .080f, .0170f, railZ - .050f);
                        Turn(Recess, 0, cy, radial, .0115f, railZ - .0858f, .0115f, railZ - .0850f);
                        // Mount clamp on the rail.
                        Slab(metal, 0, .030f, .002f, railZ - .040f, railY + .002f, .002f, railZ + .030f, railY + .002f, .002f, railZ + .030f, railY + .018f, .002f, railZ - .040f, railY + .018f, .002f);
                        if (detail)
                        {
                            Slab(metal, 0, .012f, .002f, railZ - .005f, railY + .048f, .002f, railZ + .025f, railY + .048f, .002f, railZ + .022f, railY + .058f, .003f, railZ - .002f, railY + .058f, .003f);
                            Plate(Steel, .0175f, .002f, railZ - .030f, railZ + .010f, railY + .022f, railY + .030f, .002f);
                        }
                        break;
                    }
                    case "scope4":
                        Scope(railY + (kind == "dmr" ? .039f : .036f), railZ - .19f, railZ + .17f, .015f, .023f, .07f, .019f, railY);
                        break;
                    case "scope8":
                        Scope(railY + .055f, railZ - .21f, railZ + .2f, .017f, .029f, .09f, .022f, railY);
                        break;
                }
            }

            private void Scope(float y, float rear, float front, float tube, float bell, float bellLength, float eye, float railY)
            {
                Turn(metal, 0, y, radial,
                    eye * .86f, rear, eye, rear + .004f, eye, rear + .032f, tube * 1.08f, rear + .052f, tube, rear + .058f,
                    tube, front - bellLength, bell * .96f, front - .016f, bell, front - .012f, bell, front,
                    bell * .84f, front, bell * .84f, front - .006f);
                // Lenses sit just proud of their housings so they never share a plane with the metal.
                Turn(Recess, 0, y, radial, bell * .83f, front - .0058f, bell * .83f, front - .0050f);
                Turn(Recess, 0, y, radial, eye * .70f, rear - .0008f, eye * .70f, rear + .0002f);
                float mid = (rear + front) / 2 + .01f;
                if (detail)
                {
                    // Elevation and windage turrets with knurled caps, and a parallax ring.
                    TurnAbout(metal, new Float3(0, y, mid), ZToY, radial, .0105f, tube * .5f, .0105f, tube + .006f, .0095f, tube + .008f, .0095f, tube + .017f, .0085f, tube + .019f);
                    TurnAbout(metal, new Float3(0, y, mid), ZToX, radial, .0105f, tube * .5f, .0105f, tube + .006f, .0095f, tube + .008f, .0095f, tube + .017f, .0085f, tube + .019f);
                    Turn(metal, 0, y, radial, tube * 1.10f, mid - .022f, tube * 1.18f, mid - .016f, tube * 1.18f, mid + .016f, tube * 1.10f, mid + .022f);
                }
                // Rings and their rail bases.
                for (int i = 0; i < 2; i++)
                {
                    float rz = rear + .085f + i * (front - rear - .21f);
                    Turn(Steel, 0, y, radial, tube + .0035f, rz - .008f, tube + .0035f, rz + .008f);
                    Slab(Steel, 0, .022f, .002f, rz - .009f, railY + .001f, .002f, rz + .009f, railY + .001f, .002f, rz + .007f, y - tube + .002f, .003f, rz - .007f, y - tube + .002f, .003f);
                }
            }

            private void RailTeeth(float z0, float z1, float top, float width, float pitch)
            {
                if (!detail) return;
                float step = fine ? pitch : pitch * 2;
                for (float z = z0; z + .0052f <= z1; z += step)
                    Slab(metal, 0, width, .0006f, z, top - .0005f, .0007f, z + .0052f, top - .0005f, .0007f, z + .0052f, top + .0050f, .0009f, z, top + .0050f, .0009f);
            }

            // ------------------------------------------------------------ ADR-107 mounted sights

            public MeshData Sight(string sight)
            {
                var rail = ArsenalGeometry.MountRail(kind);
                if (kind == "ak" && sight != "irons") AkRailAdapter(rail.y);
                switch (sight)
                {
                    case "irons":
                        if (kind == "ar")
                        {
                            // The 1.3.0 AR rear-sight block, now a rail part.
                            Slab(metal, 0, .022f, .002f, -.026f, .111f, .003f, .006f, .111f, .003f, .004f, .136f, .004f, -.024f, .136f, .004f);
                            if (detail) Plate(Recess, 0, .023f, -.015f, -.009f, .128f, .132f, .0015f);
                        }
                        else if (kind == "dmr") BackUpIrons(rail.y, -.05f, .33f);
                        else BackUpIrons(.1115f, -.06f, .15f);
                        break;
                    case "collimator":
                        Collimator(rail.y, rail.z - .02f);
                        break;
                    default:
                        Optic(sight, rail.y, rail.z);
                        break;
                }
                return a.Build();
            }

            /// <summary>Dust-cover rail adapter: a low railed plate clamped on the AK top cover ahead of the rear sight leaf.</summary>
            private void AkRailAdapter(float top)
            {
                Slab(metal, 0, .026f, .002f, -.045f, .1085f, .003f, .165f, .1085f, .003f, .165f, top - .005f, .002f, -.045f, top - .005f, .002f);
                Slab(metal, 0, .020f, .0015f, -.040f, top - .006f, .0016f, .160f, top - .006f, .0016f, .160f, top, .0016f, -.040f, top, .0016f);
                RailTeeth(-.036f, .156f, top - .0005f, .019f, .010f);
                if (detail)
                    for (int side = -1; side <= 1; side += 2)
                        Plate(Steel, side * .0136f, .0016f, .055f, .075f, .1080f, .1150f, .0015f);
            }

            /// <summary>
            /// ADR-115 open reflex collimator: a low box base clamped on the rail carries a thin rounded-rectangle window
            /// frame, leaning back slightly, with one clear glass pane (slot <see cref="Glass"/>) the eye looks through. The
            /// dot (a screen overlay) sits <see cref="ArsenalGeometry.CollimatorEyeHeight"/> above the rail, in the middle of
            /// the pane. Nothing opaque crosses the line of sight.
            /// </summary>
            private void Collimator(float railY, float railZ)
            {
                float cy = railY + ArsenalGeometry.CollimatorEyeHeight;
                float rear = railZ - .026f, front = railZ + .022f, top = railY + .016f;
                // Window: half width/height to the frame tube's centreline, corner radius, tube radius, lean (top rearward).
                const float hw = .0165f, hh = .0125f, corner = .0060f, tube = .0016f, lean = 8f;
                float zc = railZ + .011f;
                double t = lean * Math.PI / 180;
                float cos = (float)Math.Cos(t), sin = (float)Math.Sin(t);
                // Base: clamp and a box housing with a chamfered nose and a sloped rear.
                Slab(metal, 0, .028f, .002f, rear, railY + .001f, .002f, front, railY + .001f, .002f, front, top - .006f, .003f,
                    front - .006f, top, .003f, rear + .008f, top, .003f, rear, top - .004f, .003f);
                // Frame foot: a full-width plinth joining the lower frame rail to the base.
                float footY = cy - hh * cos;
                float footZ = zc + hh * sin;
                Slab(metal, 0, 2 * hw + .002f, .0008f, footZ - .0060f, top - .002f, .0012f, footZ + .0060f, top - .002f, .0012f,
                    footZ + .0040f, footY + .0010f, .0012f, footZ - .0040f, footY + .0010f, .0012f);
                // Frame: a round-section tube swept round the rounded rectangle.
                int k = radial >= 32 ? 6 : radial >= 16 ? 4 : radial >= 10 ? 2 : 1;
                var loop = new List<Float3>();
                void Corner(float cx, float cv, double from)
                {
                    for (int i = 0; i <= k; i++)
                    {
                        double ang = from + (Math.PI / 2) * i / k;
                        float x = cx + corner * (float)Math.Cos(ang), v = cv + corner * (float)Math.Sin(ang);
                        loop.Add(new Float3(x, cy + v * cos, zc - v * sin));
                    }
                }
                Corner(hw - corner, hh - corner, 0);
                Corner(-hw + corner, hh - corner, Math.PI / 2);
                Corner(-hw + corner, -hh + corner, Math.PI);
                Corner(hw - corner, -hh + corner, 1.5 * Math.PI);
                a.Add(ShapeKit.ClosedTube(loop, tube, new Float3(0, 0, 1), Math.Max(6, radial / 3)), new Float3(0, 0, 0), Quat.Identity, metal);
                // Glass pane seated inside the frame tube, in the lean plane; local A = X, B = up, thickness along the bore.
                float gw = hw - tube * .6f, gh = hh - tube * .6f, gr = corner - tube * .6f;
                var pane = ShapeKit.Slab(new[]
                {
                    new ProfileCorner(-gw, -gh, gr), new ProfileCorner(gw, -gh, gr), new ProfileCorner(gw, gh, gr), new ProfileCorner(-gw, gh, gr)
                }, .0010f, 0, Math.Max(2, arc * 2), 0);
                a.Add(pane, new Float3(0, cy, zc), LeanedZToX(-lean), Glass);
                if (detail)
                {
                    // Dot emitter at the rear of the base, aimed at the pane.
                    Slab(metal, 0, .010f, .001f, rear + .005f, top - .001f, .001f, rear + .017f, top - .001f, .001f, rear + .015f, top + .005f, .002f,
                        rear + .007f, top + .005f, .002f);
                    Plate(Recess, 0, .004f, rear + .0155f, rear + .0165f, top + .001f, top + .004f, .0005f);
                    // Brightness buttons on the right, battery cap on the left, clamp cross-bolt.
                    for (int i = 0; i < 2; i++)
                        TurnAbout(Polymer, new Float3(.014f, railY + .010f, rear + .011f + i * .010f), ZToX, Math.Max(8, radial / 2),
                            .0030f, -.0005f, .0030f, .0018f, .0024f, .0024f);
                    TurnAbout(metal, new Float3(-.014f, railY + .010f, rear + .016f), new Quat(0, -.70710677f, 0, .70710677f), Math.Max(10, radial / 2),
                        .0055f, -.0005f, .0055f, .0020f, .0048f, .0028f);
                    Plate(Steel, .0145f, .0020f, front - .012f, front - .004f, railY + .003f, railY + .008f, .0015f);
                }
            }

            /// <summary><see cref="ZToX"/> followed by a pitch of <paramref name="degrees"/> about +X (negative tips the top rearward).</summary>
            private static Quat LeanedZToX(float degrees)
            {
                double h = degrees * Math.PI / 360;
                float x = (float)Math.Sin(h), w = (float)Math.Cos(h), s = .70710677f;
                // (x,0,0,w) * (0,s,0,s)
                return new Quat(x * s, w * s, x * s, w * s);
            }

            /// <summary>Flip-up rear aperture and front post pair on the top rail (DMR and bolt-action irons).</summary>
            private void BackUpIrons(float railTop, float rearZ, float frontZ)
            {
                float line = railTop + .018f;
                // Rear: clamp, upright and an aperture ring.
                Slab(metal, 0, .022f, .002f, rearZ - .012f, railTop, .002f, rearZ + .014f, railTop, .002f, rearZ + .014f, railTop + .006f, .002f, rearZ - .012f, railTop + .006f, .002f);
                Slab(metal, 0, .016f, .0015f, rearZ - .004f, railTop + .005f, .002f, rearZ + .004f, railTop + .005f, .002f, rearZ + .003f, line + .006f, .002f, rearZ - .003f, line + .006f, .002f);
                TurnAbout(Steel, new Float3(0, line, rearZ), Quat.Identity, Math.Max(10, radial / 2), .0050f, -.0025f, .0050f, .0025f);
                // Front: clamp, protective ears and the post.
                Slab(metal, 0, .020f, .002f, frontZ - .012f, railTop, .002f, frontZ + .012f, railTop, .002f, frontZ + .012f, railTop + .006f, .002f, frontZ - .012f, railTop + .006f, .002f);
                for (int side = -1; side <= 1; side += 2)
                    Slab(metal, side * .0060f, .0025f, .0006f, frontZ - .005f, railTop + .005f, .0015f, frontZ + .005f, railTop + .005f, .0015f, frontZ + .004f, line + .004f, .0015f, frontZ - .004f, line + .004f, .0015f);
                Slab(Steel, 0, .0022f, .0005f, frontZ - .0015f, railTop + .005f, .0006f, frontZ + .0015f, railTop + .005f, .0006f, frontZ + .0010f, line, .0006f, frontZ - .0010f, line, .0006f);
            }

            // ------------------------------------------------------------ dispatch

            public MeshData Main()
            {
                switch (kind)
                {
                    case "ak": Ak(); break;
                    case "ar": Ar(); break;
                    case "dmr": Dmr(); break;
                    case "bolt": Bolt(); break;
                    case "pistol-striker": Striker(); break;
                    default: Eagle(); break;
                }
                return a.Build();
            }

            // ------------------------------------------------------------ Kalashnikov family

            private void Ak()
            {
                float y = .080f, front = p.BarrelFront, hg = ArsenalGeometry.AkHandguardFront(p);
                float gasZ = Math.Max(hg + .03f, Math.Min(.385f, front - .07f));
                float sightZ = Math.Max(gasZ + .03f, front - .05f);
                // Stamped receiver: curved top cover, magazine well, trigger housing.
                Slab(metal, 0, .046f, .0028f,
                    -.072f, .040f, .003f, -.072f, .099f, .004f, -.050f, .111f, .012f, .168f, .111f, .014f, .200f, .103f, .004f, .226f, .101f, .003f,
                    .226f, .045f, .003f, .135f, .044f, .003f, .068f, .044f, .003f, .066f, .016f, .004f, -.052f, .016f, .005f, -.072f, .030f, .004f);
                if (detail)
                {
                    // Top-cover stiffening rib, ejection port, safety lever and magazine-well dimple.
                    Slab(metal, 0, .026f, .0016f, -.040f, .108f, .002f, .150f, .108f, .002f, .150f, .1135f, .003f, -.040f, .1135f, .003f);
                    Plate(Recess, .0232f, .0012f, .100f, .170f, .076f, .092f, .003f);
                    Slab(Steel, .0245f, .002f, .0006f, .025f, .086f, .002f, .150f, .081f, .002f, .150f, .088f, .002f, .040f, .094f, .003f);
                    Plate(Recess, -.0232f, .0012f, -.010f, .070f, .055f, .080f, .006f);
                    for (int side = -1; side <= 1; side += 2)
                        foreach (float rz in new[] { -.050f, .040f, .200f })
                            TurnAbout(Steel, new Float3(side * .0215f, .060f, rz), side > 0 ? ZToX : new Quat(0, -.70710677f, 0, .70710677f), 10, .0030f, .0005f, .0030f, .0030f);
                }
                // Rear sight block and leaf.
                Slab(metal, 0, .034f, .0025f, .180f, .099f, .003f, .232f, .099f, .003f, .232f, .114f, .004f, .190f, .118f, .004f);
                if (detail) Slab(Steel, 0, .022f, .0008f, .186f, .116f, .001f, .226f, .116f, .001f, .226f, .1215f, .0012f, .192f, .1205f, .0012f);
                // Two-piece handguard and the gas tube cover above the barrel.
                Loft(furniture, S(.226f, .066f, .0255f, .0185f), S(.238f, .066f, .0275f, .0205f), S((.226f + hg) / 2, .0665f, .0282f, .0215f),
                    S(hg - .015f, .067f, .0268f, .0200f), S(hg, .068f, .0240f, .0170f));
                Loft(furniture, S(.234f, .1035f, .0170f, .0100f, 3), S(.246f, .1045f, .0190f, .0115f, 3), S((.234f + hg) / 2, .105f, .0192f, .0118f, 3),
                    S(hg - .012f, .1045f, .0180f, .0110f, 3), S(hg - .004f, .1035f, .0158f, .0090f, 3));
                if (detail)
                    for (int side = -1; side <= 1; side += 2)
                        Plate(Recess, side * .0280f, .0012f, .246f, hg - .022f, .061f, .066f, .002f);
                Loft(metal, S(hg - .001f, .072f, .0252f, .0258f, 3), S(hg + .008f, .072f, .0240f, .0248f, 3));
                Turn(metal, 0, .103f, Math.Max(8, radial / 2), .0098f, hg - .012f, .0098f, gasZ - .010f);
                Slab(metal, 0, .024f, .003f, gasZ - .016f, .069f, .004f, gasZ + .016f, .069f, .004f, gasZ + .016f, .095f, .004f, gasZ + .008f, .114f, .006f, gasZ - .016f, .114f, .006f);
                Barrel(y, .222f, front, .0085f, sightZ, .0098f);
                // Front sight base, protective ears and post.
                Slab(metal, 0, .022f, .0025f, sightZ - .014f, .071f, .003f, sightZ + .014f, .071f, .003f, sightZ + .014f, .090f, .004f, sightZ + .006f, .096f, .003f,
                    sightZ - .010f, .096f, .003f, sightZ - .014f, .090f, .004f);
                if (detail)
                    for (int side = -1; side <= 1; side += 2)
                        Slab(metal, side * .0080f, .003f, .0008f, sightZ - .007f, .093f, .002f, sightZ + .007f, .093f, .002f, sightZ + .005f, .123f, .002f, sightZ - .005f, .123f, .002f);
                Slab(Steel, 0, .003f, .0006f, sightZ - .0020f, .093f, .0007f, sightZ + .0020f, .093f, .0007f, sightZ + .0012f, .117f, .0007f, sightZ - .0012f, .117f, .0007f);
                MuzzleDevice(y, front);
                Grip(p.Furniture == "wood" ? Wood : Polymer, -.022f, -.030f, 18);
                Guard(metal, -.002f, .078f, -.012f, .0165f, .0040f, .012f);
                Trigger(.040f, .017f);
                if (detail) Slab(metal, 0, .040f, .0015f, .126f, .044f, .002f, .140f, .044f, .002f, .140f, .032f, .003f, .130f, .027f, .003f);
                if (p.Stock == "folding")
                {
                    Loft(furniture, S(-.405f, .040f, .0200f, .0580f, 3), S(-.392f, .041f, .0210f, .0580f, 3), S(-.300f, .052f, .0190f, .0420f, 3),
                        S(-.160f, .062f, .0170f, .0300f, 3), S(-.071f, .066f, .0180f, .0260f, 3));
                    Loft(Recess, S(-.413f, .040f, .0212f, .0592f, 3), S(-.404f, .040f, .0208f, .0588f, 3));
                    Slab(metal, 0, .032f, .003f, -.086f, .050f, .004f, -.066f, .050f, .004f, -.066f, .088f, .004f, -.086f, .088f, .004f);
                    if (detail) for (int side = -1; side <= 1; side += 2) Plate(Recess, side * .0185f, .0012f, -.330f, -.180f, .040f, .058f, .006f);
                }
                else
                {
                    Loft(furniture, S(-.405f, .025f, .0220f, .0660f), S(-.385f, .027f, .0230f, .0650f), S(-.260f, .047f, .0215f, .0470f),
                        S(-.150f, .062f, .0195f, .0310f), S(-.071f, .068f, .0200f, .0280f));
                    Loft(metal, S(-.414f, .025f, .0225f, .0665f), S(-.4055f, .025f, .0228f, .0670f));
                }
            }

            // ------------------------------------------------------------ AR / M16 family

            private void Ar()
            {
                float y = .080f, front = p.BarrelFront, sightZ = front - .13f;
                // Upper receiver with brass deflector/forward-assist housing.
                Slab(metal, 0, .048f, .0025f, -.062f, .066f, .003f, -.062f, .104f, .003f, .210f, .104f, .003f, .210f, .066f, .003f);
                // Lower receiver: magazine well flare, trigger area and buffer-tube boss.
                Slab(metal, 0, .046f, .0025f, -.062f, .067f, .003f, .165f, .067f, .003f, .165f, .045f, .004f, .122f, .003f, .004f, .038f, .003f, .004f,
                    .030f, .016f, .004f, -.012f, .016f, .005f, -.040f, .034f, .006f, -.062f, .044f, .004f);
                // Flat-top rail.
                Slab(metal, 0, .022f, .0015f, -.062f, .104f, .0016f, .210f, .104f, .0016f, .210f, .112f, .0016f, -.062f, .112f, .0016f);
                RailTeeth(-.058f, .206f, .112f, .021f, .010f);
                if (detail)
                {
                    Slab(metal, .0215f, .012f, .002f, -.005f, .078f, .003f, .045f, .078f, .003f, .045f, .099f, .004f, .010f, .104f, .003f, -.005f, .097f, .004f);
                    Slab(metal, 0, .050f, .0015f, .036f, -.003f, .002f, .124f, -.003f, .002f, .124f, .010f, .002f, .036f, .010f, .002f);
                    Plate(Recess, .0245f, .0012f, .040f, .105f, .072f, .092f, .002f);
                    TurnAbout(Steel, new Float3(.024f, .093f, -.028f), new Quat(0, 0, 0, 1), radial, .0055f, -.012f, .0070f, -.008f, .0070f, .014f);
                    Plate(Steel, -.0240f, .0030f, .020f, .040f, .046f, .058f, .002f);
                    TurnAbout(Steel, new Float3(.0235f, .040f, .118f), ZToX, 12, .0050f, 0, .0050f, .0030f);
                    for (int side = -1; side <= 1; side += 2)
                        foreach (float rz in new[] { -.045f, .140f })
                            TurnAbout(Steel, new Float3(side * .0225f, .058f, rz), side > 0 ? ZToX : new Quat(0, -.70710677f, 0, .70710677f), 10, .0032f, .0005f, .0032f, .0028f);
                }
                // A2 round handguard with lengthwise ribs, delta ring and cap.
                Loft(furniture, S(.215f, y, .0290f, .0290f, 2), S(.224f, y, .0312f, .0312f, 2), S(.360f, y, .0308f, .0308f, 2), S(.488f, y, .0298f, .0298f, 2), S(.499f, y, .0284f, .0284f, 2));
                if (detail)
                    foreach (float angle in fine ? new[] { 20f, 50f, 130f, 160f, 200f, 230f, 310f, 340f } : new[] { 35f, 145f, 215f, 325f })
                        Strip(furniture, y, .0303f, angle, .232f, .480f, .0026f, .0024f);
                Turn(metal, 0, y, radial, .0240f, .200f, .0340f, .205f, .0340f, .216f, .0280f, .221f);
                Turn(metal, 0, y, radial, .0290f, .497f, .0290f, .506f, .0150f, .512f, .0120f, .512f);
                Barrel(y, .205f, front, .0090f, sightZ, .0108f);
                // A2 front sight base, tower, ears, post and bayonet lug.
                Slab(metal, 0, .028f, .003f, sightZ - .017f, .067f, .003f, sightZ + .017f, .067f, .003f, sightZ + .017f, .095f, .004f, sightZ + .008f, .100f, .003f,
                    sightZ - .008f, .100f, .003f, sightZ - .017f, .095f, .004f);
                Slab(metal, 0, .014f, .0025f, sightZ - .011f, .096f, .003f, sightZ + .011f, .096f, .003f, sightZ + .006f, .128f, .003f, sightZ - .006f, .128f, .003f);
                if (detail)
                {
                    for (int side = -1; side <= 1; side += 2)
                        Slab(metal, side * .0055f, .003f, .0008f, sightZ - .006f, .124f, .0015f, sightZ + .006f, .124f, .0015f, sightZ + .005f, .140f, .0015f, sightZ - .005f, .140f, .0015f);
                    Slab(metal, 0, .010f, .0015f, sightZ - .005f, .055f, .002f, sightZ + .016f, .055f, .002f, sightZ + .016f, .068f, .002f, sightZ - .012f, .068f, .002f);
                }
                Slab(Steel, 0, .0025f, .0005f, sightZ - .0016f, .126f, .0006f, sightZ + .0016f, .126f, .0006f, sightZ + .0011f, .141f, .0006f, sightZ - .0011f, .141f, .0006f);
                MuzzleDevice(y, front);
                Grip(Polymer, -.022f, -.022f, 18);
                Guard(metal, .004f, .084f, -.008f, .0165f, .0045f, .012f, .006f);
                Trigger(.045f, .017f);
                // A2 fixed stock and rubber buttplate.
                Loft(Polymer, S(-.426f, .060f, .0250f, .0710f, 3.4f), S(-.410f, .061f, .0260f, .0700f, 3.4f), S(-.300f, .065f, .0230f, .0560f, 3.4f),
                    S(-.170f, .071f, .0205f, .0390f, 3.4f), S(-.061f, .074f, .0180f, .0290f, 3.4f));
                Loft(Recess, S(-.4365f, .060f, .0250f, .0715f, 3.4f), S(-.4255f, .060f, .0254f, .0712f, 3.4f));
                if (p.Optic == "irons")
                {
                    Slab(metal, 0, .022f, .002f, -.026f, .111f, .003f, .006f, .111f, .003f, .004f, .136f, .004f, -.024f, .136f, .004f);
                    if (detail) Plate(Recess, 0, .023f, -.015f, -.009f, .128f, .132f, .0015f);
                }
                else Optic(.118f, .075f);
            }

            // ------------------------------------------------------------ M14 EBR chassis

            private void Dmr()
            {
                float y = .080f, front = p.BarrelFront;
                Slab(metal, 0, .056f, .003f, -.080f, .032f, .004f, .040f, .032f, .004f, .060f, .024f, .004f, .120f, .024f, .004f, .140f, .032f, .004f,
                    .380f, .034f, .008f, .380f, .078f, .006f, -.080f, .078f, .004f);
                Slab(metal, 0, .026f, .0015f, -.070f, .112f, .002f, .350f, .112f, .002f, .350f, .124f, .002f, -.070f, .124f, .002f);
                RailTeeth(-.066f, .346f, .124f, .025f, .012f);
                Slab(Steel, 0, .040f, .0025f, -.070f, .074f, .003f, .150f, .074f, .003f, .150f, .106f, .004f, .060f, .107f, .004f, .040f, .103f, .003f, -.070f, .103f, .004f);
                Loft(metal, S(.250f, .085f, .0255f, .0225f, 6), S(.262f, .085f, .0270f, .0240f, 6), S(.378f, .085f, .0270f, .0240f, 6), S(.390f, .085f, .0250f, .0220f, 6));
                if (detail)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Slab(metal, side * .0300f, .006f, .001f, .262f, .074f, .0015f, .372f, .074f, .0015f, .372f, .086f, .0015f, .262f, .086f, .0015f);
                        for (int i = 0; i < (fine ? 5 : 2); i++) Plate(Recess, side * .0283f, .0012f, .010f + i * .060f, .040f + i * .060f, .046f, .064f, .004f);
                    }
                }
                Turn(metal, 0, y, radial, .0095f, .375f, .0095f, front);
                MuzzleDevice(y, front);
                Grip(furniture, -.012f, -.020f, 18);
                Guard(metal, .010f, .088f, -.004f, .0330f, .0045f, .012f, .006f);
                Trigger(.048f, .034f);
                Turn(metal, 0, .070f, radial, .0160f, -.280f, .0160f, -.086f, .0190f, -.083f, .0190f, -.078f);
                Slab(furniture, 0, .040f, .008f, -.410f, .000f, .010f, -.282f, .018f, .010f, -.282f, .098f, .010f, -.410f, .118f, .010f);
                Slab(furniture, 0, .036f, .006f, -.370f, .098f, .007f, -.252f, .098f, .007f, -.252f, .114f, .008f, -.370f, .119f, .008f);
                Slab(Recess, 0, .046f, .0035f, -.421f, -.003f, .0045f, -.409f, -.003f, .0045f, -.409f, .121f, .0045f, -.421f, .121f, .0045f);
                Optic(.124f, .14f);
            }

            // ------------------------------------------------------------ Heavy bolt-action

            private void Bolt()
            {
                float y = .085f, front = p.BarrelFront;
                // One-piece stock: forend, action bed, pistol grip and a cheek-riser butt.
                Loft(furniture, S(.050f, .060f, .0255f, .0310f, 3), S(.100f, .060f, .0300f, .0350f, 3), S(.420f, .060f, .0280f, .0300f, 3), S(.460f, .060f, .0235f, .0245f, 3));
                Slab(furniture, 0, .050f, .008f, -.100f, .028f, .010f, .070f, .028f, .010f, .070f, .082f, .008f, -.100f, .082f, .008f);
                Grip(furniture, -.035f, -.030f, 22, .100f, .045f, .032f);
                Slab(furniture, 0, .050f, .012f, -.090f, .080f, .012f, -.230f, .074f, .020f, -.255f, .122f, .012f, -.380f, .122f, .012f, -.458f, .100f, .014f,
                    -.458f, -.072f, .013f, -.430f, -.078f, .013f, -.300f, -.020f, .020f, -.170f, .024f, .020f, -.090f, .026f, .012f);
                Slab(Recess, 0, .052f, .0045f, -.474f, -.074f, .0055f, -.458f, -.074f, .0055f, -.458f, .100f, .0055f, -.474f, .100f, .0055f);
                if (p.Stock == "folding") Slab(metal, 0, .054f, .003f, -.112f, .040f, .004f, -.090f, .040f, .004f, -.090f, .086f, .004f, -.112f, .086f, .004f);
                // Round action, scope base, heavy barrel with flutes.
                Turn(Steel, 0, y, radial, .0175f, -.080f, .0190f, -.074f, .0190f, .130f, .0175f, .140f);
                Slab(metal, 0, .022f, .0015f, -.080f, .1035f, .002f, .160f, .1035f, .002f, .160f, .1115f, .002f, -.080f, .1115f, .002f);
                Turn(metal, 0, y, radial, .0150f, .136f, .0150f, .180f, .0125f, .200f, .0125f, front - .010f, .0115f, front);
                if (detail)
                {
                    int flutes = fine ? 6 : 3;
                    for (int i = 0; i < flutes; i++) Strip(Recess, y, .0124f, 360f * i / flutes + 30, .220f, front - .060f, .0016f, .0006f);
                }
                Guard(metal, -.004f, .072f, -.006f, .0285f, .0045f, .012f, .006f);
                Trigger(.032f, .029f);
                MuzzleDevice(y, front);
                // Folded bipod under the forend.
                Slab(metal, 0, .030f, .003f, .395f, .012f, .003f, .425f, .012f, .003f, .425f, .032f, .003f, .395f, .032f, .003f);
                if (detail)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Turn(metal, side * .012f, .014f, Math.Max(8, radial / 2), .0055f, .220f, .0055f, .400f);
                        Turn(Recess, side * .012f, .014f, Math.Max(8, radial / 2), .0040f, .212f, .0068f, .216f, .0068f, .222f);
                    }
                Optic(.107f, .04f);
            }

            // ------------------------------------------------------------ Pistols

            private void Striker()
            {
                float y = .070f, front = p.BarrelFront;
                // Polymer frame with dust cover, accessory rail and beavertail.
                Slab(Polymer, 0, .028f, .003f, -.064f, .031f, .006f, -.071f, .049f, .005f, -.062f, .0552f, .003f, .118f, .0552f, .003f, .118f, .034f, .005f,
                    .070f, .031f, .004f, -.012f, .031f, .006f);
                Turn(metal, 0, y, radial, .0072f, .080f, .0072f, front - .012f);
                Turn(metal, 0, y, radial, .0080f, front - .012f, .0080f, front, .0045f, front, .0045f, front - .006f);
                Grip(Polymer, -.026f, -.032f, 18, .124f, .048f, .028f);
                Guard(Polymer, .004f, .070f, -.008f, .0310f, .0050f, .012f, .004f);
                Trigger(.034f, .031f);
                if (detail)
                    for (int i = 0; i < 3; i++) Plate(Recess, 0, .0285f, .072f + i * .012f, .076f + i * .012f, .0285f, .0315f, .0008f);
                SlabPitched(Polymer, 0, .030f, .003f, -.048f, -.083f, 18, -.020f, -.006f, .004f, .020f, -.006f, .004f, .020f, .006f, .003f, -.020f, .006f, .003f);
            }

            private void Eagle()
            {
                float y = .090f, front = p.BarrelFront;
                Slab(metal, 0, .032f, .003f, -.060f, .032f, .006f, -.058f, .074f, .004f, .140f, .074f, .004f, .140f, .046f, .006f, .110f, .030f, .006f);
                // Fixed triangular barrel block with a top rail and flutes.
                Slab(metal, 0, .030f, .004f, .060f, .071f, .004f, front, .071f, .004f, front, .108f, .005f, .060f, .116f, .005f);
                Slab(metal, 0, .018f, .0015f, .065f, .114f, .002f, front - .006f, .114f, .002f, front - .006f, .124f, .002f, .065f, .124f, .002f);
                Turn(metal, 0, y, radial, .0105f, front - .010f, .0105f, front + .001f, .0062f, front + .001f, .0062f, front - .005f);
                Grip(grip, -.036f, -.042f, 16, .134f, .055f, .032f);
                Guard(metal, .008f, .084f, -.010f, .0330f, .0050f, .014f, .006f);
                Trigger(.042f, .033f);
                if (detail)
                {
                    SlabPitched(Steel, 0, .010f, .0015f, -.078f, .088f, -20, -.006f, -.010f, .002f, .006f, -.010f, .002f, .005f, .010f, .003f, -.005f, .010f, .003f);
                    for (int i = 0; i < (fine ? 6 : 3); i++) Plate(Recess, 0, .0185f, .090f + i * .016f, .093f + i * .016f, .1225f, .1250f, .0006f);
                    for (int side = -1; side <= 1; side += 2) Plate(Recess, side * .0152f, .0012f, front - .045f, front - .012f, .088f, .102f, .004f);
                }
                Slab(metal, 0, .006f, .001f, front - .016f, .114f, .0012f, front - .006f, .114f, .0012f, front - .007f, .128f, .0015f, front - .015f, .127f, .0015f);
                SlabPitched(metal, 0, .034f, .003f, -.065f, -.097f, 16, -.025f, -.006f, .004f, .025f, -.006f, .004f, .025f, .006f, .003f, -.025f, .006f, .003f);
            }

            // ------------------------------------------------------------ Magazines

            public MeshData Magazine()
            {
                switch (kind)
                {
                    case "ak":
                        CurvedMagazine(p.Furniture == "polymer" ? Polymer : Metal, .045f, .100f, .046f, 5, 6, 6, .031f, .028f, -30);
                        break;
                    case "ar":
                        CurvedMagazine(Metal, .040f, .075f, .060f, 3, 4, 4, .032f, .026f, -12, Polymer);
                        break;
                    case "dmr":
                        SlabPitched(Metal, 0, .032f, .003f, .075f, -.055f, -4, -.0375f, -.065f, .004f, .0375f, -.065f, .004f, .0375f, .065f, .003f, -.0375f, .065f, .003f);
                        SlabPitched(Steel, 0, .036f, .003f, .080f, -.122f, -4, -.0395f, -.005f, .003f, .0395f, -.005f, .003f, .0395f, .005f, .003f, -.0395f, .005f, .003f);
                        Plate(Recess, -.0165f, .0012f, .055f, .095f, -.095f, -.010f, .004f);
                        break;
                    case "bolt":
                        Slab(Metal, 0, .032f, .003f, -.0225f, -.030f, .004f, .0625f, -.030f, .004f, .0625f, .040f, .003f, -.0225f, .040f, .003f);
                        Slab(Steel, 0, .036f, .003f, -.0245f, -.036f, .003f, .0645f, -.036f, .003f, .0645f, -.028f, .003f, -.0245f, -.028f, .003f);
                        break;
                    case "pistol-striker":
                        SlabPitched(Metal, 0, .021f, .003f, -.036f, -.040f, 18, -.0145f, -.045f, .004f, .0145f, -.045f, .004f, .0145f, .045f, .003f, -.0145f, .045f, .003f);
                        SlabPitched(Polymer, 0, .030f, .003f, -.048f, -.083f, 18, -.020f, -.006f, .004f, .020f, -.006f, .004f, .020f, .006f, .003f, -.020f, .006f, .003f);
                        break;
                    default:
                        SlabPitched(Metal, 0, .024f, .003f, -.046f, -.050f, 16, -.018f, -.050f, .004f, .018f, -.050f, .004f, .018f, .050f, .003f, -.018f, .050f, .003f);
                        SlabPitched(metal, 0, .034f, .003f, -.065f, -.097f, 16, -.025f, -.006f, .004f, .025f, -.006f, .004f, .025f, .006f, .003f, -.025f, .006f, .003f);
                        break;
                }
                return a.Build();
            }

            /// <summary>
            /// Curved box magazine following the 1.2.0 segment walk (each segment pitched a further <paramref name="stepDegrees"/>),
            /// as one smooth extruded outline with a raised side spine and a floorplate.
            /// </summary>
            private void CurvedMagazine(ushort material, float topY, float topZ, float segment, int count, float firstDegrees, float stepDegrees,
                float halfDepth, float width, float floorDegrees, ushort? floorMaterial = null)
            {
                var cy = new float[count + 1]; var cz = new float[count + 1]; var ny = new float[count + 1]; var nz = new float[count + 1];
                cy[0] = topY; cz[0] = topZ;
                for (int i = 0; i < count; i++)
                {
                    double rad = -(firstDegrees + stepDegrees * i) * Math.PI / 180;
                    float dy = -(float)Math.Cos(rad), dz = -(float)Math.Sin(rad);
                    cy[i + 1] = cy[i] + dy * segment; cz[i + 1] = cz[i] + dz * segment;
                }
                for (int i = 0; i <= count; i++)
                {
                    int i0 = Math.Max(0, i - 1), i1 = Math.Min(count, i + 1);
                    float dz = cz[i1] - cz[i0], dy = cy[i1] - cy[i0]; float l = (float)Math.Sqrt(dz * dz + dy * dy);
                    // Forward-pointing perpendicular of the downward centreline.
                    nz[i] = -dy / l; ny[i] = dz / l;
                    if (i == 0) { nz[i] = 1; ny[i] = 0; }
                }
                var outline = new List<float>();
                for (int i = 0; i <= count; i++) { outline.Add(cz[i] + nz[i] * halfDepth); outline.Add(cy[i] + ny[i] * halfDepth); outline.Add(i == 0 ? .002f : i == count ? .006f : .030f); }
                for (int i = count; i >= 0; i--) { outline.Add(cz[i] - nz[i] * halfDepth); outline.Add(cy[i] - ny[i] * halfDepth); outline.Add(i == 0 ? .002f : i == count ? .006f : .030f); }
                Slab(material, 0, width, .003f, outline.ToArray());
                if (count >= 3)
                {
                    var spine = new List<float>();
                    for (int i = 1; i < count; i++) { spine.Add(cz[i] + nz[i] * .006f); spine.Add(cy[i] + ny[i] * .006f); spine.Add(.004f); }
                    for (int i = count - 1; i >= 1; i--) { spine.Add(cz[i] - nz[i] * .006f); spine.Add(cy[i] - ny[i] * .006f); spine.Add(.004f); }
                    Slab(material, 0, width + .0018f, .0008f, spine.ToArray());
                }
                float by = cy[count], bz = cz[count];
                SlabPitched(floorMaterial ?? material, 0, width + .004f, .003f, bz + .002f, by - .004f, floorDegrees,
                    -halfDepth - .002f, -.005f, .004f, halfDepth + .002f, -.005f, .004f, halfDepth + .002f, .005f, .003f, -halfDepth - .002f, .005f, .003f);
            }

            // ------------------------------------------------------------ Bolt carrier, bolt handle or slide

            public MeshData Cycle()
            {
                switch (kind)
                {
                    case "ak":
                        Slab(Steel, .026f, .008f, .0015f, .110f, .080f, .002f, .210f, .080f, .002f, .210f, .096f, .003f, .110f, .096f, .003f);
                        TurnAbout(Steel, new Float3(0, .088f, .205f), ZToX, radial, .0042f, .026f, .0042f, .048f, .0072f, .051f, .0076f, .060f, .0060f, .064f);
                        break;
                    case "ar":
                        Slab(Steel, 0, .012f, .0015f, -.090f, .101f, .002f, -.040f, .101f, .002f, -.040f, .109f, .002f, -.090f, .109f, .002f);
                        Slab(Steel, 0, .050f, .002f, -.098f, .101f, .003f, -.086f, .101f, .003f, -.086f, .109f, .003f, -.098f, .109f, .003f);
                        Slab(Steel, .022f, .006f, .0015f, .040f, .075f, .002f, .100f, .075f, .002f, .100f, .091f, .002f, .040f, .091f, .002f);
                        break;
                    case "dmr":
                        Slab(Steel, .032f, .010f, .0018f, .060f, .068f, .002f, .220f, .068f, .002f, .220f, .082f, .002f, .060f, .082f, .002f);
                        TurnAbout(Steel, new Float3(0, .075f, .215f), ZToX, radial, .0070f, .033f, .0070f, .046f, .0055f, .048f);
                        break;
                    case "bolt":
                        Turn(Steel, 0, .085f, radial, .0090f, -.162f, .0105f, -.156f, .0105f, -.060f);
                        WeaponProfiles.Rod(a, new Float3(.012f, .085f, -.080f), new Float3(.046f, .070f, -.080f), .0045f, Steel);
                        TurnAbout(Steel, new Float3(.052f, .067f, -.081f), ZToX, radial, .0060f, -.008f, .0095f, -.004f, .0095f, .004f, .0070f, .008f);
                        break;
                    case "pistol-striker":
                        Slab(metal, 0, .026f, .0035f, -.065f, .0545f, .003f, .124f, .0545f, .003f, .124f, .080f, .006f, .112f, .086f, .005f, -.061f, .086f, .005f, -.065f, .081f, .004f);
                        Slab(Steel, 0, .020f, .0015f, -.060f, .085f, .002f, -.050f, .085f, .002f, -.050f, .094f, .002f, -.058f, .094f, .002f);
                        Slab(Steel, 0, .005f, .0010f, .110f, .085f, .0012f, .118f, .085f, .0012f, .117f, .092f, .0012f, .111f, .092f, .0012f);
                        Plate(Recess, .0132f, .0012f, .035f, .065f, .067f, .081f, .002f);
                        for (int i = 0; i < 5; i++)
                            for (int side = -1; side <= 1; side += 2)
                                Plate(Recess, side * .0132f, .0010f, -.056f + i * .006f, -.0535f + i * .006f, .060f, .082f, .0005f);
                        break;
                    default:
                        Slab(metal, 0, .034f, .004f, -.070f, .069f, .004f, .070f, .069f, .004f, .070f, .107f, .006f, .060f, .111f, .005f, -.064f, .111f, .005f, -.070f, .104f, .004f);
                        Slab(Steel, 0, .024f, .0015f, -.066f, .109f, .002f, -.054f, .109f, .002f, -.054f, .120f, .002f, -.064f, .120f, .002f);
                        for (int i = 0; i < 6; i++)
                            for (int side = -1; side <= 1; side += 2)
                                Plate(Recess, side * .0172f, .0010f, -.062f + i * .007f, -.0595f + i * .007f, .075f, .105f, .0005f);
                        break;
                }
                return a.Build();
            }
        }
    }
}
