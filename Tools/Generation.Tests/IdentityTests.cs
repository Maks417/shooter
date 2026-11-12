using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;

public sealed class IdentityTests
{
    private static ContentId Id(char c) => new ContentId(new string(c, 64));
    [Fact]
    public void ReproducesIndependentSourceContentAndGuidFixtures()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/golden-vectors.json")));
        var g = fixture.RootElement;
        byte[] source = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/box-recipe.json"));
        var hash = ContentIdentity.SourceHash("shooter.recipe.box.v1", source);
        Assert.Equal(g.GetProperty("sourceHash").GetString(), hash.ToString());
        var args = g.GetProperty("contentKeyInputs");
        var key = ContentIdentity.ContentKey(hash, args.GetProperty("generatorId").GetString()!,
            args.GetProperty("generatorVersion").GetString()!, ContentIdentity.ParseSeed(args.GetProperty("seed").GetString()!),
            Array.Empty<ContentId>(), args.GetProperty("targetPlatform").GetString()!, args.GetProperty("visualProfile").GetString()!,
            args.GetProperty("formatVersion").GetUInt32(), new ContentId(args.GetProperty("toolchainHash").GetString()!));
        Assert.Equal(g.GetProperty("contentId").GetString(), key.ToString());
        Assert.Equal(g.GetProperty("unityGuid").GetString(), ContentIdentity.UnityGuid(g.GetProperty("logicalAssetId").GetString()!));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", ContentIdentity.Hash(Encoding.ASCII.GetBytes("abc")).ToString());
    }
    [Theory]
    [InlineData("0", 0UL)]
    [InlineData("18446744073709551615", ulong.MaxValue)]
    public void AcceptsBoundarySeeds(string value, ulong expected) => Assert.Equal(expected, ContentIdentity.ParseSeed(value));
    [Theory]
    [InlineData("01")][InlineData("-1")][InlineData("+1")][InlineData(" 1")][InlineData("18446744073709551616")][InlineData("")]
    public void RejectsAmbiguousOrOverflowSeed(string value) => Assert.Throws<ArgumentException>(() => ContentIdentity.ParseSeed(value));
    [Fact]
    public void KeySortsDependenciesAndSeparatesGameplayAndVisualVariants()
    {
        ContentId Key(string profile, ContentId[] deps, ulong seed = 0, string version = "1") =>
            ContentIdentity.ContentKey(Id('0'), "box", version, seed, deps, "windows64", profile, 1, Id('1'));
        Assert.Equal(Key("Low", new[] { Id('2'), Id('3') }), Key("Low", new[] { Id('3'), Id('2') }));
        Assert.NotEqual(Key("Low", Array.Empty<ContentId>()), Key("High", Array.Empty<ContentId>()));
        Assert.NotEqual(Key("Low", Array.Empty<ContentId>()), Key("Low", Array.Empty<ContentId>(), ulong.MaxValue));
        Assert.NotEqual(Key("Low", Array.Empty<ContentId>()), Key("Low", Array.Empty<ContentId>(), 0, "2"));
        Assert.Equal(Key("gameplay", Array.Empty<ContentId>()), ContentIdentity.GameplayKey(Id('0'), "box", "1", 0, Array.Empty<ContentId>(), "windows64", 1, Id('1')));
        Assert.Throws<ArgumentException>(() => Key("Low", new[] { Id('2'), Id('2') }));
        Assert.Throws<ArgumentException>(() => Key("Low", Enumerable.Repeat(Id('2'), 257).ToArray()));
    }
    [Fact]
    public void TopologicalOrderIgnoresDictionaryAndEdgeTraversalOrder()
    {
        var graph = new Dictionary<ContentId, IReadOnlyList<ContentId>> {
            [Id('4')] = new[] {Id('3'), Id('2')}, [Id('3')] = new[] {Id('1')},
            [Id('1')] = Array.Empty<ContentId>(), [Id('2')] = Array.Empty<ContentId>() };
        Assert.Equal(new[] { Id('1'), Id('2'), Id('3'), Id('4') }, DependencyOrder.Sort(graph));
        var reverse = graph.Reverse().ToDictionary(p => p.Key, p => (IReadOnlyList<ContentId>)p.Value.Reverse().ToArray());
        Assert.Equal(DependencyOrder.Sort(graph), DependencyOrder.Sort(reverse));
    }
    [Fact]
    public void RejectsMissingDuplicateCyclicAndOversizedDependencies()
    {
        var graph = new Dictionary<ContentId, IReadOnlyList<ContentId>> { [Id('1')] = new[] { Id('2') } };
        Assert.Throws<ArgumentException>(() => DependencyOrder.Sort(graph));
        graph[Id('2')] = new[] { Id('1') };
        Assert.Throws<ArgumentException>(() => DependencyOrder.Sort(graph));
        graph[Id('2')] = Array.Empty<ContentId>(); graph[Id('1')] = new[] {Id('2'), Id('2')};
        Assert.Throws<ArgumentException>(() => DependencyOrder.Sort(graph));
        graph[Id('1')] = Enumerable.Repeat(Id('2'), 257).ToArray();
        Assert.Throws<ArgumentException>(() => DependencyOrder.Sort(graph));
        graph.Clear(); graph[Id('1')] = new[] { Id('1') };
        Assert.Throws<ArgumentException>(() => DependencyOrder.Sort(graph));
    }
}
