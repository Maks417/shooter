#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Presentation.Hud;
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

        public static readonly Color SurfaceTop = new Color32(35, 44, 49, 255);
        public static readonly Color Gold = new Color32(232, 196, 104, 255);
        public static readonly Color GoldDeep = new Color32(150, 108, 42, 255);

        private readonly List<string> controls = new List<string>(48);
        private readonly Dictionary<int, GUIStyle> labels = new Dictionary<int, GUIStyle>();
        private readonly Dictionary<string, Vector2> emphasis = new Dictionary<string, Vector2>();

        /// <summary>Applied reduced-camera-motion preference: UI transitions snap instead of easing.</summary>
        public bool ReducedMotion { get; set; }

        /// <summary>Applied reduced-flash preference: no glints, softer glows.</summary>
        public bool ReducedFlash { get; set; }
        private GUIStyle? buttonStyle;
        private GUIStyle? sliderThumb;
        private Matrix4x4 savedMatrix;
        private Color savedColor;
        private string screen = "";
        private string activate = "";
        private string focused = "";
        private bool focusFirst;
        private bool focusMoved;
        // ADR-096 UI sounds: hover is detected per Repaint pass so each newly hovered control plays once.
        private string hoveredLastPass = "";
        private string hoveredThisPass = "";
        /// <summary>Receives hover/click/back cues; null keeps the shell silent (tests, gallery).</summary>
        public System.Action<Shooter.Presentation.Audio.SynthVoiceKind>? Sound { get; set; }
        public float Width { get; private set; }
        public float Height { get; private set; }
        public Rect Safe { get; private set; }
        public Texture? SceneTexture { get; set; }
        private float enterOffset;

        public static Rect SafeArea(float width, float height) => new Rect(width * .05f, height * .05f, width * .9f, height * .9f);

        public void Begin(float scale, string screenId, bool keyboardNavigation = true, float offsetY = 0f)
        {
            savedMatrix = GUI.matrix;
            savedColor = GUI.color;
            GUI.color = Color.white;
            // ADR-086: optional enter offset (logical px) moves the whole screen; input follows the matrix.
            GUI.matrix = Matrix4x4.TRS(new Vector3(0f, offsetY * scale, 0f), Quaternion.identity, new Vector3(scale, scale, 1));
            enterOffset = offsetY;
            Width = Screen.width / scale;
            Height = Screen.height / scale;
            Safe = SafeArea(Width, Height);
            activate = "";
            focusMoved = false;
            if (Event.current.type == EventType.Repaint)
            {
                hoveredLastPass = hoveredThisPass;
                hoveredThisPass = "";
            }
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
                    Sound?.Invoke(Shooter.Presentation.Audio.SynthVoiceKind.UiHover);
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

        /// <summary>
        /// ADR-084 rounded plate: SDF when available (anti-aliased corners, optional shadow/gradient),
        /// otherwise the flat white-texture fill.
        /// </summary>
        public static void Plate(Rect r, in UiSdf.Style style)
        {
            if (!UiSdf.Rect(r, style)) Fill(r, style.Fill);
        }

        /// <summary>Rounded solid shape part (icons/silhouettes) with flat fallback.</summary>
        private static void Part(Rect r, Color color, float radius) => Plate(r, UiSdf.Style.Solid(color, radius));

        /// <summary>ADR-085 atlas name of the mesh-traced silhouette for a kill-feed weapon.</summary>
        public static string WeaponIconName(KillFeedWeapon weapon) => weapon switch
        {
            KillFeedWeapon.Carbine => "weapon.rifle-carbine",
            KillFeedWeapon.Pistol => "weapon.pistol",
            KillFeedWeapon.Knife => "weapon.knife",
            KillFeedWeapon.Grenade => "weapon.grenade",
            KillFeedWeapon.Ak47 => "weapon.ak47",
            KillFeedWeapon.Ak102 => "weapon.ak102",
            KillFeedWeapon.M16 => "weapon.m16",
            KillFeedWeapon.Mk14 => "weapon.mk14",
            KillFeedWeapon.Mtac338 => "weapon.mtac338",
            KillFeedWeapon.G17 => "weapon.g17",
            KillFeedWeapon.GoldenEagle => "weapon.golden-eagle",
            _ => "weapon.rifle"
        };

        /// <summary>ADR-085 atlas icon with the given ink; false when unavailable (callers keep text/shape fallbacks).</summary>
        public static bool Icon(Rect r, string name, Color color, bool shadow = false)
        {
            var style = UiSdf.Style.Solid(color);
            if (shadow) style = style.WithShadow(.45f, 4f, 1.5f);
            return UiSdf.Icon(r, name, style);
        }

        /// <summary>Procedural kill-feed weapon silhouette (analytic rounded parts; no sprites).</summary>
        public static void KillFeedWeaponIcon(Rect r, KillFeedWeapon weapon, Color color)
        {
            if (weapon == KillFeedWeapon.None || r.width <= 1f || r.height <= 1f) return;
            if (Event.current.type != EventType.Repaint) return;
            // ADR-085: silhouette traced from the packaged weapon mesh when the atlas has it.
            if (UiSdf.Icon(r, WeaponIconName(weapon), UiSdf.Style.Solid(color))) return;
            float x = r.x, y = r.y, w = r.width, h = r.height;
            float k = Mathf.Max(0.6f, h * 0.06f);
            switch (weapon)
            {
                case KillFeedWeapon.Rifle:
                case KillFeedWeapon.Carbine:
                case KillFeedWeapon.Ak47:
                case KillFeedWeapon.Ak102:
                case KillFeedWeapon.M16:
                case KillFeedWeapon.Mk14:
                case KillFeedWeapon.Mtac338:
                {
                    bool carbine = weapon == KillFeedWeapon.Carbine || weapon == KillFeedWeapon.Ak102;
                    float barrelEnd = carbine ? 0.88f : 0.98f;
                    // Stock (tapered by two parts), receiver, handguard, barrel, magazine, grip, optic.
                    Part(new Rect(x, y + h * 0.30f, w * 0.20f, h * 0.30f), color, k);
                    Part(new Rect(x + w * 0.04f, y + h * 0.40f, w * 0.18f, h * 0.18f), color, k);
                    Part(new Rect(x + w * 0.18f, y + h * 0.28f, w * 0.30f, h * 0.30f), color, k);
                    Part(new Rect(x + w * 0.46f, y + h * 0.31f, w * (carbine ? 0.24f : 0.30f), h * 0.22f), color, k);
                    Part(new Rect(x + w * 0.46f, y + h * 0.37f, w * (barrelEnd - 0.46f), h * 0.09f), color, k * 0.5f);
                    Part(new Rect(x + w * 0.36f, y + h * 0.52f, w * 0.09f, h * 0.36f), color, k);
                    Part(new Rect(x + w * 0.22f, y + h * 0.52f, w * 0.07f, h * 0.28f), color, k);
                    Part(new Rect(x + w * 0.26f, y + h * 0.14f, w * 0.18f, h * 0.12f), color, k);
                    break;
                }
                case KillFeedWeapon.Pistol:
                case KillFeedWeapon.G17:
                case KillFeedWeapon.GoldenEagle:
                {
                    // Long thin slide, frame, angled grip, trigger guard.
                    Part(new Rect(x + w * 0.08f, y + h * 0.18f, w * 0.80f, h * 0.24f), color, k);
                    Part(new Rect(x + w * 0.86f, y + h * 0.24f, w * 0.08f, h * 0.10f), color, k * 0.5f);
                    Part(new Rect(x + w * 0.14f, y + h * 0.38f, w * 0.56f, h * 0.12f), color, k * 0.5f);
                    float gw = Mathf.Max(1.5f, h * 0.24f);
                    if (!UiSdf.Line(new Vector2(x + w * 0.24f, y + h * 0.46f), new Vector2(x + w * 0.15f, y + h * 0.84f), color, gw))
                        Fill(new Rect(x + w * 0.14f, y + h * 0.40f, w * 0.18f, h * 0.48f), color);
                    var guard = UiSdf.Style.Solid(color, h * 0.1f);
                    guard.Stroke = Mathf.Max(1f, h * 0.06f);
                    UiSdf.Rect(new Rect(x + w * 0.30f, y + h * 0.44f, w * 0.16f, h * 0.24f), guard);
                    break;
                }
                case KillFeedWeapon.Knife:
                {
                    // Ridged handle, tall guard, wide blade with a pointed tip.
                    Part(new Rect(x + w * 0.04f, y + h * 0.40f, w * 0.30f, h * 0.24f), color, h * 0.12f);
                    Part(new Rect(x + w * 0.32f, y + h * 0.24f, w * 0.05f, h * 0.56f), color, k);
                    Part(new Rect(x + w * 0.37f, y + h * 0.34f, w * 0.42f, h * 0.30f), color, k * 0.5f);
                    if (!UiSdf.Shape(new Rect(x + w * 0.66f, y + h * 0.34f, w * 0.30f, h * 0.30f), Hud.UiShapeKind.Diamond, UiSdf.Style.Solid(color)))
                        Fill(new Rect(x + w * 0.79f, y + h * 0.40f, w * 0.14f, h * 0.18f), color);
                    break;
                }
                case KillFeedWeapon.Grenade:
                {
                    float d = Mathf.Min(w * 0.56f, h * 0.66f);
                    Rect body = new Rect(x + w * 0.5f - d * 0.5f, y + h * 0.28f, d, d);
                    if (!UiSdf.Circle(body, UiSdf.Style.Solid(color))) { Fill(body, color); break; }
                    // Fuze cap, spoon lever hugging the body, pull ring.
                    Part(new Rect(body.center.x - d * 0.2f, body.y - h * 0.12f, d * 0.4f, h * 0.16f), color, k);
                    float lw = Mathf.Max(1.2f, h * 0.08f);
                    UiSdf.Line(new Vector2(body.center.x + d * 0.18f, body.y - h * 0.08f), new Vector2(body.xMax + d * 0.04f, body.y + d * 0.55f), color, lw);
                    var ringStyleG = UiSdf.Style.Solid(color);
                    ringStyleG.Stroke = lw;
                    float rr = h * 0.22f;
                    UiSdf.Circle(new Rect(body.center.x - d * 0.2f - rr * 1.6f, body.y - h * 0.14f - rr * 0.6f, rr, rr), ringStyleG);
                    break;
                }
            }
        }

        /// <summary>Headshot mark: anti-aliased ring with crosshair ticks (flat fallback).</summary>
        public static void KillFeedHeadshotIcon(Rect r, Color color)
        {
            if (r.width <= 1f || r.height <= 1f) return;
            if (Event.current.type != EventType.Repaint) return;
            float cx = r.x + r.width * 0.5f;
            float cy = r.y + r.height * 0.5f;
            float rad = Mathf.Min(r.width, r.height) * 0.42f;
            if (UiSdf.Icon(new Rect(cx - rad * 1.2f, cy - rad * 1.2f, rad * 2.4f, rad * 2.4f), Generation.Icons.IconLibrary.Headshot, UiSdf.Style.Solid(color))) return;
            var ringStyle = UiSdf.Style.Solid(color);
            ringStyle.Stroke = Mathf.Max(1.2f, rad * 0.24f);
            if (UiSdf.Circle(new Rect(cx - rad, cy - rad, rad * 2f, rad * 2f), ringStyle))
            {
                float tick = Mathf.Max(1.1f, rad * 0.22f);
                UiSdf.Line(new Vector2(cx - rad * 1.15f, cy), new Vector2(cx - rad * 0.35f, cy), color, tick);
                UiSdf.Line(new Vector2(cx + rad * 0.35f, cy), new Vector2(cx + rad * 1.15f, cy), color, tick);
                UiSdf.Line(new Vector2(cx, cy - rad * 1.15f), new Vector2(cx, cy - rad * 0.35f), color, tick);
                UiSdf.Line(new Vector2(cx, cy + rad * 0.35f), new Vector2(cx, cy + rad * 1.15f), color, tick);
                Part(new Rect(cx - tick * 0.6f, cy - tick * 0.6f, tick * 1.2f, tick * 1.2f), color, tick * 0.6f);
                return;
            }
            // Approximate circle with a cross of fills (readable at ~12 px).
            float t = Mathf.Max(1.2f, rad * 0.28f);
            Fill(new Rect(cx - rad, cy - t * 0.5f, rad * 2f, t), color);
            Fill(new Rect(cx - t * 0.5f, cy - rad, t, rad * 2f), color);
            float ring = Mathf.Max(1f, t * 0.55f);
            Fill(new Rect(cx - rad, cy - rad, rad * 2f, ring), color);
            Fill(new Rect(cx - rad, cy + rad - ring, rad * 2f, ring), color);
            Fill(new Rect(cx - rad, cy - rad, ring, rad * 2f), color);
            Fill(new Rect(cx + rad - ring, cy - rad, ring, rad * 2f), color);
        }

        public static void Line(Vector2 a, Vector2 b, Color color, float thickness = 1)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (UiSdf.Line(a, b, color, thickness)) return;
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
            var style = UiSdf.Style.Solid(SurfaceTop, Hud.UiShapeParams.PlateRadius)
                .WithGradient(Surface, UiSdf.Gradient.Vertical)
                .WithShadow();
            if (UiSdf.Rect(r, style))
            {
                var edge = UiSdf.Style.Solid(new Color(1f, 1f, 1f, .07f), Hud.UiShapeParams.PlateRadius);
                edge.Stroke = 1f;
                UiSdf.Rect(r, edge);
                if (accentTop) Plate(new Rect(r.x + 6, r.y, r.width - 12, 2), UiSdf.Style.Solid(Accent, 1f));
                if (rail) Plate(new Rect(r.x, r.y + 6, 3, r.height - 12), UiSdf.Style.Solid(Accent, 1.5f));
                return;
            }
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
            Color plate = strong ? HudPlateStrong : HudPlate;
            var style = UiSdf.Style.Solid(new Color(plate.r + .025f, plate.g + .03f, plate.b + .032f, plate.a), Hud.UiShapeParams.HudPlateRadius)
                .WithGradient(new Color(plate.r, plate.g, plate.b, Mathf.Min(1f, plate.a + .08f)), UiSdf.Gradient.Vertical)
                .WithShadow(.22f, 6f, 1.5f);
            if (UiSdf.Rect(r, style))
            {
                var edge = UiSdf.Style.Solid(HudEdge, Hud.UiShapeParams.HudPlateRadius);
                edge.Stroke = 1f;
                UiSdf.Rect(r, edge);
                if (accentLeft) Plate(new Rect(r.x, r.y + 3, 2, r.height - 6), UiSdf.Style.Solid(Accent, 1f));
                if (accentBottom) Plate(new Rect(r.x + 6, r.yMax - 5, Mathf.Min(22, r.width - 12), 2f), UiSdf.Style.Solid(Accent, 1f));
                return;
            }
            Fill(r, plate);
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
            float rr = Mathf.Min(rect.width, rect.height) * .5f;
            if (UiSdf.Rect(new Rect(rect.x - 0.9f, rect.y - 0.9f, rect.width + 1.8f, rect.height + 1.8f),
                    UiSdf.Style.Solid(new Color(0.02f, 0.03f, 0.035f, 0.8f), rr + .9f)))
            {
                UiSdf.Rect(rect, UiSdf.Style.Solid(Text, rr));
                return;
            }
            Fill(new Rect(rect.x - 0.75f, rect.y - 0.75f, rect.width + 1.5f, rect.height + 1.5f), new Color(0.02f, 0.03f, 0.035f, 0.85f));
            Fill(rect, Text);
        }

        /// <summary>Warface-style X hit marker at screen center. Alpha is presentation-only.</summary>
        public static void HitMarker(float centerX, float centerY, float alpha, float scale = 1f, bool headshot = false)
        {
            if (alpha <= 0.02f || Event.current.type != EventType.Repaint) return;
            float arm = 5.5f * scale, gap = 3.5f * scale, thick = 1.25f;
            Color tint = headshot ? Accent : Text;
            Color ink = new Color(tint.r, tint.g, tint.b, alpha);
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

        /// <summary>Shared gallery/tactical glyph path; yaw is clockwise from north, radians.</summary>
        public static void MapGlyph(float x, float y, string name, Color ink, float yaw = 0f, float size = 12f)
        {
            Matrix4x4 prior = GUI.matrix;
            try
            {
                GUI.matrix = prior * Matrix4x4.TRS(new Vector3(x, y, 0), Quaternion.Euler(0, 0, yaw * Mathf.Rad2Deg), Vector3.one);
                Icon(new Rect(-size * .5f, -size * .5f, size, size), name, ink, shadow: true);
            }
            finally { GUI.matrix = prior; }
        }

        public static void DamageArc(float x, float y, float angle, float alpha)
        {
            const float radius = CombatHudVisual.DamageIndicatorRadius;
            float half = CombatHudVisual.DamageIndicatorLength / radius * .5f;
            UiSdf.Arc(new Rect(x - radius, y - radius, radius * 2, radius * 2), angle - half, angle + half, 5f,
                UiSdf.Style.Solid(new Color(Enemy.r, Enemy.g, Enemy.b, alpha)));
        }

        public static void AmmoRounds(Rect track, int rounds, Color ink)
        {
            if (rounds > CombatHudVisual.MaxRoundGlyphs)
            {
                AccentBar(new Rect(track.x, track.y + track.height * .3f, track.width, track.height * .4f), ink);
                return;
            }
            float pitch = track.width / CombatHudVisual.MaxRoundGlyphs;
            for (int i = 0; i < CombatHudVisual.RoundGlyphCount(rounds); i++)
                Icon(new Rect(track.x + i * pitch, track.y, pitch - .5f, track.height), Generation.Icons.IconLibrary.Ammo, ink);
        }

        /// <summary>
        /// Procedural kill ribbon (ADR-059): striped banner + center disk. No imported sprites.
        /// </summary>
        public static void ScoreRibbon(Rect banner, float alpha, float elapsed = 10f, int streak = 1, bool reducedMotion = false, bool reducedFlash = false)
        {
            if (alpha <= 0.02f || Event.current.type != EventType.Repaint) return;
            if (UiSdf.Available)
            {
                ScoreRibbonSdf(banner, alpha, elapsed, streak, reducedMotion, reducedFlash);
                return;
            }
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

        private static Color A(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);

        /// <summary>ADR-084 kill ribbon: feathered banner, stamped medal, streak pips and one glint.</summary>
        private static void ScoreRibbonSdf(Rect banner, float alpha, float elapsed, int streak, bool reducedMotion, bool reducedFlash)
        {
            float enter = Hud.UiMotion.EnterAlpha(elapsed);
            alpha *= enter;
            if (alpha <= 0.02f) return;
            float cx = banner.x + banner.width * 0.5f, cy = banner.y + banner.height * 0.5f;
            float spread = reducedMotion ? 1f : Mathf.Lerp(0.55f, 1f, Hud.UiMotion.OutCubic(Hud.UiMotion.Progress(elapsed, Hud.UiMotion.TransitionSeconds * 1.4f)));
            float half = banner.width * 0.5f * spread;
            Color dark = new Color(0.03f, 0.042f, 0.048f, 0.9f * alpha);
            Color clear = new Color(dark.r, dark.g, dark.b, 0f);
            float top = banner.y + 6, height = banner.height - 12, fadeW = half * 0.6f;
            UiSdf.Rect(new Rect(cx - half, top, fadeW, height), UiSdf.Style.Solid(clear).WithGradient(dark, UiSdf.Gradient.Horizontal));
            UiSdf.Rect(new Rect(cx - half + fadeW - 0.5f, top, (half - fadeW) * 2f + 1f, height), UiSdf.Style.Solid(dark));
            UiSdf.Rect(new Rect(cx + half - fadeW, top, fadeW, height), UiSdf.Style.Solid(dark).WithGradient(clear, UiSdf.Gradient.Horizontal));
            Color line = A(Accent, alpha), lineClear = A(Accent, 0f);
            for (int edge = 0; edge < 2; edge++)
            {
                float ly = edge == 0 ? top : top + height - 2f;
                Color l = edge == 0 ? line : A(line, .55f);
                UiSdf.Rect(new Rect(cx - half, ly, half, 2f), UiSdf.Style.Solid(lineClear).WithGradient(l, UiSdf.Gradient.Horizontal));
                UiSdf.Rect(new Rect(cx, ly, half, 2f), UiSdf.Style.Solid(l).WithGradient(lineClear, UiSdf.Gradient.Horizontal));
            }

            int tier = Mathf.Clamp(streak, 1, 4);
            float baseSize = Mathf.Min(banner.height - 2f, 30f + 3f * (tier - 1));
            float size = baseSize * Hud.UiMotion.StampScale(elapsed, reducedMotion);
            float glint = Hud.UiMotion.GlintPosition(elapsed, reducedFlash);
            Medal(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), tier, alpha, glint);
        }

        /// <summary>
        /// ADR-084 procedural medal: shadowed gold rim, enamel disk, diamond emblem and <paramref name="tier"/> pips.
        /// Shapes only (no texture); tier 1..4.
        /// </summary>
        public static void Medal(Rect r, int tier, float alpha, float glint = -1f)
        {
            if (alpha <= 0.01f || r.width <= 2f) return;
            float s = Mathf.Min(r.width, r.height);
            Rect disk = new Rect(r.center.x - s * 0.5f, r.center.y - s * 0.5f, s, s);
            // Wings: three short swept strokes each side (laurel suggestion), more with higher tiers.
            int wings = Mathf.Clamp(tier + 1, 2, 4);
            for (int i = 0; i < wings; i++)
            {
                float t = i / (float)Mathf.Max(1, wings - 1);
                float yy = disk.center.y - s * 0.18f + t * s * 0.42f;
                float len = s * (0.36f - 0.06f * t);
                UiSdf.Line(new Vector2(disk.x - s * 0.04f, yy), new Vector2(disk.x - s * 0.04f - len, yy - len * 0.45f), A(Gold, 0.85f * alpha), Mathf.Max(1.4f, s * 0.07f));
                UiSdf.Line(new Vector2(disk.xMax + s * 0.04f, yy), new Vector2(disk.xMax + s * 0.04f + len, yy - len * 0.45f), A(Gold, 0.85f * alpha), Mathf.Max(1.4f, s * 0.07f));
            }
            UiSdf.Circle(disk, UiSdf.Style.Solid(A(Gold, alpha)).WithGradient(A(GoldDeep, alpha), UiSdf.Gradient.Vertical)
                .WithShadow(0.45f * alpha, s * 0.18f, s * 0.06f).WithGlint(glint, 0.6f * alpha));
            float inset = s * 0.14f;
            Rect enamel = new Rect(disk.x + inset, disk.y + inset, s - inset * 2f, s - inset * 2f);
            Color enamelTop = tier >= 3 ? new Color32(122, 42, 36, 255) : new Color32(30, 62, 52, 255);
            Color enamelBottom = tier >= 3 ? new Color32(70, 20, 18, 255) : new Color32(16, 34, 30, 255);
            UiSdf.Circle(enamel, UiSdf.Style.Solid(A(enamelTop, alpha)).WithGradient(A(enamelBottom, alpha), UiSdf.Gradient.Radial));
            var ring = UiSdf.Style.Solid(A(new Color(1f, .92f, .7f, .55f), alpha));
            ring.Stroke = Mathf.Max(0.8f, s * 0.035f);
            UiSdf.Circle(enamel, ring);
            float e = enamel.width * 0.52f;
            UiSdf.Shape(new Rect(enamel.center.x - e * 0.5f, enamel.center.y - e * 0.5f, e, e), Hud.UiShapeKind.Diamond,
                UiSdf.Style.Solid(A(new Color(.98f, .9f, .66f, 1f), alpha)).WithGradient(A(Gold, alpha), UiSdf.Gradient.Vertical));
            // Streak pips above the medal.
            if (tier > 1)
            {
                float pip = Mathf.Max(3f, s * 0.16f);
                float total = tier * pip + (tier - 1) * pip * 0.45f;
                float px = disk.center.x - total * 0.5f;
                for (int i = 0; i < tier; i++)
                {
                    UiSdf.Shape(new Rect(px + i * pip * 1.45f, disk.y - pip * 1.35f, pip, pip), Hud.UiShapeKind.Diamond,
                        UiSdf.Style.Solid(A(Accent, alpha)));
                }
            }
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
            if (UiSdf.EdgeFade(new Rect(0, 0, width, height), tint, Mathf.Max(40f, height * .16f), strength)) return;
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
            // Housing ring only — clear lens (~0.62 of pad) aligned to LensRadiusPixels; the mask is transparent at
            // its square edges, so no rectangle is drawn on wide screens.
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
            // Clear lens inside ~0.62R, a soft dark housing band, then fully clear again well before the texture
            // edge. The texture is a square sized from the shorter screen side, so anything opaque at its edges
            // shows up as a dark rectangle with vertical seams on wide screens.
            float clearR = mid * 0.62f;
            float softStart = mid * 0.58f;
            float bandEnd = mid * 0.72f;
            float fadeEnd = mid * 0.97f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - mid, dy = y - mid;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha;
                    if (r <= softStart || r >= fadeEnd) alpha = 0f;
                    else if (r < clearR) { float u = (r - softStart) / (clearR - softStart); alpha = u * u; }
                    else if (r <= bandEnd) alpha = 1f;
                    else { float u = 1f - (r - bandEnd) / (fadeEnd - bandEnd); alpha = u * u * (3f - 2f * u); }
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
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
            // Numeric stencil cuts across the condensed bold wordmark, not a bitmap logo.
            Fill(new Rect(r.x + 14, r.y + r.height * .47f, r.width - 14, 1.5f), Background);
        }

        /// <summary>
        /// Eased 0..1 emphasis for a control (hover/focus/selection), advanced once per Repaint.
        /// Reduced motion snaps to the target.
        /// </summary>
        public float Emphasis(string id, bool on)
        {
            float target = on ? 1f : 0f;
            float now = Time.unscaledTime;
            if (!emphasis.TryGetValue(id, out var state)) state = new Vector2(target, now);
            if (Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Clamp(now - state.y, 0f, 0.1f);
                state = new Vector2(Hud.UiMotion.Follow(state.x, target, Hud.UiMotion.FocusFollowRate, dt, ReducedMotion), now);
                emphasis[id] = state;
            }
            return state.x;
        }

        private bool ButtonSdf(Rect r, string id, bool primary, bool selected, bool quiet, bool focus, bool hover)
        {
            if (!UiSdf.Available) return false;
            if (Event.current.type != EventType.Repaint)
            {
                Emphasis(id, hover || focus || selected);
                return true;
            }
            float e = Emphasis(id, hover || focus || selected);
            float f = Emphasis(id + "#focus", focus);
            const float radius = Hud.UiShapeParams.ButtonRadius;
            if (!GUI.enabled)
            {
                UiSdf.Rect(r, UiSdf.Style.Solid(quiet ? new Color(1, 1, 1, 0) : Surface, radius));
                return true;
            }
            if (quiet)
            {
                if (e > 0.01f)
                {
                    UiSdf.Rect(r, UiSdf.Style.Solid(new Color(1, 1, 1, .10f * e), radius)
                        .WithGradient(new Color(1, 1, 1, .02f * e), UiSdf.Gradient.Horizontal));
                }
                float rail = Mathf.Max(e * (focus || selected ? 1f : 0.45f), f);
                if (rail > 0.01f)
                {
                    float h = r.height * Mathf.Lerp(0.35f, 1f, rail);
                    UiSdf.Rect(new Rect(r.x, r.center.y - h * 0.5f, 3, h), UiSdf.Style.Solid(A(Accent, rail), 1.5f));
                }
                return true;
            }
            Color baseTop = primary ? Color.Lerp(Accent, new Color(1f, .9f, .62f, 1f), .35f * e) : Color.Lerp(SurfaceTop, new Color32(56, 68, 74, 255), e);
            Color baseBottom = primary ? Color.Lerp(new Color32(196, 160, 72, 255), Accent, .3f * e) : Color.Lerp(Surface, Raised, e);
            var style = UiSdf.Style.Solid(baseTop, radius).WithGradient(baseBottom, UiSdf.Gradient.Vertical).WithShadow(.3f, 6f, 2f);
            if (f > 0.01f) style = style.WithGlow(primary ? new Color(1f, .9f, .6f, 1f) : Accent, Hud.UiShapeParams.FocusGlowWidth,
                Hud.UiShapeParams.FocusGlowAlpha * f * (ReducedFlash ? .6f : 1f));
            UiSdf.Rect(r, style);
            var edge = UiSdf.Style.Solid(primary ? new Color(1f, .95f, .8f, .35f) : new Color(1f, 1f, 1f, .06f + .08f * e), radius);
            edge.Stroke = 1f;
            UiSdf.Rect(r, edge);
            float sel = Mathf.Max(f, selected ? 1f : 0f);
            if (sel > 0.01f)
            {
                float h = r.height * Mathf.Lerp(0.4f, 0.7f, sel);
                UiSdf.Rect(new Rect(r.x + 5, r.center.y - h * 0.5f, 3, h), UiSdf.Style.Solid(A(primary ? Background : Accent, sel), 1.5f));
            }
            return true;
        }

        public bool Button(Rect r, string id, string text, bool primary = false, bool selected = false, bool quiet = false)
        {
            if (GUI.enabled && Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition)) focused = id;
            bool focus = focused == id;
            if (focus && focusMoved) GUI.ScrollTo(r);
            bool hover = r.Contains(Event.current.mousePosition);
            if (hover && GUI.enabled && Event.current.type == EventType.Repaint)
            {
                hoveredThisPass = id;
                if (id != hoveredLastPass) Sound?.Invoke(Shooter.Presentation.Audio.SynthVoiceKind.UiHover);
            }
            if (!ButtonSdf(r, id, primary, selected, quiet, focus, hover))
            {
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
            bool pressed = GUI.enabled && (clicked || activate == id);
            if (pressed) Sound?.Invoke(IsBackControl(id) ? Shooter.Presentation.Audio.SynthVoiceKind.UiBack : Shooter.Presentation.Audio.SynthVoiceKind.UiClick);
            return pressed;
        }

        /// <summary>Controls that leave or cancel get the softer back cue.</summary>
        public static bool IsBackControl(string id) =>
            id.EndsWith(".back", System.StringComparison.Ordinal) || id.EndsWith(".cancel", System.StringComparison.Ordinal)
            || id.EndsWith(".revert", System.StringComparison.Ordinal) || id.EndsWith(".menu", System.StringComparison.Ordinal)
            || id.EndsWith(".quit", System.StringComparison.Ordinal) || id.EndsWith(".leave", System.StringComparison.Ordinal);

        public bool MapCard(Rect r, string id, Texture? image, string title, string subtitle, bool selected = false)
        {
            // Operation tile: charcoal well, inset media plate, caption bar — still draws real map RTTs.
            float lift = UiSdf.Available ? Emphasis(id + "#card", selected || r.Contains(Event.current.mousePosition)) : 0f;
            r.y -= ReducedMotion ? 0f : 4f * lift;
            var well = UiSdf.Style.Solid(new Color(.02f, .03f, .035f, .98f), Hud.UiShapeParams.CardRadius)
                .WithShadow(.35f + .15f * lift, 10f + 6f * lift, 3f + 2f * lift);
            if (selected) well = well.WithGlow(Accent, 10f, .22f * (ReducedFlash ? .6f : 1f));
            if (UiSdf.Rect(r, well))
            {
                var rim = UiSdf.Style.Solid(new Color(1f, 1f, 1f, .08f + .08f * lift), Hud.UiShapeParams.CardRadius);
                rim.Stroke = 1f;
                UiSdf.Rect(r, rim);
            }
            else
            {
                Fill(r, new Color(.02f, .03f, .035f, .98f));
                Edge(r);
            }
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
            // Rounded so the card's lower corners stay round; the plate's top corners blend into the dark well.
            Plate(plate, UiSdf.Style.Solid(new Color(.025f, .035f, .04f, .96f), Hud.UiShapeParams.CardRadius)
                .WithGradient(new Color(.015f, .02f, .025f, .98f), UiSdf.Gradient.Vertical));
            Fill(new Rect(plate.x, plate.y, plate.width, 1), new Color(1f, 1f, 1f, .10f));
            Icon(new Rect(plate.xMax - 27, plate.y + 12, 16, 16), Generation.Icons.IconLibrary.Objective, selected ? Accent : Muted);
            AccentBar(new Rect(plate.x + 14, plate.y + 10, 20, 2), Accent);
            bool clicked = Button(r, id, "", selected: selected, quiet: true);
            Label(new Rect(r.x + 14, r.yMax - 48, r.width - 28, 22), title, 18);
            Label(new Rect(r.x + 14, r.yMax - 24, r.width - 28, 16), subtitle, 12, Muted);
            if (selected && UiSdf.Available)
            {
                var ring = UiSdf.Style.Solid(Accent, Hud.UiShapeParams.CardRadius);
                ring.Stroke = 2f;
                UiSdf.Rect(r, ring);
                UiSdf.Rect(new Rect(r.xMax - 22, r.y + 8, 14, 2), UiSdf.Style.Solid(Accent, 1f));
                UiSdf.Rect(new Rect(r.xMax - 10, r.y + 8, 2, 14), UiSdf.Style.Solid(Accent, 1f));
            }
            else if (selected)
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
            bool sdf = UiSdf.Rect(track, UiSdf.Style.Solid(new Color32(52, 62, 68, 255), 3f).WithGradient(new Color32(36, 44, 49, 255), UiSdf.Gradient.Vertical));
            if (sdf)
            {
                float fillW = Mathf.InverseLerp(min, max, value) * track.width;
                if (fillW > 0.5f)
                    UiSdf.Rect(new Rect(track.x, track.y, Mathf.Max(6f, fillW), 6), UiSdf.Style.Solid(new Color32(196, 160, 72, 255), 3f)
                        .WithGradient(Accent, UiSdf.Gradient.Horizontal));
            }
            else
            {
                Fill(track, Raised);
                Fill(new Rect(track.x, track.y, Mathf.InverseLerp(min, max, value) * track.width, 6), Accent);
            }
            // Use a transparent native slider to retain dragging and keyboard semantics without built-in artwork.
            controls.Add(id);
            GUI.SetNextControlName(id);
            var empty = GUIStyle.none;
            if (sliderThumb == null) sliderThumb = new GUIStyle(empty) { fixedWidth = 18, fixedHeight = 24 };
            if (focused == id && focusMoved) GUI.ScrollTo(hit);
            float next = GUI.HorizontalSlider(new Rect(track.x, track.y - 9, track.width, 24), value, min, max, empty, sliderThumb);
            if (sdf)
            {
                float f = Emphasis(id + "#thumb", focused == id || hit.Contains(Event.current.mousePosition));
                float tx = track.x + Mathf.InverseLerp(min, max, next) * track.width;
                float d = 14f + 4f * f;
                var thumb = UiSdf.Style.Solid(Text).WithGradient(new Color32(206, 210, 204, 255), UiSdf.Gradient.Vertical).WithShadow(.4f, 5f, 1.5f);
                if (f > 0.01f) thumb = thumb.WithGlow(Accent, 7f, .3f * f);
                UiSdf.Circle(new Rect(tx - d * 0.5f, track.center.y - d * 0.5f, d, d), thumb);
                UiSdf.Circle(new Rect(tx - 3f, track.center.y - 3f, 6f, 6f), UiSdf.Style.Solid(Accent));
                return next;
            }
            float x = track.x + Mathf.InverseLerp(min, max, next) * (track.width - 4);
            Fill(new Rect(x, track.y - 4, 4, 14), focused == id ? Text : Accent);
            return next;
        }

        public bool Toggle(Rect r, string id, string text, bool value)
        {
            if (!UiSdf.Available)
            {
                if (Button(r, id, text + (value ? "   [ ON ]" : "   [ OFF ]"), selected: value)) value = !value;
                return value;
            }
            if (Button(r, id, text, selected: value)) value = !value;
            // Switch pill on the right edge; knob eases between OFF and ON.
            float on = Emphasis(id + "#switch", value);
            Rect pill = new Rect(r.xMax - 56, r.center.y - 10, 42, 20);
            UiSdf.Rect(pill, UiSdf.Style.Solid(Color.Lerp(new Color32(20, 26, 30, 255), new Color32(196, 160, 72, 255), on), 10f)
                .WithGradient(Color.Lerp(Raised, Accent, on), UiSdf.Gradient.Vertical));
            var rim = UiSdf.Style.Solid(new Color(1f, 1f, 1f, .12f), 10f);
            rim.Stroke = 1f;
            UiSdf.Rect(pill, rim);
            float kx = Mathf.Lerp(pill.x + 3, pill.xMax - 17, on);
            UiSdf.Circle(new Rect(kx, pill.y + 3, 14, 14), UiSdf.Style.Solid(Text).WithShadow(.35f, 3f, 1f));
            Label(new Rect(pill.x - 50, r.y, 44, r.height), value ? "ON" : "OFF", 12, value ? Accent : Muted, TextAnchor.MiddleRight);
            return value;
        }

        public void Backdrop(bool overlay = false)
        {
            // The backdrop stays put while page content rises in (ADR-086 enter offset).
            Matrix4x4 contentMatrix = GUI.matrix;
            GUI.matrix = contentMatrix * Matrix4x4.Translate(new Vector3(0f, -enterOffset, 0f));
            try { DrawBackdrop(overlay); }
            finally { GUI.matrix = contentMatrix; }
        }

        private void DrawBackdrop(bool overlay)
        {
            if (!overlay && SceneTexture != null)
            {
                if (Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(0, 0, Width, Height), SceneTexture, ScaleMode.ScaleAndCrop);
                // Compact left rail scrim + soft bottom fade so nav/cards stay readable over the live stage.
                if (UiSdf.Available)
                {
                    // Two smooth gradient spans approximate the eased falloff without banding.
                    Color deep = new Color(.02f, .03f, .035f, .9f), mid = new Color(.02f, .03f, .035f, .38f), none = new Color(.02f, .03f, .035f, 0f);
                    float spanA = Width * .2f, spanB = Width * .38f;
                    UiSdf.Rect(new Rect(0, 0, spanA, Height), UiSdf.Style.Solid(deep).WithGradient(mid, UiSdf.Gradient.Horizontal));
                    UiSdf.Rect(new Rect(spanA, 0, spanB, Height), UiSdf.Style.Solid(mid).WithGradient(none, UiSdf.Gradient.Horizontal));
                    float fade = Height * .26f;
                    UiSdf.Rect(new Rect(0, Height - fade, Width, fade), UiSdf.Style.Solid(new Color(.015f, .025f, .03f, 0f))
                        .WithGradient(new Color(.015f, .025f, .03f, .5f), UiSdf.Gradient.Vertical));
                    return;
                }
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
            string footer,
            System.Collections.Generic.IReadOnlyList<Shooter.Generation.Environment.CollisionBox>? blueprint = null,
            Shooter.Contracts.Float3? spawn1 = null, Shooter.Contracts.Float3? spawn2 = null)
        {
            Backdrop();
            Header(ShellUiCopy.EyebrowInsertion, headerTitle);
            Rect a = Safe;
            float panelW = Mathf.Min(920, a.width);
            float panelH = Mathf.Min(268, a.height * .48f);
            Rect panel = new Rect(a.x, a.y + 78, panelW, panelH);
            Panel(panel, accentTop: true, rail: true);

            float mediaW = 0f;
            if (media != null || blueprint != null)
            {
                mediaW = Mathf.Min(280, panel.width * .34f);
                Rect mediaRect = new Rect(panel.x + 14, panel.y + 16, mediaW, panel.height - 32);
                Fill(mediaRect, new Color(.02f, .03f, .035f, .98f));
                Edge(mediaRect);
                Rect inset = new Rect(mediaRect.x + 3, mediaRect.y + 3, mediaRect.width - 6, mediaRect.height - 6);
                Fill(inset, Surface);
                if (blueprint != null) DrawBlueprint(inset, blueprint, spawn1, spawn2, reducedMotion);
                else if (media != null && Event.current.type == EventType.Repaint)
                {
                    float pan = reducedMotion ? 0f : Mathf.Sin(Time.unscaledTime * .08f) * .025f;
                    GUI.DrawTextureWithTexCoords(inset, media, new Rect(.04f + pan, .04f, .92f, .92f));
                }
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
            if (UiSdf.Rect(new Rect(barX, barY, barW, 10), UiSdf.Style.Solid(new Color32(48, 58, 64, 255), 5f).WithGradient(new Color32(30, 38, 43, 255), UiSdf.Gradient.Vertical)))
            {
                var trackRim = UiSdf.Style.Solid(new Color(1f, 1f, 1f, .10f), 5f);
                trackRim.Stroke = 1f;
                UiSdf.Rect(new Rect(barX, barY, barW, 10), trackRim);
                if (reducedMotion)
                {
                    UiSdf.Rect(new Rect(barX + 2, barY + 2, barW - 4, 6), UiSdf.Style.Solid(Accent, 3f));
                }
                else
                {
                    float seg = Mathf.Max(48f, barW * .22f);
                    float travel = Mathf.Max(1f, barW - 4f - seg);
                    float offset = Hud.UiMotion.InOutCubic(Mathf.PingPong(Time.realtimeSinceStartup * .42f, 1f)) * travel;
                    UiSdf.Rect(new Rect(barX + 2 + offset, barY + 2, seg, 6), UiSdf.Style.Solid(new Color(Accent.r, Accent.g, Accent.b, .25f), 3f)
                        .WithGradient(Accent, UiSdf.Gradient.Horizontal).WithGlow(Accent, 8f, ReducedFlash ? .12f : .3f));
                }
            }
            else
            {
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
            }

            Label(new Rect(a.x, panel.yMax + 16, panelW, 38),
                InsertionScreenCopy.TipAt(Time.unscaledTime, reducedMotion), 14, Muted);
            bool cancel = false;
            if (showCancel)
                cancel = Button(new Rect(a.x, a.yMax - 66, 180, 44), "load.cancel", "CANCEL");

            Fill(new Rect(a.x, a.yMax - 24, Mathf.Min(96, a.width * .16f), 1), Raised);
            Fill(new Rect(a.x, a.yMax - 24, 16, 1), Accent);
            Label(new Rect(a.x, a.yMax - 16, a.width, 16), footer ?? "", 12, Muted);
            return cancel;
        }

        public void Keycap(Rect r, string label, Color? ink = null)
        {
            var style = UiSdf.Style.Solid(ink ?? Text, 3f); style.Stroke = 1f;
            UiSdf.Rect(r, UiSdf.Style.Solid(Raised, 3f));
            UiSdf.Rect(r, style);
            Label(r, label, 11, ink ?? Text, TextAnchor.MiddleCenter);
        }

        private void DrawBlueprint(Rect r, System.Collections.Generic.IReadOnlyList<Shooter.Generation.Environment.CollisionBox> boxes,
            Shooter.Contracts.Float3? spawn1, Shooter.Contracts.Float3? spawn2, bool reducedMotion)
        {
            float extent = 1f;
            for (int i = 0; i < boxes.Count; i++)
                extent = Mathf.Max(extent, Mathf.Abs(boxes[i].Center.X) + boxes[i].Size.X * .5f,
                    Mathf.Abs(boxes[i].Center.Z) + boxes[i].Size.Z * .5f);
            float scale = Mathf.Min(r.width, r.height) / (extent * 2.2f);
            GUI.BeginGroup(r);
            try
            {
                Color grid = new Color(.25f, .65f, .72f, .14f);
                for (int i = 0; i <= 10; i++)
                {
                    Fill(new Rect(i * r.width / 10, 0, 1, r.height), grid);
                    Fill(new Rect(0, i * r.height / 10, r.width, 1), grid);
                }
                for (int i = 0; i < boxes.Count; i++)
                {
                    var box = boxes[i]; if (!box.Gameplay || box.Size.Y < .3f) continue;
                    float x = r.width * .5f + box.Center.X * scale, y = r.height * .5f - box.Center.Z * scale;
                    var q = new Rect(x - box.Size.X * scale * .5f, y - box.Size.Z * scale * .5f, box.Size.X * scale, box.Size.Z * scale);
                    Fill(q, new Color(.15f, .48f, .56f, .32f)); Edge(q);
                }
                if (spawn1.HasValue) MapGlyph(r.width * .5f + spawn1.Value.X * scale, r.height * .5f - spawn1.Value.Z * scale, Generation.Icons.IconLibrary.AllyArrow, Ally);
                if (spawn2.HasValue) MapGlyph(r.width * .5f + spawn2.Value.X * scale, r.height * .5f - spawn2.Value.Z * scale, Generation.Icons.IconLibrary.EnemyMarker, Enemy);
                if (!reducedMotion) Fill(new Rect(0, Mathf.Repeat(Time.unscaledTime * 14f, r.height), r.width, 1), new Color(.5f, .9f, 1f, .35f));
            }
            finally { GUI.EndGroup(); }
        }

        /// <summary>
        /// Review-only UI gallery (<c>-shooterUiGallery 1</c>): medal tiers with looping stamp, kill ribbons,
        /// kill-feed icons at feed and large size, and control states. Draws nothing gameplay-related.
        /// </summary>
        public void DrawUiGallery()
        {
            Backdrop();
            Header("REVIEW / UI GALLERY", "Interface elements", "ADR-084");
            float t = Time.realtimeSinceStartup;
            float loop = t % 2.6f;
            float x0 = Safe.x, y = Safe.y + 84;
            Label(new Rect(x0, y, 400, 16), "MEDALS  TIER 1-4  (LOOPING STAMP)", 12, Accent);
            for (int i = 0; i < 4; i++)
            {
                float size = 56f * Hud.UiMotion.StampScale(loop, ReducedMotion);
                Rect c = new Rect(x0 + 40 + i * 150, y + 70, 0, 0);
                Medal(new Rect(c.x - size * .5f, c.y - size * .5f, size, size), i + 1, Hud.UiMotion.EnterAlpha(loop), Hud.UiMotion.GlintPosition(loop, ReducedFlash));
            }
            for (int i = 0; i < 4; i++)
                Medal(new Rect(x0 + 640 + i * 70 - 16, y + 54, 32, 32), i + 1, 1f);
            y += 140;
            Label(new Rect(x0, y, 400, 16), "KILL RIBBON", 12, Accent);
            ScoreRibbon(new Rect(x0, y + 24, 260, 40), 1f, loop, 1, ReducedMotion, ReducedFlash);
            ScoreRibbon(new Rect(x0 + 300, y + 24, 260, 40), 1f, loop, 3, ReducedMotion, ReducedFlash);
            ScoreRibbon(new Rect(x0 + 600, y + 24, 260, 40), 1f, 10f, 4, ReducedMotion, ReducedFlash);
            y += 90;
            Label(new Rect(x0, y, 400, 16), "KILL FEED ICONS  (FEED SIZE / LARGE)", 12, Accent);
            var weapons = new[] { KillFeedWeapon.Ak47, KillFeedWeapon.Ak102, KillFeedWeapon.M16, KillFeedWeapon.Mk14, KillFeedWeapon.Mtac338,
                KillFeedWeapon.G17, KillFeedWeapon.GoldenEagle, KillFeedWeapon.Knife, KillFeedWeapon.Grenade };
            for (int i = 0; i < weapons.Length; i++)
            {
                CombatPlate(new Rect(x0 + i * 110, y + 22, 100, 26), strong: true);
                KillFeedWeaponIcon(new Rect(x0 + i * 110 + 30, y + 28, 40, 14), weapons[i], Text);
                KillFeedWeaponIcon(new Rect(x0 + i * 110 + 4, y + 60, 92, 32), weapons[i], Text);
            }
            CombatPlate(new Rect(x0 + 550, y + 22, 40, 26), strong: true);
            KillFeedHeadshotIcon(new Rect(x0 + 563, y + 28, 14, 14), Accent);
            KillFeedHeadshotIcon(new Rect(x0 + 550, y + 60, 32, 32), Accent);
            CombatPlate(new Rect(x0 + 610, y + 22, 300, 82), strong: true);
            Label(new Rect(x0 + 620, y + 26, 290, 14), UiIconAtlas.HasWeapons ? "MESH-TRACED (ADR-085)" : "MESH ICONS PENDING", 10, Muted);
            if (UiIconAtlas.HasWeapons)
            {
                Icon(new Rect(x0 + 618, y + 42, 120, 40), "weapon.rifle", Text, shadow: true);
                Icon(new Rect(x0 + 744, y + 42, 70, 40), "weapon.pistol", Text, shadow: true);
                Icon(new Rect(x0 + 818, y + 42, 84, 40), "weapon.knife", Text, shadow: true);
            }
            y += 112;
            Label(new Rect(x0, y, 600, 16), "ICON LIBRARY (ADR-085)", 12, Accent);
            var names = Generation.Icons.IconLibrary.All;
            for (int i = 0; i < names.Count; i++)
            {
                Rect cell = new Rect(x0 + i * 46, y + 22, 40, 40);
                CombatPlate(cell, strong: true);
                Color ink = names[i].Name == Generation.Icons.IconLibrary.EmblemAlly ? Ally
                    : names[i].Name == Generation.Icons.IconLibrary.EmblemEnemy || names[i].Name == Generation.Icons.IconLibrary.EnemyMarker ? Enemy
                    : names[i].Name.StartsWith("rank", StringComparison.Ordinal) || names[i].Name == Generation.Icons.IconLibrary.Crown
                        || names[i].Name == Generation.Icons.IconLibrary.MedalStar ? Gold : Text;
                Icon(new Rect(cell.x + 6, cell.y + 6, 28, 28), names[i].Name, ink);
            }
            y += 80;
            Label(new Rect(x0, y, 400, 16), "CONTROLS AND PLATES", 12, Accent);
            Button(new Rect(x0, y + 22, 180, 40), "gallery.a", "DEFAULT");
            Button(new Rect(x0 + 190, y + 22, 180, 40), "gallery.b", "SELECTED", selected: true);
            Button(new Rect(x0 + 380, y + 22, 180, 40), "gallery.c", "PRIMARY", primary: true);
            Toggle(new Rect(x0 + 570, y + 22, 260, 40), "gallery.t", "SWITCH", (int)(t / 1.5f) % 2 == 0);
            Slider(new Rect(x0, y + 74, 360, 24), "gallery.s", "Slider", Mathf.PingPong(t * .3f, 1f), 0f, 1f, "");
            CombatPlate(new Rect(x0 + 400, y + 74, 200, 52), accentLeft: true, accentBottom: true);
            Label(new Rect(x0 + 412, y + 86, 180, 30), "30 / 090", 24);
            float cx = x0 + 680, cy = y + 100;
            ReticleArm(new Rect(cx - 9.5f, cy - .75f, 6, 1.5f));
            ReticleArm(new Rect(cx + 3.5f, cy - .75f, 6, 1.5f));
            ReticleArm(new Rect(cx - .75f, cy - 9.5f, 1.5f, 6));
            ReticleArm(new Rect(cx - .75f, cy + 3.5f, 1.5f, 6));
            HitMarker(cx + 70, cy, 1f, CombatHudVisual.HitScale(t % 1.5f, true, ReducedMotion), true);
            DamageArc(cx + 70, cy, -.6f, ReducedFlash ? .4f : .8f);
            AmmoRounds(new Rect(x0 + 410, y + 114, 180, 8), 24, Text);
            UiSdf.Arc(new Rect(x0 + 602, y + 82, 24, 24), 0, Mathf.PI * 2 * (ReducedMotion ? .65f : t % 2f / 2f), 2,
                UiSdf.Style.Solid(Accent));
            MapGlyph(cx + 122, cy, Generation.Icons.IconLibrary.AllyArrow, Ally, ReducedMotion ? .7f : t * .4f);
            MapGlyph(cx + 148, cy, Generation.Icons.IconLibrary.EnemyMarker, Enemy);
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
