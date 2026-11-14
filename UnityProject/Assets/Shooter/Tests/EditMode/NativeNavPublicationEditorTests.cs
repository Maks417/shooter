using System.IO;
using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Generation.Environment;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    /// <summary>T067/C01: the editor registers a real Unity NavMesh provider; software grids are never labelled native.</summary>
    public sealed class NativeNavPublicationEditorTests
    {
        private static readonly ContentId Toolchain = new ContentId(new string('2', 64));

        [Test]
        public void EditorBridgeRegistersProviderThatBakesNativeTriangulation()
        {
            Assert.That(NavBakePublication.NativeProvider, Is.Not.Null, "UnityNavMeshBakeEditorBridge must register in the editor.");
            var boxes = new[] { new CollisionBox(1, 1, 1, new Float3(4f, 1f, 0f), new Float3(2f, 2f, 6f), true) };
            var native = NavBakePublication.NativeProvider(boxes, 16f, 0.32f, 1.8f, 0.25f, 45f, 0.05f);
            Assert.That(native, Is.Not.Null);
            Assert.That(native.Bake.Polygons.Count, Is.GreaterThan(0));
            Assert.That(native.Provenance, Is.EqualTo(NavBakePublication.NativeProvenance));
            Assert.That(NavBakePublication.IsNativeProvenance(native.Provenance), Is.True);
            Assert.That(native.Provenance, Does.Not.Contain("software-walkable-grid"));
        }

        [Test]
        public void PackageBuildWithEditorProviderRecordsNativeProvenanceAndSidecarValidates()
        {
            string root = Path.Combine(Path.GetTempPath(), "shooter-native-nav-" + System.Guid.NewGuid().ToString("N"));
            var previousIdentity = NavBakePublication.NativeToolIdentity;
            try
            {
                NavBakePublication.NativeToolIdentity = "unity=editmode-test";
                var result = MapPackagePublisher.Publish(root, Toolchain, new[] { "depot-yard" }, new[] { "gameplay" }, false, System.Threading.CancellationToken.None);
                Assert.That(result.Error, Is.EqualTo(ErrorCode.None), result.Diagnostic);
                Assert.That(result.Packages[0].NavBakeProvenance, Is.EqualTo(NavBakePublication.NativeProvenance));
                Assert.That(NavBakeProvenanceDocument.TryValidate(root, true, out int count, out bool allNative, out string diagnostic), Is.True, diagnostic);
                Assert.That(count, Is.EqualTo(1));
                Assert.That(allNative, Is.True);
            }
            finally
            {
                NavBakePublication.NativeToolIdentity = previousIdentity;
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { /* temp */ }
            }
        }
    }
}
