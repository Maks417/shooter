#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation
{
    public sealed class SoldierBuild
    {
        public SkeletonData Skeleton => SoldierSkeleton.Definition;
        public FrozenList<MeshData> Lods { get; }
        public Aabb Bounds { get; }
        internal SoldierBuild(MeshData[] lods, Aabb bounds) { Lods = new FrozenList<MeshData>(lods, 3); Bounds = bounds; }
    }

    /// <summary>Code-derived ring anatomy and rigid equipment. Closed components overlap at shoulders/hips; no authored vertex arrays.</summary>
    public static class SoldierGeometry
    {
        public static Aabb BindBounds { get; } = new Aabb(new Float3(-.85f,0,-.27f), new Float3(.85f,1.81f,.27f));
        public static SoldierBuild Build(SoldierRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            SoldierRecipe.Validate(recipe.Document);
            var lods = new MeshData[3];
            for (int lod=0; lod<3; lod++) lods[lod] = recipe.Document.GeneratorVersion==SoldierRecipe.ProfileVersion ? BuildProfile(lod) : BuildLod(lod,recipe.Document.GeneratorVersion==SoldierRecipe.GeneratorVersion);
            return new SoldierBuild(lods, BindBounds);
        }
        private static MeshData BuildLod(int lod,bool upgraded)
        {
            var b = new SkinBuilder(lod,upgraded); int torso = new[]{16,12,8}[lod], radial = new[]{12,8,6}[lod];
            // Ring stations derive from semantic pelvis/waist/chest/neck measures; no stored surface vertices.
            b.Loft(new Float3(0,.88f,0), new Float3(0,1.48f,0), new Float3(1,0,0), torso, 8,
                t => { float width = t < .35f ? Lerp(.156f,.156f,t/.35f) : t < .8f ? Lerp(.156f,.216f,(t-.35f)/.45f) : Lerp(.216f,.12f,(t-.8f)/.2f); return new Float2(width, t<.8f ? Lerp(.106f,.126f,t/.8f) : Lerp(.126f,.09f,(t-.8f)/.2f)); },
                t => TorsoSkin(Lerp(.88f,1.48f,t)), 0);
            // A rounded pelvis shell bridges jacket, belt and trouser roots, including during thigh rotation.
            Ellipsoid(b,new Float3(0,.91f,0),new Float3(.176f,.132f,.126f),torso,8,1,0);
            b.Loft(new Float3(0,1.48f,0), new Float3(0,1.59f,0), new Float3(1,0,0), radial, 3,
                t => new Float2(.064f,.069f), t => new Skin(5,6,t*.3f), 0);
            Ellipsoid(b,new Float3(0,1.66f,0),new Float3(.09f,.12f,.10f),torso,new[]{10,8,6}[lod],6,2);
            // Covered anatomy is the bounded first-slice policy: a generated cloth hood under the helmet.
            Ellipsoid(b,new Float3(0,1.652f,0),new Float3(.096f,.123f,.108f),torso,8,6,0);
            // Lower rim protects side silhouette instead of leaving an exposed upper-head block.
            b.Loft(new Float3(0,1.665f,0),new Float3(0,1.805f,0),new Float3(1,0,0),torso,6,
                t => {float r=(float)Math.Sqrt(Math.Max(.0064,1-t*t)); return new Float2(.106f*r,.116f*r);},t=>new Skin(6),1);
            b.Box(new Float3(.21f,.014f,.065f),new Float3(0,1.681f,.096f),6,1,.005f); // numeric helmet brim
            b.Box(new Float3(.165f,.042f,.025f),new Float3(0,1.655f,.103f),6,3,.008f); // grouped goggles
            b.Box(new Float3(.11f,.075f,.035f),new Float3(0,1.60f,.098f),6,0,.012f); // covered jaw
            foreach (int side in new[]{-1,1})
            {
                ushort upper=(ushort)(side<0?8:12), lower=(ushort)(upper+1), hand=(ushort)(upper+2);
                // Sleeve root lies inside the torso; its first rings inherit the chest before transitioning to humerus.
                b.Loft(new Float3(side*.145f,1.45f,0),new Float3(side*.76f,1.45f,0),new Float3(0,1,0),radial,10,
                    t=>new Float2(ArmRadius(t,upgraded)*(upgraded?(1f+.025f*(1-Math.Abs(2*(t*5-(float)Math.Floor(t*5))-1))):1f),(ArmRadius(t,upgraded))*.94f),
                    t=>t<.17f?new Skin(4,upper,Smooth(t/.17f)):LimbSkin(t,.355f/.615f,upper,lower,hand),0,-1,
                    new[]{0f,.065f/.615f,.17f,.30f,.45f,.355f/.615f,.67f,.78f,.88f,.95f,1f});
                b.Box(new Float3(.09f,.07f,.045f),new Float3(side*.797f,1.45f,.008f),hand,1,.008f); // covered palm/grouped fingers
                ushort thigh=(ushort)(side<0?15:19),calf=(ushort)(thigh+1),foot=(ushort)(thigh+2);
                b.Loft(new Float3(side*.10f,.98f,0),new Float3(side*.10f,.08f,.02f),new Float3(1,0,0),radial,10,
                    t=>new Float2(LegRadius(t,upgraded),LegRadius(t,upgraded)*.96f),
                    t=>t<.16f?new Skin(1,thigh,Smooth(t/.16f)):LimbSkin(t,.51f/.90f,thigh,calf,foot),0,-1,
                    new[]{0f,.06f/.90f,.16f,.29f,.43f,.51f/.90f,.68f,.78f,.88f,.95f,1f});
                // Rounded toe/heel and a tall cuff overlap trouser ends instead of a detached cuboid foot.
                b.Box(new Float3(.125f,.10f,.285f),new Float3(side*.10f,.05f,.10f),foot,1,.024f);
                b.Loft(new Float3(side*.10f,.045f,.015f),new Float3(side*.10f,.185f,.015f),new Float3(1,0,0),radial,4,
                    t=>new Float2(Lerp(.059f,.052f,t),Lerp(.075f,.063f,t)),t=>new Skin(foot),1);
            }
            if(upgraded)
            {
                // Contoured plate carrier with bounded edge roll; enclosed by the old plate proxy.
                b.Loft(new Float3(0,1.105f,.143f),new Float3(0,1.405f,.143f),new Float3(1,0,0),torso,6,
                    t=>new Float2(.149f-(t>.72f?(t-.72f)*.10f:0),.026f),t=>new Skin(4),1);
                for(int pouch=0;pouch<3;pouch++)
                    b.Loft(new Float3(-.09f+pouch*.09f,1.115f,.158f),new Float3(-.09f+pouch*.09f,1.245f,.158f),new Float3(1,0,0),radial,4,
                        t=>new Float2(.038f,.013f*(.8f+.2f*(1-Math.Abs(2*t-1)))),t=>new Skin(4),1);
                foreach(int side in new[]{-1,1})
                {
                    ushort arm=(ushort)(side<0?8:12);
                    b.Loft(new Float3(side*.275f,1.45f,0),new Float3(side*.305f,1.45f,0),new Float3(0,1,0),radial,2,
                        t=>new Float2(.075f,.072f),t=>new Skin(arm),4);
                    b.Box(new Float3(.048f,.004f,.058f),new Float3(side*.27f,1.525f,0),arm,5,.001f);
                    for(int finger=0;finger<4;finger++)
                        b.Loft(new Float3(side*.80f,1.439f,-.011f+finger*.012f),new Float3(side*.845f,1.439f,-.011f+finger*.012f),new Float3(0,1,0),6,3,
                            t=>new Float2(.008f*(1-t*.25f),.005f),t=>new Skin((ushort)(arm+2)),1);
                }
            }
            else b.Box(new Float3(.31f,.30f,.045f),new Float3(0,1.255f,.153f),4,1,.012f); // plate +12mm clearance
            // Versioned exterior reload task: the +Z grab face is chest-local(-.24,-.28,.20).
            b.Box(new Float3(.10f,.13f,.05f),new Float3(-.24f,1.10f,.175f),4,1,.006f);
            b.Box(new Float3(.05f,.08f,.025f),new Float3(-.178f,1.12f,.16f),4,0,.004f);
            b.Box(new Float3(.28f,.34f,.13f),new Float3(0,1.245f,-.198f),4,0,.018f); // pack
            b.Box(new Float3(.32f,.06f,.232f),new Float3(0,.96f,0),1,1); // belt
            if(lod<2) foreach(int side in new[]{-1,1})
                b.Box(new Float3(.026f,.30f,.025f),new Float3(side*.125f,1.255f,.181f),4,0); // straps
            MeshData mesh=b.Build(); GeometryCodec.EncodeMesh(mesh);
            foreach(var p in mesh.Positions)
                if(p.X<BindBounds.Min.X || p.X>BindBounds.Max.X || p.Y<BindBounds.Min.Y || p.Y>BindBounds.Max.Y || p.Z<BindBounds.Min.Z || p.Z>BindBounds.Max.Z)
                    throw new InvalidOperationException("Soldier escaped bind bounds.");
            return mesh;
        }
        /// <summary>
        /// ADR-106 soldier.loft 1.1.0: the 1.0.3 loft anatomy at higher resolution, with every box/ellipsoid stand-in
        /// replaced by kernel solids (<see cref="ShapeKit"/>): balaclava head, turned helmet with rails and NVG mount,
        /// strapped goggles, gloved hands, shaped boots with soles, a plate carrier with pouches, webbing and straps,
        /// backpack, belt, cargo pockets and knee pads. Slots: 0 uniform, 1 gear, 3 goggles/buckle, 4 armband, 5 patch.
        /// </summary>
        private static MeshData BuildProfile(int lod)
        {
            var b = new SkinBuilder(lod, true);
            int torso = new[] { 24, 14, 8 }[lod], radial = new[] { 16, 10, 6 }[lod];
            int arc = new[] { 3, 2, 1 }[lod], edge = new[] { 2, 1, 0 }[lod], tube = new[] { 12, 8, 6 }[lod], loft = new[] { 20, 12, 8 }[lod], lathe = new[] { 24, 14, 8 }[lod];
            bool detail = lod < 2, fine = lod == 0;
            var zToX = new Quat(0, .70710677f, 0, .70710677f); var xToZ = new Quat(0, -.70710677f, 0, .70710677f); var zToY = new Quat(-.70710677f, 0, 0, .70710677f);
            Func<Float3, Skin> Rigid(ushort joint) => p => new Skin(joint);
            Func<Float3, Skin> torsoSkin = p => TorsoSkin(p.Y);
            void Part(MeshData mesh, Float3 at, Quat rotation, Func<Float3, Skin> skin, ushort slot) => b.Mesh(mesh, at, rotation, skin, slot);
            MeshData Slab(float width, float roll, params float[] zyr)
            {
                var corners = new ProfileCorner[zyr.Length / 3];
                for (int i = 0; i < corners.Length; i++) corners[i] = new ProfileCorner(zyr[3 * i], zyr[3 * i + 1], zyr[3 * i + 2]);
                return ShapeKit.Slab(corners, width, roll, arc, edge);
            }
            ShapeKit.Section S(float z, float y, float halfWidth, float halfHeight, float squareness = 3) => new ShapeKit.Section(z, y, halfWidth, halfHeight, squareness);

            // ---- body: 1.0.3 anatomy, finer.
            b.Loft(new Float3(0, .88f, 0), new Float3(0, 1.48f, 0), new Float3(1, 0, 0), torso, fine ? 12 : 8,
                t => { float width = t < .35f ? .156f : t < .8f ? Lerp(.156f, .216f, (t - .35f) / .45f) : Lerp(.216f, .12f, (t - .8f) / .2f); return new Float2(width, t < .8f ? Lerp(.106f, .126f, t / .8f) : Lerp(.126f, .09f, (t - .8f) / .2f)); },
                t => TorsoSkin(Lerp(.88f, 1.48f, t)), 0);
            Ellipsoid(b, new Float3(0, .91f, 0), new Float3(.176f, .132f, .126f), torso, 8, 1, 0);
            b.Loft(new Float3(0, 1.47f, 0), new Float3(0, 1.575f, .004f), new Float3(1, 0, 0), radial, 3, t => new Float2(Lerp(.066f, .056f, t), Lerp(.070f, .060f, t)), t => new Skin(5, 6, t * .3f), 1);
            foreach (int side in new[] { -1, 1 })
            {
                ushort upper = (ushort)(side < 0 ? 8 : 12), lower = (ushort)(upper + 1), hand = (ushort)(upper + 2);
                b.Loft(new Float3(side * .145f, 1.45f, 0), new Float3(side * .76f, 1.45f, 0), new Float3(0, 1, 0), radial, 10,
                    t => new Float2(ArmRadius(t, true) * (1f + .025f * (1 - Math.Abs(2 * (t * 5 - (float)Math.Floor(t * 5)) - 1))), ArmRadius(t, true) * .94f),
                    t => t < .17f ? new Skin(4, upper, Smooth(t / .17f)) : LimbSkin(t, .355f / .615f, upper, lower, hand), 0, -1,
                    new[] { 0f, .065f / .615f, .17f, .30f, .45f, .355f / .615f, .67f, .78f, .88f, .95f, 1f });
                ushort thigh = (ushort)(side < 0 ? 15 : 19), calf = (ushort)(thigh + 1), foot = (ushort)(thigh + 2), toe = (ushort)(thigh + 3);
                b.Loft(new Float3(side * .10f, .98f, 0), new Float3(side * .10f, .08f, .02f), new Float3(1, 0, 0), radial, 10,
                    t => new Float2(LegRadius(t, true), LegRadius(t, true) * .96f),
                    t => t < .16f ? new Skin(1, thigh, Smooth(t / .16f)) : LimbSkin(t, .51f / .90f, thigh, calf, foot), 0, -1,
                    new[] { 0f, .06f / .90f, .16f, .29f, .43f, .51f / .90f, .68f, .78f, .88f, .95f, 1f });

                // Gloved hand: moulded palm, four curled fingers and a thumb (a mitten at the far LOD).
                var at = new Float3(side * .757f, 1.448f, 0); var along = side > 0 ? zToX : xToZ;
                Part(ShapeKit.Loft(detail
                    ? new[] { S(0, 0, .038f, .0150f), S(.020f, 0, .043f, .0165f), S(.045f, .001f, .042f, .0155f), S(.058f, .001f, .038f, .0120f) }
                    : new[] { S(0, 0, .038f, .0150f), S(.030f, 0, .043f, .0160f), S(.070f, -.004f, .040f, .0140f), S(.084f, -.010f, .030f, .0100f) }, Math.Max(8, loft)),
                    at, along, Rigid(hand), 1);
                if (detail)
                {
                    float[] fz = { -.027f, -.009f, .009f, .027f }; float[] fr = { .0082f, .0086f, .0084f, .0074f };
                    for (int f = 0; f < 4; f++)
                    {
                        var path = ShapeKit.Spline(new[] { new Float3(side * .800f, 1.449f, fz[f]), new Float3(side * .823f, 1.446f, fz[f]),
                            new Float3(side * .838f, 1.433f, fz[f]), new Float3(side * .841f, 1.416f, fz[f]) }, fine ? 3 : 2);
                        var radii = new Float2[path.Count]; for (int i = 0; i < radii.Length; i++) { float k = 1 - .18f * i / (radii.Length - 1); radii[i] = new Float2(fr[f] * k, fr[f] * .9f * k); }
                        Part(ShapeKit.Tube(path, radii, new Float3(0, 0, 1), tube, 2), new Float3(0, 0, 0), Quat.Identity, Rigid(hand), 1);
                    }
                    var thumb = ShapeKit.Spline(new[] { new Float3(side * .772f, 1.444f, .030f), new Float3(side * .795f, 1.438f, .050f), new Float3(side * .818f, 1.430f, .055f) }, fine ? 3 : 2);
                    var thumbRadii = new Float2[thumb.Count]; for (int i = 0; i < thumbRadii.Length; i++) { float k = 1 - .2f * i / (thumbRadii.Length - 1); thumbRadii[i] = new Float2(.0100f * k, .0090f * k); }
                    Part(ShapeKit.Tube(thumb, thumbRadii, new Float3(0, 1, 0), tube, 2), new Float3(0, 0, 0), Quat.Identity, Rigid(hand), 1);
                }

                // Boot: shaped upper, cuff over the trouser hem and a moulded sole with toe spring.
                Func<Float3, Skin> bootSkin = p => p.Z < .10f ? new Skin(foot) : new Skin(foot, toe, Smooth((p.Z - .10f) / .10f));
                Part(ShapeKit.Loft(new[] { S(-.070f, .062f, .046f, .056f), S(-.050f, .066f, .053f, .062f), S(.020f, .062f, .056f, .060f), S(.110f, .050f, .054f, .046f),
                    S(.180f, .040f, .049f, .034f), S(.222f, .034f, .040f, .024f), S(.238f, .032f, .029f, .017f) }, Math.Max(8, loft)), new Float3(side * .10f, 0, 0), Quat.Identity, bootSkin, 1);
                Part(Slab(.114f, .006f, -.075f, .000f, .010f, .198f, .000f, .020f, .243f, .011f, .012f, .238f, .020f, .008f, -.075f, .021f, .010f), new Float3(side * .10f, 0, 0), Quat.Identity, bootSkin, 1);
                b.Loft(new Float3(side * .10f, .045f, .015f), new Float3(side * .10f, .195f, .015f), new Float3(1, 0, 0), radial, 4,
                    t => new Float2(Lerp(.060f, .054f, t), Lerp(.076f, .065f, t)), t => new Skin(foot), 1);
                // Cargo pocket and knee pad.
                Part(Slab(.030f, .008f, -.050f, .600f, .012f, .050f, .600f, .012f, .050f, .722f, .010f, -.050f, .726f, .010f), new Float3(side * .190f, 0, .004f), Quat.Identity, Rigid(thigh), 0);
                Part(Slab(.030f, .010f, -.045f, .410f, .020f, .045f, .410f, .020f, .048f, .540f, .022f, -.048f, .540f, .022f), new Float3(side * .10f, 0, .070f), xToZ, Rigid(calf), 1);
            }

            // ---- head: balaclava, nose, turned helmet, rails, NVG mount, goggles with strap.
            b.Loft(new Float3(0, 1.540f, .014f), new Float3(0, 1.785f, -.006f), new Float3(1, 0, 0), torso, fine ? 14 : 8, HeadRadius, t => t < .12f ? new Skin(5, 6, t / .12f) : new Skin(6), 1);
            if (detail) Part(Slab(.020f, .004f, -.010f, -.024f, .004f, .016f, -.018f, .006f, .012f, .006f, .006f, -.010f, .016f, .004f), new Float3(0, 1.636f, .094f), Quat.Identity, Rigid(6), 1);
            Part(ShapeKit.Lathe(new[] { new Float2(.1170f, 0), new Float2(.1178f, .004f), new Float2(.1135f, .010f), new Float2(.1125f, .034f), new Float2(.1065f, .062f),
                new Float2(.0925f, .088f), new Float2(.0705f, .108f), new Float2(.0425f, .120f), new Float2(.0150f, .1255f) }, lathe), new Float3(0, 1.672f, -.004f), zToY, Rigid(6), 0);
            if (detail)
            {
                Part(Slab(.040f, .003f, -.004f, -.020f, .004f, .013f, -.016f, .004f, .013f, .016f, .004f, -.004f, .020f, .004f), new Float3(0, 1.735f, .100f), Quat.Identity, Rigid(6), 1);
                foreach (int side in new[] { -1, 1 })
                    Part(Slab(.006f, .0012f, -.034f, -.006f, .003f, .034f, -.006f, .003f, .034f, .006f, .003f, -.034f, .006f, .003f), new Float3(side * .110f, 1.703f, -.004f), Quat.Identity, Rigid(6), 1);
            }
            Float3 Arc(float a, float bz, float degrees, float y) { double r = degrees * Math.PI / 180; return new Float3((float)(a * Math.Cos(r)), y, (float)(bz * Math.Sin(r))); }
            var strap = new List<Float3>(); for (int d = 30; d >= -210; d -= fine ? 15 : 30) strap.Add(Arc(.088f, .104f, d, 1.652f));
            var strapRadii = new Float2[strap.Count]; for (int i = 0; i < strapRadii.Length; i++) strapRadii[i] = new Float2(.0105f, .0026f);
            Part(ShapeKit.Tube(strap, strapRadii, new Float3(0, 1, 0), 6, 0), new Float3(0, 0, 0), Quat.Identity, Rigid(6), 1);
            var frame = new List<Float3>(); for (int d = 28; d <= 152; d += fine ? 8 : 31) frame.Add(Arc(.091f, .109f, d, 1.652f));
            var frameRadii = new Float2[frame.Count]; for (int i = 0; i < frameRadii.Length; i++) frameRadii[i] = new Float2(.0190f, .0095f);
            Part(ShapeKit.Tube(frame, frameRadii, new Float3(0, 1, 0), tube, 2), new Float3(0, 0, 0), Quat.Identity, Rigid(6), 1);
            var lens = new List<Float3>(); for (int d = 38; d <= 142; d += fine ? 8 : 26) lens.Add(Arc(.095f, .116f, d, 1.653f));
            var lensRadii = new Float2[lens.Count]; for (int i = 0; i < lensRadii.Length; i++) lensRadii[i] = new Float2(.0150f, .0050f);
            Part(ShapeKit.Tube(lens, lensRadii, new Float3(0, 1, 0), tube, 2), new Float3(0, 0, 0), Quat.Identity, Rigid(6), 3);

            // ---- plate carrier: front plate, cummerbund, webbing, magazine pouches, straps; backpack and belt.
            Part(Slab(.040f, .010f, -.150f, 1.105f, .025f, .150f, 1.105f, .025f, .150f, 1.330f, .030f, .100f, 1.405f, .020f, -.100f, 1.405f, .020f, -.150f, 1.330f, .030f),
                new Float3(0, 0, .148f), xToZ, torsoSkin, 1);
            b.Loft(new Float3(0, 1.095f, 0), new Float3(0, 1.245f, 0), new Float3(1, 0, 0), torso, 3, t => new Float2(.181f, .129f), t => TorsoSkin(Lerp(1.095f, 1.245f, t)), 1);
            if (detail)
                for (int k = 0; k < 3; k++)
                {
                    float y0 = 1.272f + .040f * k;
                    if (y0 + .018f > 1.392f) break;
                    Part(Slab(.005f, .0012f, -.128f, y0, .002f, .128f, y0, .002f, .128f, y0 + .018f, .002f, -.128f, y0 + .018f, .002f), new Float3(0, 0, .169f), xToZ, torsoSkin, 1);
                }
            for (int k = -1; k <= 1; k++)
            {
                Part(Slab(.046f, .010f, -.034f, 1.105f, .010f, .034f, 1.105f, .010f, .034f, 1.235f, .012f, -.034f, 1.235f, .012f), new Float3(k * .078f, 0, .190f), xToZ, torsoSkin, 1);
                if (detail) Part(Slab(.050f, .006f, -.036f, 1.215f, .006f, .036f, 1.215f, .006f, .036f, 1.248f, .008f, -.036f, 1.250f, .008f), new Float3(k * .078f, 0, .192f), xToZ, torsoSkin, 0);
            }
            // Versioned exterior reload task: the +Z grab face is chest-local(-.24,-.28,.20).
            Part(Slab(.050f, .010f, -.050f, 1.035f, .012f, .050f, 1.035f, .012f, .050f, 1.165f, .012f, -.050f, 1.165f, .012f), new Float3(-.240f, 0, .175f), xToZ, torsoSkin, 1);
            foreach (int side in new[] { -1, 1 })
            {
                var path = ShapeKit.Spline(new[] { new Float3(side * .105f, 1.395f, .150f), new Float3(side * .122f, 1.500f, .095f), new Float3(side * .126f, 1.535f, 0),
                    new Float3(side * .122f, 1.500f, -.095f), new Float3(side * .105f, 1.395f, -.150f) }, fine ? 4 : 2);
                var radii = new Float2[path.Count]; for (int i = 0; i < radii.Length; i++) radii[i] = new Float2(.026f, .0070f);
                Part(ShapeKit.Tube(path, radii, new Float3(1, 0, 0), Math.Max(6, tube - 4), 0), new Float3(0, 0, 0), Quat.Identity, torsoSkin, 1);
            }
            Part(Slab(.120f, .030f, -.150f, 1.080f, .040f, .150f, 1.080f, .040f, .155f, 1.380f, .050f, -.155f, 1.380f, .050f), new Float3(0, 0, -.205f), xToZ, torsoSkin, 1);
            if (detail) Part(Slab(.112f, .016f, -.140f, 1.355f, .030f, .140f, 1.355f, .030f, .136f, 1.402f, .022f, -.136f, 1.402f, .022f), new Float3(0, 0, -.206f), xToZ, torsoSkin, 0);
            b.Loft(new Float3(0, .930f, 0), new Float3(0, .990f, 0), new Float3(1, 0, 0), torso, 2, t => new Float2(.175f, .129f), t => new Skin(1), 1);
            Part(Slab(.010f, .002f, -.028f, .938f, .004f, .028f, .938f, .004f, .028f, .982f, .004f, -.028f, .982f, .004f), new Float3(0, 0, .131f), xToZ, Rigid(1), 3);

            // ---- identification: armband and shoulder patch (team materials).
            foreach (int side in new[] { -1, 1 })
            {
                ushort arm = (ushort)(side < 0 ? 8 : 12);
                b.Loft(new Float3(side * .275f, 1.45f, 0), new Float3(side * .305f, 1.45f, 0), new Float3(0, 1, 0), radial, 2, t => new Float2(.075f, .072f), t => new Skin(arm), 4);
                b.Box(new Float3(.048f, .004f, .058f), new Float3(side * .27f, 1.525f, 0), arm, 5, .001f);
            }
            MeshData result = b.Build(); GeometryCodec.EncodeMesh(result);
            foreach (var p in result.Positions)
                if (p.X < BindBounds.Min.X || p.X > BindBounds.Max.X || p.Y < BindBounds.Min.Y || p.Y > BindBounds.Max.Y || p.Z < BindBounds.Min.Z || p.Z > BindBounds.Max.Z)
                    throw new InvalidOperationException("Soldier escaped bind bounds.");
            return result;
        }

        /// <summary>Balaclava head profile from under the chin (t = 0) to the crown (t = 1): (half width, half depth).</summary>
        private static Float2 HeadRadius(float t)
        {
            float[] ts = { 0, .06f, .20f, .38f, .56f, .80f, .93f, 1 };
            float[] xs = { .040f, .060f, .071f, .079f, .082f, .074f, .053f, .016f };
            float[] zs = { .052f, .078f, .094f, .103f, .105f, .095f, .069f, .022f };
            for (int i = 0; i + 1 < ts.Length; i++)
                if (t <= ts[i + 1])
                {
                    float u = Smooth((t - ts[i]) / (ts[i + 1] - ts[i]));
                    return new Float2(Lerp(xs[i], xs[i + 1], u), Lerp(zs[i], zs[i + 1], u));
                }
            return new Float2(xs[xs.Length - 1], zs[zs.Length - 1]);
        }

        private static float ArmRadius(float t,bool deterministic)
        {
            const float elbow=.355f/.615f;
            float radius=t<elbow?Lerp(.080f,.054f,t/elbow):Lerp(.054f,.039f,(t-elbow)/(1-elbow));
            return radius+.006f*(deterministic?(float)StableSin(Math.PI*Math.Min(1,t/elbow)):(float)Math.Sin(Math.PI*Math.Min(1,t/elbow)));
        }
        private static float LegRadius(float t,bool deterministic)
        {
            const float knee=.51f/.90f;
            float radius=t<knee?Lerp(.108f,.073f,t/knee):Lerp(.073f,.054f,(t-knee)/(1-knee));
            // Broad analytic cloth fullness, not image displacement or authored folds.
            return radius+.010f*(deterministic?(float)StableSin(Math.PI*Math.Min(1,t/knee)):(float)Math.Sin(Math.PI*Math.Min(1,t/knee)));
        }
        private static double StableSin(double angle)
        {
            double tau=6.2831853071795864769;angle-=Math.Floor((angle+3.14159265358979323846)/tau)*tau;
            double square=angle*angle,term=angle,sum=angle;
            for(int i=1;i<=13;i++){term=term*(-square)/((2*i)*(2*i+1));sum=sum+term;}
            return sum;
        }
        private static float Smooth(float t) {t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        private static float Lerp(float a,float c,float t)=>a+(c-a)*t;
        private static Skin TorsoSkin(float height)
        {
            if(height<=.96f)return new Skin(1);
            if(height<1.1f)return new Skin(1,2,(height-.96f)/.14f);
            if(height<1.24f)return new Skin(2,3,(height-1.1f)/.14f);
            return new Skin(3,4,Math.Min(1,(height-1.24f)/.14f));
        }
        private static Skin LimbSkin(float t,float joint,ushort first,ushort second,ushort end)
        {
            const float blend=.16f;
            if(t<joint-blend)return new Skin(first);
            if(t<joint+blend)return new Skin(first,second,(t-joint+blend)/(2*blend));
            if(t<.88f)return new Skin(second);
            return new Skin(second,end,(t-.88f)/.12f*.4f);
        }
        private static void Ellipsoid(SkinBuilder b,Float3 center,Float3 radii,int radial,int rings,ushort joint,ushort slot)
        {
            // Tiny circular end caps avoid zero-area pole triangles at all LODs.
            b.Loft(new Float3(center.X,center.Y-radii.Y,center.Z),new Float3(center.X,center.Y+radii.Y,center.Z),new Float3(1,0,0),radial,rings,
                t=>{float y=2*t-1; float f=(float)Math.Sqrt(Math.Max(.0025,1-y*y));return new Float2(radii.X*f,radii.Z*f);},t=>new Skin(joint),slot);
        }
        private readonly struct Skin
        {
            internal readonly ushort A,B; internal readonly float Blend;
            internal Skin(ushort a) {A=a;B=a;Blend=0;}
            internal Skin(ushort a,ushort b,float blend) {A=a;B=b;Blend=blend;}
        }
        private sealed class SkinBuilder
        {
            private readonly int lod;private readonly bool deterministic;
            private readonly List<Float3> positions=new List<Float3>(),normals=new List<Float3>();
            private readonly List<Float4> tangents=new List<Float4>(),weights=new List<Float4>();
            private readonly List<Float2> uv=new List<Float2>();
            private readonly List<UInt16x4> joints=new List<UInt16x4>();
            private readonly SortedDictionary<ushort,List<uint>> groups=new SortedDictionary<ushort,List<uint>>();
            internal SkinBuilder(int lod,bool deterministic){this.lod=lod;this.deterministic=deterministic;}
            private ushort Remap(ushort id)
            {
                if(lod<2)return id;
                // nearest retained semantic parent: remove spine0,spine1,neck,clavicles,toes =>16 influencing joints.
                while(id==2 || id==3 || id==5 || id==7 || id==11 || id==18 || id==22)id=SoldierSkeleton.Definition.Joints[id].ParentId;
                return id;
            }
            private uint Vertex(Float3 p,Float3 n,Float3 tangent,Float2 tex,Skin skin)
            {
                // Version 1.0.3 publishes a numeric 10-micrometre lattice. Intermediate
                // radius samples use the same lattice before finite differences, avoiding
                // runtime float contraction magnification in surface normals. Legacy bytes stay frozen.
                if(deterministic){p=Canonical(p);n=Canonical(n);tangent=Canonical(tangent);tex=new Float2(Canonical(tex.X),Canonical(tex.Y));}
                uint index=(uint)positions.Count; positions.Add(p);normals.Add(n);tangents.Add(new Float4(tangent.X,tangent.Y,tangent.Z,1));uv.Add(tex);
                ushort a=Remap(skin.A),c=Remap(skin.B); float blend=Math.Max(0,Math.Min(1,skin.Blend));
                if(deterministic)blend=Canonical(blend);
                joints.Add(new UInt16x4(a,c,0,0)); weights.Add(new Float4(1-blend,blend,0,0)); return index;
            }
            private static float Canonical(float value){double rounded=Math.Round((double)value*100000,MidpointRounding.ToEven);return rounded==0?0f:(float)(rounded/100000);}
            private static Float3 Canonical(Float3 value)=>new Float3(Canonical(value.X),Canonical(value.Y),Canonical(value.Z));
            private static Float2 Canonical(Float2 value)=>new Float2(Canonical(value.X),Canonical(value.Y));
            private void Triangle(ushort slot,uint a,uint c,uint d)
            {if(!groups.TryGetValue(slot,out var g)){g=new List<uint>();groups.Add(slot,g);}g.Add(a);g.Add(c);g.Add(d);}
            internal void Box(Float3 size,Float3 center,ushort joint,ushort slot,float bevel=0)
            {
                MeshData box=PrimitiveGeometry.Box(size,bevel);uint offset=(uint)positions.Count;
                for(int i=0;i<box.Positions.Count;i++){var t=box.Tangents[i]; uint id=Vertex(GeometryMath.Add(center,box.Positions[i]),box.Normals[i],new Float3(t.X,t.Y,t.Z),box.Uv[i],new Skin(joint));tangents[(int)id]=t;}
                for(int i=0;i<box.Indices.Count;i+=3)Triangle(slot,offset+box.Indices[i],offset+box.Indices[i+1],offset+box.Indices[i+2]);
            }
            /// <summary>ADR-106: adds a closed kernel solid, rotated then translated, skinned per vertex from its bind position.</summary>
            internal void Mesh(MeshData part,Float3 at,Quat rotation,Func<Float3,Skin> skin,ushort slot)
            {
                uint offset=(uint)positions.Count;
                for(int i=0;i<part.Positions.Count;i++)
                {
                    var p=GeometryMath.Add(GeometryMath.Rotate(part.Positions[i],rotation),at);
                    var n=GeometryMath.Normalize(GeometryMath.Rotate(part.Normals[i],rotation));
                    var t4=part.Tangents[i];var t=GeometryMath.Normalize(GeometryMath.Rotate(new Float3(t4.X,t4.Y,t4.Z),rotation));
                    uint id=Vertex(p,n,t,part.Uv[i],skin(p));
                    // Re-orthogonalise the published frame after lattice rounding and keep the source handedness.
                    var nn=normals[(int)id];var tt=GeometryMath.Normalize(GeometryMath.Sub(t,GeometryMath.Scale(nn,GeometryMath.Dot(t,nn))));
                    tangents[(int)id]=new Float4(tt.X,tt.Y,tt.Z,t4.W);
                }
                for(int i=0;i<part.Indices.Count;i+=3)Triangle(slot,offset+part.Indices[i],offset+part.Indices[i+1],offset+part.Indices[i+2]);
            }
            internal void Loft(Float3 start,Float3 end,Float3 u,int radial,int rings,Func<float,Float2> radius,Func<float,Skin> skin,ushort slot,float seam=-1,float[]? stations=null)
            {
                Float3 delta=GeometryMath.Sub(end,start);float length=(float)Math.Sqrt(delta.LengthSquared); Float3 axis=GeometryMath.Normalize(delta);
                u=GeometryMath.Normalize(GeometryMath.Sub(u,GeometryMath.Scale(axis,GeometryMath.Dot(u,axis))));
                Float3 v=new Float3(u.Y*axis.Z-u.Z*axis.Y,u.Z*axis.X-u.X*axis.Z,u.X*axis.Y-u.Y*axis.X);
                uint first=(uint)positions.Count;
                for(int ring=0;ring<=rings;ring++)
                {
                    float t=stations!=null?stations[ring]:seam<0?ring/(float)rings:ring<=rings/2?seam*ring/(rings/2):seam+(1-seam)*(ring-rings/2)/(rings-rings/2);var r=radius(t);float lo=Math.Max(0,t-.001f),hi=Math.Min(1,t+.001f);var ra=radius(lo);var rb=radius(hi);
                    if(deterministic){r=Canonical(r);ra=Canonical(ra);rb=Canonical(rb);}
                    float dx=(rb.X-ra.X)/((hi-lo)*length),dz=(rb.Y-ra.Y)/((hi-lo)*length);
                    var center=GeometryMath.Add(start,GeometryMath.Scale(delta,t));
                    for(int i=0;i<=radial;i++)
                    {
                        double angle=i==radial?0:2*Math.PI*i/radial;float cos=(float)(deterministic?StableSin(angle+Math.PI*.5):Math.Cos(angle)),sin=(float)(deterministic?StableSin(angle):Math.Sin(angle));
                        Float3 normal=GeometryMath.Add(GeometryMath.Scale(u,cos/r.X),GeometryMath.Scale(v,sin/r.Y));
                        normal=GeometryMath.Normalize(GeometryMath.Sub(normal,GeometryMath.Scale(axis,dx*cos*cos/r.X+dz*sin*sin/r.Y)));
                        Float3 tangent=GeometryMath.Add(GeometryMath.Scale(u,-r.X*sin),GeometryMath.Scale(v,r.Y*cos));
                        if(deterministic)
                        {
                            // Calculate the projected frame in explicit double precision, then
                            // publish the same lattice as positions/normals; no runtime SIMD/FMA drift.
                            var nn=Canonical(normal);double x=(double)u.X*(-(double)r.X*sin)+(double)v.X*r.Y*cos;
                            double y=(double)u.Y*(-(double)r.X*sin)+(double)v.Y*r.Y*cos;
                            double z=(double)u.Z*(-(double)r.X*sin)+(double)v.Z*r.Y*cos;
                            double dot=x*nn.X+y*nn.Y+z*nn.Z;x-=nn.X*dot;y-=nn.Y*dot;z-=nn.Z*dot;
                            double norm=Math.Sqrt(x*x+y*y+z*z);tangent=new Float3((float)(x/norm),(float)(y/norm),(float)(z/norm));
                        }
                        else tangent=GeometryMath.Normalize(GeometryMath.Sub(tangent,GeometryMath.Scale(normal,GeometryMath.Dot(tangent,normal))));
                        Float3 p=GeometryMath.Add(center,GeometryMath.Add(GeometryMath.Scale(u,r.X*cos),GeometryMath.Scale(v,r.Y*sin)));
                        Vertex(p,normal,tangent,new Float2((float)(2*Math.PI*i/radial*Math.Sqrt(deterministic?((double)r.X*r.X+(double)r.Y*r.Y)*.5:(r.X*r.X+r.Y*r.Y)*.5)),deterministic?(float)((double)Canonical(t)*Canonical(length)):t*length),skin(t));
                    }
                }
                for(int ring=0;ring<rings;ring++)for(int i=0;i<radial;i++)
                {uint a=first+(uint)(ring*(radial+1)+i),c=a+(uint)(radial+1);Triangle(slot,a,c,a+1);Triangle(slot,a+1,c,c+1);}
                for(int cap=0;cap<2;cap++)
                {
                    float t=cap; var center=cap==0?start:end; var r=radius(t);var n=GeometryMath.Scale(axis,cap==0?-1:1);
                    uint mid=Vertex(center,n,u,new Float2(0,0),skin(t));uint rim=(uint)positions.Count;
                    for(int i=0;i<=radial;i++)
                    {double a=i==radial?0:2*Math.PI*i/radial;float x=r.X*(float)(deterministic?StableSin(a+Math.PI*.5):Math.Cos(a)),z=r.Y*(float)(deterministic?StableSin(a):Math.Sin(a));Vertex(GeometryMath.Add(center,GeometryMath.Add(GeometryMath.Scale(u,x),GeometryMath.Scale(v,z))),n,u,new Float2(x,z),skin(t));}
                    for(int i=0;i<radial;i++)if(cap==0)Triangle(slot,mid,rim+(uint)i,rim+(uint)i+1);else Triangle(slot,mid,rim+(uint)i+1,rim+(uint)i);
                }
            }
            internal MeshData Build()
            {
                var indices=new List<uint>();var subs=new List<Submesh>();
                foreach(var group in groups){subs.Add(new Submesh((uint)indices.Count,(uint)group.Value.Count,group.Key));indices.AddRange(group.Value);}
                return new MeshData(positions,normals,tangents,uv,indices,subs,joints,weights);
            }
        }
    }
}
