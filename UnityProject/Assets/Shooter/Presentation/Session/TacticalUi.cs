#nullable enable
using System;
using System.Collections.Generic;
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
        public static readonly Color HudPlate = new Color(.04f, .055f, .06f, .58f);
        public static readonly Color HudPlateStrong = new Color(.035f, .048f, .055f, .72f);
        public static readonly Color HudEdge = new Color(1f, 1f, 1f, .10f);
        public static readonly Color HudEdgeSoft = new Color(1f, 1f, 1f, .06f);

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
                style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = size >= 28 ? FontStyle.Bold : FontStyle.Normal, wordWrap = true, padding = new RectOffset(0, 0, 0, 0), richText = false };
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
            if (accentBottom) AccentBar(new Rect(r.x + 2, r.yMax - 2, Mathf.Min(28, r.width - 4), 1), Accent);
        }

        /// <summary>1px plate edge: soft top/left highlight, quiet bottom/right shadow.</summary>
        public static void Edge(Rect r)
        {
            Fill(new Rect(r.x, r.y, r.width, 1), HudEdge);
            Fill(new Rect(r.x, r.yMax - 1, r.width, 1), new Color(0, 0, 0, .28f));
            Fill(new Rect(r.x, r.y, 1, r.height), HudEdgeSoft);
            Fill(new Rect(r.xMax - 1, r.y, 1, r.height), new Color(0, 0, 0, .18f));
        }

        /// <summary>Thin accent rail or progress fill (ally/enemy/amber).</summary>
        public static void AccentBar(Rect r, Color color) => Fill(r, color);

        /// <summary>Soft edge vignette from observed health/damage. Analytic fills only.</summary>
        public static void EdgeVignette(float width, float height, float strength, Color tint)
        {
            if (strength <= 0f || Event.current.type != EventType.Repaint) return;
            float band = Mathf.Max(28f, height * .12f);
            int steps = 10;
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
            Fill(r, Surface);
            Fill(new Rect(r.x, r.y, r.width, 1), PanelEdge);
            if (image != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(r, image, ScaleMode.ScaleAndCrop);
            // Dense caption plate with amber selection rail.
            Fill(new Rect(r.x, r.yMax - 62, r.width, 62), new Color(.02f, .03f, .035f, .92f));
            Fill(new Rect(r.x, r.yMax - 62, r.width, 1), new Color(1, 1, 1, .08f));
            bool clicked = Button(r, id, "", selected: selected, quiet: true);
            Label(new Rect(r.x + 14, r.yMax - 52, r.width - 28, 22), title, 18);
            Label(new Rect(r.x + 14, r.yMax - 26, r.width - 28, 18), subtitle, 12, Muted);
            if (selected)
            {
                Fill(new Rect(r.x, r.y, 3, r.height), Accent);
                Fill(new Rect(r.x, r.yMax - 3, r.width, 3), Accent);
                Fill(new Rect(r.x, r.y, r.width, 2), Accent);
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

        public void Header(string eyebrow, string title, string right = "LOCAL / OFFLINE")
        {
            Label(new Rect(Safe.x, Safe.y, Safe.width * .65f, 16), eyebrow, 12, Accent);
            Label(new Rect(Safe.x, Safe.y + 16, Safe.width * .7f, 36), title, 28);
            Label(new Rect(Safe.x + Safe.width * .62f, Safe.y, Safe.width * .38f, 18), right, 12, Muted, TextAnchor.UpperRight);
            Fill(new Rect(Safe.x, Safe.y + 58, Safe.width, 1), Raised);
            Fill(new Rect(Safe.x, Safe.y + 58, 56, 2), Accent);
            Fill(new Rect(Safe.x + 56, Safe.y + 58, 24, 1), PanelEdge);
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
