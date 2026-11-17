#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation
{
    public sealed class WeaponSocket
    {
        public string Id { get; }
        public Float3 Position { get; }
        public Quat Rotation { get; }
        internal WeaponSocket(string id, int xMm, int yMm, int zMm)
        { Id = id; Position = new Float3(xMm / 1000f, yMm / 1000f, zMm / 1000f); Rotation = Quat.Identity; }
    }
    public sealed class WeaponBuild
    {
        public FrozenList<MeshData> Lods { get; }
        /// <summary>Detachable magazine mesh in weapon space (seated pose).</summary>
        public MeshData Magazine { get; }
        /// <summary>Rifle bolt carrier / pistol slide mesh in closed pose.</summary>
        public MeshData CyclePart { get; }
        public CollisionData Collision { get; }
        public Aabb Bounds { get; }
        public FrozenList<WeaponSocket> Sockets { get; }
        internal WeaponBuild(MeshData[] lods, MeshData magazine, MeshData cyclePart, CollisionData collision, Aabb bounds, WeaponSocket[] sockets)
        {
            Lods = new FrozenList<MeshData>(lods, 4);
            Magazine = magazine;
            CyclePart = cyclePart;
            Collision = collision;
            Bounds = bounds;
            Sockets = new FrozenList<WeaponSocket>(sockets, 8);
        }
    }
    /// <summary>Rule-built fictional weapons. No combat rules, authored mesh vertices or seed-dependent mechanics.</summary>
    public static class WeaponGeometry
    {
        // Shared mechanical dimensions drive the visual part and its independent gameplay proxy.
        private const float RifleMagazineCenterY = -.080f;
        private const float RifleMagazineHeight = .200f;
        public static WeaponBuild Build(WeaponRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (recipe.Arsenal != null) return ArsenalGeometry.Build(recipe.Kind, recipe.Arsenal);
            var lods = new MeshData[4];
            for (int i = 0; i < 4; i++) lods[i] = BuildLod(recipe.Kind, recipe.Parts, i);
            var magazine = BuildMagazine(recipe.Kind, 0);
            var cycle = BuildCyclePart(recipe.Kind, 0);
            var collision = BuildCollision(recipe.Kind);
            var sockets = Sockets(recipe.Kind);
            var bounds = BoundsFor(recipe.Kind);
            foreach (var lod in lods) AssertInside(lod, bounds);
            AssertInside(magazine, bounds);
            AssertInside(cycle, bounds);
            return new WeaponBuild(lods, magazine, cycle, collision, bounds, sockets);
        }
        public static FrozenList<WeaponSocket> Sockets(WeaponRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (recipe.Arsenal != null) return new FrozenList<WeaponSocket>(ArsenalGeometry.Sockets(recipe.Kind, recipe.Arsenal), 8);
            return new FrozenList<WeaponSocket>(Sockets(recipe.Kind), 8);
        }
        public static CollisionData Collision(WeaponRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (recipe.Arsenal != null) return ArsenalGeometry.Collision(recipe.Kind, recipe.Arsenal);
            return BuildCollision(recipe.Kind);
        }
        // Construction envelope only — sockets stay on the numeric baseline; bevelled detail may fill this AABB.
        internal static Aabb BoundsFor(string kind) => kind == "rifle" ? new Aabb(new Float3(-.068f, -.205f, -.330f), new Float3(.068f, .155f, .645f))
            : new Aabb(new Float3(-.038f, -.095f, -.085f), new Float3(.038f, .115f, .135f));
        private static void AssertInside(MeshData mesh, Aabb bounds)
        {
            foreach (var p in mesh.Positions)
                if (p.X < bounds.Min.X || p.X > bounds.Max.X || p.Y < bounds.Min.Y || p.Y > bounds.Max.Y || p.Z < bounds.Min.Z || p.Z > bounds.Max.Z)
                    throw new InvalidOperationException("Weapon construction escaped its declared envelope.");
        }
        // ADR-082: every rifle dimension that differed between the 1.0.5 standard and carbine silhouettes is a part.
        // Rail, under-rail and fastener counts stop at the handguard front, which reproduces both presets exactly.
        private static MeshData BuildLod(string kind, WeaponParts? parts, int lod)
        {
            var assembly = new MeshAssembly();
            int radial = new[] { 28, 14, 10, 6 }[lod];
            float bevel = lod == 0 ? .003f : lod == 1 ? .0015f : 0;
            var p = parts ?? WeaponParts.Standard;
            float handguardFront = p.HandguardFront;
            float barrelFront = p.BarrelFront;
            void Box(float x, float y, float z, float sx, float sy, float sz, ushort material = 0, float pitch = 0)
            {
                float b = Math.Min(bevel, Math.Min(sx, Math.Min(sy, sz)) * .2f);
                assembly.Add(PrimitiveGeometry.Box(new Float3(sx, sy, sz), b, lod == 0 && sx >= .030f && sy >= .030f && sz >= .045f ? 2 : 1), new Float3(x, y, z), Pitch(pitch), material);
            }
            void Barrel(float y, float rear, float front, float radius)
            {
                float muzzleLength = kind == "rifle" ? .018f : .012f;
                assembly.Add(PrimitiveGeometry.Cylinder(radius, front - rear - muzzleLength, radial),
                    new Float3(0, y, (rear + front - muzzleLength) / 2), Quat.Identity, 0);
                assembly.Add(BoredMuzzle.Build(radius * 1.15f, radius * .47f, muzzleLength, muzzleLength * .25f, radial),
                    new Float3(0, y, front - muzzleLength), Quat.Identity, 0);
            }
            if (kind == "rifle")
            {
                assembly.Add(WeaponProfiles.Loft(new[] { -.045f, -.025f, .16f, .225f },
                    new[] { .022f, .029f, .029f, .024f }, new[] { .034f, .045f, .045f, .032f }, 8),
                    new Float3(0, .065f, 0), Quat.Identity);
                if (lod < 2)
                {
                    Box(0, .108f, .09f, .052f, .018f, .24f);
                    Box(0, .028f, .095f, .054f, .016f, .22f);
                    Box(-.027f, .065f, .02f, .006f, .055f, .08f);
                    Box(.027f, .065f, .02f, .006f, .055f, .08f);
                }
                assembly.Add(WeaponProfiles.Loft(new[] { .205f, .22f, handguardFront - .03f, handguardFront },
                    new[] { .025f, .030f, .028f, .023f }, new[] { .027f, .033f, .029f, .023f }, 8),
                    new Float3(0, .08f, 0), Quat.Identity, 0);
                float handguardMid = (0.205f + handguardFront) * 0.5f;
                float handguardLen = Math.Max(0.12f, handguardFront - 0.225f);
                if (lod < 2)
                {
                    Box(0, .105f, handguardMid, .042f, .012f, handguardLen, 1);
                    Box(0, .055f, handguardMid, .048f, .014f, handguardLen, 1);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Box(side * .030f, .099f, handguardMid, .004f, .010f, handguardLen - .016f);
                        Box(side * .030f, .061f, handguardMid, .004f, .010f, handguardLen - .016f);
                        int railCount = lod == 0 ? 7 : 4;
                        for (int i = 0; i < railCount; i++)
                        {
                            float z = .23f + i * (lod == 0 ? .036f : .068f);
                            if (z > handguardFront - .02f) break;
                            Box(side * .030f, .080f, z, .004f, .030f, .008f);
                            Box(side * .0285f, .080f, z + .018f, .001f, .014f, .023f, 3);
                        }
                    }
                    if (lod == 0)
                    {
                        int underCount = 5;
                        for (int i = 0; i < underCount; i++)
                        {
                            float z = .26f + i * .04f;
                            if (z > handguardFront - .02f) break;
                            Box(0, .052f, z, .030f, .006f, .014f, 1);
                        }
                    }
                }
                if (p.Stock)
                    assembly.Add(WeaponProfiles.Loft(new[] { -.32f, -.305f, -.22f, -.18f },
                        new[] { .027f, .030f, .024f, .015f }, new[] { .050f, .055f, .045f, .020f }, 8),
                        new Float3(0, .06f, 0), Quat.Identity, 1);
                Box(0, .065f, -.11f, .030f, .035f, .16f, 0);
                if (lod < 2 && p.Stock)
                {
                    Box(0, .095f, -.27f, .048f, .035f, .08f, 1);
                    Box(0, .015f, -.275f, .052f, .035f, .08f, 1);
                    Box(0, .045f, -.18f, .022f, .055f, .04f, 1);
                }
                Box(0, -.045f, -.015f, .037f, .13f, .055f, 1, 15);
                if (lod < 2)
                {
                    for (int i = 0; i < (lod == 0 ? 4 : 2); i++)
                    {
                        float y = -.02f - i * .028f;
                        Box(-.019f, y, -.015f, .004f, .018f, .045f, 1);
                        Box(.019f, y, -.015f, .004f, .018f, .045f, 1);
                    }
                }
                Barrel(.08f, .21f, barrelFront, .009f);
                if (lod < 2)
                {
                    // Compact optic with a bounded dark bore; existing sight socket stays authoritative.
                    if (p.Optic)
                    {
                        Box(0, .123f, .035f, .030f, .015f, .070f);
                        assembly.Add(BoredMuzzle.Build(.0145f, .0115f, .100f, .018f, radial),
                            new Float3(0, .139f, -.015f), Quat.Identity, 0);
                        assembly.Add(PrimitiveGeometry.Cylinder(.0105f, .001f, radial),
                            new Float3(0, .139f, .067f), Quat.Identity, 3);
                    }
                    // Barrel shoulder, muzzle collar and rail teeth catch grazing light.
                    assembly.Add(PrimitiveGeometry.Cylinder(.013f, .025f, radial), new Float3(0, .08f, barrelFront - .036f), Quat.Identity, 2);
                    int railTeeth = lod == 0 ? p.TopRailTeeth : p.TopRailTeethLod1;
                    for (int i = 0; i < railTeeth; i++)
                        Box(0, .123f, .02f + i * (lod == 0 ? .028f : .056f), .041f, .006f, .010f);
                    Box(.030f, .075f, .13f, .002f, .029f, .091f, 3);
                    Box(-.031f, .037f, .055f, .003f, .025f, .070f);
                    for (int side = -1; side <= 1; side += 2)
                        foreach (float z in (.46f <= handguardFront - .02f ? new[] { -.012f, .16f, .222f, .46f } : new[] { -.012f, .16f, .222f }))
                            WeaponProfiles.Fastener(assembly, side * .033f, .080f, z, lod);
                    if (p.Stock) Box(0, .06f, -.323f, .056f, .104f, .010f, 3);
                    Box(-.029f, .105f, -.025f, .004f, .016f, .016f, 2);
                }
                if (lod < 2)
                {
                    float gasZ = p.GasBlockZ;
                    Box(0, .08f, gasZ, .022f, .022f, .04f);
                    Box(0, .095f, gasZ, .010f, .018f, .018f);
                    assembly.Add(PrimitiveGeometry.Cylinder(.006f, p.GasTubeLength, Math.Max(6, radial / 2)),
                        new Float3(0, .095f, p.GasTubeZ), Quat.Identity, 0);
                }
                Box(0, -.041f, .055f, .014f, .010f, .085f);
                Box(0, -.017f, .095f, .014f, .055f, .010f);
                Box(0, -.017f, .015f, .014f, .055f, .010f);
                Box(-.018f, .114f, .22f, .008f, .012f, .24f);
                Box(.018f, .114f, .22f, .008f, .012f, .24f);
                Box(-.008f, .115f, -.01f, .006f, .016f, .015f);
                Box(.008f, .115f, -.01f, .006f, .016f, .015f);
                Box(0, .107f, p.FrontSightZ, .008f, .016f, .018f);
                if (lod < 2)
                {
                    Box(0, .003f, .053f, .008f, .050f, .009f, 0, -15);
                    Box(0, .013f, .068f, .047f, .018f, .074f);
                    if (lod == 0)
                    {
                        Box(0, .120f, .10f, .036f, .010f, .12f);
                        Box(-.022f, .070f, .16f, .008f, .028f, .06f);
                    }
                }
            }
            else
            {
                Box(0, .035f, .012f, .032f, .035f, .15f, 1);
                Box(0, -.018f, -.030f, .032f, .11f, .045f, 1, 16);
                if (lod < 2)
                {
                    for (int i = 0; i < (lod == 0 ? 5 : 2); i++)
                    {
                        float y = .01f - i * .022f;
                        Box(-.016f, y, -.030f, .003f, .014f, .038f, 1);
                        Box(.016f, y, -.030f, .003f, .014f, .038f, 1);
                    }
                    Box(0, .052f, .012f, .028f, .008f, .14f, 1);
                    Box(0, .018f, -.055f, .028f, .012f, .02f, 1);
                }
                Barrel(.07f, .025f, .13f, .007f);
                if (lod < 2)
                {
                    Box(0, .038f, .082f, .030f, .022f, .047f, 1);
                    for (int i = 0; i < 3; i++) Box(0, .025f, .064f + i * .014f, .031f, .005f, .006f);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        WeaponProfiles.Fastener(assembly, side * .019f, .025f, -.035f, lod, .003f);
                        WeaponProfiles.Fastener(assembly, side * .019f, .043f, .028f, lod, .0025f);
                    }
                }
                Box(0, -.020f, .035f, .012f, .008f, .060f);
                Box(0, .003f, .063f, .012f, .052f, .008f);
                if (lod < 3) Box(.018f, .045f, -.010f, .009f, .008f, .035f);
                if (lod < 2)
                {
                    Box(0, .010f, .030f, .007f, .030f, .009f, 0, -14);
                    Box(-.014f, .040f, .055f, .006f, .016f, .022f);
                    if (lod == 0)
                    {
                        Box(0, .055f, -.02f, .024f, .012f, .04f, 1);
                        Box(0, -.055f, -.02f, .026f, .014f, .038f, 1);
                    }
                }
            }
            return assembly.Build();
        }

        public static MeshData BuildMagazine(string kind, int lod)
        {
            var assembly = new MeshAssembly();
            float bevel = lod == 0 ? .0025f : lod == 1 ? .0012f : 0;
            void Box(float x, float y, float z, float sx, float sy, float sz, ushort material = 0, float pitch = 0)
            {
                float b = Math.Min(bevel, Math.Min(sx, Math.Min(sy, sz)) * .2f);
                assembly.Add(PrimitiveGeometry.Box(new Float3(sx, sy, sz), b, lod == 0 && sx >= .030f && sy >= .030f && sz >= .045f ? 2 : 1), new Float3(x, y, z), Pitch(pitch), material);
            }
            if (kind == "rifle")
            {
                Box(0, RifleMagazineCenterY, .07f, .032f, RifleMagazineHeight, .055f, 0, -8);
                if (lod < 2)
                {
                    Box(0, -.176f, .083f, .038f, .009f, .059f, 2, -8);
                    for (int side = -1; side <= 1; side += 2)
                        Box(side * .017f, -.08f, .07f, .002f, .125f, .018f, 3, -8);
                }
                if (lod < 2)
                {
                    Box(0, RifleMagazineCenterY - .06f, .07f, .028f, .04f, .048f, 0, -8);
                    for (int i = 0; i < (lod == 0 ? 6 : 3); i++)
                    {
                        float y = RifleMagazineCenterY + .06f - i * .028f;
                        Box(-.016f, y, .07f, .003f, .016f, .048f, 0, -8);
                        Box(.016f, y, .07f, .003f, .016f, .048f, 0, -8);
                    }
                }
            }
            else
            {
                Box(0, -.070f, -.045f, .033f, .012f, .045f);
                Box(0, -.040f, -.040f, .028f, .055f, .038f, 1);
                if (lod < 2)
                {
                    Box(0, -.075f, -.045f, .030f, .006f, .042f);
                    for (int i = 0; i < (lod == 0 ? 3 : 1); i++)
                    {
                        float y = -.055f - i * .012f;
                        Box(-.014f, y, -.040f, .003f, .008f, .032f, 1);
                        Box(.014f, y, -.040f, .003f, .008f, .032f, 1);
                    }
                }
            }
            return assembly.Build();
        }

        public static MeshData BuildCyclePart(string kind, int lod)
        {
            var assembly = new MeshAssembly();
            float bevel = lod == 0 ? .0025f : lod == 1 ? .0012f : 0;
            void Box(float x, float y, float z, float sx, float sy, float sz, ushort material = 0, float pitch = 0)
            {
                float b = Math.Min(bevel, Math.Min(sx, Math.Min(sy, sz)) * .2f);
                assembly.Add(PrimitiveGeometry.Box(new Float3(sx, sy, sz), b, lod == 0 && sx >= .030f && sy >= .030f && sz >= .045f ? 2 : 1), new Float3(x, y, z), Pitch(pitch), material);
            }
            if (kind == "rifle")
            {
                Box(.032f, .071f, .11f, .012f, .020f, .08f);
                Box(.043f, .085f, .055f, .034f, .012f, .018f);
                if (lod < 2)
                {
                    Box(.032f, .071f, .15f, .010f, .014f, .04f);
                    Box(.050f, .085f, .04f, .012f, .010f, .012f);
                }
            }
            else
            {
                assembly.Add(WeaponProfiles.Loft(new[] { -.080f, -.070f, .068f, .090f },
                    new[] { .013f, .017f, .017f, .014f }, new[] { .014f, .018f, .018f, .014f }, 8),
                    new Float3(0, .07f, 0), Quat.Identity);
                if (lod < 2)
                {
                    Box(.017f, .077f, .039f, .0015f, .018f, .038f, 3);
                    Box(.018f, .079f, .038f, .001f, .012f, .026f, 2);
                    for (int i = 0; i < 4; i++)
                        Box(-.017f, .070f, .046f + i * .008f, .002f, .023f, .002f, 3);
                }
                Box(-.008f, .095f, -.07f, .006f, .012f, .013f);
                Box(.008f, .095f, -.07f, .006f, .012f, .013f);
                Box(0, .092f, .078f, .006f, .010f, .010f);
                if (lod < 2)
                {
                    for (int i = 0; i < (lod == 0 ? 5 : 2); i++)
                    {
                        float z = -.062f + i * (lod == 0 ? .008f : .024f);
                        Box(-.017f, .07f, z, .004f, .025f, .003f);
                        Box(.017f, .07f, z, .004f, .025f, .003f);
                    }
                    Box(0, .082f, -.02f, .028f, .012f, .06f);
                    if (lod == 0)
                        Box(0, .088f, .04f, .020f, .008f, .05f);
                }
            }
            return assembly.Build();
        }

        internal static CollisionData BuildCollision(string kind)
        {
            var boxes = new List<TaggedBox>();
            void Add(float y, float z, float xSize, float ySize, float zSize, float pitch = 0)
                => boxes.Add(new TaggedBox(new Box(new Float3(0, y, z), new Float3(xSize, ySize, zSize), Pitch(pitch)), 0));
            if (kind == "rifle")
            {
                Add(.065f, .09f, .058f, .09f, .27f); Add(.08f, .345f, .056f, .06f, .28f);
                Add(.06f, -.25f, .06f, .11f, .14f); Add(.065f, -.11f, .03f, .035f, .16f);
                Add(-.045f, -.015f, .037f, .13f, .055f, 15); Add(RifleMagazineCenterY, .07f, .032f, RifleMagazineHeight, .055f, -8);
                return new CollisionData(new[] { new TaggedCapsule(new Capsule(new Float3(0, .08f, .221f), new Float3(0, .08f, .629f), .011f), 0) }, boxes, Array.Empty<TaggedHeightGrid>());
            }
            Add(.07f, .005f, .032f, .034f, .17f); Add(.035f, .012f, .032f, .035f, .15f);
            Add(-.018f, -.03f, .032f, .11f, .045f, 16); Add(-.07f, -.045f, .033f, .012f, .045f);
            return new CollisionData(new[] { new TaggedCapsule(new Capsule(new Float3(0, .07f, .034f), new Float3(0, .07f, .121f), .009f), 0) }, boxes, Array.Empty<TaggedHeightGrid>());
        }
        internal static Quat Pitch(float degrees)
        { double angle = degrees * Math.PI / 360; return new Quat((float)Math.Sin(angle), 0, 0, (float)Math.Cos(angle)); }
        private static WeaponSocket[] Sockets(string kind) => kind == "rifle" ? new[]
        {
            new WeaponSocket("attachment-rail",0,120,220), new WeaponSocket("chamber-ejection",35,85,100), new WeaponSocket("left-hand-grip",0,10,340),
            new WeaponSocket("magazine-grip",0,-75,65), new WeaponSocket("muzzle",0,80,640), new WeaponSocket("right-hand-grip",0,0,0),
            new WeaponSocket("shoulder",0,60,-320), new WeaponSocket("sight-eye",0,115,-40)
        } : new[]
        {
            new WeaponSocket("attachment-rail",0,20,70), new WeaponSocket("chamber-ejection",16,75,65), new WeaponSocket("left-hand-grip",-22,0,-5),
            new WeaponSocket("magazine-grip",0,-70,-20), new WeaponSocket("muzzle",0,70,130), new WeaponSocket("right-hand-grip",0,0,0),
            new WeaponSocket("sight-eye",0,100,-70)
        };
    }
}
