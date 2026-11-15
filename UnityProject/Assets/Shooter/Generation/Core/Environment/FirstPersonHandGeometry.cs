#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>Offline camera-only glove/sleeve meshes. Does not modify soldier anatomy, hit poses or weapon geometry.</summary>
    public static class FirstPersonHandGeometry
    {
        public const int MaxVerticesPerHand = 6000;

        public static MeshData Build(bool pistol, bool support)
        {
            var mesh = new MeshAssembly();
            float side = support ? -1f : 1f;
            float z = support && !pistol ? .34f : -.015f;
            float palmY = support && !pistol ? .012f : -.026f;
            var palm = new Float3(side * .039f, palmY, z - .013f);
            var wrist = new Float3(side * .052f, palmY - .065f, z - .04f);
            var elbow = new Float3(side * (support ? .24f : .18f), -.24f, -.29f);
            // Slightly bent cloth profile and shallow folds replace a single smooth cone.
            Float3 SleevePoint(float t) => new Float3(elbow.X + (wrist.X - elbow.X) * t,
                elbow.Y + (wrist.Y - elbow.Y) * t + .012f * (float)Math.Sin(Math.PI * t),
                elbow.Z + (wrist.Z - elbow.Z) * t);
            float Radius(float t) => .055f - .023f * t + .0028f * (float)(Math.Sin(5 * Math.PI * t) * Math.Sin(Math.PI * t));
            float clothDistance = 0f;
            for (int fold = 0; fold < 8; fold++)
            {
                float a = fold / 8f, b = (fold + 1) / 8f;
                var start = SleevePoint(a); var end = SleevePoint(b);
                Segment(mesh, start, end, Radius(a), Radius(b), 0, clothDistance);
                clothDistance += (float)Math.Sqrt(GeometryMath.Sub(end, start).LengthSquared);
            }
            Segment(mesh, new Float3(wrist.X, wrist.Y - .018f, wrist.Z - .012f), wrist, .036f, .035f, 2);
            Box(mesh, palm, new Float3(.039f, .085f, .062f), .012f, 1);
            Box(mesh, new Float3(side * .062f, palmY + .012f, z - .012f), new Float3(.009f, .036f, .042f), .003f, 2);
            // Individual curved fingers wrap the grip. Index rests alongside the trigger guard.
            for (int finger = 0; finger < 4; finger++)
            {
                float y = palmY + .029f - finger * .019f;
                var a = new Float3(side * .047f, y, z + .005f);
                var b = new Float3(side * .022f, y + .002f, z + .035f);
                var c = new Float3(-side * .006f, y + .004f, z + .020f);
                if (!support && finger == 0)
                {
                    b = new Float3(side * .037f, y, z + .052f);
                    c = new Float3(side * .030f, y, z + .092f);
                }
                Segment(mesh, a, b, .010f, .009f, 1);
                Segment(mesh, b, c, .009f, .0075f, 1);
                Box(mesh, a, new Float3(.018f, .012f, .017f), .004f, 2);
            }
            var thumb = new Float3(side * .046f, palmY + .034f, z - .035f);
            var thumbJoint = new Float3(side * .016f, palmY + .058f, z - .020f);
            Segment(mesh, thumb, thumbJoint, .012f, .010f, 1);
            Segment(mesh, thumbJoint, new Float3(-side * .008f, palmY + .053f, z + .008f), .010f, .008f, 1);
            if (mesh.VertexCount > MaxVerticesPerHand) throw new InvalidOperationException("First-person hand vertex budget exceeded.");
            return mesh.Build();
        }

        private static void Box(MeshAssembly mesh, Float3 position, Float3 size, float bevel, ushort slot)
            => mesh.Add(PrimitiveGeometry.Box(size, bevel), position, Quat.Identity, slot);

        private static void Segment(MeshAssembly mesh, Float3 a, Float3 b, float radiusA, float radiusB, ushort slot, float uvOffset = 0f)
        {
            float x = b.X - a.X, y = b.Y - a.Y, z = b.Z - a.Z;
            float length = (float)Math.Sqrt(x * x + y * y + z * z);
            float dx = x / length, dy = y / length, dz = z / length;
            Quat rotation;
            if (dz < -.9999f) rotation = new Quat(0, 1, 0, 0);
            else
            {
                float w = 1f + dz;
                float norm = (float)Math.Sqrt(dx * dx + dy * dy + w * w);
                rotation = new Quat(-dy / norm, dx / norm, 0, w / norm);
            }
            var surface = PrimitiveGeometry.Cone(radiusA, radiusB, length, 12);
            if (uvOffset != 0f)
            {
                var uv = new Float2[surface.Uv.Count];
                for (int i = 0; i < uv.Length; i++) uv[i] = new Float2(surface.Uv[i].X, surface.Uv[i].Y + uvOffset);
                surface = new MeshData(surface.Positions, surface.Normals, surface.Tangents, uv, surface.Indices, surface.Submeshes);
            }
            mesh.Add(surface,
                new Float3((a.X + b.X) * .5f, (a.Y + b.Y) * .5f, (a.Z + b.Z) * .5f), rotation, slot);
        }
    }
}
