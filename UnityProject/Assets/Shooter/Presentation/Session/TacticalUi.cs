#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Presentation.Menus;
using UnityEngine;

namespace Shooter.Presentation.Session
{
    /// <summary>Code-only tactical shell. Coordinates are logical pixels after the applied UI scale.</summary>
    public sealed class TacticalUi
    {
        // Brandbook tokens: charcoal surface, ivory text, sand-amber focus, ally cyan, danger coral.
        public static readonly Color Background = new Color32(22, 28, 32, 255);
        public static readonly Color Surface = new Color32(28, 36, 41, 255);
        public static readonly Color Raised = new Color32(42, 52, 57, 255);
        public static readonly Color PanelEdge = new Color32(54, 66, 72, 255);
        public static readonly Color Text = new Color32(241, 243, 239, 255);
        public static readonly Color Muted = new Color32(173, 183, 183, 255);
        public static readonly Color Accent = new Color32(230, 197, 106, 255);
        public static readonly Color Ally = new Color32(123, 197, 209, 255);
        public static readonly Color Enemy = new Color32(228, 123, 112, 255);
        // Combat HUD plates: thinner charcoal with restrained edge contrast (world-first readability).
        public static readonly Color HudPlate = new Color(.035f, .048f, .055f, .48f);
        public static readonly Color HudPlateStrong = new Color(.03f, .042f, .05f, .62f);
        public static readonly Color HudEdge = new Color(1f, 1f, 1f, .09f);
        public static readonly Color HudEdgeSoft = new Color(1f, 1f, 1f, .05f);

        private readonly List<string> controls = new List<string>(48);
        private readonly Dictionary<int, GUIStyle> labels = new Dictionary<int, GUIStyle>();
        private GUIStyle? buttonStyle;
        private GUIStyle? sliderThumb;
        private Matrix4x4 savedMatrix;
        private Color savedColor;
        private string screen = "";
        private string activate = "";
        private string focused = "";
        private bool focusFirst;
        private bool focusMoved;
        public float Width { get; private set; }
        public float Height { get; private set; }
        public Rect Safe { get; private set; }
        public Texture? SceneTexture { get; set; }

        public static Rect SafeArea(float width, float height) => new Rect(width * .05f, height * .05f, width * .9f, height * .9f);

        public void Begin(float scale, string screenId, bool keyboardNavigation = true)
        {
            savedMatrix = GUI.matrix;
            savedColor = GUI.color;
            GUI.color = Color.white;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            Width = Screen.width / scale;
            Height = Screen.height / scale;
            Safe = SafeArea(Width, Height);
            activate = "";
            focusMoved = false;
            if (screen != screenId)
            {
                screen = screenId;
                controls.Clear();
                focused = "";
                focusFirst = keyboardNavigation;
            }
            var e = Event.current;
            if (keyboardNavigation && e.type == EventType.KeyDown && controls.Count > 0)
            {
                bool previous = e.keyCode == KeyCode.UpArrow || (e.keyCode == KeyCode.Tab && e.shift);
                bool next = e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.Tab;
                if (previous || next)
                {
                    int current = controls.IndexOf(focused);
                    int delta = previous ? -1 : 1;
                    int index = current < 0 ? (previous ? controls.Count - 1 : 0) : (current + delta + controls.Count) % controls.Count;
                    focused = controls[index];
                    focusMoved = true;
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space)
                {
                    activate = focused;
                    e.Use();
                }
            }
            controls.Clear();
        }

        public void End()
        {
            if (focusFirst && controls.Count > 0 && Event.current.type == EventType.Repaint)
            {
                focused = controls[0];
                focusFirst = false;
            }
            GUI.matrix = savedMatrix;
            GUI.color = savedColor;
        }

        public static void Fill(Rect r, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color prior = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prior;
        }

        public static void Line(Vector2 a, Vector2 b, Color color, float thickness = 1)
        {
            if (Event.current.type != EventType.Repaint) return;
            Matrix4x4 prior = GUI.matrix;
            GUI.matrix = prior * Matrix4x4.TRS(new Vector3(a.x, a.y, 0),
                Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg), Vector3.one);
            Fill(new Rect(0, 0, Vector2.Distance(a, b), thickness), color);
            GUI.matrix = prior;
        }

        public void Label(Rect r, string text, int size = 18, Color? color = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            if (!labels.TryGetValue(size, out var style))
            {
                // ADR-066: condensed bank supplies weight; avoid double-bold on the engine fallback.
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size,
                    fontStyle = FontStyle.Normal,
                    wordWrap = true,
                    padding = new RectOffset(0, 0, 0, 0),
                    richText = false
                };
                Font? face = UiFonts.ForLabelSize(size);
                if (face != null) style.font = face;
                else if (size >= 28) style.fontStyle = FontStyle.Bold;
                labels.Add(size, style);
            }
            style.normal.textColor = StyleInk(color ?? Text);
            style.alignment = align;
            GUI.Label(r, text, style);
        }

        public void Panel(Rect r, bool accentTop = false, bool rail = false)
        {
            Fill(r, Surface);
            Fill(new Rect(r.x, r.y, r.width, 1), PanelEdge);
            Fill(new Rect(r.x, r.yMax - 1, r.width, 1), new Color(0, 0, 0, .35f));
            Fill(new Rect(r.xMax - 1, r.y, 1, r.height), new Color(0, 0, 0, .22f));
            if (accentTop) Fill(new Rect(r.x, r.y, r.width, 2), Accent);
            if (rail) Fill(new Rect(r.x, r.y, 3, r.height), Accent);
        }

        /// <summary>Compact in-match plate: translucent charcoal + 1px edge, optional accent hairline.</summary>
        public static void CombatPlate(Rect r, bool strong = false, bool accentLeft = false, bool accentBottom = false)
        {
            Fill(r, strong ? HudPlateStrong : HudPlate);
            Edge(r);
            if (accentLeft) AccentBar(new Rect(r.x, r.y + 1, 2, r.height - 2), Accent);
            if (accentBottom) AccentBar(new Rect(r.x + 2, r.yMax - 2, Mathf.Min(22, r.width - 4), 1), Accent);
        }

        /// <summary>1px plate edge: soft top/left highlight, quiet bottom/right shadow.</summary>
        public static void Edge(Rect r)
        {
            Fill(new Rect(r.x, r.y, r.width, 1), HudEdge);
            Fill(new Rect(r.x, r.yMax - 1, r.width, 1), new Color(0, 0, 0, .22f));
            Fill(new Rect(r.x, r.y, 1, r.height), HudEdgeSoft);
            Fill(new Rect(r.xMax - 1, r.y, 1, r.height), new Color(0, 0, 0, .14f));
        }

        /// <summary>Thin accent rail or progress fill (ally/enemy/amber).</summary>
        public static void AccentBar(Rect r, Color color) => Fill(r, color);

        /// <summary>Outlined reticle arm (dark understroke + ivory stroke) for hip-fire readability.</summary>
        public static void ReticleArm(Rect rect)
        {
            Fill(new Rect(rect.x - 0.75f, rect.y - 0.75f, rect.width + 1.5f, rect.height + 1.5f), new Color(0.02f, 0.03f, 0.035f, 0.85f));
            Fill(rect, Text);
        }

        /// <summary>Warface-style X hit marker at screen center. Alpha is presentation-only.</summary>
        public static void HitMarker(float centerX, float centerY, float alpha)
        {
            if (alpha <= 0.02f || Event.current.type != EventType.Repaint) return;
            const float arm = 5.5f, gap = 3.5f, thick = 1.25f;
            Color ink = new Color(Text.r, Text.g, Text.b, alpha);
            Color under = new Color(0.02f, 0.03f, 0.035f, alpha * 0.9f);
            DrawDiagonal(centerX - gap - arm, centerY - gap - arm, centerX - gap, centerY - gap, under, thick + 1.2f);
            DrawDiagonal(centerX + gap, centerY - gap - arm, centerX + gap + arm, centerY - gap, under, thick + 1.2f);
            DrawDiagonal(centerX - gap - arm, centerY + gap + arm, centerX - gap, centerY + gap, under, thick + 1.2f);
            DrawDiagonal(centerX + gap, centerY + gap + arm, centerX + gap + arm, centerY + gap, under, thick + 1.2f);
            DrawDiagonal(centerX - gap - arm, centerY - gap - arm, centerX - gap, centerY - gap, ink, thick);
            DrawDiagonal(centerX + gap, centerY - gap - arm, centerX + gap + arm, centerY - gap, ink, thick);
            DrawDiagonal(centerX - gap - arm, centerY + gap + arm, centerX - gap, centerY + gap, ink, thick);
            DrawDiagonal(centerX + gap, centerY + gap + arm, centerX + gap + arm, centerY + gap, ink, thick);
        }

        /// <summary>
        /// Procedural kill ribbon (ADR-059): striped banner + center disk. No imported sprites.
        /// </summary>
        public static void ScoreRibbon(Rect banner, float alpha)
        {
            if (alpha <= 0.02f || Event.current.type != EventType.Repaint) return;
            Fill(banner, new Color(0.04f, 0.055f, 0.06f, 0.72f * alpha));
            // Alternating amber/green stripes (Battlefield-style composition reference only).
            float stripe = Mathf.Max(6f, banner.width / 18f);
            for (int i = 0; i < 18; i++)
            {
                float x = banner.x + i * stripe;
                if (x >= banner.xMax) break;
                float w = Mathf.Min(stripe * 0.55f, banner.xMax - x);
                Color c = (i % 2 == 0)
                    ? new Color(Accent.r, Accent.g, Accent.b, 0.55f * alpha)
                    : new Color(0.25f, 0.55f, 0.32f, 0.45f * alpha);
                Fill(new Rect(x, banner.y + 4, w, banner.height - 8), c);
            }
            float disk = Mathf.Min(banner.height - 6f, 28f);
            float cx = banner.x + banner.width * 0.5f;
            float cy = banner.y + banner.height * 0.5f;
            Fill(new Rect(cx - disk * 0.5f, cy - disk * 0.5f, disk, disk), new Color(0.92f, 0.9f, 0.82f, 0.95f * alpha));
            Fill(new Rect(cx - disk * 0.28f, cy - disk * 0.28f, disk * 0.56f, disk * 0.56f), new Color(0.55f, 0.42f, 0.18f, 0.9f * alpha));
            AccentBar(new Rect(banner.x, banner.y, banner.width, 2), new Color(Accent.r, Accent.g, Accent.b, alpha));
            AccentBar(new Rect(banner.x, banner.yMax - 2, banner.width, 2), new Color(Accent.r, Accent.g, Accent.b, 0.7f * alpha));
        }

        /// <summary>Brief full-frame coral flash when damage is taken (analytic fill).</summary>
        public static void DamagePlateFlash(float width, float height, float strength)
        {
            if (strength <= 0.01f || Event.current.type != EventType.Repaint) return;
            Fill(new Rect(0, 0, width, height), new Color(Enemy.r, Enemy.g, Enemy.b, strength * 0.55f));
            EdgeVignette(width, height, strength * 0.85f, Enemy);
        }

        private static void DrawDiagonal(float x0, float y0, float x1, float y1, Color color, float thickness) =>
            Line(new Vector2(x0, y0), new Vector2(x1, y1), color, thickness);

        /// <summary>Soft edge vignette from observed health/damage. Analytic fills only.</summary>
        public static void EdgeVignette(float width, float height, float strength, Color tint)
        {
            if (strength <= 0f || Event.current.type != EventType.Repaint) return;
            float band = Mathf.Max(26f, height * .11f);
            int steps = 11;
            for (int i = 0; i < steps; i++)
            {
                float u = i / (float)(steps - 1);
                float a = strength * (1f - u) * (1f - u);
                Color c = new Color(tint.r, tint.g, tint.b, a);
                float t = band * u / steps + 1f;
                Fill(new Rect(0, i * band / steps, width, t), c);
                Fill(new Rect(0, height - (i + 1) * band / steps, width, t), c);
                Fill(new Rect(i * band / steps, 0, t, height), c);
                Fill(new Rect(width - (i + 1) * band / steps, 0, t, height), c);
            }
        }

        private static Texture2D? scopeMaskTexture;

        /// <summary>
        /// Rifle ADS optic overlay: soft housing ring + mil-dot only.
        /// Periphery blur and digital lens zoom are owned by <c>AdsScopePipPresenter</c>
        /// (no opaque full-screen black fill — live blurred world stays visible outside the lens).
        /// </summary>
        public static void AdsScope(float width, float height, float opacity)
        {
            if (opacity <= 0.01f || Event.current.type != EventType.Repaint) return;
            float a = Mathf.Clamp01(opacity);
            float cx = width * 0.5f, cy = height * 0.5f;
            float radius = View.AdsOpticVisual.LensRadiusPixels(width, height);
            Texture2D mask = EnsureScopeMask();
            Color prior = GUI.color;
            // Housing ring only — clear lens (~0.62 of pad) aligned to LensRadiusPixels; no opaque corners.
            float pad = radius / 0.62f;
            GUI.color = new Color(1f, 1f, 1f, a * 0.85f);
            GUI.DrawTexture(new Rect(cx - pad, cy - pad, pad * 2f, pad * 2f), mask, ScaleMode.StretchToFill);
            GUI.color = prior;
            DrawMilDotReticle(cx, cy, radius, a);
        }

        /// <summary>Pistol ADS: soft edge vignette only so iron sights stay readable.</summary>
        public static void AdsIronVignette(float width, float height, float opacity)
        {
            if (opacity <= 0.01f) return;
            EdgeVignette(width, height, opacity, new Color(0.02f, 0.025f, 0.03f, 1f));
        }

        private static void DrawMilDotReticle(float cx, float cy, float radius, float opacity)
        {
            float spacing = View.AdsOpticVisual.MilDotSpacingPixels(radius);
            int dots = View.AdsOpticVisual.MilDotCount(radius);
            float arm = spacing * dots + 2f;
            float gap = 3.5f;
            Color ink = new Color(0.04f, 0.045f, 0.05f, 0.92f * opacity);
            Color soft = new Color(0.02f, 0.025f, 0.03f, 0.55f * opacity);
            // Cross arms with center gap (scope picture stays clear at the aim point).
            Fill(new Rect(cx - arm, cy - 0.6f, arm - gap, 1.2f), ink);
            Fill(new Rect(cx + gap, cy - 0.6f, arm - gap, 1.2f), ink);
            Fill(new Rect(cx - 0.6f, cy - arm, 1.2f, arm - gap), ink);
            Fill(new Rect(cx - 0.6f, cy + gap, 1.2f, arm - gap), ink);
            // Tiny center pip.
            Fill(new Rect(cx - 1.1f, cy - 1.1f, 2.2f, 2.2f), ink);
            for (int i = 1; i <= dots; i++)
            {
                float d = i * spacing;
                float s = i == dots ? 2.4f : 1.7f;
                Fill(new Rect(cx - d - s * 0.5f, cy - s * 0.5f, s, s), soft);
                Fill(new Rect(cx + d - s * 0.5f, cy - s * 0.5f, s, s), soft);
                Fill(new Rect(cx - s * 0.5f, cy - d - s * 0.5f, s, s), soft);
                Fill(new Rect(cx - s * 0.5f, cy + d - s * 0.5f, s, s), soft);
            }
        }

        private static Texture2D EnsureScopeMask()
        {
            if (scopeMaskTexture != null) return scopeMaskTexture;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "AdsScopeMask",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            float mid = (size - 1) * 0.5f;
            // Clear lens inside ~0.62R; soft black housing to the square edge.
            float clearR = mid * 0.62f;
            float softStart = mid * 0.58f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - mid, dy = y - mid;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a;
                    if (r <= softStart) a = 0;
                    else if (r >= mid) a = 255;
                    else
                    {
                        float u = (r - softStart) / (mid - softStart);
                        a = (byte)Mathf.Clamp(Mathf.RoundToInt(u * u * 255f), 0, 255);
                    }
                    // Slight warmth on the housing rim so the tube does not read as flat GUI black.
                    byte v = r > clearR ? (byte)8 : (byte)0;
                    pixels[y * size + x] = new Color32(v, v, (byte)(v + 2), a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            scopeMaskTexture = tex;
            return tex;
        }

        public void Section(Rect r, string title)
        {
            Label(r, title, 12, Accent);
            Fill(new Rect(r.x, r.yMax - 2, Mathf.Min(88, r.width), 1), Raised);
            Fill(new Rect(r.x, r.yMax - 2, 18, 1), Accent);
        }

        /// <summary>Compact brand mark used on Main left rail — charcoal plate + amber chip.</summary>
        public void BrandMark(Rect r, string title)
        {
            Fill(new Rect(r.x, r.y + 6, 4, r.height - 12), Accent);
            Label(new Rect(r.x + 14, r.y, r.width - 14, r.height), title, 24);
        }

        public bool Button(Rect r, string id, string text, bool primary = false, bool selected = false, bool quiet = false)
        {
            if (GUI.enabled && Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition)) focused = id;
            bool focus = focused == id;
            if (focus && focusMoved) GUI.ScrollTo(r);
            bool hover = r.Contains(Event.current.mousePosition);
            Color fill = quiet
                ? new Color(1, 1, 1, hover || selected || focus ? .12f : 0)
                : primary ? Accent : (hover || selected ? Raised : Surface);
            Fill(r, GUI.enabled ? fill : Surface);
            if (quiet && (focus || selected))
            {
                Fill(new Rect(r.x, r.y, 3, r.height), Accent);
                Fill(new Rect(r.x + 3, r.y, r.width - 3, r.height), new Color(1, 1, 1, .06f));
            }
            else if (focus || selected)
            {
                Fill(new Rect(r.x, r.y, 3, r.height), primary ? Background : Accent);
            }
            if (focus && !quiet)
            {
                Color edge = primary ? Background : Accent;
                Fill(new Rect(r.x + 4, r.y + 3, r.width - 8, 1), edge);
                Fill(new Rect(r.x + 4, r.yMax - 4, r.width - 8, 1), edge);
            }
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(16, 12, 0, 0), wordWrap = true };
                Font? face = UiFonts.Button;
                if (face != null) buttonStyle.font = face;
                foreach (var state in new[] { buttonStyle.normal, buttonStyle.hover, buttonStyle.active, buttonStyle.focused }) state.background = null;
            }
            Color ink = StyleInk(GUI.enabled ? (primary ? Background : Text) : Muted);
            buttonStyle.fontSize = quiet ? 20 : 18;
            buttonStyle.padding = quiet ? new RectOffset(18, 12, 0, 0) : new RectOffset(16, 12, 0, 0);
            buttonStyle.normal.textColor = ink;
            buttonStyle.hover.textColor = ink;
            buttonStyle.active.textColor = ink;
            buttonStyle.focused.textColor = ink;
            if (GUI.enabled) controls.Add(id);
            GUI.SetNextControlName(id);
            bool clicked = GUI.Button(r, text, buttonStyle);
            return GUI.enabled && (clicked || activate == id);
        }

        public bool MapCard(Rect r, string id, Texture? image, string title, string subtitle, bool selected = false)
        {
            // Operation tile: charcoal well, inset media plate, caption bar — still draws real map RTTs.
            Fill(r, new Color(.02f, .03f, .035f, .98f));
            Edge(r);
            Rect media = new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 66);
            Fill(media, Surface);
            if (image != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(media, image, ScaleMode.ScaleAndCrop);
            // Soft top scrim so title plate stays readable over bright map solids.
            for (int i = 0; i < 7; i++)
            {
                float u = i / 6f;
                Fill(new Rect(media.x, media.y + i * 3f, media.width, 4f), new Color(0f, 0f, 0f, .16f * (1f - u)));
            }
            Rect plate = new Rect(r.x, r.yMax - 62, r.width, 62);
            Fill(plate, new Color(.02f, .03f, .035f, .94f));
            Fill(new Rect(plate.x, plate.y, plate.width, 1), new Color(1f, 1f, 1f, .10f));
            AccentBar(new Rect(plate.x + 14, plate.y + 10, 20, 2), Accent);
            bool clicked = Button(r, id, "", selected: selected, quiet: true);
            Label(new Rect(r.x + 14, r.yMax - 48, r.width - 28, 22), title, 18);
            Label(new Rect(r.x + 14, r.yMax - 24, r.width - 28, 16), subtitle, 12, Muted);
            if (selected)
            {
                AccentBar(new Rect(r.x, r.y, 3, r.height), Accent);
                AccentBar(new Rect(r.x, r.y, r.width, 2), Accent);
                AccentBar(new Rect(r.x, r.yMax - 3, r.width, 3), Accent);
                AccentBar(new Rect(r.xMax - 18, r.y + 6, 12, 1), Accent);
                AccentBar(new Rect(r.xMax - 7, r.y + 6, 1, 12), Accent);
            }
            return clicked;
        }

        public float Slider(Rect r, string id, string text, float value, float min, float max, string formatted)
        {
            Label(new Rect(r.x, r.y, r.width * .5f, r.height), text, 18, Muted, TextAnchor.MiddleLeft);
            Label(new Rect(r.x + r.width * .5f, r.y, r.width * .5f, r.height), formatted, 18, Text, TextAnchor.MiddleRight);
            Rect track = new Rect(r.x, r.yMax + 8, r.width, 6);
            var hit = new Rect(r.x, r.y, r.width, 52);
            if (Event.current.type == EventType.MouseDown && hit.Contains(Event.current.mousePosition)) focused = id;
            if (focused == id && Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.LeftArrow || Event.current.keyCode == KeyCode.RightArrow))
            {
                value = Mathf.Clamp(value + (Event.current.keyCode == KeyCode.LeftArrow ? -1 : 1) * (max - min) * .02f, min, max);
                Event.current.Use();
            }
            Fill(track, Raised);
            Fill(new Rect(track.x, track.y, Mathf.InverseLerp(min, max, value) * track.width, 6), Accent);
            // Use a transparent native slider to retain dragging and keyboard semantics without built-in artwork.
            controls.Add(id);
            GUI.SetNextControlName(id);
            var empty = GUIStyle.none;
            if (sliderThumb == null) sliderThumb = new GUIStyle(empty) { fixedWidth = 18, fixedHeight = 24 };
            if (focused == id && focusMoved) GUI.ScrollTo(hit);
            float next = GUI.HorizontalSlider(new Rect(track.x, track.y - 9, track.width, 24), value, min, max, empty, sliderThumb);
            float x = track.x + Mathf.InverseLerp(min, max, next) * (track.width - 4);
            Fill(new Rect(x, track.y - 4, 4, 14), focused == id ? Text : Accent);
            return next;
        }

        public bool Toggle(Rect r, string id, string text, bool value)
        {
            if (Button(r, id, text + (value ? "   [ ON ]" : "   [ OFF ]"), selected: value)) value = !value;
            return value;
        }

        public void Backdrop(bool overlay = false)
        {
            if (!overlay && SceneTexture != null)
            {
                if (Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(0, 0, Width, Height), SceneTexture, ScaleMode.ScaleAndCrop);
                // Compact left rail scrim + soft bottom fade so nav/cards stay readable over the live stage.
                for (int i = 0; i < 28; i++)
                {
                    float u = i / 27f;
                    Fill(new Rect(i * Width / 48, 0, Width / 48 + 1, Height), new Color(.02f, .03f, .035f, .9f * Mathf.Pow(1 - u, 1.85f)));
                    Fill(new Rect(0, Height - i * Height / 110, Width, Height / 110 + 1), new Color(.015f, .025f, .03f, .48f * (1 - u)));
                }
                return;
            }
            Fill(new Rect(0, 0, Width, Height), overlay ? new Color(.03f, .045f, .05f, .95f) : Background);
            if (overlay) return;
            // Analytic contour lines: decorative terrain, never a tactical map or an imported image.
            Color contour = new Color(.20f, .27f, .28f, .28f);
            float x0 = Width * .46f;
            for (int band = 0; band < 12; band++)
            {
                Vector2 previous = default;
                for (int step = 0; step <= 24; step++)
                {
                    float u = step / 24f;
                    float x = x0 + u * Width * .54f;
                    float y = Height * .22f + band * Height * .05f + Mathf.Sin(u * 7f + band * .21f) * Height * .08f + Mathf.Cos(u * 13f - band * .19f) * Height * .022f;
                    var point = new Vector2(x, y);
                    if (step > 0) Line(previous, point, contour);
                    previous = point;
                }
            }
            Fill(new Rect(0, Height - 8, Width * .22f, 2), Accent);
        }

        public void Header(string eyebrow, string title, string right = "LOCAL MATCH")
        {
            Label(new Rect(Safe.x, Safe.y, Safe.width * .65f, 16), eyebrow, 12, Accent);
            Label(new Rect(Safe.x, Safe.y + 16, Safe.width * .7f, 36), title, 32);
            Label(new Rect(Safe.x + Safe.width * .62f, Safe.y, Safe.width * .38f, 18), right, 12, Muted, TextAnchor.UpperRight);
            Fill(new Rect(Safe.x, Safe.y + 58, Safe.width, 1), Raised);
            Fill(new Rect(Safe.x, Safe.y + 58, 56, 2), Accent);
            Fill(new Rect(Safe.x + 56, Safe.y + 58, 24, 1), PanelEdge);
        }

        /// <summary>
        /// Full-screen operations insertion briefing. Indeterminate activity only; optional packaged map-card media.
        /// Returns true when the Cancel control is clicked (match path).
        /// </summary>
        public bool DrawInsertionScreen(
            string headerTitle,
            string briefingTitle,
            string mapDisplayName,
            string rulesLine,
            string phaseText,
            Texture? media,
            bool showCancel,
            bool reducedMotion,
            string footer)
        {
            Backdrop();
            Header(ShellUiCopy.EyebrowInsertion, headerTitle);
            Rect a = Safe;
            float panelW = Mathf.Min(920, a.width);
            float panelH = Mathf.Min(268, a.height * .48f);
            Rect panel = new Rect(a.x, a.y + 78, panelW, panelH);
            Panel(panel, accentTop: true, rail: true);

            float mediaW = 0f;
            if (media != null)
            {
                mediaW = Mathf.Min(280, panel.width * .34f);
                Rect mediaRect = new Rect(panel.x + 14, panel.y + 16, mediaW, panel.height - 32);
                Fill(mediaRect, new Color(.02f, .03f, .035f, .98f));
                Edge(mediaRect);
                Rect inset = new Rect(mediaRect.x + 3, mediaRect.y + 3, mediaRect.width - 6, mediaRect.height - 6);
                Fill(inset, Surface);
                if (Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(inset, media, ScaleMode.ScaleAndCrop);
                for (int i = 0; i < 7; i++)
                {
                    float u = i / 6f;
                    Fill(new Rect(inset.x, inset.y + i * 3f, inset.width, 4f), new Color(0f, 0f, 0f, .18f * (1f - u)));
                }
                // Selection-style corner brackets on the media plate.
                AccentBar(new Rect(mediaRect.x, mediaRect.y, 14, 2), Accent);
                AccentBar(new Rect(mediaRect.x, mediaRect.y, 2, 14), Accent);
                AccentBar(new Rect(mediaRect.xMax - 14, mediaRect.y, 14, 2), Accent);
                AccentBar(new Rect(mediaRect.xMax - 2, mediaRect.y, 2, 14), Accent);
                AccentBar(new Rect(mediaRect.x, mediaRect.yMax - 2, 14, 2), Accent);
                AccentBar(new Rect(mediaRect.x, mediaRect.yMax - 14, 2, 14), Accent);
                AccentBar(new Rect(mediaRect.xMax - 14, mediaRect.yMax - 2, 14, 2), Accent);
                AccentBar(new Rect(mediaRect.xMax - 2, mediaRect.yMax - 14, 2, 14), Accent);
                mediaW += 18f;
            }

            float textX = panel.x + 18 + mediaW;
            float textW = panel.xMax - textX - 18;
            Label(new Rect(textX, panel.y + 18, textW, 28), briefingTitle, 22);
            Label(new Rect(textX, panel.y + 52, textW, 28), (mapDisplayName ?? "").ToUpperInvariant(), 26);
            Label(new Rect(textX, panel.y + 88, textW, 22), rulesLine ?? "", 14, Muted);

            // Phase strip with accent tick.
            float phaseY = panel.y + 128;
            Fill(new Rect(textX, phaseY, 3, 36), Accent);
            Fill(new Rect(textX + 3, phaseY, textW - 3, 36), new Color(1f, 1f, 1f, .04f));
            Label(new Rect(textX + 14, phaseY + 6, textW - 28, 24), phaseText ?? "", 16, Muted);

            // Dual-track indeterminate activity with corner brackets (never a fabricated %).
            float barY = panel.yMax - 36;
            float barX = textX;
            float barW = textW;
            Fill(new Rect(barX, barY, barW, 10), Raised);
            Fill(new Rect(barX, barY, barW, 1), PanelEdge);
            Fill(new Rect(barX, barY + 9, barW, 1), new Color(0f, 0f, 0f, .35f));
            AccentBar(new Rect(barX, barY - 4, 10, 2), Accent);
            AccentBar(new Rect(barX, barY - 4, 2, 10), Accent);
            AccentBar(new Rect(barX + barW - 10, barY - 4, 10, 2), Accent);
            AccentBar(new Rect(barX + barW - 2, barY - 4, 2, 10), Accent);
            AccentBar(new Rect(barX, barY + 12, 10, 2), Accent);
            AccentBar(new Rect(barX, barY + 4, 2, 10), Accent);
            AccentBar(new Rect(barX + barW - 10, barY + 12, 10, 2), Accent);
            AccentBar(new Rect(barX + barW - 2, barY + 4, 2, 10), Accent);
            if (reducedMotion)
            {
                Fill(new Rect(barX, barY + 3, barW, 4), Accent);
            }
            else
            {
                float seg = Mathf.Max(48f, barW * .22f);
                float travel = Mathf.Max(1f, barW - seg);
                float offset = Mathf.PingPong(Time.realtimeSinceStartup * .42f, 1f) * travel;
                Fill(new Rect(barX + offset, barY + 2, seg, 6), Accent);
                Fill(new Rect(barX + offset, barY + 2, 3, 6), Text);
            }

            bool cancel = false;
            if (showCancel)
                cancel = Button(new Rect(a.x, a.yMax - 66, 180, 44), "load.cancel", "CANCEL");

            Fill(new Rect(a.x, a.yMax - 24, Mathf.Min(96, a.width * .16f), 1), Raised);
            Fill(new Rect(a.x, a.yMax - 24, 16, 1), Accent);
            Label(new Rect(a.x, a.yMax - 16, a.width, 16), footer ?? "", 12, Muted);
            return cancel;
        }

        public static string Clock(uint remainingTicks)
        {
            uint seconds = (uint)Math.Ceiling(remainingTicks / 60.0);
            return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }

        // Unity's IMGUI font path linearizes style tints; compensate so the displayed sRGB
        // text matches the palette used for contrast, just like the DrawTexture backing plates.
        private static Color StyleInk(Color color) => QualitySettings.activeColorSpace == ColorSpace.Linear ? color.gamma : color;

        public static string ControlLabel(string control)
        {
            if (control.StartsWith("Key.", StringComparison.Ordinal)) return control.Substring(4);
            if (control.StartsWith("Mouse.", StringComparison.Ordinal)) return "Mouse " + control.Substring(6);
            return control;
        }
    }
}
