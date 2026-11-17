#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>
    /// ADR-102 geometry.weapon 1.2.0: rule-built archetypes modelled on real firearm families (Kalashnikov, AR-15/M16,
    /// M14 EBR, heavy bolt-action, striker and gas-operated .50 pistols). Every dimension is a numeric constant or a recipe
    /// part; there are no authored vertices. Weapon space: +Z forward along the bore, +Y up, origin at the trigger-hand grip.
    /// Material slots: 0 coated metal, 1 polymer, 2 machined steel, 3 recess/rubber, 5 oiled wood, 6 polished gold.
    /// </summary>
    internal static class ArsenalGeometry
    {
        private const ushort Metal = 0, Polymer = 1, Steel = 2, Recess = 3, Wood = 5, Gold = 6;

        public static WeaponBuild Build(string kind, ArsenalParts parts)
        {
            var lods = new MeshData[4];
            for (int i = 0; i < 4; i++) lods[i] = new Builder(kind, parts, i).Main();
            var magazine = new Builder(kind, parts, 0).Magazine();
            var cycle = new Builder(kind, parts, 0).Cycle();
            var bounds = BoundsFor(kind, parts);
            foreach (var mesh in new[] { lods[0], lods[1], lods[2], lods[3], magazine, cycle })
                foreach (var p in mesh.Positions)
                    if (p.X < bounds.Min.X || p.X > bounds.Max.X || p.Y < bounds.Min.Y || p.Y > bounds.Max.Y || p.Z < bounds.Min.Z || p.Z > bounds.Max.Z)
                        throw new InvalidOperationException("Arsenal construction escaped its declared envelope (" + kind + ").");
            return new WeaponBuild(lods, magazine, cycle, Collision(kind, parts), bounds, Sockets(kind, parts));
        }

        public static float MuzzleDeviceLength(string muzzle) => muzzle switch
        {
            "slant" => .070f,
            "flash" => .045f,
            "birdcage" => .050f,
            "brake" => .080f,
            _ => 0f
        };

        public static Aabb BoundsFor(string kind, ArsenalParts parts)
        {
            float front = parts.BarrelFront + MuzzleDeviceLength(parts.Muzzle) + .012f;
            return kind switch
            {
                "ak" => new Aabb(new Float3(-.075f, -.215f, -.43f), new Float3(.075f, .14f, front)),
                "ar" => new Aabb(new Float3(-.075f, -.21f, -.45f), new Float3(.075f, .20f, front)),
                "dmr" => new Aabb(new Float3(-.075f, -.21f, -.44f), new Float3(.075f, .21f, front)),
                "bolt" => new Aabb(new Float3(-.08f, -.17f, -.49f), new Float3(.08f, .215f, front)),
                "pistol-striker" => new Aabb(new Float3(-.04f, -.105f, -.085f), new Float3(.04f, .105f, parts.BarrelFront / 1f + .012f)),
                _ => new Aabb(new Float3(-.046f, -.125f, -.095f), new Float3(.046f, .145f, parts.BarrelFront + .012f))
            };
        }

        private static int Mm(float meters) => (int)Math.Round(meters * 1000f);

        /// <summary>Rail height and rail centre (y, z) each long-gun archetype mounts its optic on.</summary>
        private static (float y, float z) Rail(string kind) => kind switch
        {
            "ar" => (.118f, .075f),
            "dmr" => (.124f, .14f),
            _ => (.107f, .04f)
        };

        /// <summary>Line of sight through the issued optic or iron sights (y, z) in metres.</summary>
        private static (float y, float z) SightEye(string kind, ArsenalParts parts)
        {
            var rail = Rail(kind);
            if (parts.Optic == "scope2") return (rail.y + .035f, rail.z - .085f);
            if (parts.Optic == "scope4") return (rail.y + (kind == "dmr" ? .039f : .036f), rail.z - .19f);
            if (parts.Optic == "scope8") return (rail.y + .055f, rail.z - .21f);
            return kind switch
            {
                "ak" => (.118f, -.03f),
                "ar" => (.131f, -.05f),
                "pistol-striker" => (.094f, -.06f),
                "pistol-eagle" => (.124f, -.07f),
                _ => (.12f, -.04f)
            };
        }

        public static WeaponSocket[] Sockets(string kind, ArsenalParts parts)
        {
            var sight = SightEye(kind, parts);
            int muzzleZ = Mm(parts.BarrelFront + MuzzleDeviceLength(parts.Muzzle));
            switch (kind)
            {
                case "ak":
                    return new[]
                    {
                        new WeaponSocket("attachment-rail", 0, 112, 120), new WeaponSocket("chamber-ejection", 28, 95, 110),
                        new WeaponSocket("left-hand-grip", 0, 5, Mm(AkHandguardFront(parts) - .05f)), new WeaponSocket("magazine-grip", 0, -80, 130),
                        new WeaponSocket("muzzle", 0, 80, muzzleZ), new WeaponSocket("right-hand-grip", 0, 0, 0),
                        new WeaponSocket("shoulder", 0, 15, -395), new WeaponSocket("sight-eye", 0, Mm(sight.y), Mm(sight.z))
                    };
                case "ar":
                    return new[]
                    {
                        new WeaponSocket("attachment-rail", 0, 120, 80), new WeaponSocket("chamber-ejection", 28, 85, 70),
                        new WeaponSocket("left-hand-grip", 0, 10, 340), new WeaponSocket("magazine-grip", 0, -75, 75),
                        new WeaponSocket("muzzle", 0, 80, muzzleZ), new WeaponSocket("right-hand-grip", 0, 0, 0),
                        new WeaponSocket("shoulder", 0, 60, -425), new WeaponSocket("sight-eye", 0, Mm(sight.y), Mm(sight.z))
                    };
                case "dmr":
                    return new[]
                    {
                        new WeaponSocket("attachment-rail", 0, 124, 140), new WeaponSocket("chamber-ejection", 25, 95, 60),
                        new WeaponSocket("left-hand-grip", 0, -10, 330), new WeaponSocket("magazine-grip", 0, -80, 80),
                        new WeaponSocket("muzzle", 0, 80, muzzleZ), new WeaponSocket("right-hand-grip", 0, 0, 0),
                        new WeaponSocket("shoulder", 0, 60, -405), new WeaponSocket("sight-eye", 0, Mm(sight.y), Mm(sight.z))
                    };
                case "bolt":
                    return new[]
                    {
                        new WeaponSocket("attachment-rail", 0, 112, 40), new WeaponSocket("chamber-ejection", 25, 95, 10),
                        new WeaponSocket("left-hand-grip", 0, -15, 330), new WeaponSocket("magazine-grip", 0, -30, 20),
                        new WeaponSocket("muzzle", 0, 85, muzzleZ), new WeaponSocket("right-hand-grip", 0, 0, 0),
                        new WeaponSocket("shoulder", 0, 50, -460), new WeaponSocket("sight-eye", 0, Mm(sight.y), Mm(sight.z))
                    };
                case "pistol-striker":
                    return new[]
                    {
                        new WeaponSocket("attachment-rail", 0, 20, 70), new WeaponSocket("chamber-ejection", 14, 80, 50),
                        new WeaponSocket("left-hand-grip", -22, 0, -5), new WeaponSocket("magazine-grip", 0, -70, -20),
                        new WeaponSocket("muzzle", 0, 70, Mm(parts.BarrelFront)), new WeaponSocket("right-hand-grip", 0, 0, 0),
                        new WeaponSocket("sight-eye", 0, Mm(sight.y), Mm(sight.z))
                    };
                default:
                    return new[]
                    {
                        new WeaponSocket("attachment-rail", 0, 22, 90), new WeaponSocket("chamber-ejection", 17, 95, 20),
                        new WeaponSocket("left-hand-grip", -25, -5, -10), new WeaponSocket("magazine-grip", 0, -80, -30),
                        new WeaponSocket("muzzle", 0, 90, Mm(parts.BarrelFront)), new WeaponSocket("right-hand-grip", 0, 0, 0),
                        new WeaponSocket("sight-eye", 0, Mm(sight.y), Mm(sight.z))
                    };
            }
        }

        internal static float AkHandguardFront(ArsenalParts parts) => Math.Min(.355f, parts.BarrelFront - .07f);

        public static CollisionData Collision(string kind, ArsenalParts parts)
        {
            var boxes = new List<TaggedBox>();
            void Add(float y, float z, float xSize, float ySize, float zSize, float pitch = 0)
                => boxes.Add(new TaggedBox(new Box(new Float3(0, y, z), new Float3(xSize, ySize, zSize), WeaponGeometry.Pitch(pitch)), 0));
            float front = parts.BarrelFront;
            switch (kind)
            {
                case "ak":
                    Add(.07f, .075f, .05f, .065f, .29f); Add(.08f, .29f, .056f, .065f, .16f); Add(.035f, -.24f, .046f, .075f, .33f);
                    Add(-.045f, -.025f, .032f, .105f, .042f, 18); Add(-.07f, .14f, .03f, .2f, .07f, -18);
                    return new CollisionData(new[] { new TaggedCapsule(new Capsule(new Float3(0, .08f, .22f), new Float3(0, .08f, front), .012f), 0) }, boxes, Array.Empty<TaggedHeightGrid>());
                case "ar":
                case "dmr":
                    Add(.07f, .08f, .055f, .09f, .28f); Add(.08f, .36f, .06f, .065f, .28f); Add(.06f, -.26f, .05f, .11f, .3f);
                    Add(-.045f, -.02f, .032f, .11f, .042f, 18); Add(-.06f, .075f, .032f, .16f, .07f, -6);
                    return new CollisionData(new[] { new TaggedCapsule(new Capsule(new Float3(0, .08f, .22f), new Float3(0, .08f, front), .012f), 0) }, boxes, Array.Empty<TaggedHeightGrid>());
                case "bolt":
                    Add(.06f, .25f, .06f, .075f, .44f); Add(.05f, -.28f, .05f, .16f, .36f); Add(-.035f, -.03f, .034f, .10f, .045f, 22);
                    Add(.155f, .04f, .06f, .06f, .4f);
                    return new CollisionData(new[] { new TaggedCapsule(new Capsule(new Float3(0, .085f, .14f), new Float3(0, .085f, front), .014f), 0) }, boxes, Array.Empty<TaggedHeightGrid>());
                case "pistol-striker":
                    Add(.06f, .03f, .03f, .06f, .19f); Add(-.02f, -.03f, .03f, .105f, .048f, 18);
                    return new CollisionData(new[] { new TaggedCapsule(new Capsule(new Float3(0, .07f, .04f), new Float3(0, .07f, front), .009f), 0) }, boxes, Array.Empty<TaggedHeightGrid>());
                default:
                    Add(.07f, .05f, .036f, .08f, .26f); Add(-.03f, -.04f, .034f, .12f, .055f, 16);
                    return new CollisionData(new[] { new TaggedCapsule(new Capsule(new Float3(0, .09f, .06f), new Float3(0, .09f, front), .012f), 0) }, boxes, Array.Empty<TaggedHeightGrid>());
            }
        }

        private sealed class Builder
        {
            private readonly string kind;
            private readonly ArsenalParts p;
            private readonly int lod;
            private readonly MeshAssembly a = new MeshAssembly();
            private readonly int radial;
            private readonly float bevel;
            private readonly ushort metal, furniture, grip;
            private readonly bool detail, fine;

            public Builder(string kind, ArsenalParts parts, int lod)
            {
                this.kind = kind; p = parts; this.lod = lod;
                radial = new[] { 24, 14, 10, 6 }[lod];
                bevel = lod == 0 ? .003f : lod == 1 ? .0015f : 0;
                detail = lod < 2; fine = lod == 0;
                metal = parts.Furniture == "gold" ? Gold : Metal;
                furniture = parts.Furniture == "wood" ? Wood : parts.Furniture == "gold" ? Gold : Polymer;
                // Gold pistols keep dark grip panels so the hand reads against the finish.
                grip = parts.Furniture == "gold" ? Polymer : furniture;
            }

            private void Box(float x, float y, float z, float sx, float sy, float sz, ushort material, float pitch = 0)
            {
                float b = Math.Min(bevel, Math.Min(sx, Math.Min(sy, sz)) * .2f);
                a.Add(PrimitiveGeometry.Box(new Float3(sx, sy, sz), b, fine && sx >= .030f && sy >= .030f && sz >= .045f ? 2 : 1),
                    new Float3(x, y, z), WeaponGeometry.Pitch(pitch), material);
            }

            /// <summary>Cylinder along +Z between <paramref name="rear"/> and <paramref name="front"/>.</summary>
            private void Tube(float x, float y, float rear, float front, float radius, ushort material, int? segments = null)
                => a.Add(PrimitiveGeometry.Cylinder(radius, front - rear, segments ?? radial), new Float3(x, y, (rear + front) / 2), Quat.Identity, material);

            private void Cone(float y, float rear, float front, float rearRadius, float frontRadius, ushort material)
                => a.Add(PrimitiveGeometry.Cone(rearRadius, frontRadius, front - rear, radial), new Float3(0, y, (rear + front) / 2), Quat.Identity, material);

            private void Bore(float y, float front, float outer, float inner, float length)
                => a.Add(BoredMuzzle.Build(outer, inner, length, length * .3f, radial), new Float3(0, y, front - length), Quat.Identity, metal);

            private void Loft(float y, float[] z, float[] width, float[] height, ushort material, float pitch = 0)
                => a.Add(WeaponProfiles.Loft(z, width, height, 8), new Float3(0, y, 0), WeaponGeometry.Pitch(pitch), material);

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

            // ---------------------------------------------------------------- shared long-gun parts

            private void PistolGrip(float y, float z, float pitch, ushort material)
            {
                Box(0, y, z, .030f, .105f, .042f, material, pitch);
                if (detail) GripGrooves(y, z, pitch, .030f, .105f, .042f, fine ? 4 : 2);
            }

            /// <summary>Flush finger grooves on both grip faces, placed along the grip's own pitched axis.</summary>
            private void GripGrooves(float y, float z, float pitch, float sx, float sy, float sz, int count)
            {
                double rad = pitch * Math.PI / 180;
                float up = (float)Math.Cos(rad), forward = (float)Math.Sin(rad);
                for (int i = 0; i < count; i++)
                {
                    float along = sy * (.32f - i * .64f / Math.Max(1, count - 1));
                    float gy = y + along * up, gz = z + along * forward;
                    for (int side = -1; side <= 1; side += 2)
                        Box(side * (sx / 2 - .0004f), gy, gz, .0012f, sy * .07f, sz * .7f, Recess, pitch);
                }
            }

            private void TriggerGroup(float guardZ)
            {
                Box(0, -.012f, guardZ, .012f, .006f, .075f, metal);
                Box(0, .010f, guardZ + .036f, .012f, .040f, .006f, metal);
                if (detail) Box(0, .012f, guardZ - .008f, .006f, .026f, .006f, Steel, -12);
            }

            private void MuzzleDevice(float y, float front)
            {
                float length = MuzzleDeviceLength(p.Muzzle);
                if (length <= 0) { Bore(y, front, .011f, .0045f, .014f); return; }
                float end = front + length;
                switch (p.Muzzle)
                {
                    case "slant":
                        // AK-47 slant brake: a tube with the cut face on the right and a top port.
                        Tube(0, y, front, end - .014f, .0130f, metal);
                        Bore(y, end, .0130f, .0055f, .014f);
                        if (detail) { Box(.008f, y + .009f, end - .02f, .010f, .006f, .02f, Recess, 30); Box(0, y + .012f, front + .028f, .006f, .003f, .016f, Recess); }
                        break;
                    case "flash":
                        Cone(y, front, end - .012f, .0105f, .0128f, metal);
                        Bore(y, end, .0128f, .0065f, .012f);
                        if (detail) for (int i = 0; i < 2; i++) Box(i == 0 ? -.012f : .012f, y, front + .022f, .002f, .004f, .024f, Recess);
                        break;
                    case "birdcage":
                        Tube(0, y, front, end - .012f, .0108f, metal);
                        Bore(y, end, .0108f, .0055f, .012f);
                        if (detail) for (int i = -1; i <= 1; i += 2) Box(i * .0105f, y + .004f, front + .024f, .002f, .010f, .026f, Recess);
                        break;
                    default:
                        // Muzzle brake: rectangular body with side ports venting gas left and right.
                        Box(0, y, front + (length - .014f) / 2, .038f, .030f, length - .014f, metal);
                        Bore(y, end, .016f, .0075f, .014f);
                        if (detail)
                            for (int i = 0; i < 3; i++)
                                for (int side = -1; side <= 1; side += 2)
                                    Box(side * .0185f, y, front + .012f + i * .018f, .002f, .020f, .010f, Recess);
                        break;
                }
            }

            private void Scope(float y, float rear, float front, float tube, float bell, float bellLength, float eye)
            {
                Tube(0, y, rear + .05f, front - bellLength, tube, metal);
                Cone(y, front - bellLength, front - .01f, tube, bell, metal);
                a.Add(BoredMuzzle.Build(bell, bell * .82f, .012f, .006f, radial), new Float3(0, y, front - .012f), Quat.Identity, metal);
                Cone(y, rear, rear + .05f, eye, tube, metal);
                if (detail)
                {
                    // Windage and elevation turrets, mounting rings.
                    Box(0, y + tube + .010f, (rear + front) / 2, .022f, .020f, .022f, metal);
                    Box(tube + .010f, y, (rear + front) / 2, .020f, .022f, .022f, metal);
                    for (int i = 0; i < 2; i++)
                    {
                        float rz = rear + .08f + i * (front - rear - .2f);
                        Box(0, y - tube - .006f, rz, .026f, .016f, .016f, Steel);
                    }
                    a.Add(PrimitiveGeometry.Cylinder(eye * .78f, .001f, radial), new Float3(0, y, rear + .0005f), Quat.Identity, Recess);
                }
            }

            private void Optic(float railY, float railCenterZ)
            {
                switch (p.Optic)
                {
                    case "scope2":
                        Box(0, railY + .032f, railCenterZ, .034f, .030f, .11f, metal);
                        Tube(0, railY + .035f, railCenterZ + .055f, railCenterZ + .10f, .021f, metal);
                        Tube(0, railY + .035f, railCenterZ - .085f, railCenterZ - .055f, .017f, metal);
                        if (detail)
                        {
                            Box(0, railY + .052f, railCenterZ + .01f, .012f, .010f, .03f, metal);
                            a.Add(PrimitiveGeometry.Cylinder(.016f, .001f, radial), new Float3(0, railY + .035f, railCenterZ + .1005f), Quat.Identity, Recess);
                        }
                        break;
                    case "scope4":
                        Scope(railY + (kind == "dmr" ? .039f : .036f), railCenterZ - .19f, railCenterZ + .17f, .015f, .023f, .07f, .019f);
                        break;
                    case "scope8":
                        Scope(railY + .055f, railCenterZ - .21f, railCenterZ + .2f, .017f, .029f, .09f, .022f);
                        break;
                }
            }

            // ---------------------------------------------------------------- Kalashnikov family

            private void Ak()
            {
                float y = .080f, front = p.BarrelFront, hg = AkHandguardFront(p);
                float gasZ = Math.Max(hg + .03f, Math.Min(.385f, front - .07f));
                float sightZ = Math.Max(gasZ + .03f, front - .05f);
                // Stamped receiver with the curved top cover.
                Box(0, .068f, .075f, .046f, .058f, .29f, metal);
                Box(0, .103f, .065f, .040f, .014f, .25f, metal);
                if (detail)
                {
                    Box(-.0235f, .068f, .03f, .002f, .030f, .12f, Recess);       // magazine-well dimple, left
                    Box(.0235f, .085f, .05f, .003f, .012f, .13f, Steel);        // safety lever, right
                    Box(.0235f, .085f, .135f, .002f, .016f, .07f, Recess);      // ejection port
                    Box(0, .114f, .205f, .022f, .006f, .04f, Steel);            // rear sight leaf
                }
                Box(0, .105f, .205f, .034f, .016f, .05f, metal);                // rear sight block
                // Two-piece handguard and the gas tube over the barrel.
                Box(0, .066f, (.225f + hg) / 2, .054f, .042f, hg - .225f, furniture);
                Box(0, .104f, (.225f + hg) / 2 + .01f, .036f, .020f, hg - .245f, furniture);
                if (detail)
                    for (int side = -1; side <= 1; side += 2)
                        Box(side * .0272f, .066f, (.225f + hg) / 2, .002f, .006f, hg - .26f, Recess);
                Tube(0, .103f, hg - .01f, gasZ, .0100f, metal, Math.Max(6, radial / 2));
                Box(0, .092f, gasZ, .024f, .040f, .030f, metal);
                Tube(0, y, .22f, front, .0085f, metal);
                if (detail) Tube(0, y, front - .02f, front, .0110f, Steel);
                Box(0, .095f, sightZ, .020f, .028f, .022f, metal);
                Box(0, .112f, sightZ, .004f, .012f, .004f, Steel);
                if (detail) for (int side = -1; side <= 1; side += 2) Box(side * .008f, .114f, sightZ, .003f, .016f, .016f, metal);
                MuzzleDevice(y, front);
                PistolGrip(-.022f, -.03f, 18, p.Furniture == "wood" ? Wood : Polymer);
                Box(0, .030f, .01f, .040f, .030f, .13f, metal);               // lower receiver / trigger housing
                TriggerGroup(.04f);
                if (detail) Box(0, .032f, .11f, .040f, .018f, .03f, metal);   // magazine catch
                if (p.Stock == "folding")
                {
                    Loft(.050f, new[] { -.40f, -.385f, -.16f, -.07f }, new[] { .019f, .021f, .017f, .016f }, new[] { .060f, .060f, .032f, .026f }, furniture, -4);
                    Box(0, .068f, -.075f, .030f, .034f, .020f, metal);
                    if (detail) Box(0, .045f, -.26f, .040f, .022f, .12f, Recess, -4);
                    Box(0, .02f, -.403f, .044f, .12f, .010f, Recess);
                }
                else
                {
                    Loft(.050f, new[] { -.40f, -.38f, -.15f, -.07f }, new[] { .022f, .024f, .019f, .018f }, new[] { .065f, .062f, .034f, .026f }, furniture, -5);
                    Box(0, .016f, -.405f, .046f, .13f, .010f, metal);
                }
            }

            // ---------------------------------------------------------------- AR / M16 family

            private void Ar()
            {
                float y = .080f, front = p.BarrelFront, sightZ = front - .13f;
                Box(0, .040f, .06f, .046f, .050f, .20f, metal);                 // lower receiver
                Box(0, .022f, .075f, .044f, .050f, .075f, metal);               // magazine well
                Box(0, .085f, .075f, .050f, .042f, .26f, metal);                // upper receiver
                Box(0, .112f, .075f, .024f, .012f, .25f, metal);                // flat-top rail
                if (detail)
                {
                    for (int i = 0; i < (fine ? 10 : 5); i++) Box(0, .1205f, -.035f + i * (fine ? .025f : .05f), .026f, .004f, .010f, metal);
                    Box(.030f, .090f, -.020f, .014f, .016f, .04f, Steel);       // forward assist
                    Box(.0255f, .080f, .07f, .002f, .022f, .07f, Recess);       // ejection port cover
                    Box(-.024f, .048f, .03f, .004f, .012f, .016f, Steel);       // bolt catch
                }
                a.Add(WeaponProfiles.Loft(new[] { .215f, .225f, .49f, .50f }, new[] { .030f, .032f, .030f, .028f }, new[] { .030f, .032f, .030f, .028f }, detail ? 14 : 6),
                    new Float3(0, y, 0), Quat.Identity, furniture);
                if (fine) for (int i = 0; i < 6; i++) Box(0, y - .031f, .25f + i * .04f, .012f, .004f, .018f, Recess);
                Tube(0, y, .205f, .218f, .034f, metal);                         // delta ring
                Tube(0, y, .21f, front, .0090f, metal);
                Box(0, .087f, sightZ, .026f, .026f, .030f, metal);              // front sight base
                Box(0, .112f, sightZ, .012f, .042f, .016f, metal);              // A2 sight tower
                if (detail) Box(0, .132f, sightZ, .003f, .010f, .003f, Steel);
                MuzzleDevice(y, front);
                PistolGrip(-.022f, -.022f, 18, Polymer);
                TriggerGroup(.045f);
                Loft(.062f, new[] { -.43f, -.415f, -.17f, -.06f }, new[] { .024f, .026f, .020f, .016f }, new[] { .070f, .068f, .040f, .030f }, Polymer);
                Box(0, .06f, -.433f, .050f, .14f, .012f, Recess);
                if (p.Optic == "irons") { Box(0, .128f, -.01f, .022f, .020f, .030f, metal); if (detail) Box(0, .136f, -.01f, .004f, .004f, .004f, Recess); }
                else Optic(Rail(kind).y, Rail(kind).z);
            }

            // ---------------------------------------------------------------- M14 EBR chassis

            private void Dmr()
            {
                float y = .080f, front = p.BarrelFront;
                Box(0, .055f, .15f, .056f, .050f, .46f, metal);                 // chassis lower
                Box(0, .118f, .14f, .026f, .012f, .42f, metal);                 // top rail
                Box(0, .090f, .04f, .040f, .036f, .22f, Steel);                 // M14 receiver
                Box(0, .085f, .32f, .052f, .046f, .14f, metal);                 // rail handguard
                if (detail)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Box(side * .032f, .080f, .30f, .006f, .012f, .16f, metal);
                        for (int i = 0; i < (fine ? 5 : 2); i++) Box(side * .0285f, .055f, .02f + i * .06f, .002f, .016f, .026f, Recess);
                    }
                    for (int i = 0; i < (fine ? 14 : 7); i++) Box(0, .1265f, -.05f + i * (fine ? .028f : .056f), .028f, .004f, .010f, metal);
                }
                Tube(0, y, .38f, front, .0095f, metal);
                MuzzleDevice(y, front);
                PistolGrip(-.012f, -.02f, 18, furniture);
                TriggerGroup(.045f);
                Box(0, .010f, .05f, .036f, .022f, .12f, metal);                 // trigger housing
                Tube(0, .070f, -.28f, -.08f, .0160f, metal);                     // buffer tube
                Box(0, .060f, -.345f, .040f, .100f, .13f, furniture);            // collapsible stock body
                Box(0, .106f, -.31f, .036f, .020f, .12f, furniture);             // cheek riser
                Box(0, .060f, -.414f, .046f, .12f, .012f, Recess);
                Optic(Rail(kind).y, Rail(kind).z);
            }

            // ---------------------------------------------------------------- Heavy bolt-action

            private void Bolt()
            {
                float y = .085f, front = p.BarrelFront;
                // One-piece fibreglass stock: forend, action bed, vertical grip, butt with cheek riser.
                Loft(.060f, new[] { .05f, .10f, .42f, .46f }, new[] { .026f, .030f, .028f, .024f }, new[] { .032f, .035f, .030f, .025f }, furniture);
                Box(0, .055f, -.02f, .050f, .055f, .16f, furniture);
                Box(0, -.035f, -.03f, .032f, .100f, .045f, furniture, 22);
                Loft(.050f, new[] { -.46f, -.44f, -.20f, -.10f }, new[] { .024f, .026f, .022f, .022f }, new[] { .075f, .075f, .045f, .040f }, furniture);
                Box(0, .106f, -.28f, .034f, .022f, .16f, furniture);
                Box(0, .050f, -.465f, .050f, .15f, .016f, Recess);
                if (p.Stock == "folding") Box(0, .062f, -.10f, .054f, .050f, .020f, metal);
                // Round action, scope base, heavy fluted barrel.
                Tube(0, y, -.08f, .14f, .0190f, Steel);
                Box(0, .107f, .04f, .022f, .008f, .24f, metal);
                Tube(0, y, .14f, front, .0125f, metal);
                if (detail)
                {
                    int flutes = fine ? 6 : 3;
                    for (int i = 0; i < flutes; i++)
                    {
                        double angle = Math.PI * 2 * i / flutes;
                        float fx = (float)Math.Cos(angle) * .0122f, fy = (float)Math.Sin(angle) * .0122f;
                        Box(fx, y + fy, .40f, .003f, .003f, .40f, Recess);
                    }
                    TriggerGroup(.03f);
                }
                MuzzleDevice(y, front);
                // Folded bipod under the forend.
                Box(0, .022f, .41f, .030f, .016f, .030f, metal);
                if (detail)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Tube(side * .012f, .014f, .22f, .40f, .0055f, metal, 6);
                        Box(side * .012f, .014f, .215f, .010f, .010f, .012f, Recess);
                    }
                }
                Optic(Rail(kind).y, Rail(kind).z);
            }

            // ---------------------------------------------------------------- Pistols

            private void Striker()
            {
                float y = .070f, front = p.BarrelFront;
                Box(0, .040f, .03f, .028f, .022f, .16f, Polymer);               // frame with dust cover
                Tube(0, y, .08f, front - .012f, .0072f, metal);
                Bore(y, front, .0080f, .0045f, .012f);
                PistolGripPistol(-.02f, -.03f, 18, .028f, .105f, .048f, Polymer);
                Box(0, -.004f, .035f, .012f, .006f, .060f, Polymer);             // square trigger guard
                Box(0, .014f, .062f, .012f, .035f, .008f, Polymer);
                if (detail)
                {
                    Box(0, .010f, .030f, .006f, .022f, .006f, Steel, -10);
                    for (int i = 0; i < 3; i++) Box(0, .028f, .07f + i * .012f, .029f, .003f, .004f, Recess);
                }
                Box(0, -.083f, -.048f, .030f, .012f, .040f, Polymer, 18);         // magazine floorplate
            }

            private void Eagle()
            {
                float y = .090f, front = p.BarrelFront;
                Box(0, .052f, .04f, .032f, .044f, .20f, metal);                  // frame
                Box(0, .098f, (.06f + front) / 2, .030f, .036f, front - .06f, metal); // fixed barrel block
                Box(0, .120f, (.06f + front) / 2, .018f, .008f, front - .07f, metal); // top rail
                Bore(y, front + .001f, .0105f, .0062f, .012f);
                PistolGripPistol(-.03f, -.04f, 16, .032f, .12f, .055f, grip);
                Box(0, -.006f, .045f, .014f, .008f, .070f, metal);
                Box(0, .016f, .078f, .014f, .040f, .008f, metal);
                if (detail)
                {
                    Box(0, .010f, .040f, .007f, .024f, .007f, Steel, -10);
                    Box(0, .088f, -.078f, .010f, .020f, .012f, Steel, -20);       // hammer
                    for (int i = 0; i < (fine ? 6 : 3); i++) Box(0, .1245f, .09f + i * .016f, .019f, .002f, .004f, Recess);
                    for (int side = -1; side <= 1; side += 2) Box(side * .0155f, .098f, front - .03f, .002f, .010f, .03f, Recess);
                }
                Box(0, .122f, front - .012f, .006f, .010f, .008f, metal);         // front sight
                Box(0, -.097f, -.065f, .034f, .012f, .050f, metal, 16);           // magazine floorplate
            }

            private void PistolGripPistol(float y, float z, float pitch, float sx, float sy, float sz, ushort material)
            {
                Box(0, y, z, sx, sy, sz, material, pitch);
                if (detail) GripGrooves(y, z, pitch, sx, sy, sz, fine ? 5 : 2);
            }

            // ---------------------------------------------------------------- Magazines

            public MeshData Magazine()
            {
                switch (kind)
                {
                    case "ak":
                    {
                        // Curved 30-round magazine: five pitched segments walking forward as they descend.
                        ushort material = p.Furniture == "polymer" ? Polymer : Metal;
                        float py = .045f, pz = .100f, h = .046f;
                        for (int i = 0; i < 5; i++)
                        {
                            float angle = -(6 + 6 * i);
                            double rad = angle * Math.PI / 180;
                            float dy = -(float)Math.Cos(rad), dz = -(float)Math.Sin(rad);
                            float cy = py + dy * h / 2, cz = pz + dz * h / 2;
                            Box(0, cy, cz, .028f, h + .002f, .062f, material, angle);
                            Box(-.0145f, cy, cz, .002f, h * .6f, .02f, Recess, angle);
                            Box(.0145f, cy, cz, .002f, h * .6f, .02f, Recess, angle);
                            py += dy * h; pz += dz * h;
                        }
                        Box(0, py - .004f, pz + .002f, .032f, .010f, .066f, material, -30);
                        break;
                    }
                    case "ar":
                    {
                        float py = .040f, pz = .075f, h = .060f;
                        for (int i = 0; i < 3; i++)
                        {
                            float angle = -(4 + 4 * i);
                            double rad = angle * Math.PI / 180;
                            float dy = -(float)Math.Cos(rad), dz = -(float)Math.Sin(rad);
                            Box(0, py + dy * h / 2, pz + dz * h / 2, .026f, h + .002f, .064f, Metal, angle);
                            Box(-.0135f, py + dy * h / 2, pz + dz * h / 2, .002f, h * .7f, .016f, Recess, angle);
                            py += dy * h; pz += dz * h;
                        }
                        Box(0, py - .004f, pz, .030f, .010f, .068f, Polymer, -12);
                        break;
                    }
                    case "dmr":
                        Box(0, -.055f, .075f, .032f, .13f, .075f, Metal, -4);
                        Box(0, -.122f, .080f, .036f, .010f, .079f, Steel, -4);
                        Box(-.0165f, -.05f, .075f, .002f, .09f, .02f, Recess, -4);
                        break;
                    case "bolt":
                        Box(0, .005f, .02f, .032f, .070f, .085f, Metal);
                        Box(0, -.032f, .02f, .036f, .008f, .089f, Steel);
                        break;
                    case "pistol-striker":
                        Box(0, -.040f, -.040f, .024f, .090f, .034f, Metal, 18);
                        Box(0, -.083f, -.048f, .030f, .012f, .040f, Polymer, 18);
                        break;
                    default:
                        Box(0, -.050f, -.050f, .026f, .100f, .042f, Metal, 16);
                        Box(0, -.097f, -.065f, .034f, .012f, .050f, metal, 16);
                        break;
                }
                return a.Build();
            }

            // ---------------------------------------------------------------- Bolt carrier, bolt handle or slide

            public MeshData Cycle()
            {
                switch (kind)
                {
                    case "ak":
                        Box(.026f, .088f, .16f, .008f, .016f, .10f, Steel);         // carrier rail
                        Box(.040f, .088f, .205f, .030f, .010f, .014f, Steel);       // charging handle
                        Box(.056f, .088f, .205f, .012f, .016f, .016f, Steel);
                        break;
                    case "ar":
                        Box(0, .105f, -.065f, .012f, .010f, .050f, Steel);          // charging handle
                        Box(0, .105f, -.092f, .050f, .008f, .012f, Steel);
                        Box(.022f, .083f, .07f, .006f, .016f, .060f, Steel);        // carrier through the port
                        break;
                    case "dmr":
                        Box(.032f, .075f, .14f, .010f, .014f, .16f, Steel);         // operating rod
                        Box(.040f, .075f, .215f, .014f, .016f, .018f, Steel);
                        break;
                    case "bolt":
                        Tube(0, .085f, -.16f, -.06f, .0105f, Steel);                 // bolt shroud
                        WeaponProfiles.Rod(a, new Float3(.012f, .085f, -.080f), new Float3(.046f, .070f, -.080f), .0045f, Steel);
                        Box(.052f, .067f, -.081f, .016f, .020f, .016f, Steel);       // bolt knob
                        break;
                    case "pistol-striker":
                        Box(0, .071f, .03f, .026f, .030f, .19f, metal);              // slide
                        Box(0, .090f, -.055f, .022f, .008f, .008f, Steel);            // rear sight
                        Box(0, .089f, .115f, .006f, .007f, .006f, Steel);             // front sight
                        Box(.0132f, .074f, .05f, .002f, .014f, .03f, Recess);         // ejection port
                        for (int i = 0; i < 5; i++)
                            for (int side = -1; side <= 1; side += 2)
                                Box(side * .0132f, .071f, -.055f + i * .006f, .001f, .022f, .002f, Recess);
                        break;
                    default:
                        Box(0, .090f, 0f, .034f, .042f, .14f, metal);                 // slide
                        Box(0, .116f, -.06f, .024f, .010f, .010f, Steel);             // rear sight
                        for (int i = 0; i < 6; i++)
                            for (int side = -1; side <= 1; side += 2)
                                Box(side * .0172f, .090f, -.06f + i * .007f, .001f, .030f, .0025f, Recess);
                        break;
                }
                return a.Build();
            }
        }
    }
}
