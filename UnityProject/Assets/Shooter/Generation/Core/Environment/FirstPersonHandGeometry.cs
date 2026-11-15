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
            float sleeveLength;
            Quat sleeveRotation = Align(elbow, wrist, out sleeveLength);
            mesh.Add(BuildSleeve(sleeveLength), new Float3((elbow.X + wrist.X) * .5f,
                (elbow.Y + wrist.Y) * .5f, (elbow.Z + wrist.Z) * .5f), sleeveRotation, 0);
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

        /// <summary>One welded ring loft with smooth tangent frames; no stacked-cone cap seams.</summary>
        public static MeshData BuildSleeve(float length)
        {
            if (!float.IsFinite(length) || length < .1f || length > .8f) throw new ArgumentOutOfRangeException(nameof(length));
            const int rings = 16, radial = 16;
            var mesh = new GeometryBuffer();
            float Radius(double t) => (float)(.055 - .023 * t + .0028 * Math.Sin(5 * Math.PI * t) * Math.Sin(Math.PI * t));
            for (int ring = 0; ring <= rings; ring++)
            {
                double t = (double)ring / rings;
                float radius = Radius(t);
                float bend = ring == 0 || ring == rings ? 0 : .012f * (float)Math.Sin(Math.PI * t);
                double slope = -.023 + .0028 * (5 * Math.PI * Math.Cos(5 * Math.PI * t) * Math.Sin(Math.PI * t) +
                    Math.PI * Math.Sin(5 * Math.PI * t) * Math.Cos(Math.PI * t));
                double bendSlope = .012 * Math.PI * Math.Cos(Math.PI * t);
                for (int i = 0; i <= radial; i++)
                {
                    double angle = 2 * Math.PI * i / radial;
                    float c = i == radial ? 1f : (float)Math.Cos(angle), sine = i == radial ? 0f : (float)Math.Sin(angle);
                    var normal = GeometryMath.Normalize(new Float3(c, sine, (float)(-(slope + bendSlope * sine) / length)));
                    mesh.Vertex(new Float3(radius * c, radius * sine + bend, (float)t * length - length * .5f), normal,
                        new Float4(-sine, c, 0, 1), new Float2((float)(2 * Math.PI * .055 * i / radial), (float)t * length));
                }
            }
            for (int ring = 0; ring < rings; ring++)
                for (int i = 0; i < radial; i++)
                {
                    int a = ring * (radial + 1) + i, b = a + 1, c = a + radial + 1, d = c + 1;
                    mesh.Triangle(a, b, c); mesh.Triangle(b, d, c);
                }
            for (int end = 0; end < 2; end++)
            {
                float radius = Radius(end), sign = end == 0 ? -1f : 1f, z = sign * length * .5f;
                for (int i = 0; i < radial; i++)
                {
                    double a = 2 * Math.PI * i / radial, b = 2 * Math.PI * (i + 1) / radial;
                    float ax = radius * (float)Math.Cos(a), ay = radius * (float)Math.Sin(a);
                    float bx = i + 1 == radial ? radius : radius * (float)Math.Cos(b);
                    float by = i + 1 == radial ? 0 : radius * (float)Math.Sin(b);
                    int first = mesh.Positions.Count;
                    var normal = new Float3(0, 0, sign); var tangent = new Float4(1, 0, 0, sign);
                    mesh.Vertex(new Float3(0, 0, z), normal, tangent, new Float2(radius, radius));
                    mesh.Vertex(new Float3(ax, ay, z), normal, tangent, new Float2(ax + radius, ay + radius));
                    mesh.Vertex(new Float3(bx, by, z), normal, tangent, new Float2(bx + radius, by + radius));
                    if (end == 0) mesh.Triangle(first, first + 2, first + 1); else mesh.Triangle(first, first + 1, first + 2);
                }
            }
            return mesh.Build();
        }

        private static Quat Align(Float3 a, Float3 b, out float length)
        {
            float x = b.X - a.X, y = b.Y - a.Y, z = b.Z - a.Z;
            length = (float)Math.Sqrt(x * x + y * y + z * z);
            float dx = x / length, dy = y / length, dz = z / length;
            if (dz < -.9999f) return new Quat(0, 1, 0, 0);
            float w = 1f + dz;
            float norm = (float)Math.Sqrt(dx * dx + dy * dy + w * w);
            return new Quat(-dy / norm, dx / norm, 0, w / norm);
        }

        private static void Segment(MeshAssembly mesh, Float3 a, Float3 b, float radiusA, float radiusB, ushort slot)
        {
            float length;
            Quat rotation = Align(a, b, out length);
            mesh.Add(PrimitiveGeometry.Cone(radiusA, radiusB, length, 12),
                new Float3((a.X + b.X) * .5f, (a.Y + b.Y) * .5f, (a.Z + b.Z) * .5f), rotation, slot);
        }
    }
}
