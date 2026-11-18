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

    // ADR-109 site plans replace the ADR-074 arena layouts: collision, nav and gameplay roots re-frozen from the site planners.
    // The low-profile roots are the pre-ADR-088 goldens, kept as a migration guard (visual bytes must not fall back to them).
    [Theory]
    [InlineData("training-compound", "2c94e2d1959c0b2291ef46ed362ffc7719ba8dfed6d5e29eb2192cfd3c6efc44", "cf1fc0367c0750646497336bdeddd85a380d1682ae56e71bf0ac8345b62c458f",
        "68b8aacce2c8ae0fa784683e9820f664f0539faa0e203236f65af4ecaa6304b2", "d91f0d513b4375b8a1b46287e236869a47377278d8aa2487832ea64752b2eab6")]
    [InlineData("depot-yard", "47e3deffc76a60847d03923b96c66f6c1f0a07161a28e2db51ccfec7baaa2f73", "89aa9d46f8143f1df35fe383e9d4c0d1931e6eb221b08102da3e3802a73fd7eb",
        "65d600b28bd7816c931214677fe311cdb6ebbe755927ae2f0e2c650e905e49f8", "764fb5808141ef3876d85feed74547c553e13d7f4ebfba6032333025f78d702c")]
    [InlineData("oil-depot", "7531240dfac4239c7b63612d9fe520d49bf3e33e30ad79f767735904d4c3f922", "c5b0149a2660a370c7ad5195181c61392fe5da506b77c4722cff837bd9cf5098",
        "0ec0833164eb34b41690b725088c2c600860e3c2472424c512fff8623889433f", "ef40f57d1d82264b6ddcc00e88dd3fdbb68cb20c422dce32f45520502018a8c4")]
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
    public void ProductMapsAreSitePlannedCodePlanners()
    {
        // ADR-109: every product map is a site plan (buildings, zones, box roles), not a layout document.
        foreach (string id in MapRegistry.MapIds)
        {
            var definition = MapRegistry.Require(id);
            Assert.False(definition.HasLayoutDocument);
            Assert.StartsWith("code:", definition.Planner);
            Assert.NotNull(definition.Generate().Site);
        }
        Assert.Equal("Old Town", MapRegistry.Require("training-compound").DisplayName);
        Assert.Equal("Harbour", MapRegistry.Require("depot-yard").DisplayName);
        Assert.Equal(ArenaSymmetry.Rotate180, OldTownSitePlanner.Layout.Symmetry);
        Assert.Equal(new[] { "harbour-site", "oil-site", "town-site" }, MapRegistry.CodePlannerNames.OrderBy(n => n, StringComparer.Ordinal));
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
