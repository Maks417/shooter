#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>
    /// Procedural knife and grenade meshes for T087 loadout slots. Packaged as map visual props
    /// (map fingerprint) so the geometry.weapon rifle/pistol recipes stay independently versioned.
    /// </summary>
    public static class LoadoutPropGeometry
    {
        public static MeshData BuildKnife(int lod)
        {
            var assembly = new MeshAssembly();
            float bevel = lod == 0 ? .0015f : 0f;
            void Box(float x, float y, float z, float sx, float sy, float sz, ushort material = 0)
                => assembly.Add(PrimitiveGeometry.Box(new Float3(sx, sy, sz), Math.Min(bevel, Math.Min(sx, Math.Min(sy, sz)) * .2f), 1),
                    new Float3(x, y, z), Quat.Identity, material);
            // Handle (polymer) + blade (metal), grip at origin for right-hand hold.
            Box(0, 0, -.04f, .022f, .035f, .09f, 1);
            Box(0, .01f, .06f, .006f, .022f, .16f, 0);
            if (lod < 2) Box(0, .01f, .135f, .003f, .014f, .02f, 0);
            return assembly.Build();
        }

        public static MeshData BuildGrenade(int lod)
        {
            var assembly = new MeshAssembly();
            int radial = lod == 0 ? 14 : lod == 1 ? 10 : 6;
            // Body sphere approximated by a short cylinder + capped discs; spoon/lever as a thin box.
            assembly.Add(PrimitiveGeometry.Cylinder(.032f, .07f, radial), new Float3(0, 0, 0), Quat.Identity, 0);
            assembly.Add(PrimitiveGeometry.Cylinder(.018f, .025f, Math.Max(6, radial / 2)), new Float3(0, .045f, 0), Quat.Identity, 1);
            if (lod < 2)
                assembly.Add(PrimitiveGeometry.Box(new Float3(.01f, .055f, .008f), 0f, 1), new Float3(.028f, .02f, 0), Quat.Identity, 1);
            return assembly.Build();
        }
    }
}
