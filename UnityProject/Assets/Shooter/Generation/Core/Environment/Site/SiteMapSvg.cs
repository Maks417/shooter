#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// Top-down review render of a site-planned map (ADR-109): zones, buildings, gameplay boxes by role, upper floors,
    /// spawn slots, exits, lane points and any spawn-exit exposure rays. Review aid only; nothing ships it.
    /// </summary>
    public static class SiteMapSvg
    {
        private const float Scale = 10f;
        private const float Margin = 3f;

        public static string Render(CompoundPlan plan, string title, IReadOnlyList<SpawnCandidate>? slots, SiteRulesReport? report, ArenaRulesReport? arenaReport)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var arena = plan.Arena ?? throw new ArgumentException("Arena plan required.", nameof(plan));
            var site = plan.Site;
            var inv = CultureInfo.InvariantCulture;
            float hx = arena.HalfExtentX, hz = arena.HalfExtentZ;
            float mapW = (hx * 2f + Margin * 2f) * Scale, mapH = (hz * 2f + Margin * 2f) * Scale;
            float legendW = 360f;
            string N(float v) => v.ToString("0.#", inv);
            float X(float x) => (x + hx + Margin) * Scale;
            float Y(float z) => (hz - z + Margin) * Scale;

            var sb = new StringBuilder();
            sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(N(mapW + legendW)).Append("\" height=\"").Append(N(mapH))
              .Append("\" viewBox=\"0 0 ").Append(N(mapW + legendW)).Append(' ').Append(N(mapH)).Append("\" font-family=\"Segoe UI, Arial, sans-serif\">\n");
            sb.Append("<defs><pattern id=\"floor\" width=\"6\" height=\"6\" patternUnits=\"userSpaceOnUse\" patternTransform=\"rotate(45)\">")
              .Append("<line x1=\"0\" y1=\"0\" x2=\"0\" y2=\"6\" stroke=\"#3a6fb0\" stroke-width=\"2\"/></pattern></defs>\n");
            sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#f4f1ea\"/>\n");
            sb.Append(Rect(X(-hx), Y(hz), hx * 2f * Scale, hz * 2f * Scale, "#e2dccd", "#555", 1f));

            // Grid every 8 m.
            for (float gx = -hx; gx <= hx + 0.01f; gx += 8f)
                sb.Append("<line x1=\"").Append(N(X(gx))).Append("\" y1=\"").Append(N(Y(hz))).Append("\" x2=\"").Append(N(X(gx))).Append("\" y2=\"").Append(N(Y(-hz))).Append("\" stroke=\"#d3ccbb\" stroke-width=\"0.6\"/>\n");
            for (float gz = -hz; gz <= hz + 0.01f; gz += 8f)
                sb.Append("<line x1=\"").Append(N(X(-hx))).Append("\" y1=\"").Append(N(Y(gz))).Append("\" x2=\"").Append(N(X(hx))).Append("\" y2=\"").Append(N(Y(gz))).Append("\" stroke=\"#d3ccbb\" stroke-width=\"0.6\"/>\n");

            if (site != null)
            {
                for (int i = 0; i < site.Zones.Count; i++)
                {
                    var z = site.Zones[i];
                    string fill = z.Kind switch
                    {
                        SiteZoneKind.Plaza => "#efe2b8", SiteZoneKind.Quay => "#cfd8dc", SiteZoneKind.Staging => "#d6e6f5",
                        SiteZoneKind.Yard => "#e6e0cf", SiteZoneKind.Street => "#d9d5cc", _ => "#ddd8cb"
                    };
                    sb.Append("<rect x=\"").Append(N(X(z.Area.MinX))).Append("\" y=\"").Append(N(Y(z.Area.MaxZ))).Append("\" width=\"").Append(N(z.Area.SizeX * Scale))
                      .Append("\" height=\"").Append(N(z.Area.SizeZ * Scale)).Append("\" fill=\"").Append(fill).Append("\" stroke=\"#9a917c\" stroke-dasharray=\"4 3\" stroke-width=\"0.8\"/>\n");
                }
                for (int i = 0; i < site.Buildings.Count; i++)
                {
                    var bd = site.Buildings[i];
                    string fill = bd.Storeys > 1 ? "#cdd9ea" : "#ece6d8";
                    sb.Append(Rect(X(bd.Footprint.MinX), Y(bd.Footprint.MaxZ), bd.Footprint.SizeX * Scale, bd.Footprint.SizeZ * Scale, fill, "none", 0f));
                }
            }

            // Gameplay boxes, low first so walls draw over props.
            var order = new List<CollisionBox>();
            for (int i = 0; i < plan.CollisionBoxes.Count; i++) if (plan.CollisionBoxes[i].Gameplay) order.Add(plan.CollisionBoxes[i]);
            order.Sort((a, b) => (a.Center.Y + a.Size.Y * 0.5f).CompareTo(b.Center.Y + b.Size.Y * 0.5f));
            for (int i = 0; i < order.Count; i++)
            {
                var b = order[i];
                SiteRole role = 0;
                if (site != null && site.TryGetBox(b.StableId, out var info)) role = info.Role;
                float top = b.Center.Y + b.Size.Y * 0.5f, bottom = b.Center.Y - b.Size.Y * 0.5f;
                string fill, stroke = "none";
                switch (role)
                {
                    case SiteRole.Boundary: fill = "#3d3d3d"; break;
                    case SiteRole.Wall: fill = bottom > 0.6f ? "#9e9e9e" : (top < 2f ? "#c28d4a" : "#4a4a4a"); break;
                    case SiteRole.Floor: fill = "url(#floor)"; stroke = "#3a6fb0"; break;
                    case SiteRole.Stair: fill = "#7fa7d6"; stroke = "#3a6fb0"; break;
                    case SiteRole.Attached: fill = top < 2f ? "#e0a458" : "#8d6e4f"; break;
                    case SiteRole.Landmark: fill = "#8e6bb0"; break;
                    default: fill = "#ff00ff"; stroke = "#ff00ff"; break;
                }
                if (role == SiteRole.Attached && bottom > 0.6f) fill = "#5d8fcf";
                sb.Append(Rect(X(b.Center.X - b.Size.X * 0.5f), Y(b.Center.Z + b.Size.Z * 0.5f), b.Size.X * Scale, b.Size.Z * Scale, fill, stroke, stroke == "none" ? 0f : 0.8f));
            }

            if (site != null)
            {
                for (int i = 0; i < site.Buildings.Count; i++)
                {
                    var bd = site.Buildings[i];
                    sb.Append(Rect(X(bd.Footprint.MinX), Y(bd.Footprint.MaxZ), bd.Footprint.SizeX * Scale, bd.Footprint.SizeZ * Scale, "none", "#333", 1.2f));
                    if (bd.Team == 2) continue;
                    sb.Append(Label(X(bd.Footprint.CenterX), Y(bd.Footprint.CenterZ), bd.Name + (bd.Storeys > 1 ? " (2F)" : ""), "#222", 10));
                }
                for (int i = 0; i < site.Zones.Count; i++)
                {
                    var z = site.Zones[i];
                    if (z.Team == 2) continue;
                    sb.Append(Label(X(z.Area.MinX) + 3f, Y(z.Area.MaxZ) + 10f, z.Name, "#6b604a", 9, "start", true));
                }
                for (int i = 0; i < site.Landmarks.Count; i++)
                {
                    var l = site.Landmarks[i];
                    if (l.Team == 2) continue;
                    sb.Append(Label(X(l.Footprint.CenterX), Y(l.Footprint.MinZ) + 11f, l.Name, "#5b3f80", 9));
                }
            }

            // Staging rectangles.
            for (int team = 1; team <= 2; team++)
            {
                var r = arena.StagingRect(team);
                sb.Append("<rect x=\"").Append(N(X(r.minX))).Append("\" y=\"").Append(N(Y(r.maxZ))).Append("\" width=\"").Append(N((r.maxX - r.minX) * Scale))
                  .Append("\" height=\"").Append(N((r.maxZ - r.minZ) * Scale)).Append("\" fill=\"none\" stroke=\"").Append(team == 1 ? "#1f5fbf" : "#c0392b")
                  .Append("\" stroke-width=\"2\" stroke-dasharray=\"8 4\"/>\n");
            }

            if (report != null)
                for (int i = 0; i < report.Exposures.Count; i++)
                {
                    var e = report.Exposures[i];
                    sb.Append("<line x1=\"").Append(N(X(e.From.X))).Append("\" y1=\"").Append(N(Y(e.From.Z))).Append("\" x2=\"").Append(N(X(e.To.X))).Append("\" y2=\"").Append(N(Y(e.To.Z)))
                      .Append("\" stroke=\"#e53935\" stroke-width=\"1\" opacity=\"0.55\"/>\n");
                }

            if (slots != null)
                for (int i = 0; i < slots.Count; i++)
                {
                    var s = slots[i];
                    sb.Append("<circle cx=\"").Append(N(X(s.Position.X))).Append("\" cy=\"").Append(N(Y(s.Position.Z))).Append("\" r=\"4\" fill=\"")
                      .Append(s.Team == 1 ? "#1f5fbf" : "#c0392b").Append("\"/>\n");
                }
            for (int team = 1; team <= 2; team++)
            {
                var exits = arena.Exits(team);
                for (int i = 0; i < exits.Count; i++)
                    sb.Append("<rect x=\"").Append(N(X(exits[i].X) - 5f)).Append("\" y=\"").Append(N(Y(exits[i].Z) - 5f)).Append("\" width=\"10\" height=\"10\" fill=\"#2e7d32\" transform=\"rotate(45 ")
                      .Append(N(X(exits[i].X))).Append(' ').Append(N(Y(exits[i].Z))).Append(")\"/>\n");
            }
            for (int i = 0; i < arena.LanePoints.Count; i++)
            {
                var p = arena.LanePoints[i];
                sb.Append("<circle cx=\"").Append(N(X(p.X))).Append("\" cy=\"").Append(N(Y(p.Z))).Append("\" r=\"6\" fill=\"none\" stroke=\"#2e7d32\" stroke-width=\"2\"/>\n");
            }
            for (int i = 0; i < plan.RouteCorners.Count - 1; i++)
            {
                var a = plan.RouteCorners[i]; var c = plan.RouteCorners[i + 1];
                sb.Append("<line x1=\"").Append(N(X(a.X))).Append("\" y1=\"").Append(N(Y(a.Z))).Append("\" x2=\"").Append(N(X(c.X))).Append("\" y2=\"").Append(N(Y(c.Z)))
                  .Append("\" stroke=\"#2e7d32\" stroke-width=\"1\" stroke-dasharray=\"3 3\" opacity=\"0.6\"/>\n");
            }

            // Legend and rule summary.
            float lx = mapW + 10f, ly = 24f;
            sb.Append(Label(lx, ly, title, "#111", 16, "start")); ly += 22f;
            sb.Append(Label(lx, ly, "1 grid square = 8 m; team 1 (blue) bottom, team 2 (red) top", "#444", 10, "start")); ly += 20f;
            (string fill, string text)[] keys =
            {
                ("#4a4a4a", "building wall (full height)"), ("#c28d4a", "window sill (half cover)"), ("#9e9e9e", "lintel / upper wall (above head)"),
                ("url(#floor)", "walkable upper floor"), ("#7fa7d6", "stair"), ("#8d6e4f", "attached prop, tall"), ("#e0a458", "attached prop, half cover"),
                ("#5d8fcf", "upper-floor rail"), ("#8e6bb0", "landmark"), ("#3d3d3d", "boundary"), ("#ff00ff", "UNTAGGED (rule failure)")
            };
            for (int i = 0; i < keys.Length; i++)
            {
                sb.Append(Rect(lx, ly - 9f, 14f, 10f, keys[i].fill, "#333", 0.5f));
                sb.Append(Label(lx + 20f, ly, keys[i].text, "#333", 10, "start"));
                ly += 15f;
            }
            ly += 8f;
            void RuleLines(IReadOnlyList<ArenaRuleResult> rules)
            {
                for (int i = 0; i < rules.Count; i++)
                {
                    var r = rules[i];
                    sb.Append(Label(lx, ly, (r.Passed ? "PASS " : "FAIL ") + r.Id, r.Passed ? "#2e7d32" : "#c62828", 10, "start")); ly += 12f;
                    foreach (var chunk in Wrap(r.Measured, 58)) { sb.Append(Label(lx + 10f, ly, chunk, "#555", 9, "start")); ly += 11f; }
                }
            }
            if (report != null) { sb.Append(Label(lx, ly, "Site rules (ADR-109)", "#111", 12, "start")); ly += 15f; RuleLines(report.Rules); ly += 6f; }
            if (arenaReport != null) { sb.Append(Label(lx, ly, "Arena rules (ADR-074)", "#111", 12, "start")); ly += 15f; RuleLines(arenaReport.Rules); }
            sb.Append("</svg>\n");
            return sb.ToString();

            string Rect(float x, float y, float w, float h, string fill, string stroke, float sw)
                => "<rect x=\"" + N(x) + "\" y=\"" + N(y) + "\" width=\"" + N(w) + "\" height=\"" + N(h) + "\" fill=\"" + fill + "\"" +
                   (stroke == "none" ? "" : " stroke=\"" + stroke + "\" stroke-width=\"" + N(sw) + "\"") + "/>\n";
            string Label(float x, float y, string text, string color, int size, string anchor = "middle", bool italic = false)
                => "<text x=\"" + N(x) + "\" y=\"" + N(y) + "\" font-size=\"" + size + "\" fill=\"" + color + "\" text-anchor=\"" + anchor + "\"" +
                   (italic ? " font-style=\"italic\"" : "") + ">" + Escape(text) + "</text>\n";
        }

        private static IEnumerable<string> Wrap(string s, int width)
        {
            for (int i = 0; i < s.Length; i += width) yield return s.Substring(i, Math.Min(width, s.Length - i));
        }

        private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
