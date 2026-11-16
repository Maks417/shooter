#nullable enable
using System;
using Shooter.Contracts;
using Shooter.VectorMath;

namespace Shooter.Generation.Environment
{
    /// <summary>Offline camera-only glove/sleeve meshes. Does not modify soldier anatomy, hit poses or weapon geometry.</summary>
    public static class FirstPersonHandGeometry
    {
        public const int MaxVerticesPerHand = 6000;

        public static MeshData Build(bool pistol, bool support) => Build(pistol ? (byte)1 : (byte)0, support);
        public static MeshData Build(byte slot, bool support)
        {
            var rig = new FirstPersonHandRig(slot, support);
            var mesh = new MeshAssembly();
            var skin = new System.Collections.Generic.List<UInt16x4>();
            var weights = new System.Collections.Generic.List<Float4>();
            void Bind(int first, ushort joint)
            {
                for (int i = first; i < mesh.VertexCount; i++)
                { skin.Add(new UInt16x4(joint,0,0,0)); weights.Add(new Float4(1,0,0,0)); }
            }
            float side = support ? -1 : 1;
            var palm = rig.Palm; var wrist = rig.Wrist; var elbow = rig.Elbow;
            float length; Quat rotation = Align(elbow, wrist, out length);
            var sleeve = BuildSleeve(length);
            mesh.Add(sleeve, new Float3((elbow.X+wrist.X)*.5f,(elbow.Y+wrist.Y)*.5f,(elbow.Z+wrist.Z)*.5f), rotation, 0);
            // Elbow stays planted; wrist translation stretches the continuous forearm loft.
            for (int i = 0; i < sleeve.Positions.Count; i++)
            {
                float t = Math.Clamp((sleeve.Positions[i].Z + length*.5f)/length,0,1);
                skin.Add(new UInt16x4(1,2,0,0)); weights.Add(new Float4(1-t,t,0,0));
            }
            int first = mesh.VertexCount;
            Segment(mesh, new Float3(wrist.X,wrist.Y-.018f,wrist.Z-.012f), wrist,.036f,.035f,2);
            Box(mesh,palm,new Float3(.039f,.085f,.062f),.012f,1);
            Box(mesh,new Float3(side*.062f,palm.Y+.012f,palm.Z+.001f),new Float3(.009f,.036f,.042f),.003f,2);
            Bind(first,2);
            for (int finger = 0; finger < 4; finger++)
            {
                int j = 3 + finger*3;
                for (int segment = 0; segment < 3; segment++)
                {
                    first = mesh.VertexCount;
                    var a = rig.BindPositions[j+segment];
                    var b = segment == 2 ? rig.FingerTip(finger,support,slot) : rig.BindPositions[j+segment+1];
                    Segment(mesh,a,b,.009f-segment*.001f,.008f-segment*.001f,1);
                    // Small overlapping knuckle volumes keep articulation from opening visible gaps.
                    Box(mesh,a,new Float3(.016f-segment*.002f,.014f-segment*.002f,.016f-segment*.002f),.003f,2);
                    Bind(first,(ushort)(j+segment));
                }
            }
            first=mesh.VertexCount;
            Segment(mesh,rig.BindPositions[15],rig.BindPositions[16],.012f,.010f,1); Bind(first,15);
            first=mesh.VertexCount;
            Segment(mesh,rig.BindPositions[16],new Float3(-side*.008f,palm.Y+.053f,palm.Z+.021f),.010f,.008f,1); Bind(first,16);
            if(mesh.VertexCount>MaxVerticesPerHand) throw new InvalidOperationException("First-person hand vertex budget exceeded.");
            var data=mesh.Build();
            return new MeshData(data.Positions,data.Normals,data.Tangents,data.Uv,data.Indices,data.Submeshes,skin,weights);
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
                    var normal = VecMath.Normalize(new Float3(c, sine, (float)(-(slope + bendSlope * sine) / length)));
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
