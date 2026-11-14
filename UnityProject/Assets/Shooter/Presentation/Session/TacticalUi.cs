#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Presentation.Session
{
    /// <summary>Code-only tactical shell. Coordinates are logical pixels after the applied UI scale.</summary>
    public sealed class TacticalUi
    {
        public static readonly Color Background = new Color32(15, 20, 23, 255);
        public static readonly Color Surface = new Color32(24, 31, 35, 255);
        public static readonly Color Raised = new Color32(37, 46, 50, 255);
        public static readonly Color Text = new Color32(241, 243, 239, 255);
        public static readonly Color Muted = new Color32(173, 183, 183, 255);
        public static readonly Color Accent = new Color32(230, 197, 106, 255);
        public static readonly Color Ally = new Color32(123, 197, 209, 255);
        public static readonly Color Enemy = new Color32(228, 123, 112, 255);

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
                style = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true, padding = new RectOffset(0, 0, 0, 0), richText = false };
                labels.Add(size, style);
            }
            style.normal.textColor = StyleInk(color ?? Text);
            style.alignment = align;
            GUI.Label(r, text, style);
        }

        public bool Button(Rect r, string id, string text, bool primary = false, bool selected = false)
        {
            if (GUI.enabled && Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition)) focused = id;
            bool focus = focused == id;
            if (focus && focusMoved) GUI.ScrollTo(r);
            bool hover = r.Contains(Event.current.mousePosition);
            Color fill = primary ? Accent : (hover || selected ? Raised : Surface);
            Fill(r, GUI.enabled ? fill : Surface);
            if (focus || selected) Fill(new Rect(r.x, r.y, 3, r.height), primary ? Background : Accent);
            if (focus)
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
            buttonStyle.normal.textColor = ink;
            buttonStyle.hover.textColor = ink;
            buttonStyle.active.textColor = ink;
            buttonStyle.focused.textColor = ink;
            if (GUI.enabled) controls.Add(id);
            GUI.SetNextControlName(id);
            bool clicked = GUI.Button(r, text, buttonStyle);
            return GUI.enabled && (clicked || activate == id);
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
            Fill(new Rect(0, 0, Width, Height), overlay ? new Color(.03f, .05f, .06f, .94f) : Background);
            if (overlay) return;
            // Analytic contour lines: decorative terrain, never a tactical map or an imported image.
            Color contour = new Color(.20f, .27f, .28f, .32f);
            float x0 = Width * .44f;
            for (int band = 0; band < 13; band++)
            {
                Vector2 previous = default;
                for (int step = 0; step <= 24; step++)
                {
                    float u = step / 24f;
                    float x = x0 + u * Width * .56f;
                    float y = Height * .2f + band * Height * .052f + Mathf.Sin(u * 7f + band * .21f) * Height * .09f + Mathf.Cos(u * 13f - band * .19f) * Height * .025f;
                    var point = new Vector2(x, y);
                    if (step > 0) Line(previous, point, contour);
                    previous = point;
                }
            }
            Fill(new Rect(0, Height - 8, Width * .25f, 2), Accent);
        }

        public void Header(string eyebrow, string title, string right = "LOCAL / OFFLINE")
        {
            Label(new Rect(Safe.x, Safe.y, Safe.width * .65f, 22), eyebrow, 14, Accent);
            Label(new Rect(Safe.x, Safe.y + 23, Safe.width * .7f, 48), title, 32);
            Label(new Rect(Safe.x + Safe.width * .72f, Safe.y, Safe.width * .28f, 24), right, 14, Muted, TextAnchor.UpperRight);
            Fill(new Rect(Safe.x, Safe.y + 77, Safe.width, 1), Raised);
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
