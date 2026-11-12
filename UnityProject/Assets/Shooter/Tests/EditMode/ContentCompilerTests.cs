using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Shooter.Content;
using Shooter.Contracts;
using Shooter.Generation;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class ContentCompilerTests
    {
        [Test]
        public void NumericSourceCompilesReproducesAndLoadsVerifiedLeaseInUnity()
        {
            string root = Path.Combine(Path.GetTempPath(), "shooter-unity-content-" + Guid.NewGuid().ToString("N"));
            try
            {
                var toolchain = new ContentId(new string('1', 64));
                byte[] source = File.ReadAllBytes(Path.Combine(Application.dataPath, "Shooter/Generation/Recipes/box-collision.json"));
                var node = new CompilationNode(source, "windows64", "gameplay", toolchain, new CpuBoxCollisionGenerator());
                var files = new CacheFiles(root, toolchain); var compiler = new ContentCompiler(files);
                var cold = compiler.CompileAsync(new[] { node }, false, CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(cold.Error, Is.EqualTo(ErrorCode.None), cold.Diagnostic);
                Assert.That(cold.Entries[0].Reused, Is.False);
                var warm = compiler.CompileAsync(new[] { node }, true, CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(warm.Error, Is.EqualTo(ErrorCode.None), warm.Diagnostic);
                Assert.That(warm.Entries[0].Reused, Is.True);
                using var store = new FileContentStore(files);
                var loaded = store.AcquireAsync(node.Id, ContentKind.Collision, CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(loaded.Error, Is.EqualTo(ErrorCode.None), loaded.Diagnostic);
                using (loaded.Lease)
                {
                    byte[] payload = new byte[loaded.Lease.Length]; loaded.Lease.CopyTo(payload);
                    var shape = GeometryCodec.DecodeCollision(ArtifactCodec.Decode(payload, ContentKind.Collision, warm.Entries[0].Manifest.GeneratorHash, toolchain));
                    Assert.That(shape.Boxes.Count, Is.EqualTo(1)); Assert.That(shape.Boxes[0].Shape.Size.X, Is.EqualTo(1));
                }
                Assert.That(store.ActiveLeases, Is.Zero); Assert.That(store.ResidentBytes, Is.Zero);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
