#nullable enable
using System;
using System.IO;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>Typed compiler body codecs v1. No engine/native struct serialization.</summary>
    public static class GeometryCodec
    {
        public static byte[] EncodeMesh(MeshData mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            bool skin = mesh.Joints != null;
            BodyWriting.Size(16L + mesh.Positions.Count * (skin ? 72L : 48L) + mesh.Indices.Count * 4L + mesh.Submeshes.Count * 10L);
            using var stream = new MemoryStream(); using var w = new BinaryWriter(stream, Encoding.UTF8, true);
            w.Write((uint)mesh.Positions.Count); w.Write((uint)mesh.Indices.Count); w.Write((uint)mesh.Submeshes.Count); w.Write(skin ? 1U : 0U);
            for (int i = 0; i < mesh.Positions.Count; i++)
            {
                BodyWriting.V3(w, mesh.Positions[i]); BodyWriting.V3(w, mesh.Normals[i]); BodyWriting.V4(w, mesh.Tangents[i]); BodyWriting.V2(w, mesh.Uv[i]);
                if (skin) { var j = mesh.Joints![i]; w.Write(j.X); w.Write(j.Y); w.Write(j.Z); w.Write(j.W); BodyWriting.V4(w, mesh.Weights![i]); }
            }
            foreach (uint index in mesh.Indices) w.Write(index);
            foreach (var sub in mesh.Submeshes) { w.Write(sub.FirstIndex); w.Write(sub.IndexCount); w.Write(sub.MaterialSlot); }
            var bytes = BodyWriting.Finish(stream); DecodeMesh(bytes); return bytes;
        }
        public static MeshData DecodeMesh(ReadOnlySpan<byte> body)
        {
            var r = new BodyReader(body);
            int n = r.Count(2000000, true), ni = r.Count(6000000, true), ns = r.Count(256, true); uint flags = r.U32();
            BodyWriting.Valid(flags <= 1 && ni % 3 == 0, "Invalid mesh flags/index count.");
            bool skin = flags == 1;
            long required = n * (skin ? 72L : 48L) + ni * 4L + ns * 10L;
            BodyWriting.Valid(required == r.Remaining, "Mesh array byte counts mismatch.");
            var p = new Float3[n]; var normal = new Float3[n]; var tangent = new Float4[n]; var uv = new Float2[n];
            UInt16x4[]? joints = skin ? new UInt16x4[n] : null; Float4[]? weights = skin ? new Float4[n] : null;
            for (int i = 0; i < n; i++)
            {
                p[i] = r.V3(); normal[i] = r.V3(); tangent[i] = r.V4(); uv[i] = r.V2();
                var t = tangent[i]; var nt = new Float3(t.X, t.Y, t.Z);
                BodyWriting.Valid(normal[i].IsUnit && nt.IsUnit && Math.Abs(Math.Abs(t.W) - 1) <= 1e-4 &&
                    Math.Abs((double)normal[i].X * t.X + (double)normal[i].Y * t.Y + (double)normal[i].Z * t.Z) <= 1e-4, "Invalid normal/tangent frame.");
                if (skin) { joints![i] = new UInt16x4(r.U16(), r.U16(), r.U16(), r.U16()); weights![i] = r.V4(); }
            }
            var indices = new uint[ni];
            for (int i = 0; i < ni; i++) { indices[i] = r.U32(); BodyWriting.Valid(indices[i] < n, "Mesh index outside vertex array."); }
            for (int i = 0; i < ni; i += 3)
            {
                var a = p[indices[i]]; var b = p[indices[i + 1]]; var c = p[indices[i + 2]];
                double ax = (double)b.X - a.X, ay = (double)b.Y - a.Y, az = (double)b.Z - a.Z;
                double bx = (double)c.X - a.X, by = (double)c.Y - a.Y, bz = (double)c.Z - a.Z;
                double cx = ay * bz - az * by, cy = az * bx - ax * bz, cz = ax * by - ay * bx;
                BodyWriting.Valid(cx * cx + cy * cy + cz * cz > 1e-20, "Degenerate mesh triangle (cross-product squared <= 1e-20 m^4).");
            }
            var subs = new Submesh[ns]; uint end = 0;
            for (int i = 0; i < ns; i++)
            {
                uint first = r.U32(), count = r.U32(); ushort slot = r.U16();
                BodyWriting.Valid(first == end && count > 0 && count % 3 == 0 && (ulong)first + count <= (ulong)ni, "Submeshes must cover all triangle indices exactly once.");
                subs[i] = new Submesh(first, count, slot); end = first + count;
            }
            BodyWriting.Valid(end == ni, "Incomplete mesh submesh coverage."); r.End();
            return new MeshData(p, normal, tangent, uv, indices, subs, joints, weights);
        }
        public static byte[] EncodeSkeleton(SkeletonData skeleton)
        {
            if (skeleton == null) throw new ArgumentNullException(nameof(skeleton));
            using var stream = new MemoryStream(); using var w = new BinaryWriter(stream, Encoding.UTF8, true);
            w.Write((uint)skeleton.Joints.Count);
            foreach (var j in skeleton.Joints)
            { w.Write(j.Id); w.Write(j.ParentId); BodyWriting.V3(w, j.LocalBindPosition); BodyWriting.Q(w, j.LocalBindRotation); BodyWriting.V3(w, j.MinAngles); BodyWriting.V3(w, j.MaxAngles); }
            var bytes = BodyWriting.Finish(stream); DecodeSkeleton(bytes); return bytes;
        }
        public static SkeletonData DecodeSkeleton(ReadOnlySpan<byte> body)
        {
            var r = new BodyReader(body); int n = r.Count(256, true);
            BodyWriting.Valid(r.Remaining == n * 56L, "Skeleton joint byte counts mismatch.");
            var joints = new JointDefinition[n];
            for (int i = 0; i < n; i++) joints[i] = new JointDefinition(r.U16(), r.U16(), r.V3(), r.Q(), r.V3(), r.V3());
            r.End(); return new SkeletonData(joints);
        }
        public static byte[] EncodeMaterial(MaterialData material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            long total = 12L + material.ChannelFormat.Length;
            foreach (var mip in material.MipBytes) total += 4L + mip.Count;
            BodyWriting.Size(total);
            using var stream = new MemoryStream(); using var w = new BinaryWriter(stream, Encoding.UTF8, true);
            w.Write(material.Width); w.Write(material.Height); BodyWriting.Text(w, material.ChannelFormat); w.Write((uint)material.MipBytes.Count);
            foreach (var mip in material.MipBytes) { w.Write((uint)mip.Count); foreach (byte b in mip) w.Write(b); }
            var bytes = BodyWriting.Finish(stream); DecodeMaterial(bytes); return bytes;
        }
        public static MaterialData DecodeMaterial(ReadOnlySpan<byte> body)
        {
            var r = new BodyReader(body); ushort width = r.U16(), height = r.U16(); string format = r.Text(); int n = r.Count(16, true);
            BodyWriting.Valid(width > 0 && height > 0 && (format == "rgba8-linear" || format == "rgba8-srgb"), "Invalid material dimensions/format.");
            int expected = 1, x = width, y = height;
            while (x > 1 || y > 1) { expected++; x = Math.Max(1, x / 2); y = Math.Max(1, y / 2); }
            BodyWriting.Valid(n == expected, "Material requires complete mip chain.");
            long total = 0; x = width; y = height;
            for (int i = 0; i < n; i++) { total += 4L + (long)x * y * 4; x = Math.Max(1, x / 2); y = Math.Max(1, y / 2); }
            BodyWriting.Valid(total == r.Remaining, "Material byte counts mismatch.");
            var mips = new byte[n][]; x = width; y = height;
            for (int i = 0; i < n; i++)
            {
                int length = r.Count(ArtifactCodec.MaxBodyBytes, true);
                BodyWriting.Valid(length == (long)x * y * 4, "Material mip byte count mismatch.");
                mips[i] = r.Bytes(length).ToArray(); x = Math.Max(1, x / 2); y = Math.Max(1, y / 2);
            }
            r.End(); return new MaterialData(width, height, format, mips);
        }
        public static byte[] EncodeCollision(CollisionData collision)
        {
            if (collision == null) throw new ArgumentNullException(nameof(collision));
            long total = 12L + collision.Capsules.Count * 30L + collision.Boxes.Count * 42L;
            foreach (var grid in collision.HeightGrids) total += 22L + grid.Shape.Heights.Count * 4L;
            BodyWriting.Size(total);
            using var stream = new MemoryStream(); using var w = new BinaryWriter(stream, Encoding.UTF8, true);
            w.Write((uint)collision.Capsules.Count); w.Write((uint)collision.Boxes.Count); w.Write((uint)collision.HeightGrids.Count);
            foreach (var c in collision.Capsules)
            { BodyWriting.V3(w, c.Shape.BottomSphereCenter); BodyWriting.V3(w, c.Shape.TopSphereCenter); w.Write(c.Shape.Radius); w.Write(c.SurfaceTag); }
            foreach (var b in collision.Boxes)
            { BodyWriting.V3(w, b.Shape.Center); BodyWriting.V3(w, b.Shape.Size); BodyWriting.Q(w, b.Shape.Rotation); w.Write(b.SurfaceTag); }
            foreach (var tagged in collision.HeightGrids)
            { var g = tagged.Shape; BodyWriting.V3(w, g.Origin); w.Write(g.Width); w.Write(g.Depth); w.Write(g.SpacingMeters); w.Write(tagged.SurfaceTag); foreach (float h in g.Heights) w.Write(h); }
            var bytes = BodyWriting.Finish(stream); DecodeCollision(bytes); return bytes;
        }
        public static CollisionData DecodeCollision(ReadOnlySpan<byte> body)
        {
            var r = new BodyReader(body);
            int nc = r.Count(ContractVersion.MaxArtifactBytes / 32), nb = r.Count(ContractVersion.MaxArtifactBytes / 44), ng = r.Count(256);
            r.Need(nc * 30L + nb * 42L + ng * 38L);
            // The DTO's independently conservative allocation accounting must also fit.
            BodyWriting.Valid(nc * 32L + nb * 44L + ng * 32L <= ContractVersion.MaxArtifactBytes, "Collision allocation limit.");
            var capsules = new TaggedCapsule[nc]; var boxes = new TaggedBox[nb]; var grids = new TaggedHeightGrid[ng];
            for (int i = 0; i < nc; i++) capsules[i] = new TaggedCapsule(new Capsule(r.V3(), r.V3(), r.F32()), r.U16());
            for (int i = 0; i < nb; i++) boxes[i] = new TaggedBox(new Box(r.V3(), r.V3(), r.Q()), r.U16());
            long allocation = nc * 32L + nb * 44L;
            for (int i = 0; i < ng; i++)
            {
                Float3 origin = r.V3(); ushort width = r.U16(), depth = r.U16(); float spacing = r.F32(); ushort tag = r.U16();
                long count = (long)width * depth;
                BodyWriting.Valid(width >= 2 && depth >= 2 && count <= ContractVersion.MaxArtifactBytes / 4 && spacing > 0, "Invalid height-grid dimensions/spacing.");
                allocation += count * 4 + 32;
                BodyWriting.Valid(allocation <= ContractVersion.MaxArtifactBytes, "Height-grid allocation limit."); r.Need(count * 4);
                var heights = new float[(int)count]; for (int h = 0; h < heights.Length; h++) heights[h] = r.F32();
                grids[i] = new TaggedHeightGrid(new HeightGrid(origin, width, depth, spacing, heights), tag);
            }
            r.End(); return new CollisionData(capsules, boxes, grids);
        }
        public static byte[] EncodeNavigation(NavigationData navigation)
        {
            if (navigation == null) throw new ArgumentNullException(nameof(navigation));
            BodyWriting.Size(44L + navigation.Format.Length + navigation.Blob.Length);
            using var stream = new MemoryStream(); using var w = new BinaryWriter(stream, Encoding.UTF8, true);
            BodyWriting.Text(w, navigation.Format); w.Write(navigation.Version); w.Write(navigation.SettingsHash.ToBytes()); w.Write((uint)navigation.Blob.Length); w.Write(navigation.Blob.ToArray());
            var bytes = BodyWriting.Finish(stream); DecodeNavigation(bytes); return bytes;
        }
        /// <summary>Structural decode only; the named navigation adapter must separately validate the blob.</summary>
        public static NavigationData DecodeNavigation(ReadOnlySpan<byte> body)
        {
            var r = new BodyReader(body); string format = r.Text(); uint version = r.U32(); var settings = new ContentId(r.Bytes(32)); int n = r.Count(ArtifactCodec.MaxBodyBytes, true);
            BodyWriting.Valid(version > 0 && n == r.Remaining, "Navigation version/length.");
            var result = new NavigationData(format, version, settings, r.Bytes(n)); r.End(); return result;
        }
    }
}
