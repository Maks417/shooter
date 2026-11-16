#nullable enable
using System.IO;
using Shooter.Generation;
using NUnit.Framework;
namespace Shooter.Tests.EditMode { public sealed class NoiseMaterialEditModeTests {
        [Test]
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
            Assert.That(ContentIdentity.Hash(bytes.ToArray()).ToString(), Is.EqualTo("8fd47c9600989aea58861356b319ef56cdeaebb2d676655e3d728ed49bd058c8"));
        }
    } }
