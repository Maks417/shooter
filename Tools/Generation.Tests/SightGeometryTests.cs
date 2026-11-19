using System;
using System.IO;
using System.Linq;
using System.Text;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Xunit;
using Xunit.Abstractions;

/// <summary>ADR-107 rail-mounted sights: separate meshes built in each long gun's weapon space.</summary>
public sealed class SightGeometryTests
{
    private readonly ITestOutputHelper output;
    public SightGeometryTests(ITestOutputHelper output) { this.output = output; }

    public static TheoryData<string, string> Mounts()
    {
        var data = new TheoryData<string, string>();
        foreach (string kind in new[] { "ak", "ar", "dmr", "bolt", "scar", "pump" })
            foreach (string sight in WeaponSights.All)
                if (WeaponSights.HasMesh(kind, sight)) data.Add(kind, sight);
        return data;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "toolchain-lock.json"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    private static WeaponRecipe Load(string name) => WeaponRecipe.Parse(File.ReadAllBytes(
        Path.Combine(RepoRoot(), "UnityProject", "Assets", "Shooter", "Generation", "FeatureRecipes", name + ".json")));

    [Theory, MemberData(nameof(Mounts))]
    public void Sight_lods_are_valid_cheaper_and_inside_the_sight_envelope(string kind, string sight)
    {
        var lods = WeaponSights.BuildLods(kind, sight);
        Assert.Equal(4, lods.Length);
        int previous = int.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            int triangles = lods[i].Indices.Count / 3;
            output.WriteLine(sight + "@" + kind + " lod" + i + ": " + triangles + " triangles");
            Assert.InRange(triangles, 1, 9000);
            Assert.True(triangles <= previous, sight + "@" + kind + " lod" + i + " is not cheaper");
            previous = triangles;
            MeshValidation.Check(lods[i], false);
            GeometryCodec.DecodeMesh(GeometryCodec.EncodeMesh(lods[i]));
            Assert.All(lods[i].Submeshes, s => Assert.True(s.MaterialSlot <= 3 || s.MaterialSlot == 11));
        }
    }

    [Theory, MemberData(nameof(Mounts))]
    public void Line_of_sight_runs_above_the_rail_through_the_sight(string kind, string sight)
    {
        var eye = WeaponSights.EyeFor(kind, sight);
        var mesh = WeaponSights.BuildLods(kind, sight)[0];
        float top = mesh.Positions.Max(p => p.Y), bottom = mesh.Positions.Min(p => p.Y);
        Assert.Equal(0f, eye.X);
        // The eye line passes through the sight's own height band (a tube, aperture or prism), never above it.
        Assert.InRange(eye.Y, bottom, top);
        Assert.True(eye.Y > .11f, sight + "@" + kind + " eye sits below the receiver top");
    }

    [Fact]
    public void M16_prism_keeps_its_1_3_0_line_of_sight()
    {
        // The 1.3.0 M16 sight-eye socket was (0, 153, -10) mm; the reusable prism on the AR rail lands there too.
        var eye = WeaponSights.EyeFor("ar", "scope2");
        Assert.Equal(.153f, eye.Y, 3);
        Assert.Equal(-.010f, eye.Z, 3);
    }

    [Fact]
    public void Collimator_sits_no_higher_than_the_prism_on_every_rail()
    {
        foreach (string kind in new[] { "ak", "ar", "dmr", "bolt", "scar", "pump" })
            Assert.True(WeaponSights.EyeFor(kind, "collimator").Y < WeaponSights.EyeFor(kind, "scope2").Y + .0001f, kind);
    }

    [Theory]
    [InlineData("ak")]
    [InlineData("ar")]
    [InlineData("dmr")]
    [InlineData("bolt")]
    [InlineData("scar")]
    [InlineData("pump")]
    public void Collimator_eye_line_passes_only_through_clear_glass(string kind)
    {
        // ADR-115: the reflex window is open; the only surface on the line of sight is the slot-11 glass pane.
        var eye = WeaponSights.EyeFor(kind, "collimator");
        foreach (var mesh in WeaponSights.BuildLods(kind, "collimator"))
        {
            int glassHits = 0;
            foreach (var sub in mesh.Submeshes)
                for (int i = (int)sub.FirstIndex; i < sub.FirstIndex + sub.IndexCount; i += 3)
                {
                    var p0 = mesh.Positions[(int)mesh.Indices[i]];
                    var p1 = mesh.Positions[(int)mesh.Indices[i + 1]];
                    var p2 = mesh.Positions[(int)mesh.Indices[i + 2]];
                    if (!RayHits(eye.X, eye.Y, p0, p1, p2)) continue;
                    Assert.True(sub.MaterialSlot == 11, kind + ": slot " + sub.MaterialSlot + " blocks the collimator window");
                    glassHits++;
                }
            Assert.True(glassHits > 0, kind + ": no glass pane on the line of sight");
        }
    }

    /// <summary>Whether the bore-parallel line through (x, y) crosses triangle p0 p1 p2 (2D point-in-triangle in XY).</summary>
    private static bool RayHits(float x, float y, Float3 p0, Float3 p1, Float3 p2)
    {
        double D(Float3 a, Float3 b) => (b.X - a.X) * (y - a.Y) - (b.Y - a.Y) * (x - a.X);
        double d0 = D(p0, p1), d1 = D(p1, p2), d2 = D(p2, p0);
        bool neg = d0 < 0 || d1 < 0 || d2 < 0, pos = d0 > 0 || d1 > 0 || d2 > 0;
        return !(neg && pos) && (d0 != 0 || d1 != 0 || d2 != 0);
    }

    [Fact]
    public void Ak_irons_are_part_of_the_body_and_other_sights_bring_a_rail_adapter()
    {
        Assert.False(WeaponSights.HasMesh("ak", "irons"));
        Assert.Throws<ArgumentException>(() => WeaponSights.BuildLods("ak", "irons"));
        // The adapter plate sits on the top cover and stops ahead of the rear sight leaf (z .18).
        var collimator = WeaponSights.BuildLods("ak", "collimator")[0];
        Assert.True(collimator.Positions.Min(p => p.Y) >= .108f);
        Assert.True(collimator.Positions.Max(p => p.Z) < .18f);
    }

    [Fact]
    public void Bare_bodies_carry_no_optic_and_use_their_irons_line_of_sight()
    {
        foreach (var (name, kind) in new[] { ("m16", "ar"), ("mk14", "dmr"), ("mtac338", "bolt"), ("ak47", "ak"), ("m4a1", "ar"), ("m24", "bolt"), ("mk16", "scar"), ("m870", "pump") })
        {
            var recipe = Load(name);
            Assert.Equal("mount", recipe.Arsenal!.Optic);
            var body = WeaponGeometry.Build(recipe).Lods[0];
            // Scopes stood up to 8 cm proud of the rail; the bare body tops out at its rail, front sight or AK leaf.
            Assert.True(body.Positions.Max(p => p.Y) < .145f, name + " still carries an optic");
            var socket = WeaponGeometry.Sockets(recipe).Single(s => s.Id == "sight-eye").Position;
            var irons = WeaponSights.EyeFor(kind, "irons");
            Assert.Equal(irons.Y, socket.Y, 3);
            Assert.Equal(irons.Z, socket.Z, 3);
        }
    }

    [Fact]
    public void Mount_optic_needs_generator_1_4_0_and_1_4_0_long_guns_need_mount()
    {
        string Recipe(string version, string optic) => "{\"schemaId\":\"shooter.recipe.weapon.v1\",\"recipeId\":\"weapons/x\",\"generatorId\":\"geometry.weapon\",\"generatorVersion\":\"" + version +
            "\",\"seed\":\"460072030\",\"parameters\":{\"kind\":\"ar\",\"parts\":{\"barrelFrontMm\":640,\"furniture\":\"black\",\"muzzle\":\"birdcage\",\"optic\":\"" + optic + "\",\"stock\":\"fixed\"}},\"dependencies\":[]}";
        Assert.Throws<FormatException>(() => WeaponRecipe.Parse(Encoding.UTF8.GetBytes(Recipe("1.3.0", "mount"))));
        Assert.Throws<FormatException>(() => WeaponRecipe.Parse(Encoding.UTF8.GetBytes(Recipe("1.4.0", "scope2"))));
        Assert.Equal(WeaponRecipe.MountVersion, WeaponRecipe.Parse(Encoding.UTF8.GetBytes(Recipe("1.4.0", "mount"))).Version);
    }

    [Fact]
    public void Weapon_package_catalog_holds_every_allowed_sight_mesh()
    {
        var sights = WeaponMeshCatalog.All.Where(s => s.IsSight).Select(s => s.MeshKey).ToArray();
        output.WriteLine(string.Join(", ", sights));
        foreach (string kind in new[] { "ar", "dmr", "bolt" })
            foreach (string sight in new[] { "irons", "collimator", "scope2" })
                Assert.Contains(WeaponSights.MeshKey(sight, kind), sights);
        Assert.Contains("sight-collimator-ak", sights);
        Assert.Contains("sight-scope2-ak", sights);
        Assert.Contains("sight-scope4-dmr", sights);
        Assert.Contains("sight-scope8-bolt", sights);
        Assert.DoesNotContain("sight-irons-ak", sights);
        var source = WeaponMeshCatalog.Require("sight-collimator-ar");
        Assert.False(source.IsProp);
        Assert.False(source.HasMagazineCycle);
        Assert.Null(WeaponMeshCatalog.MagazinePath("sight-collimator-ar"));
        Assert.Equal("mesh-sight-collimator-ar-lod0.bin", WeaponMeshCatalog.LodPath("sight-collimator-ar", 0));
        Assert.Equal(WeaponMeshCatalog.SightFingerprint, source.Fingerprint);
    }
}
