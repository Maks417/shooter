using System;
using System.Diagnostics;
using System.Linq;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Xunit;
using Xunit.Abstractions;

/// <summary>ADR-081 baked ambient-occlusion volume: deterministic, sky-visibility semantics, packaged per map.</summary>
public sealed class AmbientOcclusionVolumeTests
{
    private readonly ITestOutputHelper output;
    public AmbientOcclusionVolumeTests(ITestOutputHelper output) { this.output = output; }

    private static CollisionBox Box(ulong id, float cx, float cy, float cz, float sx, float sy, float sz) =>
        new CollisionBox(id, id, 1, new Float3(cx, cy, cz), new Float3(sx, sy, sz), true);

    private static byte At(byte[] volume, double x, double y, double z) => volume[MapAmbientOcclusionVolume.Index(
        (int)Math.Floor((x - MapAmbientOcclusionVolume.MinX) / MapAmbientOcclusionVolume.CellXZ),
        (int)Math.Floor((y - MapAmbientOcclusionVolume.MinY) / MapAmbientOcclusionVolume.CellY),
        (int)Math.Floor((z - MapAmbientOcclusionVolume.MinZ) / MapAmbientOcclusionVolume.CellXZ))];

    [Fact]
    public void OpenArenaIsFullyVisible()
    {
        var volume = MapAmbientOcclusionVolume.BakeUncached(Array.Empty<CollisionBox>());
        Assert.Equal(MapAmbientOcclusionVolume.CellCount, volume.Length);
        Assert.All(volume, v => Assert.Equal(255, v));
        Assert.Equal(17, MapAmbientOcclusionVolume.RayCount);
    }

    [Fact]
    public void RoofDarkensBelowAndWallDarkensBesideButNotFarAway()
    {
        // 6 x 6 m roof slab at 3 m, plus a 4 m tall wall elsewhere.
        var boxes = new[] { Box(1, 0, 3.1f, 0, 6, 0.2f, 6), Box(2, 20, 2, 0, 0.3f, 4, 8) };
        var volume = MapAmbientOcclusionVolume.BakeUncached(boxes);
        byte underRoof = At(volume, 0.1, 1.2, 0.1);
        byte besideWall = At(volume, 20.6, 1.2, 0.1);
        byte open = At(volume, -25, 1.2, 25);
        Assert.Equal(255, open);
        Assert.True(underRoof < 140, "under roof " + underRoof);
        Assert.True(besideWall < open && besideWall > underRoof, "beside wall " + besideWall);
        // Cells inside the wall are dilated from their neighbours instead of staying black.
        Assert.True(At(volume, 20.0, 1.2, 0.1) > 0);
    }

    [Fact]
    public void AtlasRoundTripsAndRejectsWrongShape()
    {
        var boxes = new[] { Box(1, 3, 1, -7, 2, 2, 2) };
        var volume = MapAmbientOcclusionVolume.BakeUncached(boxes);
        var atlas = MapAmbientOcclusionVolume.ToAtlas(volume);
        Assert.Equal(MapAmbientOcclusionVolume.SizeX, atlas.Width);
        Assert.Equal(MapAmbientOcclusionVolume.SizeZ * 4, atlas.Height);
        Assert.Equal("rgba8-linear", atlas.ChannelFormat);
        Assert.Equal(volume, MapAmbientOcclusionVolume.FromAtlas(GeometryCodec.DecodeMaterial(GeometryCodec.EncodeMaterial(atlas))));
        Assert.Throws<FormatException>(() => MapAmbientOcclusionVolume.FromAtlas(new MaterialData(1, 1, "rgba8-linear", new[] { new byte[4] })));
    }

    [Theory]
    [InlineData("training-compound")]
    [InlineData("depot-yard")]
    [InlineData("oil-depot")]
    public void RealArenaBakeIsDeterministicAndPackaged(string mapId)
    {
        var plan = ProductMapPlanner.Generate(mapId);
        var clock = Stopwatch.StartNew();
        var first = MapAmbientOcclusionVolume.BakeUncached(plan.CollisionBoxes);
        clock.Stop();
        var second = MapAmbientOcclusionVolume.BakeUncached(plan.CollisionBoxes);
        Assert.Equal(first, second);
        string hash = ContentIdentity.Hash(first).ToString();
        output.WriteLine(mapId + " boxes=" + plan.CollisionBoxes.Count + " bakeMs=" + clock.ElapsedMilliseconds + " sha256=" + hash);
        Assert.Equal(MapAmbientOcclusionGolden.Sha256(mapId), hash);
        // Some cells are occluded, most of the arena is open.
        Assert.Contains(first, v => v < 128);
        Assert.True(first.Count(v => v == 255) > first.Length / 2);

        var toolchain = ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("ao-volume-tests"));
        var payloads = PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build(mapId, "low", toolchain));
        Assert.Equal(first, MapVisualClosure.DecodeAmbientOcclusionVolume(payloads));
        Assert.Contains(MapAmbientOcclusionVolume.Path, MapVisualClosure.RequiredPaths("low"));
    }
}

/// <summary>Cross-lane goldens (ADR-081): the Unity EditMode test asserts the same hashes.</summary>
internal static class MapAmbientOcclusionGolden
{
    public static string Sha256(string mapId) => mapId switch
    {
        "training-compound" => "e59f18c6edef2494f1ee767f72f58fae55233b9f5da973c71a1ac94c582c210d",
        "depot-yard" => "f7de906b6c16ac62a9b0bedadedc7885975c7ae08708d610c58feeab7888c284",
        "oil-depot" => "d4b5cdf773e7bf71e53da4a02431dbc6b576e9de8bad023090cbf15cc6dfbf89",
        _ => throw new ArgumentOutOfRangeException(nameof(mapId))
    };
}
