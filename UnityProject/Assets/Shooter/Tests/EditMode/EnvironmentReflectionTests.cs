#nullable enable
using NUnit.Framework;
using Shooter.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Tests
{
    public sealed class EnvironmentReflectionTests
    {
        [Test]
        public void GlobalNumericReflectionHasSkyGroundAndRestoresPreviousOwner()
        {
            var root=new GameObject("Reflection test");
            var prior=RenderSettings.customReflectionTexture;
            var mode=RenderSettings.defaultReflectionMode;
            GeneratedSky? sky=null;
            try
            {
                sky=GeneratedSky.Apply(root.transform,false);
                var cube=sky.EnvironmentReflection!;
                Assert.That(cube.width,Is.EqualTo(64));
                Assert.That(cube.mipmapCount,Is.EqualTo(7));
                Assert.That(RenderSettings.defaultReflectionMode,Is.EqualTo(DefaultReflectionMode.Custom));
                Assert.That(RenderSettings.customReflectionTexture,Is.SameAs(cube));
                var top=cube.GetPixel(CubemapFace.PositiveY,32,32);
                var bottom=cube.GetPixel(CubemapFace.NegativeY,32,32);
                Assert.That(top.b,Is.GreaterThan(bottom.b+.1f));
                using(var nested=GeneratedSky.Apply(root.transform,false))
                    Assert.That(RenderSettings.customReflectionTexture,Is.SameAs(nested.EnvironmentReflection));
                Assert.That(RenderSettings.customReflectionTexture,Is.SameAs(cube));
                sky.Dispose(); sky=null;
                Assert.That(cube==null,Is.True,"Owned cubemap must be released.");
                Assert.That(RenderSettings.customReflectionTexture,Is.SameAs(prior));
                Assert.That(RenderSettings.defaultReflectionMode,Is.EqualTo(mode));
            }
            finally { sky?.Dispose(); Object.DestroyImmediate(root); RenderSettings.customReflectionTexture=prior; RenderSettings.defaultReflectionMode=mode; }
        }
    }
}
