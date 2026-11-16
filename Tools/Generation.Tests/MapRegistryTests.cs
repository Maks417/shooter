using System;
using System.Linq;
using System.Text;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Xunit;

/// <summary>ADR-083: map registry, arena layout documents and the generic map.arena recipe.</summary>
[Collection("NativeNavProvider")]
public sealed class MapRegistryTests
{
    private static readonly ContentId Toolchain = ContentIdentity.Hash(Encoding.ASCII.GetBytes("map-registry-golden"));

    // Captured from the pre-ADR-083 C# planners (commit b1fd112); the registry and layout documents must not move them.
    [Theory]
    [InlineData("training-compound", "0acdcbcc9c56dd9297a3cf27e1c822fed158412cd012290718df1843f1220cb9", "1b506b637da07266ec4bb9ec53f3612121988965d84d2532c9f277043730cb4d",
        "84df2eed88230e9115b70568319a7d1af8277e76444f1184b2c15cae449959b1", "d91f0d513b4375b8a1b46287e236869a47377278d8aa2487832ea64752b2eab6")]
    [InlineData("depot-yard", "ee0a4cae11f63649f645278b07af6ad4b3c262ab370cf2e5aeab91780f77dea0", "35855448e5203c5725a749a55f56793c3975828608849055844915eae59ec403",
        "ebf442ecd59900d604b581475bfaa09fcd026bd52e6dc625217e3ff8af0703d1", "764fb5808141ef3876d85feed74547c553e13d7f4ebfba6032333025f78d702c")]
    [InlineData("oil-depot", "62017fa929c60f085fdd9e728f43b1a5f01a80b294bebcb75b0d83e0efae58a2", "f6f95df6c1bed8177fe89bb14cc8c847611b679ef653643733ed4b4e5290fb71",
        "4322540497839f0007f7a20aac7b561a3345ed65e91c4f44040948dd4fe8e341", "ef40f57d1d82264b6ddcc00e88dd3fdbb68cb20c422dce32f45520502018a8c4")]
    public void RegisteredMapsRetainGameplayAndIntentionallyMigrateVisualClosures(string mapId, string collision, string nav, string gameplayRoot, string lowRoot)
    {
        var plan = ProductMapPlanner.Generate(mapId);
        Assert.Equal(collision, plan.CollisionHash.ToString());
        Assert.Equal(nav, plan.NavHash.ToString());
        Assert.Equal(gameplayRoot, MapPackageBuilder.Build(mapId, "gameplay", Toolchain).PackageRootHash.ToString());
        // ADR-088/090/092/094 intentionally publish new visual bytes; the old golden remains as a migration guard.
        var visual=MapPackageBuilder.Build(mapId,"low",Toolchain);
        Assert.NotEqual(lowRoot,visual.PackageRootHash.ToString());
        Assert.Contains(visual.Dependencies,d=>d.Path==ProceduralDecals.AtlasPath);
        Assert.Contains(visual.Dependencies,d=>d.Path==Shooter.Generation.Fx.FxFlipbook.Path(Shooter.Generation.Fx.FlipbookKind.Smoke));
    }

    [Fact]
    public void RegistryDrivesRecipeIdentityAndCatalogOrder()
    {
        Assert.Equal(new[] { "training-compound", "depot-yard", "oil-depot" }, MapRegistry.MapIds);
        Assert.Equal(MapRegistry.MapIds, ReleaseCatalogDocument.RequiredMaps);
        Assert.Equal(460072031UL, MapRecipe.ApprovedSeed("depot-yard"));
        Assert.Equal("map.oil", MapRecipe.GeneratorIdFor("oil-depot"));
        Assert.Equal(MapRecipe.CompoundSchema, MapRecipe.SchemaFor("training-compound"));
        Assert.Equal("Oil Depot", MapRecipe.DisplayName("oil-depot"));
        Assert.False(MapRecipe.IsApprovedMapId("unknown-map"));
        foreach (string id in MapRegistry.MapIds)
            Assert.Equal(id, MapRecipe.Parse(MapRecipe.CreateSource(id)).MapId);
        Assert.Equal("compound", MapRegistry.Require("training-compound").SkyPreset);
        Assert.Equal("asphalt", MapRegistry.Require("oil-depot").GroundSurface);
    }

    [Fact]
    public void CompoundIsALayoutDocumentWithThePlannerIdentity()
    {
        var definition = MapRegistry.Require("training-compound");
        Assert.True(definition.HasLayoutDocument);
        var document = definition.LayoutDocument;
        Assert.Equal(CompoundArenaPlanner.GeneratorId, document.GeneratorId);
        Assert.Equal(CompoundArenaPlanner.GeneratorVersion, document.GeneratorVersion);
        Assert.Equal(CompoundArenaPlanner.StableIdBase, document.StableIdBase);
        Assert.Equal(ArenaSymmetry.Rotate180, document.Layout.Symmetry);
        Assert.Equal(40, document.OperationCount-document.Decals.Count);
        Assert.Equal(2,document.DocumentVersion);Assert.Equal(3,document.Decals.Count);
        Assert.False(MapRegistry.Require("depot-yard").HasLayoutDocument);
        Assert.Equal(new[] { "depot-arena", "oil-arena" }, MapRegistry.CodePlannerNames.OrderBy(n => n, StringComparer.Ordinal));
    }

    private const string Arena = "\"arena\":{\"halfExtentX\":32000,\"halfExtentZ\":40000,\"staging\":[-32000,32000,-40000,-30000],\"team1Anchor\":[0,0,-34000],\"team1Exits\":[[-20000,0,-29000]],\"lanePoints\":[[0,0,0]],\"symmetry\":\"MirrorZ\"}";
    private static string Doc(string ops) => "{\"planner\":{\"id\":\"environment.test.arena\",\"version\":\"1.0.0\",\"idBase\":50000}," + Arena + ",\"ops\":[" + ops + "]}";

    [Fact]
    public void LayoutDocumentsBuildMirroredKitObjects()
    {
        var document = ArenaLayoutDocument.Parse(Doc(
            "{\"op\":\"perimeter\",\"height\":5000,\"thickness\":1000}," +
            "{\"op\":\"shell\",\"x\":0,\"z\":-20000,\"sizeX\":10000,\"sizeZ\":8000,\"thickness\":400,\"height\":3500,\"doors\":[{\"side\":\"North\",\"offset\":0,\"width\":2400}],\"roof\":true}," +
            "{\"op\":\"vehicle\",\"kind\":\"Truck\",\"x\":12000,\"z\":-10000,\"length\":7000,\"width\":2500,\"height\":3000,\"alongX\":false,\"threat\":[12000,0,0],\"facing\":-1}," +
            "{\"op\":\"sandbags\",\"x\":-8000,\"z\":-6000,\"length\":3000,\"alongX\":true,\"threat\":[-8000,0,0]}"));
        var plan = document.Generate(7);
        Assert.Contains(plan.CollisionBoxes, b => Math.Abs(b.Center.X - 12f) < 1e-4f && Math.Abs(b.Center.Z + 10f) < 1e-4f);
        Assert.Contains(plan.CollisionBoxes, b => Math.Abs(b.Center.X - 12f) < 1e-4f && Math.Abs(b.Center.Z - 10f) < 1e-4f);
        Assert.Contains(plan.Features, f => f.Kind == ArenaFeatureKind.Truck);
        Assert.Equal(plan.CollisionHash, document.Generate(7).CollisionHash);
    }

    [Theory]
    [InlineData("{\"op\":\"teleporter\"}", "unknown op")]
    [InlineData("{\"op\":\"perimeter\",\"height\":5000,\"thickness\":1000,\"colour\":1}", "unknown field colour")]
    [InlineData("{\"op\":\"perimeter\",\"height\":5000.5,\"thickness\":1000}", "wrong type")]
    [InlineData("{\"op\":\"vehicle\",\"kind\":\"Hovercraft\",\"x\":0,\"z\":0,\"length\":1,\"width\":1,\"height\":1,\"alongX\":true,\"threat\":[0,0,0]}", "Unknown feature kind")]
    [InlineData("{\"op\":\"cover\",\"x\":0,\"z\":0,\"sizeX\":1,\"sizeY\":1,\"sizeZ\":1}", "missing threat")]
    public void LayoutDocumentsRejectUnknownOrMalformedOperations(string op, string expected)
    {
        var error = Assert.Throws<FormatException>(() => ArenaLayoutDocument.Parse(Doc(op)));
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void GenericArenaRecipeRequiresARegisteredMap()
    {
        string source = "{\"schemaId\":\"shooter.recipe.map-arena.v1\",\"recipeId\":\"harbor\",\"generatorId\":\"map.arena\",\"generatorVersion\":\"1.0.1\",\"seed\":\"7\"," +
                        "\"parameters\":{\"mapId\":\"harbor\",\"halfExtentMeters\":128,\"chunkSizeMeters\":32,\"navAdapterFormat\":\"unity.navmesh.bake.v1\",\"navAdapterVersion\":1},\"dependencies\":[]}";
        Assert.Contains("not registered", Assert.Throws<FormatException>(() => MapRecipe.Parse(Encoding.ASCII.GetBytes(source))).Message);
        Assert.Equal(MapRecipe.ArenaSchema, MapRecipe.SchemaForGenerator(MapRecipe.ArenaGenerator));
        // A frozen per-map generator cannot be reused for a different registered map.
        string crossed = Encoding.ASCII.GetString(MapRecipe.CreateSource("depot-yard")).Replace("map.depot", "map.oil").Replace("map-depot", "map-oil");
        Assert.Throws<FormatException>(() => MapRecipe.Parse(Encoding.ASCII.GetBytes(crossed)));
    }
}
