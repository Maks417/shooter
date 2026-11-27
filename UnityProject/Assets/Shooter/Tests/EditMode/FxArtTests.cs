using System;
using System.Linq;
using NUnit.Framework;
using Shooter.Presentation.Fx;
using UnityEditor;
using UnityEngine;

namespace Shooter.Tests
{
    /// <summary>
    /// ADR-126 licensed effect flipbooks: the catalog loads a sheet for every role, the sheets import through the
    /// code-owned settings (sRGB, alpha, mips, clamp, 1024 cap), and a broken catalog throws instead of falling back.
    /// </summary>
    public sealed class FxArtTests
    {
        [Test]
        public void CatalogLoadsEverySheetWithCodeOwnedImportSettings()
        {
            var catalog = FxArtCatalog.Load();
            Assert.That(catalog.Sheets.Length, Is.EqualTo((int)FxArtRole.Count));
            for (int r = 0; r < catalog.Sheets.Length; r++)
            {
                var texture = catalog.Textures[r];
                Assert.That(texture, Is.Not.Null, catalog.Sheets[r].Resource);
                Assert.That(texture.width, Is.LessThanOrEqualTo(1024), texture.name);
                Assert.That(texture.mipmapCount, Is.GreaterThan(1), texture.name);
                Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), texture.name);
                string path = AssetDatabase.GetAssetPath(texture);
                StringAssert.StartsWith("Assets/Shooter/Art/Fx/", path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.sRGBTexture, Is.True, path);
                Assert.That(importer.alphaIsTransparency, Is.True, path);
                Assert.That(importer.maxTextureSize, Is.EqualTo(1024), path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ), path);
            }
        }

        [Test]
        public void BrokenCatalogThrowsInsteadOfFallingBack()
        {
            Assert.Throws<InvalidOperationException>(() => FxArtCatalog.Parse("{\"schemaVersion\":2}"));
            var text = Resources.Load<TextAsset>(FxArtCatalog.ResourcePath).text;
            Assert.Throws<InvalidOperationException>(() => FxArtCatalog.Parse(text.Replace("\"FxArt/Flame02_16x4\"", "\"FxArt/missing\"")));
            var withoutFlame = string.Join("\n", text.Split('\n').Where(line => !line.Contains("\"Flame\"")));
            Assert.Throws<InvalidOperationException>(() => FxArtCatalog.Parse(withoutFlame));
        }

        [Test]
        public void CombatParticleShaderCompilesWithTheNewProperties()
        {
            var shader = Shader.Find("Shooter/CombatParticle");
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var material = new Material(shader);
            try
            {
                foreach (var property in new[] { "_FrameBlend", "_Lit", "_Emission", "_Brightness", "_SoftParticles" })
                    Assert.That(material.HasProperty(property), Is.True, property);
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }
    }
}
