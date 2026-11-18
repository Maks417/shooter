using System;
using System.IO;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Xunit;

[Collection("NativeNavProvider")]
public sealed class NavBakeProvenanceDocumentTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "shooter-nav-prov-" + Guid.NewGuid().ToString("N"));
    private readonly ContentId toolchain = ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("nav-provenance-document-test"));

    public void Dispose()
    {
        NavBakePublication.NativeProvider = null;
        NavBakePublication.NativeToolIdentity = null;
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [Fact]
    public void SoftwarePublicationIsLabelledSoftwareAndRejectedWhenNativeRequired()
    {
        var result = MapPackagePublisher.Publish(root, toolchain, new[] { "depot-yard" }, new[] { "gameplay" }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.None, result.Error);
        Assert.False(NavBakePublication.IsNativeProvenance(result.Packages[0].NavBakeProvenance));

        Assert.True(NavBakeProvenanceDocument.TryValidate(root, false, out int count, out bool allNative, out string diagnostic), diagnostic);
        Assert.Equal(1, count);
        Assert.False(allNative);
        Assert.False(NavBakeProvenanceDocument.TryValidate(root, true, out _, out _, out string rejected));
        Assert.Equal("software-navigation-not-release-native", rejected);
        string text = File.ReadAllText(NavBakeProvenanceDocument.SidecarPath(root));
        Assert.Contains(NavBakePublication.SoftwarePendingProvenance, text, StringComparison.Ordinal);
        Assert.Contains("\"allNative\":false", text, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeProviderPublicationRecordsNativeToolIdentityAndValidates()
    {
        NavBakePublication.NativeProvider = (boxes, half, radius, height, step, slope, margin) =>
            new NavBakePublication.NativeBake(CleanNavBake.Build(boxes, half, 2f), NavBakePublication.NativeProvenance);
        NavBakePublication.NativeToolIdentity = "unity=test;revision=abc";
        var result = MapPackagePublisher.Publish(root, toolchain, new[] { "depot-yard" }, new[] { "gameplay" }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.None, result.Error);
        Assert.True(NavBakeProvenanceDocument.TryValidate(root, true, out int count, out bool allNative, out string diagnostic), diagnostic);
        Assert.Equal(1, count);
        Assert.True(allNative);
        Assert.Contains("unity=test;revision=abc", File.ReadAllText(NavBakeProvenanceDocument.SidecarPath(root)), StringComparison.Ordinal);
    }

    [Fact]
    public void NativeClaimWithoutToolIdentityOrWithTamperedNavigationHashIsRejected()
    {
        NavBakePublication.NativeProvider = (boxes, half, radius, height, step, slope, margin) =>
            new NavBakePublication.NativeBake(CleanNavBake.Build(boxes, half, 2f), NavBakePublication.NativeProvenance);
        NavBakePublication.NativeToolIdentity = null;
        var result = MapPackagePublisher.Publish(root, toolchain, new[] { "depot-yard" }, new[] { "gameplay" }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.None, result.Error);
        Assert.False(NavBakeProvenanceDocument.TryValidate(root, true, out _, out _, out string missing));
        Assert.Equal("native-tool-identity-missing", missing);

        string path = NavBakeProvenanceDocument.SidecarPath(root);
        string text = File.ReadAllText(path);
        string hash = result.Packages[0].NavigationHash.ToString();
        File.WriteAllText(path, text.Replace(hash, new string('0', 64)));
        Assert.False(NavBakeProvenanceDocument.TryValidate(root, false, out _, out _, out string tampered));
        Assert.StartsWith("navigation-hash-mismatch", tampered, StringComparison.Ordinal);
    }

    [Fact]
    public void SoftwareGridNeverBecomesNativeProvenanceByLabelOnly()
    {
        Assert.False(NavBakePublication.IsNativeProvenance(NavBakePublication.SoftwarePendingProvenance));
        Assert.False(NavBakePublication.IsNativeProvenance("software-walkable-grid;" + NavBakePublication.NativeProvenance));
        Assert.False(NavBakePublication.IsNativeProvenance(null));
        Assert.True(NavBakePublication.IsNativeProvenance(NavBakePublication.NativeProvenance));
        Assert.False(NavBakeProvenanceDocument.TryValidate(Path.Combine(root, "missing"), false, out _, out _, out string missing));
        Assert.Equal("missing", missing);
    }
}
