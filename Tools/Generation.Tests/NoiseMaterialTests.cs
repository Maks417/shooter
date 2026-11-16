#nullable enable
using System.IO;
using Shooter.Generation;
using Xunit;
namespace Shooter.Generation.Tests { public sealed class NoiseMaterialTests {
        [Fact]
        public void All_noise_roles_match_the_reviewed_byte_identity()
        {
            using var bytes = new MemoryStream();
            foreach (int resolution in new[] { 128, 256 })
            foreach (string role in NoiseMaterialRecipe.Roles)
            {
                var bake = NoiseMaterialBaker.Bake(new NoiseMaterialRecipe(role, resolution, 460072030));
                foreach (var plane in new[] { bake.Albedo, bake.Normal, bake.Mask })
                {
                    var encoded = GeometryCodec.EncodeMaterial(plane); bytes.Write(encoded, 0, encoded.Length);
                }
            }
            Assert.Equal("8fd47c9600989aea58861356b319ef56cdeaebb2d676655e3d728ed49bd058c8", ContentIdentity.Hash(bytes.ToArray()).ToString());
        }

        [Fact] public void Resolution_roles_cancellation_and_normal_planes_are_bounded()
        {
            Assert.Throws<System.ArgumentException>(() => new NoiseMaterialRecipe("unknown",128,1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new NoiseMaterialRecipe("concrete",512,1));
            Assert.Throws<System.OperationCanceledException>(() => NoiseMaterialBaker.Bake(new NoiseMaterialRecipe("concrete",128,1), new System.Threading.CancellationToken(true)));
            var bake=NoiseMaterialBaker.Bake(new NoiseMaterialRecipe("rust",256,1));
            Assert.Equal(256,bake.Albedo.Width); Assert.Equal(9,bake.Albedo.MipBytes.Count);
            foreach(var mip in bake.Normal.MipBytes) for(int i=0;i<mip.Count;i+=4) Assert.True(mip[i+2] >= 128);
            Assert.NotEqual(ContentIdentity.Hash(GeometryCodec.EncodeMaterial(bake.Albedo)),ContentIdentity.Hash(GeometryCodec.EncodeMaterial(NoiseMaterialBaker.Bake(new NoiseMaterialRecipe("rust",256,2)).Albedo)));
        }
    } }
