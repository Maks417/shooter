#nullable enable
using System;
using System.Security.Cryptography;
using NUnit.Framework;
using Shooter.Generation.Icons;
using Shooter.Presentation.Hud;
using Shooter.Presentation.Session;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    /// <summary>ADR-085: the Mono lane builds the same icon atlas bytes as .NET, and the shader draws glyphs.</summary>
    public sealed class IconAtlasEditModeTests
    {
        // Must equal IconAtlasGoldens.Library in Tools/Generation.Tests/IconAtlasTests.cs.
        private const string LibraryGolden = "983eafa393ad9569e705e712c2425b8681a80bddb605065154b16005869af024";

        [Test]
        public void Library_atlas_matches_the_dotnet_golden()
        {
            var atlas = IconAtlas.Build(IconLibrary.All);
            using var sha = SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(atlas.Pixels)).Replace("-", "").ToLowerInvariant();
            Assert.That(hash, Is.EqualTo(LibraryGolden));
        }

        [Test]
        public void Runtime_atlas_exposes_library_icons()
        {
            Assert.That(UiIconAtlas.Texture, Is.Not.Null);
            Assert.That(UiIconAtlas.TryGet(IconLibrary.Health, out var e), Is.True);
            Assert.That(e.Width, Is.EqualTo(IconAtlas.CellPixels));
        }

        [Test]
        public void Glyph_mode_draws_the_health_cross()
        {
            const int size = 64;
            var tex = UiIconAtlas.Texture!;
            Assert.That(UiIconAtlas.TryGet(IconLibrary.Health, out var e), Is.True);
            var material = new Material(Shader.Find("Shooter/UiSdf")) { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var read = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            try
            {
                Shader.SetGlobalTexture("_GUIClipTexture", Texture2D.whiteTexture);
                Shader.SetGlobalMatrix("unity_GUIClipTextureMatrix", Matrix4x4.zero);
                material.SetVector("_SizePad", new Vector4(size, size, 0f, 1f));
                material.SetVector("_ShapeA", new Vector4((float)UiShapeKind.Glyph, 0f, 0f, 0f));
                material.SetVector("_Color", Vector4.one);
                material.SetVector("_Color2", Vector4.one);
                material.SetVector("_Shadow", Vector4.zero);
                material.SetVector("_Glow", new Vector4(0f, 0f, -1f, 0f));
                float aw = tex.width, ah = tex.height;
                material.SetVector("_GlyphRect", new Vector4(e.X / aw, (ah - e.Y - e.Height) / ah, e.Width / aw, e.Height / ah));
                material.SetVector("_GlyphMap", new Vector4(IconAtlas.SpreadPixels, size / (float)e.Height, 0f, 0f));
                RenderTexture.active = rt;
                GL.Clear(true, true, new Color(0, 0, 0, 0));
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, size, size, 0);
                Graphics.DrawTexture(new Rect(0, 0, size, size), tex, new Rect(0, 0, 1, 1), 0, 0, 0, 0, Color.white, material);
                GL.PopMatrix();
                read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                read.Apply();
                var px = read.GetPixels32();
                Assert.That(px[(size / 2) * size + size / 2].a, Is.GreaterThan(240), "cross centre is inked");
                Assert.That(px[(size / 2) * size + size / 2 + 22].a, Is.GreaterThan(240), "horizontal bar reaches out");
                Assert.That(px[8 * size + 8].a, Is.LessThan(5), "corner between the bars is clear");
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(read);
            }
        }
    }
}
