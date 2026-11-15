using System;
using System.Linq;
using System.Text;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;
using Xunit.Abstractions;

public sealed class WeaponTests
{
    private readonly ITestOutputHelper output;
    public WeaponTests(ITestOutputHelper output) { this.output = output; }
    private static byte[] Source(string kind = "rifle", string seed = "460072030", string deps = "[]", string silhouette = "standard") => Encoding.UTF8.GetBytes(
        "{\"recipeId\":\"weapons/" + kind + "\",\"schemaId\":\"shooter.recipe.weapon.v1\",\"generatorId\":\"geometry.weapon\",\"generatorVersion\":\"1.0.5\",\"seed\":\"" + seed + "\",\"parameters\":{\"kind\":\"" + kind + "\",\"silhouette\":\"" + silhouette + "\"},\"dependencies\":" + deps + "}");
    private static GenerationRequest Request(WeaponRecipe recipe, string profile) => new GenerationRequest(recipe.Document, "windows64", profile, new ContentId(new string('1', 64)));
    [Theory]
    [InlineData("rifle")][InlineData("pistol")]
    public void FourRuleDerivedLodsAreValidAndFitTriangleBudgets(string kind)
    {
        var build = WeaponGeometry.Build(WeaponRecipe.Parse(Source(kind)));
        int[] ceilings = kind == "rifle" ? new[] {18000,8000,3000,800} : new[] {8000,4000,1500,400};
        Assert.Equal(4, build.Lods.Count); int previous = int.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            var mesh = build.Lods[i]; int triangles = mesh.Indices.Count / 3;
            Assert.InRange(triangles, 1, ceilings[i]); Assert.True(triangles < previous); previous = triangles;
            GeometryCodec.DecodeMesh(GeometryCodec.EncodeMesh(mesh));
            MeshValidation.Check(mesh, false); // Closed solid components can overlap at mechanical joins.
            Assert.All(mesh.Submeshes, s => Assert.InRange(s.MaterialSlot, (ushort)0, (ushort)4));
            Assert.All(mesh.Positions, p => Assert.True(p.X >= build.Bounds.Min.X && p.X <= build.Bounds.Max.X && p.Y >= build.Bounds.Min.Y && p.Y <= build.Bounds.Max.Y && p.Z >= build.Bounds.Min.Z && p.Z <= build.Bounds.Max.Z));
            output.WriteLine(kind + " lod" + i + ": " + triangles + " triangles, " + mesh.Positions.Count + " vertices, " + mesh.Submeshes.Count + " submeshes");
        }
    }
    [Theory]
    [InlineData("rifle", 18000)]
    [InlineData("pistol", 8000)]
    public void AssembledNearFieldWeaponIncludesMovingPartsInsideBudget(string kind, int ceiling)
    {
        var build = WeaponGeometry.Build(WeaponRecipe.Parse(Source(kind)));
        int triangles = (build.Lods[0].Indices.Count + build.Magazine.Indices.Count + build.CyclePart.Indices.Count) / 3;
        output.WriteLine(kind + " assembled lod0: " + triangles);
        Assert.InRange(triangles, 1, ceiling);
        foreach (var part in new[] { build.Magazine, build.CyclePart })
        {
            MeshValidation.Check(part, true);
            Assert.All(part.Submeshes, sub => Assert.InRange(sub.MaterialSlot, (ushort)0, (ushort)4));
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadoutPropsAreClosedDeterministicAndHaveLegibleProfiles(bool grenade)
    {
        for (int lod = 0; lod < 4; lod++)
        {
            var mesh = grenade ? LoadoutPropGeometry.BuildGrenade(lod) : LoadoutPropGeometry.BuildKnife(lod);
            var repeat = grenade ? LoadoutPropGeometry.BuildGrenade(lod) : LoadoutPropGeometry.BuildKnife(lod);
            MeshValidation.Check(mesh, true);
            Assert.Equal(GeometryCodec.EncodeMesh(mesh), GeometryCodec.EncodeMesh(repeat));
            Assert.All(mesh.Submeshes, sub => Assert.InRange(sub.MaterialSlot, (ushort)0, (ushort)4));
            Assert.InRange(mesh.Indices.Count / 3, 1, grenade ? 3000 : 1800);
            if (grenade)
            {
                float height = mesh.Positions.Max(p => p.Y) - mesh.Positions.Min(p => p.Y);
                float depth = mesh.Positions.Max(p => p.Z) - mesh.Positions.Min(p => p.Z);
                Assert.True(height > depth * 1.4f, "Shell and fuse must stand upright in the hand.");
                Assert.Contains(mesh.Submeshes, sub => sub.MaterialSlot == 4);
            }
            else
            {
                var tip = mesh.Positions.Where(p => p.Z > .158f).ToArray();
                Assert.NotEmpty(tip);
                Assert.All(tip, p => Assert.True(Math.Abs(p.Y - .006f) < .001f && Math.Abs(p.X) < .001f));
                Assert.Contains(mesh.Submeshes, sub => sub.MaterialSlot == 2);
            }
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => LoadoutPropGeometry.BuildKnife(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoadoutPropGeometry.BuildGrenade(-1));
    }
    [Theory]
    [InlineData("rifle",0,.08f,.64f,8)][InlineData("pistol",0,.07f,.13f,7)]
    public void SocketsMatchAssignedMechanicalBaseline(string kind, float muzzleX, float muzzleY, float muzzleZ, int count)
    {
        var build = WeaponGeometry.Build(WeaponRecipe.Parse(Source(kind))); Assert.Equal(count, build.Sockets.Count);
        Assert.Equal(build.Sockets.Select(s => s.Id).OrderBy(s => s, StringComparer.Ordinal), build.Sockets.Select(s => s.Id));
        var muzzle = Assert.Single(build.Sockets, s => s.Id == "muzzle");
        Assert.Equal(muzzleX, muzzle.Position.X); Assert.Equal(muzzleY, muzzle.Position.Y); Assert.Equal(muzzleZ, muzzle.Position.Z);
        var grip = Assert.Single(build.Sockets, s => s.Id == "right-hand-grip"); Assert.Equal(0, grip.Position.LengthSquared);
        Assert.All(build.Sockets, s => { Assert.Equal(0, s.Rotation.X); Assert.Equal(0, s.Rotation.Y); Assert.Equal(0, s.Rotation.Z); Assert.Equal(1, s.Rotation.W); });
        Assert.Equal(kind == "rifle", build.Sockets.Any(s => s.Id == "shoulder"));
        var sight = Assert.Single(build.Sockets, s => s.Id == "sight-eye");
        Assert.Equal(kind == "rifle" ? .115f : .1f, sight.Position.Y);
    }
    [Theory]
    [InlineData("rifle")][InlineData("pistol")]
    public void SeedsDoNotChangeMechanicsTopologyOrSockets(string kind)
    {
        var baseline = WeaponGeometry.Build(WeaponRecipe.Parse(Source(kind, "0")));
        foreach (string seed in new[] {"42", "460072030", "18446744073709551615"})
        {
            var other = WeaponGeometry.Build(WeaponRecipe.Parse(Source(kind, seed)));
            for (int lod = 0; lod < 4; lod++) Assert.Equal(GeometryCodec.EncodeMesh(baseline.Lods[lod]), GeometryCodec.EncodeMesh(other.Lods[lod]));
            Assert.Equal(GeometryCodec.EncodeCollision(baseline.Collision), GeometryCodec.EncodeCollision(other.Collision));
            for (int i = 0; i < baseline.Sockets.Count; i++)
            {
                var a = baseline.Sockets[i]; var b = other.Sockets[i]; Assert.Equal(a.Id, b.Id);
                Assert.Equal(a.Position.X, b.Position.X); Assert.Equal(a.Position.Y, b.Position.Y); Assert.Equal(a.Position.Z, b.Position.Z);
            }
        }
    }
    [Theory]
    [InlineData("rifle")][InlineData("pistol")]
    public void GeneratorSeparatesVisualAndGameplayArtifacts(string kind)
    {
        var recipe = WeaponRecipe.Parse(Source(kind)); var generator = new WeaponAssetGenerator();
        var gameplay = generator.Generate(Request(recipe, "gameplay"), CancellationToken.None);
        Assert.Equal(ErrorCode.None, gameplay.Error); Assert.Equal(ContentKind.Collision, Assert.Single(gameplay.Artifacts).Kind);
        ContentManifest.Create(Request(recipe, "gameplay"), recipe.SourceHash, gameplay);
        var fingerprint = GeneratorFingerprint.Compute(WeaponRecipe.GeneratorId, WeaponRecipe.GeneratorVersion);
        byte[][]? expected = null;
        foreach (string profile in new[] {"Low","Balanced","High"})
        {
            var request = Request(recipe, profile); var result = generator.Generate(request, CancellationToken.None);
            Assert.Equal(ErrorCode.None, result.Error); Assert.Equal(6, result.Artifacts.Count);
            Assert.All(result.Artifacts, a => Assert.Equal(ContentKind.Mesh, a.Kind));
            Assert.Equal(new[] {"lod0","lod1","lod2","lod3","magazine","cycle"}, result.Artifacts.Select(a => a.Variant));
            var bodies = result.Artifacts.Select(a => ArtifactCodec.Decode(a.Payload, a.Kind, fingerprint, request.ToolchainHash)).ToArray();
            if (expected == null) expected = bodies; else for (int i = 0; i < 6; i++) Assert.Equal(expected[i], bodies[i]);
            ContentManifest.Create(request, recipe.SourceHash, result, new[] {"metal","polymer"});
        }
    }
    [Theory]
    [InlineData("unknown")][InlineData("")]
    public void RejectsUnknownWeaponKind(string kind) => Assert.Throws<FormatException>(() => WeaponRecipe.Parse(Source(kind)));
    [Fact]
    public void SchemaRejectsUnknownFieldsRawVerticesVersionsAndAmbiguousSeeds()
    {
        string source = Encoding.UTF8.GetString(Source());
        foreach (string bad in new[] {
            source.Replace("\"kind\":\"rifle\"", "\"kind\":\"rifle\",\"vertices\":[[0,0,0]]"),
            source.Replace("\"seed\":\"460072030\"", "\"seed\":\"01\""),
            source.Replace("\"seed\":\"460072030\"", "\"seed\":\"18446744073709551616\""),
            source.Replace("\"generatorVersion\":\"1.0.5\"", "\"generatorVersion\":\"2.0.0\""),
            source.Replace("\"dependencies\":[]", "\"dependencies\":[],\"unexpected\":0"),
            source.Replace("\"kind\":\"rifle\"", "\"kind\":\"rifle\",\"kind\":\"pistol\"") })
            Assert.Throws<FormatException>(() => WeaponRecipe.Parse(Encoding.UTF8.GetBytes(bad)));
    }
    [Fact]
    public void RecipeIsDefensiveAndRejectsDependencyDuplicatesOrOrdering()
    {
        var recipe = WeaponRecipe.Parse(Source()); byte[] first = recipe.CanonicalSource; first[0] = 0;
        Assert.Equal((byte)'{', recipe.CanonicalSource[0]); Assert.True(new WeaponRecipeValidator().Validate(recipe.Document).Valid);
        string a = "\"" + new string('1', 64) + "\"", b = "\"" + new string('2', 64) + "\"";
        Assert.Throws<FormatException>(() => WeaponRecipe.Parse(Source(deps: "[" + a + "," + a + "]")));
        Assert.Throws<FormatException>(() => WeaponRecipe.Parse(Source(deps: "[" + b + "," + a + "]")));
        var withDependency = WeaponRecipe.Parse(Source(deps: "[" + a + "]"));
        Assert.Equal(ErrorCode.InvalidInput, new WeaponAssetGenerator().Generate(Request(withDependency, "Low"), CancellationToken.None).Error);
    }
    [Fact]
    public void CancellationAndUnknownProfilesReturnExplicitFailures()
    {
        var generator = new WeaponAssetGenerator(); var recipe = WeaponRecipe.Parse(Source());
        Assert.Equal(ErrorCode.Cancelled, generator.Generate(Request(recipe, "Low"), new CancellationToken(true)).Error);
        Assert.Equal(ErrorCode.InvalidInput, generator.Generate(Request(recipe, "Ultra"), CancellationToken.None).Error);
        var badVersion = new RecipeDocument(new DefinitionId("weapons/rifle"), WeaponRecipe.Schema, WeaponRecipe.GeneratorId, "9.0.0", "0", recipe.Document.Parameters, Array.Empty<ContentId>());
        Assert.False(new WeaponRecipeValidator().Validate(badVersion).Valid);
        Assert.Equal(ErrorCode.UnsupportedVersion, generator.Generate(new GenerationRequest(badVersion,"windows64","Low",new ContentId(new string('1',64))),CancellationToken.None).Error);
    }
    [Fact]
    public void OldGeneratorVersionRejectsInsteadOfReinterpretingExistingContent()
    {
        foreach (string legacy in new[] { "1.0.0", "1.0.1", "1.0.2", "1.0.3", "1.0.4" })
        {
            var source = Encoding.UTF8.GetString(Source()).Replace("\"generatorVersion\":\"1.0.5\"", "\"generatorVersion\":\"" + legacy + "\"");
            Assert.Throws<FormatException>(() => WeaponRecipe.Parse(Encoding.UTF8.GetBytes(source)));
            var current = WeaponRecipe.Parse(Source());
            var old = new RecipeDocument(current.Document.RecipeId, WeaponRecipe.Schema, WeaponRecipe.GeneratorId, legacy, current.Document.Seed, current.Document.Parameters, Array.Empty<ContentId>());
            Assert.False(new WeaponRecipeValidator().Validate(old).Valid);
            Assert.Equal(ErrorCode.UnsupportedVersion, new WeaponAssetGenerator().Generate(new GenerationRequest(old, "windows64", "gameplay", new ContentId(new string('1',64))), CancellationToken.None).Error);
        }
    }
    [Fact]
    public void CarbineSilhouetteBuildsDistinctPrimaryInsideSharedEnvelope()
    {
        var standard = WeaponGeometry.Build(WeaponRecipe.Parse(Source("rifle", silhouette: "standard")));
        var carbine = WeaponGeometry.Build(WeaponRecipe.Parse(Source("rifle", silhouette: "carbine")));
        Assert.Equal(standard.Bounds.Min.X, carbine.Bounds.Min.X);
        Assert.Equal(standard.Bounds.Max.Z, carbine.Bounds.Max.Z);
        Assert.Equal(standard.Sockets.Count, carbine.Sockets.Count);
        Assert.NotEqual(
            ContentIdentity.Hash(GeometryCodec.EncodeMesh(standard.Lods[0])),
            ContentIdentity.Hash(GeometryCodec.EncodeMesh(carbine.Lods[0])));
    }
    [Fact]
    public void RifleMagazineVisualAndProxyJoinTheReceiverAndMagazineWell()
    {
        var build = WeaponGeometry.Build(WeaponRecipe.Parse(Source()));
        var magazine = Assert.Single(build.Collision.Boxes, b => Math.Abs(b.Shape.Center.Z - .07f) < 1e-6 && b.Shape.Center.Y < -.05f).Shape;
        var rotation = magazine.Rotation;
        float yExtent = Math.Abs(1 - 2 * rotation.X * rotation.X) * magazine.Size.Y / 2 + Math.Abs(2 * rotation.X * rotation.W) * magazine.Size.Z / 2;
        Assert.True(magazine.Center.Y + yExtent >= .02f, "Magazine must overlap the receiver bottom at .020m, not float below its well.");
        Assert.True(magazine.Center.Y - yExtent >= build.Bounds.Min.Y);
        // Detachable magazine mesh uses the same mechanical dimensions as the proxy (hard-edge LOD for corner match).
        var magMesh = WeaponGeometry.BuildMagazine("rifle", 2);
        float halfY = magazine.Size.Y / 2, halfZ = magazine.Size.Z / 2;
        float expectedY = magazine.Center.Y + (1 - 2 * rotation.X * rotation.X) * halfY - 2 * rotation.X * rotation.W * halfZ;
        float expectedZ = magazine.Center.Z + 2 * rotation.X * rotation.W * halfY + (1 - 2 * rotation.X * rotation.X) * halfZ;
        Assert.Contains(magMesh.Positions, p => Math.Abs(p.X - magazine.Size.X / 2) < 1e-6 && Math.Abs(p.Y - expectedY) < 1e-6 && Math.Abs(p.Z - expectedZ) < 1e-6);
        Assert.True(build.Magazine.Positions.Count > 0);
        Assert.True(build.CyclePart.Positions.Count > 0);
    }
    [Theory]
    [InlineData("rifle", .0041f, .03f, .07f, .020f, -.036f)]
    [InlineData("pistol", .0036f, .01f, .05f, .0175f, -.016f)]
    public void TriggerTopJoinsBodyWhileLowerGuardOpeningStaysClear(string kind, float halfWidth, float zMin, float zMax, float bodyBottom, float guardTop)
    {
        var build = WeaponGeometry.Build(WeaponRecipe.Parse(Source(kind)));
        foreach (var lod in build.Lods.Take(2))
        {
            var trigger = lod.Positions.Where(p => Math.Abs(p.X) <= halfWidth && p.Z >= zMin && p.Z <= zMax && p.Y > -.03f && p.Y < .035f).ToArray();
            Assert.NotEmpty(trigger);
            Assert.True(trigger.Max(p => p.Y) > bodyBottom, "Trigger must enter the receiver/frame rather than float in its guard.");
            Assert.True(trigger.Min(p => p.Y) > guardTop + .005f, "Trigger must leave an open gap above the lower trigger guard.");
        }
    }
}
