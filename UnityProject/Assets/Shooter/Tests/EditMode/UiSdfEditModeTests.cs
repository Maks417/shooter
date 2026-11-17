#nullable enable
using NUnit.Framework;
using Shooter.Presentation.Hud;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    /// <summary>ADR-084: the code-only UI SDF shader compiles and draws anti-aliased rounded shapes.</summary>
    public sealed class UiSdfEditModeTests
    {
        private static readonly int GuiClipTexture = Shader.PropertyToID("_GUIClipTexture");
        private static readonly int GuiClipMatrix = Shader.PropertyToID("unity_GUIClipTextureMatrix");

        [Test]
        public void Shader_is_present_and_supported()
        {
            var shader = Shader.Find("Shooter/UiSdf");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
        }

        private static Color32[] Render(int kind, float radius, float stroke, Color color, int size = 64, Vector4 clipCircle = default)
        {
            var shader = Shader.Find("Shooter/UiSdf");
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var read = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            try
            {
                Shader.SetGlobalTexture(GuiClipTexture, Texture2D.whiteTexture);
                Shader.SetGlobalMatrix(GuiClipMatrix, Matrix4x4.zero);
                material.SetVector("_SizePad", new Vector4(size, size, 0f, 1f));
                material.SetVector("_ShapeA", new Vector4(kind, radius, stroke, 0f));
                material.SetVector("_Color", new Vector4(color.r, color.g, color.b, color.a));
                material.SetVector("_Color2", new Vector4(color.r, color.g, color.b, color.a));
                material.SetVector("_Shadow", Vector4.zero);
                material.SetVector("_Glow", new Vector4(0f, 0f, -1f, 0f));
                material.SetVector("_Arc", Vector4.zero);
                material.SetVector("_ClipCircle", clipCircle);
                RenderTexture.active = rt;
                GL.Clear(true, true, new Color(0, 0, 0, 0));
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, size, size, 0);
                Graphics.DrawTexture(new Rect(0, 0, size, size), Texture2D.whiteTexture, new Rect(0, 0, 1, 1), 0, 0, 0, 0, Color.white, material);
                GL.PopMatrix();
                read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                read.Apply();
                return read.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(read);
            }
        }

        [Test]
        public void Rounded_rect_fills_the_centre_and_cuts_the_corners()
        {
            const int size = 64;
            var px = Render((int)UiShapeKind.RoundedRect, 16f, 0f, Color.white, size);
            Assert.That(px[(size / 2) * size + size / 2].a, Is.GreaterThan(250), "centre is opaque");
            Assert.That(px[0].a, Is.LessThan(5), "rounded corner is cut");
            Assert.That(px[(size / 2) * size + 1].a, Is.GreaterThan(200), "flat edge reaches the rect border");
        }

        [Test]
        public void Stroked_circle_is_hollow()
        {
            const int size = 64;
            var px = Render((int)UiShapeKind.Circle, 0f, 4f, Color.white, size);
            Assert.That(px[(size / 2) * size + size / 2].a, Is.LessThan(5), "ring centre is empty");
            Assert.That(px[(size / 2) * size + 2].a, Is.GreaterThan(150), "ring band is drawn");
        }

        [Test]
        public void Circle_clip_masks_outside_the_disc()
        {
            const int size = 64;
            var clipped = Render((int)UiShapeKind.RoundedRect, 0f, 0f, Color.white, size, new Vector4(32f, 32f, 16f, 1f));
            Assert.That(clipped[(size / 2) * size + size / 2].a, Is.GreaterThan(250), "centre stays inside the mask");
            Assert.That(clipped[0].a, Is.LessThan(5), "corner is masked");
            Assert.That(clipped[(size / 2) * size + 4].a, Is.LessThan(5), "edge beyond the radius is masked");
            var open = Render((int)UiShapeKind.RoundedRect, 0f, 0f, Color.white, size);
            Assert.That(open[0].a, Is.GreaterThan(250), "radius 0 leaves the mask off");
        }

        [Test]
        public void Output_is_premultiplied()
        {
            const int size = 32;
            var px = Render((int)UiShapeKind.RoundedRect, 0f, 0f, new Color(1f, 0f, 0f, 0.5f), size);
            var c = px[(size / 2) * size + size / 2];
            Assert.That(c.a, Is.InRange(120, 136));
            Assert.That(c.r, Is.InRange(120, 136), "colour channels carry alpha");
            Assert.That(c.g, Is.LessThan(5));
        }
    }
}
