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
        public CollisionData Collision { get; }
        public Aabb Bounds { get; }
        public FrozenList<WeaponSocket> Sockets { get; }
        internal WeaponBuild(MeshData[] lods, CollisionData collision, Aabb bounds, WeaponSocket[] sockets)
        { Lods = new FrozenList<MeshData>(lods, 4); Collision = collision; Bounds = bounds; Sockets = new FrozenList<WeaponSocket>(sockets, 8); }
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
            var lods = new MeshData[4];
            for (int i = 0; i < 4; i++) lods[i] = BuildLod(recipe.Kind, i);
            var collision = BuildCollision(recipe.Kind);
            var sockets = Sockets(recipe.Kind);
            // Deliberately conservative fixed construction envelope, independent of seed/LOD/profile.
            var bounds = BoundsFor(recipe.Kind);
            foreach (var lod in lods) foreach (var p in lod.Positions)
                if (p.X < bounds.Min.X || p.X > bounds.Max.X || p.Y < bounds.Min.Y || p.Y > bounds.Max.Y || p.Z < bounds.Min.Z || p.Z > bounds.Max.Z)
                    throw new InvalidOperationException("Weapon construction escaped its declared envelope.");
            return new WeaponBuild(lods, collision, bounds, sockets);
        }
        public static FrozenList<WeaponSocket> Sockets(WeaponRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            return new FrozenList<WeaponSocket>(Sockets(recipe.Kind), 8);
        }
        public static CollisionData Collision(WeaponRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            return BuildCollision(recipe.Kind);
        }
        internal static Aabb BoundsFor(string kind) => kind == "rifle" ? new Aabb(new Float3(-.065f, -.20f, -.321f), new Float3(.065f, .15f, .641f))
            : new Aabb(new Float3(-.035f, -.09f, -.081f), new Float3(.035f, .11f, .131f));
        private static MeshData BuildLod(string kind, int lod)
        {
            var assembly = new MeshAssembly();
            int radial = new[] { 20, 14, 10, 6 }[lod];
            float bevel = lod == 0 ? .003f : lod == 1 ? .0015f : 0;
            void Box(float x, float y, float z, float sx, float sy, float sz, ushort material = 0, float pitch = 0)
            {
                float b = Math.Min(bevel, Math.Min(sx, Math.Min(sy, sz)) * .2f);
                assembly.Add(PrimitiveGeometry.Box(new Float3(sx, sy, sz), b, 1), new Float3(x, y, z), Pitch(pitch), material);
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
                Box(0, .065f, .09f, .058f, .09f, .27f); // receiver
                Box(0, .08f, .345f, .056f, .06f, .28f, 1); // handguard
                Box(0, .06f, -.25f, .060f, .11f, .14f, 1); // butt stock
                Box(0, .065f, -.11f, .030f, .035f, .16f, 0); // stock tube
                Box(0, -.045f, -.015f, .037f, .13f, .055f, 1, 15); // grip
                Box(0, RifleMagazineCenterY, .07f, .032f, RifleMagazineHeight, .055f, 0, -8); // magazine penetrates receiver/well
                Barrel(.08f, .21f, .64f, .009f);
                // Trigger guard is assembled from closed strips around an actual open gap.
                Box(0, -.041f, .055f, .014f, .010f, .085f);
                Box(0, -.017f, .095f, .014f, .055f, .010f);
                Box(0, -.017f, .015f, .014f, .055f, .010f);
                // Paired rail ridges leave the assigned .115m sight line clear down the center.
                Box(-.018f, .114f, .22f, .008f, .012f, .24f);
                Box(.018f, .114f, .22f, .008f, .012f, .24f);
                Box(-.008f, .115f, -.01f, .006f, .016f, .015f);
                Box(.008f, .115f, -.01f, .006f, .016f, .015f);
                Box(0, .107f, .53f, .008f, .016f, .018f); // front post at eye line
                if (lod < 3)
                {
                    Box(.032f, .071f, .11f, .012f, .020f, .08f); // bolt/ejection proxy
                    Box(.043f, .085f, .055f, .034f, .012f, .018f); // charging handle
                }
                if (lod < 2)
                {
                    Box(0, .003f, .053f, .008f, .050f, .009f, 0, -15); // upper trigger joins receiver; lower guard remains open
                    Box(0, .013f, .068f, .047f, .018f, .074f); // magazine well
                    for (int i = 0; i < (lod == 0 ? 6 : 3); i++)
                    {
                        float z = .24f + i * (lod == 0 ? .038f : .076f);
                        Box(-.030f, .08f, z, .006f, .045f, .015f);
                        Box(.030f, .08f, z, .006f, .045f, .015f);
                    }
                }
            }
            else
            {
                Box(0, .07f, .005f, .032f, .034f, .17f); // slide
                Box(0, .035f, .012f, .032f, .035f, .15f, 1); // frame
                Box(0, -.018f, -.030f, .032f, .11f, .045f, 1, 16); // grip
                Box(0, -.070f, -.045f, .033f, .012f, .045f); // magazine base
                Barrel(.07f, .025f, .13f, .007f);
                Box(0, -.020f, .035f, .012f, .008f, .060f); // guard lower
                Box(0, .003f, .063f, .012f, .052f, .008f); // guard front
                Box(-.008f, .095f, -.07f, .006f, .012f, .013f); // rear notch posts
                Box(.008f, .095f, -.07f, .006f, .012f, .013f);
                Box(0, .092f, .078f, .006f, .010f, .010f); // front sight
                if (lod < 3) Box(.018f, .045f, -.010f, .009f, .008f, .035f); // slide release
                if (lod < 2)
                {
                    Box(0, .010f, .030f, .007f, .030f, .009f, 0, -14); // upper trigger joins frame; lower guard remains open
                    for (int i = 0; i < (lod == 0 ? 5 : 2); i++)
                    {
                        float z = -.062f + i * (lod == 0 ? .008f : .024f);
                        Box(-.017f, .07f, z, .004f, .025f, .003f);
                        Box(.017f, .07f, z, .004f, .025f, .003f);
                    }
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
