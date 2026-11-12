using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;

public sealed class ManifestTests
{
    private static ContentManifest Fixture()
    {
        string source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/box-recipe.json"))
            .Replace("shooter.recipe.box.v1", "shooter.recipe.box-collision.v1").Replace("geometry.box", "collision.box");
        var recipe = BoxRecipe.Parse(System.Text.Encoding.UTF8.GetBytes(source));
        var request = new GenerationRequest(recipe.Document, "windows64", "gameplay", new ContentId(new string('1', 64)));
        return ContentManifest.Create(request, recipe.SourceHash, new CpuBoxCollisionGenerator().Generate(request, CancellationToken.None));
    }
    [Fact]
    public void ProductionManifestRoundtripsCanonicalBytesWithoutTelemetry()
    {
        var manifest = Fixture(); byte[] bytes = manifest.Encode();
        Assert.Equal(bytes, ContentManifest.Decode(bytes).Encode());
        Assert.Equal(bytes, CanonicalJson.Encode(bytes));
        Assert.DoesNotContain("timestamp", System.Text.Encoding.UTF8.GetString(bytes));
    }
    [Fact]
    public void DecoderRejectsAllocationCountsAndIntegerBoundsBeforeFloatRounding()
    {
        var manifest = Fixture(); JsonObject root = JsonNode.Parse(manifest.Encode())!.AsObject();
        root["dependencies"] = new JsonArray(Enumerable.Repeat(new string('0', 64), 257).Select(s => (JsonNode)System.Text.Json.Nodes.JsonValue.Create(s)!).ToArray());
        Assert.Throws<FormatException>(() => ContentManifest.Decode(System.Text.Encoding.UTF8.GetBytes(root.ToJsonString())));
        root = JsonNode.Parse(manifest.Encode())!.AsObject();
        root["artifacts"] = new JsonArray(Enumerable.Range(0, 257).Select(_ => root["artifacts"]![0]!.DeepClone()).ToArray());
        Assert.Throws<FormatException>(() => ContentManifest.Decode(System.Text.Encoding.UTF8.GetBytes(root.ToJsonString())));
        root = JsonNode.Parse(manifest.Encode())!.AsObject();
        root["bounds"]!["minMm"] = new JsonArray(int.MaxValue, 0, 0); root["bounds"]!["maxMm"] = new JsonArray(int.MaxValue - 1, 1, 1);
        Assert.Throws<FormatException>(() => ContentManifest.Decode(System.Text.Encoding.UTF8.GetBytes(root.ToJsonString())));
    }
    [Fact]
    public void DecoderRejectsUnknownFieldsAndGameplayProfileDrift()
    {
        var root = JsonNode.Parse(Fixture().Encode())!.AsObject(); root["unexpected"] = 1;
        Assert.Throws<FormatException>(() => ContentManifest.Decode(System.Text.Encoding.UTF8.GetBytes(root.ToJsonString())));
        root.Remove("unexpected"); root["visualProfile"] = "Low";
        Assert.Throws<FormatException>(() => ContentManifest.Decode(System.Text.Encoding.UTF8.GetBytes(root.ToJsonString())));
    }
}
