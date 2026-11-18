using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;
using Xunit.Abstractions;

/// <summary>ADR-102 archetypes built from the shipped feature recipes (ADR-106 geometry.weapon 1.3.0 profile build).</summary>
public sealed class ArsenalGeometryTests
{
    private readonly ITestOutputHelper output;
    public ArsenalGeometryTests(ITestOutputHelper output) { this.output = output; }

    public static readonly string[] Names = { "ak47", "ak102", "m16", "mk14", "mtac338", "g17", "golden-eagle" };
    public static TheoryData<string> Recipes() { var data = new TheoryData<string>(); foreach (var n in Names) data.Add(n); return data; }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "toolchain-lock.json"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    private static WeaponRecipe Load(string name) => WeaponRecipe.Parse(File.ReadAllBytes(
        Path.Combine(RepoRoot(), "UnityProject", "Assets", "Shooter", "Generation", "FeatureRecipes", name + ".json")));

    [Theory, MemberData(nameof(Recipes))]
    public void Archetype_lods_are_valid_ordered_and_inside_the_envelope(string name)
    {
        var recipe = Load(name);
        // ADR-107: long guns ship as bare-rail 1.4.0 bodies (sights are separate meshes); pistols stay on 1.3.0.
        Assert.Equal(recipe.IsLongGun ? WeaponRecipe.MountVersion : WeaponRecipe.ProfileVersion, recipe.Version);
        Assert.NotNull(recipe.Arsenal);
        var build = WeaponGeometry.Build(recipe);
        bool pistol = !recipe.IsLongGun;
        int[] ceilings = pistol ? new[] { 16000, 7000, 2500, 1200 } : new[] { 34000, 14000, 5000, 2500 };
        int previous = int.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            var mesh = build.Lods[i]; int triangles = mesh.Indices.Count / 3;
            output.WriteLine(name + " lod" + i + ": " + triangles + " triangles, " + mesh.Submeshes.Count + " submeshes");
            Assert.InRange(triangles, 1, ceilings[i]);
            Assert.True(triangles < previous, name + " lod" + i + " is not cheaper than the previous LOD");
            previous = triangles;
            GeometryCodec.DecodeMesh(GeometryCodec.EncodeMesh(mesh));
            MeshValidation.Check(mesh, false);
            Assert.All(mesh.Submeshes, s => Assert.True(s.MaterialSlot <= 6 && s.MaterialSlot != 4));
        }
        foreach (var part in new[] { build.Magazine, build.CyclePart }) MeshValidation.Check(part, true);
        int assembled = (build.Lods[0].Indices.Count + build.Magazine.Indices.Count + build.CyclePart.Indices.Count) / 3;
        Assert.InRange(assembled, 1, pistol ? 24000 : 40000);
    }

    [Theory, MemberData(nameof(Recipes))]
    public void Sockets_sit_on_the_weapon(string name)
    {
        var recipe = Load(name);
        var build = WeaponGeometry.Build(recipe);
        var ids = build.Sockets.Select(s => s.Id).ToArray();
        Assert.Contains("muzzle", ids); Assert.Contains("sight-eye", ids); Assert.Contains("left-hand-grip", ids);
        Assert.Equal(recipe.IsLongGun, ids.Contains("shoulder"));
        var muzzle = build.Sockets.Single(s => s.Id == "muzzle").Position;
        // The muzzle socket is the front of the mesh: nothing sticks out more than the bore lip ahead of it.
        float front = build.Lods[0].Positions.Max(p => p.Z);
        Assert.InRange(front - muzzle.Z, -0.001f, 0.0125f);
        Assert.Equal(WeaponGeometry.Sockets(recipe).Count, build.Sockets.Count);
    }

    [Fact]
    public void Archetypes_have_distinct_silhouettes()
    {
        var lengths = Names.ToDictionary(n => n, n => { var m = WeaponGeometry.Build(Load(n)).Lods[0]; return m.Positions.Max(p => p.Z) - m.Positions.Min(p => p.Z); });
        foreach (var kv in lengths) output.WriteLine(kv.Key + " length " + kv.Value.ToString("0.000") + " m");
        Assert.True(lengths["mtac338"] > lengths["m16"]);
        Assert.True(lengths["m16"] > lengths["ak47"]);
        Assert.True(lengths["ak47"] > lengths["ak102"]);
        Assert.True(lengths["golden-eagle"] > lengths["g17"]);
        Assert.True(lengths["ak102"] > 0.75f && lengths["mtac338"] > 1.2f);
    }

    [Fact]
    public void Finishes_use_wood_and_gold_slots_only_where_requested()
    {
        bool Uses(string name, ushort slot) => WeaponGeometry.Build(Load(name)).Lods[0].Submeshes.Any(s => s.MaterialSlot == slot);
        Assert.True(Uses("ak47", 5));
        Assert.False(Uses("ak102", 5));
        Assert.True(Uses("golden-eagle", 6));
        Assert.False(Uses("g17", 6));
    }

    [Fact]
    public void Recipe_round_trips_canonically_and_rejects_bad_parts()
    {
        var recipe = Load("ak47");
        Assert.True(new WeaponRecipeValidator().Validate(recipe.Document).Valid);
        string Bad(string parts) => "{\"schemaId\":\"shooter.recipe.weapon.v1\",\"recipeId\":\"weapons/x\",\"generatorId\":\"geometry.weapon\",\"generatorVersion\":\"1.2.0\",\"seed\":\"460072030\",\"parameters\":{\"kind\":\"ak\",\"parts\":" + parts + "},\"dependencies\":[]}";
        Assert.Throws<FormatException>(() => WeaponRecipe.Parse(Encoding.UTF8.GetBytes(Bad("{\"barrelFrontMm\":900,\"furniture\":\"wood\",\"muzzle\":\"slant\",\"optic\":\"irons\",\"stock\":\"wood\"}"))));
        Assert.Throws<FormatException>(() => WeaponRecipe.Parse(Encoding.UTF8.GetBytes(Bad("{\"barrelFrontMm\":560,\"furniture\":\"chrome\",\"muzzle\":\"slant\",\"optic\":\"irons\",\"stock\":\"wood\"}"))));
        Assert.Throws<FormatException>(() => WeaponRecipe.Parse(Encoding.UTF8.GetBytes(Bad("{\"barrelFrontMm\":560,\"furniture\":\"wood\",\"muzzle\":\"slant\",\"optic\":\"irons\",\"stock\":\"none\"}"))));
    }

    [Fact]
    public void Asset_generator_emits_lods_moving_parts_and_collision()
    {
        var recipe = Load("mtac338");
        var visual = new WeaponAssetGenerator().Generate(new GenerationRequest(recipe.Document, "windows64", "High", new ContentId(new string('1', 64))), CancellationToken.None);
        Assert.Equal(ErrorCode.None, visual.Error);
        Assert.Equal(6, visual.Artifacts.Count);
        var gameplay = new WeaponAssetGenerator().Generate(new GenerationRequest(recipe.Document, "windows64", "gameplay", new ContentId(new string('1', 64))), CancellationToken.None);
        Assert.Equal(ErrorCode.None, gameplay.Error);
        Assert.Single(gameplay.Artifacts);
    }
}
