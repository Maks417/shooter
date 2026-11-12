using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;

public sealed class CodecTests
{
    private static readonly ContentId Generator = new ContentId(new string('0', 64));
    private static readonly ContentId Toolchain = new ContentId(new string('1', 64));
    // Independently produced by CodecFixtures/generate_vectors.py using Python struct, numeric diagnostic triangle.
    private const string MeshHex = "0300000003000000010000000000000000000000000000000000000000000000000000000000803f0000803f00000000000000000000803f00000000000000000000803f000000000000000000000000000000000000803f0000803f00000000000000000000803f0000803f00000000000000000000803f0000000000000000000000000000803f0000803f00000000000000000000803f000000000000803f00000000010000000200000000000000030000000000";
    private static MeshData Triangle(bool skin = false)
    {
        var p = new Float3[3]; var n = new Float3[3]; var t = new Float4[3]; var uv = new Float2[3];
        for (int i = 0; i < 3; i++) { float x = i == 1 ? 1 : 0, y = i == 2 ? 1 : 0; p[i] = new Float3(x, y, 0); n[i] = new Float3(0, 0, 1); t[i] = new Float4(1, 0, 0, 1); uv[i] = new Float2(x, y); }
        return new MeshData(p, n, t, uv, new uint[] { 0, 1, 2 }, new[] { new Submesh(0, 3, 0) },
            skin ? new UInt16x4[3] : null, skin ? Enumerable.Repeat(new Float4(1, 0, 0, 0), 3).ToArray() : null);
    }
    private static void U32(byte[] bytes, int offset, uint value)
    { for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (8 * i)); }
    private static byte[] Mutate(byte[] bytes, int offset, uint value) { var result = (byte[])bytes.Clone(); U32(result, offset, value); return result; }
    private static void Reject(Action action)
    {
        Exception? error = Record.Exception(action);
        Assert.NotNull(error); Assert.True(error is FormatException || error is ArgumentException, error!.ToString());
    }
    [Fact]
    public void MeshAndEnvelopeMatchIndependentPythonVectors()
    {
        byte[] mesh = GeometryCodec.EncodeMesh(Triangle());
        Assert.Equal(Convert.FromHexString(MeshHex), mesh);
        Assert.Equal("5fadfc0750ffea4a40dfc5cbaadf455f95fead77e5d61907d7bb125492b73232", ContentIdentity.Hash(mesh).ToString());
        byte[] file = ArtifactCodec.Encode(ContentKind.Mesh, mesh, Generator, Toolchain);
        Assert.Equal(298, file.Length);
        Assert.Equal("4ba40a98b6167bfc5bdeb040c26134b2368cca013e478e58fc8bf38e154d8f22", ContentIdentity.Hash(file).ToString());
        Assert.Equal(mesh, ArtifactCodec.Decode(file, ContentKind.Mesh, Generator, Toolchain));
        ArtifactCodec.ValidateTyped(ContentKind.Mesh, mesh);
    }
    [Fact]
    public void EveryTruncatedEnvelopeAndTrailingByteRejects()
    {
        byte[] file = ArtifactCodec.Encode(ContentKind.Mesh, GeometryCodec.EncodeMesh(Triangle()), Generator, Toolchain);
        for (int n = 0; n < file.Length; n++) { byte[] truncated = file.Take(n).ToArray(); Reject(() => ArtifactCodec.Decode(truncated, ContentKind.Mesh, Generator, Toolchain)); }
        Reject(() => ArtifactCodec.Decode(file.Concat(new byte[1]).ToArray(), ContentKind.Mesh, Generator, Toolchain));
    }
    [Theory]
    [InlineData(0, 0U)][InlineData(4, 2U)][InlineData(8, 2U)][InlineData(12, 1U)][InlineData(16, uint.MaxValue)][InlineData(20, 0U)][InlineData(52, 1U)][InlineData(84, 0U)]
    public void EnvelopeRejectsBadMagicVersionKindCompressionCountHashAndFingerprints(int offset, uint value)
    {
        byte[] file = ArtifactCodec.Encode(ContentKind.Mesh, GeometryCodec.EncodeMesh(Triangle()), Generator, Toolchain);
        Reject(() => ArtifactCodec.Decode(Mutate(file, offset, value), ContentKind.Mesh, Generator, Toolchain));
    }
    [Fact]
    public void EnvelopeRejectsBodyMutationUnknownKindAndFileLimit()
    {
        byte[] file = ArtifactCodec.Encode(ContentKind.Mesh, GeometryCodec.EncodeMesh(Triangle()), Generator, Toolchain); file[^1] ^= 1;
        Reject(() => ArtifactCodec.Decode(file, ContentKind.Mesh, Generator, Toolchain));
        Reject(() => ArtifactCodec.Encode((ContentKind)99, Array.Empty<byte>(), Generator, Toolchain));
        Reject(() => ArtifactCodec.ValidateTyped((ContentKind)99, Array.Empty<byte>()));
        Reject(() => ArtifactCodec.Decode(new byte[ContractVersion.MaxArtifactBytes + 1], ContentKind.Mesh, Generator, Toolchain));
    }
    [Fact]
    public void MeshSkinnedAndUnskinnedRoundTripExactly()
    {
        foreach (bool skin in new[] { false, true })
        {
            byte[] bytes = GeometryCodec.EncodeMesh(Triangle(skin)); var decoded = GeometryCodec.DecodeMesh(bytes);
            Assert.Equal(skin, decoded.Joints != null); Assert.Equal(bytes, GeometryCodec.EncodeMesh(decoded));
        }
    }
    [Theory]
    [InlineData(0, uint.MaxValue)][InlineData(4, uint.MaxValue)][InlineData(8, 257U)][InlineData(12, 2U)]
    [InlineData(16, 0x7fc00000U)][InlineData(28, 0x3f800000U)][InlineData(36, 0U)][InlineData(40, 0U)][InlineData(52, 0U)]
    [InlineData(160, 3U)][InlineData(164, 0U)][InlineData(172, 3U)][InlineData(176, 0U)]
    public void MeshRejectsBadCountsFramesDegeneracyIndicesAndCoverage(int offset, uint value)
    {
        byte[] bytes = GeometryCodec.EncodeMesh(Triangle());
        Reject(() => GeometryCodec.DecodeMesh(Mutate(bytes, offset, value)));
    }
    [Fact]
    public void MeshRejectsEveryTruncationTrailingDataAndInvalidSkin()
    {
        byte[] bytes = GeometryCodec.EncodeMesh(Triangle());
        for (int n = 0; n < bytes.Length; n++) { byte[] truncated = bytes.Take(n).ToArray(); Reject(() => GeometryCodec.DecodeMesh(truncated)); }
        Reject(() => GeometryCodec.DecodeMesh(bytes.Concat(new byte[1]).ToArray()));
        byte[] skin = GeometryCodec.EncodeMesh(Triangle(true));
        Reject(() => GeometryCodec.DecodeMesh(Mutate(skin, 16 + 48, 256)));
        Reject(() => GeometryCodec.DecodeMesh(Mutate(skin, 16 + 56, 0)));
    }
    [Fact]
    public void SkeletonRoundTripsAndRejectsMissingParentsCyclesAndBadRotation()
    {
        var skeleton = new SkeletonData(new[] {
            new JointDefinition(0, ushort.MaxValue, new Float3(), Quat.Identity, new Float3(-1,-1,-1), new Float3(1,1,1)),
            new JointDefinition(1, 0, new Float3(0,1,0), Quat.Identity, new Float3(-1,-1,-1), new Float3(1,1,1)) });
        byte[] body = GeometryCodec.EncodeSkeleton(skeleton);
        Assert.Equal(body, GeometryCodec.EncodeSkeleton(GeometryCodec.DecodeSkeleton(body)));
        Reject(() => GeometryCodec.DecodeSkeleton(Mutate(body, 0, uint.MaxValue)));
        Reject(() => GeometryCodec.DecodeSkeleton(Mutate(body, 4, 0U))); // root self-parent
        Reject(() => GeometryCodec.DecodeSkeleton(Mutate(body, 60, 2U | (3U << 16)))); // missing parent
        Reject(() => GeometryCodec.DecodeSkeleton(Mutate(body, 60, 0U))); // duplicate root ID
        Reject(() => GeometryCodec.DecodeSkeleton(Mutate(body, 32, 0U))); // invalid zero quaternion
        Reject(() => GeometryCodec.DecodeSkeleton(body.Concat(new byte[1]).ToArray()));
    }
    [Fact]
    public void MaterialRequiresKnownFormatExactSizesAndCompleteMipChain()
    {
        var data = new MaterialData(2, 2, "rgba8-linear", new[] {new byte[16], new byte[4]});
        byte[] body = GeometryCodec.EncodeMaterial(data);
        Assert.Equal(body, GeometryCodec.EncodeMaterial(GeometryCodec.DecodeMaterial(body)));
        Reject(() => GeometryCodec.EncodeMaterial(new MaterialData(2, 2, "rgba8-linear", new[] { new byte[16] })));
        Reject(() => GeometryCodec.EncodeMaterial(new MaterialData(1, 1, "unknown", new[] { new byte[4] })));
        Reject(() => GeometryCodec.DecodeMaterial(Mutate(body, 0, uint.MaxValue)));
        Reject(() => GeometryCodec.DecodeMaterial(Mutate(body, 20, uint.MaxValue)));
        Reject(() => GeometryCodec.DecodeMaterial(Mutate(body, 24, 3)));
        Reject(() => GeometryCodec.DecodeMaterial(body.Take(body.Length-1).ToArray()));
    }
    [Fact]
    public void CollisionRoundTripsAllShapesAndRejectsCountGridAndGeometryAttacks()
    {
        var collision = new CollisionData(new[] {new TaggedCapsule(new Capsule(new Float3(), new Float3(0,1,0), .2f), 1)},
            new[] {new TaggedBox(new Box(new Float3(), new Float3(1,2,3), Quat.Identity), 2)},
            new[] {new TaggedHeightGrid(new HeightGrid(new Float3(), 2, 2, 1, new float[] {0,1,2,3}), 3)});
        byte[] body = GeometryCodec.EncodeCollision(collision);
        Assert.Equal(body, GeometryCodec.EncodeCollision(GeometryCodec.DecodeCollision(body)));
        Reject(() => GeometryCodec.DecodeCollision(Mutate(body, 0, uint.MaxValue)));
        Reject(() => GeometryCodec.DecodeCollision(Mutate(body, 8, 257)));
        Reject(() => GeometryCodec.DecodeCollision(Mutate(body, 12 + 24, 0U)));
        Reject(() => GeometryCodec.DecodeCollision(Mutate(body, 42 + 12, 0U)));
        Reject(() => GeometryCodec.DecodeCollision(Mutate(body, 84 + 12, uint.MaxValue)));
        Reject(() => GeometryCodec.DecodeCollision(Mutate(body, 84 + 16, 0x7f800000U)));
        Reject(() => GeometryCodec.DecodeCollision(body.Take(body.Length-1).ToArray()));
        Assert.Empty(GeometryCodec.DecodeCollision(GeometryCodec.EncodeCollision(new CollisionData(Array.Empty<TaggedCapsule>(), Array.Empty<TaggedBox>(), Array.Empty<TaggedHeightGrid>()))).Boxes);
    }
    [Fact]
    public void NavigationOnlyPromisesBoundedStructuralDecode()
    {
        var data = new NavigationData("test-adapter", 1, Generator, new byte[] {1,2,3});
        byte[] body = GeometryCodec.EncodeNavigation(data);
        Assert.Equal(body, GeometryCodec.EncodeNavigation(GeometryCodec.DecodeNavigation(body)));
        Reject(() => GeometryCodec.DecodeNavigation(Mutate(body, 0, uint.MaxValue)));
        Reject(() => GeometryCodec.DecodeNavigation(Mutate(body, 16, 0)));
        Reject(() => GeometryCodec.DecodeNavigation(Mutate(body, 52, uint.MaxValue)));
        Reject(() => GeometryCodec.DecodeNavigation(body.Concat(new byte[1]).ToArray()));
    }
    [Fact]
    public void TypedDispatcherAcceptsEveryKind()
    {
        var bodies = new Dictionary<ContentKind, byte[]> {
            [ContentKind.Mesh] = GeometryCodec.EncodeMesh(Triangle()),
            [ContentKind.Skeleton] = GeometryCodec.EncodeSkeleton(new SkeletonData(new[] {new JointDefinition(0, ushort.MaxValue,new Float3(),Quat.Identity,new Float3(),new Float3())})),
            [ContentKind.Material] = GeometryCodec.EncodeMaterial(new MaterialData(1,1,"rgba8-srgb",new[]{new byte[4]})),
            [ContentKind.Collision] = GeometryCodec.EncodeCollision(new CollisionData(Array.Empty<TaggedCapsule>(),Array.Empty<TaggedBox>(),Array.Empty<TaggedHeightGrid>())),
            [ContentKind.Navigation] = GeometryCodec.EncodeNavigation(new NavigationData("adapter",1,Generator,new byte[]{1})) };
        foreach (var pair in bodies) ArtifactCodec.ValidateTyped(pair.Key,pair.Value);
    }
}

