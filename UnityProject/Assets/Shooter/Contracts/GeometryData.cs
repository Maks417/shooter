#nullable enable
using System;
using System.Collections.Generic;

namespace Shooter.Contracts
{
    public readonly struct Submesh
    {
        public uint FirstIndex { get; }
        public uint IndexCount { get; }
        public ushort MaterialSlot { get; }
        public Submesh(uint firstIndex, uint indexCount, ushort materialSlot)
        {
            Require.That(firstIndex % 3 == 0 && indexCount > 0 && indexCount % 3 == 0, "triangle range");
            FirstIndex = firstIndex; IndexCount = indexCount; MaterialSlot = materialSlot;
        }
    }
    /// <summary>Typed memory only. P02 will freeze a field-by-field binary format separately.</summary>
    public sealed class MeshData
    {
        public FrozenList<Float3> Positions { get; }
        public FrozenList<Float3> Normals { get; }
        public FrozenList<Float4> Tangents { get; }
        public FrozenList<Float2> Uv { get; }
        public FrozenList<uint> Indices { get; }
        public FrozenList<Submesh> Submeshes { get; }
        public FrozenList<UInt16x4>? Joints { get; }
        public FrozenList<Float4>? Weights { get; }
        public MeshData(IReadOnlyList<Float3> positions, IReadOnlyList<Float3> normals, IReadOnlyList<Float4> tangents, IReadOnlyList<Float2> uv,
            IReadOnlyList<uint> indices, IReadOnlyList<Submesh> submeshes, IReadOnlyList<UInt16x4>? joints = null, IReadOnlyList<Float4>? weights = null)
        {
            if (positions == null || normals == null || tangents == null || uv == null || indices == null || submeshes == null) throw new ArgumentNullException("mesh arrays");
            int n = positions.Count;
            Require.That(n > 0 && n <= 2000000 && normals.Count == n && tangents.Count == n && uv.Count == n, "vertex counts");
            Require.That(indices.Count > 0 && indices.Count <= 6000000 && indices.Count % 3 == 0 && submeshes.Count > 0 && submeshes.Count <= 256, "index/submesh counts");
            long size = (long)n * 48 + (long)indices.Count * 4 + (long)submeshes.Count * 10;
            Require.That((joints == null) == (weights == null), "skin arrays");
            if (joints != null && weights != null)
            {
                Require.That(joints.Count == n && weights.Count == n, "skin counts"); size += (long)n * 24;
            }
            Require.That(size <= ContractVersion.MaxArtifactBytes, "mesh byte bound");
            // Validate before making defensive copies.
            foreach (uint index in indices) Require.That(index < n, "vertex index");
            ulong previousEnd = 0;
            foreach (var part in submeshes)
            {
                ulong end = (ulong)part.FirstIndex + part.IndexCount;
                Require.That(part.IndexCount > 0 && part.FirstIndex >= previousEnd && end <= (ulong)indices.Count, "submesh ranges");
                previousEnd = end;
            }
            if (joints != null && weights != null)
            {
                for (int i = 0; i < n; i++)
                {
                    var j = joints[i]; var w = weights[i];
                    Require.That(j.X < 256 && j.Y < 256 && j.Z < 256 && j.W < 256, "joint index");
                    Require.That(w.X >= 0 && w.Y >= 0 && w.Z >= 0 && w.W >= 0 && Math.Abs((double)w.X + w.Y + w.Z + w.W - 1) <= 1e-4, "skin weights");
                }
            }
            Positions = new FrozenList<Float3>(positions, n); Normals = new FrozenList<Float3>(normals, n); Tangents = new FrozenList<Float4>(tangents, n);
            Uv = new FrozenList<Float2>(uv, n); Indices = new FrozenList<uint>(indices, 6000000); Submeshes = new FrozenList<Submesh>(submeshes, 256);
            if (joints != null && weights != null) { Joints = new FrozenList<UInt16x4>(joints, n); Weights = new FrozenList<Float4>(weights, n); }
        }
    }
    public readonly struct JointDefinition
    {
        public ushort Id { get; }
        public ushort ParentId { get; }
        public Float3 LocalBindPosition { get; }
        public Quat LocalBindRotation { get; }
        public Float3 MinAngles { get; }
        public Float3 MaxAngles { get; }
        public bool IsValid => Id != ushort.MaxValue && LocalBindRotation.IsValid;
        public JointDefinition(ushort id, ushort parentId, Float3 localBindPosition, Quat localBindRotation, Float3 minAngles, Float3 maxAngles)
        {
            Require.That(id != ushort.MaxValue && id != parentId && localBindRotation.IsValid, "joint identity/rotation");
            Require.That(minAngles.X <= maxAngles.X && minAngles.Y <= maxAngles.Y && minAngles.Z <= maxAngles.Z, "joint limits");
            Id = id; ParentId = parentId; LocalBindPosition = localBindPosition; LocalBindRotation = localBindRotation; MinAngles = minAngles; MaxAngles = maxAngles;
        }
    }
    public sealed class SkeletonData
    {
        public FrozenList<JointDefinition> Joints { get; }
        public SkeletonData(IReadOnlyList<JointDefinition> joints)
        {
            Joints = new FrozenList<JointDefinition>(joints, 256); Require.That(Joints.Count > 0, nameof(joints));
            var parents = new Dictionary<ushort, ushort>(); int roots = 0;
            foreach (var joint in Joints)
            {
                Require.That(joint.IsValid && !parents.ContainsKey(joint.Id), "joint identity"); parents.Add(joint.Id, joint.ParentId);
                if (joint.ParentId == ushort.MaxValue) roots++;
            }
            Require.That(roots == 1, "one skeleton root");
            foreach (var joint in Joints)
            {
                ushort id = joint.Id; var visited = new HashSet<ushort>();
                while (id != ushort.MaxValue)
                {
                    Require.That(visited.Add(id) && parents.ContainsKey(id), "joint cycle/missing parent"); id = parents[id];
                }
            }
        }
    }
    public sealed class MaterialData
    {
        public ushort Width { get; }
        public ushort Height { get; }
        public string ChannelFormat { get; }
        public FrozenList<FrozenList<byte>> MipBytes { get; }
        public MaterialData(ushort width, ushort height, string channelFormat, IReadOnlyList<byte[]> mipBytes)
        {
            Require.That(width > 0 && height > 0 && mipBytes != null && mipBytes.Count > 0 && mipBytes.Count <= 16, "material dimensions/mips");
            long size = 0;
            foreach (var mip in mipBytes) { Require.That(mip != null && mip.Length > 0, "mip"); size += mip.Length; }
            Require.That(size <= ContractVersion.MaxArtifactBytes, "material bytes");
            var copies = new FrozenList<byte>[mipBytes.Count];
            for (int i = 0; i < copies.Length; i++) copies[i] = new FrozenList<byte>(mipBytes[i], ContractVersion.MaxArtifactBytes);
            Width = width; Height = height; ChannelFormat = Require.Text(channelFormat, nameof(channelFormat)); MipBytes = new FrozenList<FrozenList<byte>>(copies, 16);
        }
    }
    public readonly struct Box
    {
        public Float3 Center { get; }
        public Float3 Size { get; }
        public Quat Rotation { get; }
        public bool IsValid => Size.X > 0 && Size.Y > 0 && Size.Z > 0 && Rotation.IsValid;
        public Box(Float3 center, Float3 size, Quat rotation)
        { Require.That(size.X > 0 && size.Y > 0 && size.Z > 0 && rotation.IsValid, "box"); Center = center; Size = size; Rotation = rotation; }
    }
    public sealed class HeightGrid
    {
        public Float3 Origin { get; }
        public ushort Width { get; }
        public ushort Depth { get; }
        public float SpacingMeters { get; }
        public FrozenList<float> Heights { get; }
        public HeightGrid(Float3 origin, ushort width, ushort depth, float spacingMeters, IReadOnlyList<float> heights)
        {
            Require.That(width >= 2 && depth >= 2 && (long)width * depth <= ContractVersion.MaxArtifactBytes / 4 && heights != null && heights.Count == (long)width * depth, "height grid dimensions");
            Require.That(Require.Finite(spacingMeters, nameof(spacingMeters)) > 0, nameof(spacingMeters));
            foreach (float h in heights) Require.Finite(h, "height");
            Origin = origin; Width = width; Depth = depth; SpacingMeters = spacingMeters; Heights = new FrozenList<float>(heights, ContractVersion.MaxArtifactBytes / 4);
        }
    }
    public readonly struct TaggedCapsule
    {
        public Capsule Shape { get; }
        public ushort SurfaceTag { get; }
        public TaggedCapsule(Capsule shape, ushort surfaceTag) { Require.That(shape.IsValid, nameof(shape)); Shape = shape; SurfaceTag = surfaceTag; }
    }
    public readonly struct TaggedBox
    {
        public Box Shape { get; }
        public ushort SurfaceTag { get; }
        public TaggedBox(Box shape, ushort surfaceTag) { Require.That(shape.IsValid, nameof(shape)); Shape = shape; SurfaceTag = surfaceTag; }
    }
    public sealed class TaggedHeightGrid
    {
        public HeightGrid Shape { get; }
        public ushort SurfaceTag { get; }
        public TaggedHeightGrid(HeightGrid shape, ushort surfaceTag) { Shape = shape ?? throw new ArgumentNullException(nameof(shape)); SurfaceTag = surfaceTag; }
    }
    public sealed class CollisionData
    {
        public FrozenList<TaggedCapsule> Capsules { get; }
        public FrozenList<TaggedBox> Boxes { get; }
        public FrozenList<TaggedHeightGrid> HeightGrids { get; }
        public CollisionData(IReadOnlyList<TaggedCapsule> capsules, IReadOnlyList<TaggedBox> boxes, IReadOnlyList<TaggedHeightGrid> heightGrids)
        {
            if (capsules == null || boxes == null || heightGrids == null) throw new ArgumentNullException("collision arrays");
            Require.That(heightGrids.Count <= 256, "height grid count");
            long total = (long)capsules.Count * 32 + (long)boxes.Count * 44;
            Require.That(total <= ContractVersion.MaxArtifactBytes, "collision bytes");
            foreach (var grid in heightGrids) { Require.That(grid != null, nameof(heightGrids)); total += (long)grid.Shape.Heights.Count * 4 + 32; }
            Require.That(total <= ContractVersion.MaxArtifactBytes, "collision bytes");
            foreach (var c in capsules) Require.That(c.Shape.IsValid, nameof(capsules));
            foreach (var b in boxes) Require.That(b.Shape.IsValid, nameof(boxes));
            Capsules = new FrozenList<TaggedCapsule>(capsules, ContractVersion.MaxArtifactBytes / 32);
            Boxes = new FrozenList<TaggedBox>(boxes, ContractVersion.MaxArtifactBytes / 44);
            HeightGrids = new FrozenList<TaggedHeightGrid>(heightGrids, 256);
        }
    }
    public sealed class NavigationData
    {
        public string Format { get; }
        public uint Version { get; }
        public ContentId SettingsHash { get; }
        private readonly byte[] blob;
        public ReadOnlySpan<byte> Blob => blob;
        public NavigationData(string format, uint version, ContentId settingsHash, ReadOnlySpan<byte> validatedBlob)
        {
            Require.That(version > 0 && validatedBlob.Length > 0 && validatedBlob.Length <= ContractVersion.MaxArtifactBytes, "nav blob");
            Format = Require.Text(format, nameof(format)); Version = version; SettingsHash = settingsHash; blob = validatedBlob.ToArray();
        }
    }
}
