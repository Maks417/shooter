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
        /// <summary>
        /// ADR-106 glove: a pillow-shaped palm, one smooth swept tube per finger and thumb through its rig joints, a knuckle
        /// guard, a back-of-hand pad and a wrist cuff. Finger tubes blend their skin weights across every knuckle so the
        /// glove bends instead of hinging like separate puppet segments. Slots: 0 sleeve, 1 glove, 2 glove reinforcement.
        /// </summary>
        public static MeshData Build(byte slot, bool support)
        {
            var rig = new FirstPersonHandRig(slot, support);
            var mesh = new MeshAssembly();
            var skin = new System.Collections.Generic.List<UInt16x4>();
            var weights = new System.Collections.Generic.List<Float4>();
            void Rigid(MeshData part, Float3 at, Quat rotation, ushort material, ushort joint)
            {
                mesh.Add(part, at, rotation, material);
                for (int i = 0; i < part.Positions.Count; i++) { skin.Add(new UInt16x4(joint, 0, 0, 0)); weights.Add(new Float4(1, 0, 0, 0)); }
            }
            void Chain(MeshData part, ushort material, Float3[] chain, ushort[] bones)
            {
                mesh.Add(part, new Float3(0, 0, 0), Quat.Identity, material);
                foreach (var p in part.Positions) { ChainWeights(p, chain, bones, out var j, out var w); skin.Add(j); weights.Add(w); }
            }
            float side = support ? -1 : 1;
            var palm = rig.Palm; var wrist = rig.Wrist; var elbow = rig.Elbow;

            // Sleeve: elbow stays planted, wrist translation stretches the continuous forearm loft.
            float length; Quat rotation = Align(elbow, wrist, out length);
            var sleeve = BuildSleeve(length);
            var sleeveCentre = new Float3((elbow.X + wrist.X) * .5f, (elbow.Y + wrist.Y) * .5f, (elbow.Z + wrist.Z) * .5f);
            mesh.Add(sleeve, sleeveCentre, rotation, 0);
            for (int i = 0; i < sleeve.Positions.Count; i++)
            {
                float t = Math.Clamp((sleeve.Positions[i].Z + length * .5f) / length, 0, 1);
                skin.Add(new UInt16x4(1, 2, 0, 0)); weights.Add(new Float4(1 - t, t, 0, 0));
            }
            // Glove cuff over the sleeve end.
            Rigid(ShapeKit.Lathe(new[] { new Float2(.0300f, -.030f), new Float2(.0338f, -.025f), new Float2(.0348f, .002f), new Float2(.0318f, .010f), new Float2(.0280f, .012f) }, 20),
                wrist, rotation, 1, 2);
            Rigid(ShapeKit.Lathe(new[] { new Float2(.0352f, -.020f), new Float2(.0362f, -.017f), new Float2(.0362f, -.005f), new Float2(.0352f, -.002f) }, 20),
                wrist, rotation, 2, 2);

            // Palm: side-view outline (z, y) extruded across X with a deep edge roll, heel reaching down to the wrist.
            float pz = palm.Z, py = palm.Y;
            var palmCorners = new[]
            {
                new ProfileCorner(pz + .030f, py + .043f, .013f), new ProfileCorner(pz + .032f, py - .036f, .013f), new ProfileCorner(pz + .010f, py - .050f, .016f),
                new ProfileCorner(wrist.Z + .010f, wrist.Y + .010f, .014f), new ProfileCorner(pz - .038f, py - .036f, .016f),
                new ProfileCorner(pz - .034f, py + .022f, .016f), new ProfileCorner(pz - .018f, py + .046f, .013f),
            };
            Rigid(ShapeKit.Slab(palmCorners, .034f, .0125f, 4, 3), new Float3(palm.X, 0, 0), Quat.Identity, 1, 2);
            // Back-of-hand pad and the moulded knuckle guard on the outer face.
            float outer = palm.X + side * .0165f;
            Rigid(ShapeKit.Slab(new[] { new ProfileCorner(pz - .036f, py - .028f, .009f), new ProfileCorner(pz + .004f, py - .030f, .009f),
                new ProfileCorner(pz + .006f, py + .030f, .009f), new ProfileCorner(pz - .030f, py + .026f, .009f) }, .005f, .0018f, 4, 2),
                new Float3(outer, 0, 0), Quat.Identity, 2, 2);
            float knuckleZ = rig.BindPositions[3].Z, top = rig.BindPositions[3].Y, bottom = rig.BindPositions[12].Y;
            Rigid(ShapeKit.Slab(new[] { new ProfileCorner(knuckleZ - .010f, bottom - .009f, .006f), new ProfileCorner(knuckleZ + .008f, bottom - .009f, .006f),
                new ProfileCorner(knuckleZ + .009f, top + .009f, .006f), new ProfileCorner(knuckleZ - .009f, top + .009f, .006f) }, .007f, .0026f, 4, 2),
                new Float3(rig.BindPositions[3].X + side * .0085f, 0, 0), Quat.Identity, 2, 2);

            // Fingers: base inside the palm, knuckle, middle, distal joints and the fingertip.
            float[] scale = { 1.00f, 1.03f, .98f, .87f };
            for (int finger = 0; finger < 4; finger++)
            {
                int j = 3 + finger * 3;
                var knuckle = rig.BindPositions[j];
                var chain = new[]
                {
                    new Float3(knuckle.X - side * .004f, knuckle.Y, knuckle.Z - .020f), knuckle, rig.BindPositions[j + 1], rig.BindPositions[j + 2],
                    rig.FingerTip(finger, support, slot)
                };
                float k = scale[finger];
                var radii = new[] { new Float2(.0100f * k, .0092f * k), new Float2(.0096f * k, .0088f * k), new Float2(.0088f * k, .0080f * k),
                    new Float2(.0080f * k, .0072f * k), new Float2(.0074f * k, .0066f * k) };
                Chain(Finger(chain, radii, new Float3(0, 1, 0)), 1, chain, new[] { (ushort)2, (ushort)j, (ushort)(j + 1), (ushort)(j + 2) });
            }
            // Thumb from the thenar pad through joints 15 and 16.
            {
                var t15 = rig.BindPositions[15]; var t16 = rig.BindPositions[16];
                var chain = new[]
                {
                    new Float3(t15.X + side * .002f, t15.Y - .020f, t15.Z - .008f), t15, t16, new Float3(-side * .008f, palm.Y + .053f, palm.Z + .021f)
                };
                var radii = new[] { new Float2(.0108f, .0098f), new Float2(.0098f, .0088f), new Float2(.0088f, .0080f), new Float2(.0078f, .0070f) };
                Chain(Finger(chain, radii, new Float3(0, 0, 1)), 1, chain, new[] { (ushort)2, (ushort)15, (ushort)16 });
            }
            if (mesh.VertexCount > MaxVerticesPerHand) throw new InvalidOperationException("First-person hand vertex budget exceeded.");
            var data = mesh.Build();
            return new MeshData(data.Positions, data.Normals, data.Tangents, data.Uv, data.Indices, data.Submeshes, skin, weights);
        }

        /// <summary>Smooth tube through the joint chain: spline samples with radii interpolated by arc length.</summary>
        private static MeshData Finger(Float3[] chain, Float2[] jointRadii, Float3 up)
        {
            var path = ShapeKit.Spline(chain, 5);
            var cumulative = new double[path.Count];
            for (int i = 1; i < path.Count; i++) cumulative[i] = cumulative[i - 1] + Distance(path[i - 1], path[i]);
            var radii = new Float2[path.Count];
            int samples = 5;
            for (int i = 0; i < path.Count; i++)
            {
                int segment = Math.Min(chain.Length - 2, i / samples);
                float t = i >= (chain.Length - 1) * samples ? 1 : (float)(i - segment * samples) / samples;
                var a = jointRadii[segment]; var b = jointRadii[segment + 1];
                radii[i] = new Float2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            }
            return ShapeKit.Tube(path, radii, up, 12, 3);
        }

        private static double Distance(Float3 a, Float3 b)
        { double x = (double)a.X - b.X, y = (double)a.Y - b.Y, z = (double)a.Z - b.Z; return Math.Sqrt(x * x + y * y + z * z); }

        /// <summary>
        /// Two-joint smooth skinning along a joint chain: the vertex follows the bone of the chain segment nearest to it and
        /// cross-fades to the neighbouring bone within a blend zone around each joint (smoothstep), instead of a hard hinge.
        /// </summary>
        internal static void ChainWeights(Float3 p, Float3[] chain, ushort[] bones, out UInt16x4 joints, out Float4 weights)
        {
            int segments = chain.Length - 1;
            var lengths = new double[segments]; var starts = new double[segments];
            double best = double.MaxValue, s = 0;
            for (int k = 0; k < segments; k++)
            {
                var a = chain[k]; var b = chain[k + 1];
                lengths[k] = Distance(a, b); starts[k] = k == 0 ? 0 : starts[k - 1] + lengths[k - 1];
                double dx = (double)b.X - a.X, dy = (double)b.Y - a.Y, dz = (double)b.Z - a.Z;
                double t = lengths[k] < 1e-9 ? 0 : Math.Max(0, Math.Min(1, (((double)p.X - a.X) * dx + ((double)p.Y - a.Y) * dy + ((double)p.Z - a.Z) * dz) / (lengths[k] * lengths[k])));
                double cx = a.X + dx * t - p.X, cy = a.Y + dy * t - p.Y, cz = a.Z + dz * t - p.Z, d = cx * cx + cy * cy + cz * cz;
                if (d < best - 1e-12) { best = d; s = starts[k] + t * lengths[k]; }
            }
            int bone = segments - 1;
            for (int k = 0; k < segments; k++) if (s <= starts[k] + lengths[k]) { bone = k; break; }
            ushort parent = bones[bone], child = bones[bone];
            float childWeight = 0;
            // Nearest interior joint boundary and its blend half-width.
            double boundary = double.NaN; int boundaryIndex = -1;
            if (bone > 0 && s - starts[bone] <= starts[bone] + lengths[bone] - s) { boundary = starts[bone]; boundaryIndex = bone; }
            else if (bone + 1 < segments) { boundary = starts[bone + 1]; boundaryIndex = bone + 1; }
            else if (bone > 0) { boundary = starts[bone]; boundaryIndex = bone; }
            if (boundaryIndex > 0)
            {
                double blend = Math.Min(.009, .4 * Math.Min(lengths[boundaryIndex - 1], lengths[boundaryIndex]));
                double x = (s - boundary + blend) / (2 * blend);
                if (x > 0 && x < 1)
                {
                    parent = bones[boundaryIndex - 1]; child = bones[boundaryIndex];
                    childWeight = (float)(x * x * (3 - 2 * x));
                }
            }
            if (parent == child || childWeight <= 0) { joints = new UInt16x4(bones[bone], 0, 0, 0); weights = new Float4(1, 0, 0, 0); return; }
            if (childWeight >= 1) { joints = new UInt16x4(child, 0, 0, 0); weights = new Float4(1, 0, 0, 0); return; }
            joints = new UInt16x4(parent, child, 0, 0); weights = new Float4(1 - childWeight, childWeight, 0, 0);
        }

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
    }
}
