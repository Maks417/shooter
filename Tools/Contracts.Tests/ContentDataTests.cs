using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shooter.Contracts;
using Xunit;

namespace Shooter.Contracts.Tests
{
    public sealed class ContentDataTests
    {
        private static MeshData Triangle(uint[]? indices = null, Float4[]? weights = null, UInt16x4[]? joints = null) => new MeshData(
            new[] { default(Float3), new Float3(1, 0, 0), new Float3(0, 0, 1) }, Enumerable.Repeat(new Float3(0, 1, 0), 3).ToArray(), Enumerable.Repeat(new Float4(1, 0, 0, 1), 3).ToArray(),
            new[] { new Float2(0, 0), new Float2(1, 0), new Float2(0, 1) }, indices ?? new uint[] { 0, 2, 1 }, new[] { new Submesh(0, 3, 0) }, joints, weights);
        [Fact]
        public void MeshOwnsIndicesAndRejectsInvalidTopologyOrSkin()
        {
            uint[] indices = { 0, 2, 1 }; var mesh = Triangle(indices); indices[0] = uint.MaxValue; Assert.Equal(0u, mesh.Indices[0]);
            Assert.Throws<ArgumentException>(() => Triangle(new uint[] { 0, 1, 3 }));
            Assert.Throws<ArgumentException>(() => Triangle(new uint[] { 0, 1 }));
            Assert.Throws<ArgumentException>(() => Triangle(weights: new Float4[3]));
            Assert.Throws<ArgumentException>(() => Triangle(weights: new Float4[3], joints: new UInt16x4[3]));
            var weights = Enumerable.Repeat(new Float4(1, 0, 0, 0), 3).ToArray(); var joints = new UInt16x4[3];
            Assert.NotNull(Triangle(weights: weights, joints: joints).Weights);
            joints[0] = new UInt16x4(256, 0, 0, 0); Assert.Throws<ArgumentException>(() => Triangle(weights: weights, joints: joints));
        }
        [Fact]
        public void SizeGuardsRejectHugeDeclaredInputsBeforeAllocation()
        {
            Assert.Throws<ArgumentException>(() => new MeshData(new HugeList<Float3>(2000001), Array.Empty<Float3>(), Array.Empty<Float4>(), Array.Empty<Float2>(), Array.Empty<uint>(), Array.Empty<Submesh>()));
            Assert.Throws<ArgumentException>(() => new HeightGrid(default, ushort.MaxValue, ushort.MaxValue, 1, new HugeList<float>(int.MaxValue)));
            Assert.Throws<ArgumentException>(() => new FrozenList<byte>(new HugeList<byte>(ContractVersion.MaxArtifactBytes + 1), ContractVersion.MaxArtifactBytes));
            Assert.Throws<ArgumentException>(() => new CollisionData(new HugeList<TaggedCapsule>(int.MaxValue), Array.Empty<TaggedBox>(), Array.Empty<TaggedHeightGrid>()));
        }
        private sealed class HugeList<T> : IReadOnlyList<T>
        {
            public int Count { get; }
            public HugeList(int count) { Count = count; }
            public T this[int index] => throw new InvalidOperationException("Must reject before reading");
            public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("Must reject before reading");
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
        private static JointDefinition Joint(ushort id, ushort parent) => new JointDefinition(id, parent, default, Quat.Identity, default, default);
        [Fact]
        public void SkeletonAllowsUnorderedTreeButRejectsCyclesMissingParentsAndDuplicateIds()
        {
            Assert.Equal(2, new SkeletonData(new[] { Joint(1, 0), Joint(0, ushort.MaxValue) }).Joints.Count);
            Assert.Throws<ArgumentException>(() => new SkeletonData(new[] { Joint(0, ushort.MaxValue), Joint(1, 2), Joint(2, 1) }));
            Assert.Throws<ArgumentException>(() => new SkeletonData(new[] { Joint(0, ushort.MaxValue), Joint(1, 2) }));
            Assert.Throws<ArgumentException>(() => new SkeletonData(new[] { Joint(0, ushort.MaxValue), Joint(0, ushort.MaxValue) }));
            Assert.Throws<ArgumentException>(() => new SkeletonData(new[] { default(JointDefinition) }));
            Assert.Throws<ArgumentException>(() => new JointDefinition(1, 0, default, default, default, default));
        }
        [Fact]
        public void MaterialAndNavOwnBytesWithoutExposingWritableArrays()
        {
            byte[] source = { 1, 2, 3, 4 }; var material = new MaterialData(1, 1, "rgba8", new[] { source }); var nav = new NavigationData("fixture-nav", 1, default, source);
            source[0] = 99; Assert.Equal(1, material.MipBytes[0][0]); Assert.Equal(1, nav.Blob[0]);
            Assert.Throws<ArgumentException>(() => new MaterialData(0, 1, "rgba8", new[] { source }));
            Assert.Throws<ArgumentException>(() => new NavigationData("fixture-nav", 0, default, source));
            Assert.Throws<ArgumentException>(() => new HeightGrid(default, 2, 2, 1, new[] { 0f, 0f, float.NaN, 0f }));
            Assert.Throws<ArgumentException>(() => new Box(default, new Float3(1, 0, 1), Quat.Identity));
        }
        [Fact]
        public void ArtifactsVerifyHashesAndFailureResultsCannotPublishPartialContent()
        {
            byte[] payload = Encoding.ASCII.GetBytes("abc"); var hash = new ContentId("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
            var artifact = new GeneratedArtifact(ContentKind.Mesh, "fixture-only", 1, payload, hash); payload[0] = 0;
            Assert.Equal((byte)'a', artifact.Payload[0]);
            Assert.Throws<ArgumentException>(() => new GeneratedArtifact(ContentKind.Mesh, "fixture", 1, payload, hash));
            Assert.Throws<ArgumentException>(() => new GenerationResult(ErrorCode.Cancelled, new[] { artifact }, default, Array.Empty<Diagnostic>()));
            Assert.Throws<ArgumentException>(() => new GenerationResult(ErrorCode.None, Array.Empty<GeneratedArtifact>(), default, Array.Empty<Diagnostic>()));
            Assert.Throws<ArgumentException>(() => new ValidationReport(false, Array.Empty<Diagnostic>()));
            Assert.Equal(ErrorCode.Cancelled, new GenerationResult(ErrorCode.Cancelled, Array.Empty<GeneratedArtifact>(), default, new[] { new Diagnostic(ErrorCode.Cancelled, "$", "Cancelled") }).Error);
        }
        [Fact]
        public void RecipeSeedAndDependenciesMustBeCanonicalAndOwned()
        {
            byte[] parameters = Encoding.UTF8.GetBytes("{\"widthMm\":1000}"); var id = new DefinitionId("fixtures/box");
            var recipe = new RecipeDocument(id, "shooter.recipe.box.v1", "geometry.box", "1.0.0", "18446744073709551615", parameters, Array.Empty<ContentId>());
            parameters[0] = 0; Assert.Equal((byte)'{', recipe.Parameters[0]); Assert.Equal(ulong.MaxValue, recipe.SeedValue);
            foreach (string seed in new[] { "01", "-1", "18446744073709551616", " 1", "+1" })
                Assert.Throws<ArgumentException>(() => new RecipeDocument(id, "schema", "generator", "1", seed, new byte[] { 1 }, Array.Empty<ContentId>()));
            var deps = new[] { default(ContentId), default(ContentId) };
            Assert.Throws<ArgumentException>(() => new RecipeDocument(id, "schema", "generator", "1", "0", new byte[] { 1 }, deps));
        }
    }
}
