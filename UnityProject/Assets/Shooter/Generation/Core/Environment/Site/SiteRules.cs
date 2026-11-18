#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>Visibility probe found by <see cref="SiteRules"/>: an apron point that sees into the enemy half.</summary>
    public readonly struct SiteExposure
    {
        public SiteExposure(Float3 from, Float3 to) { From = from; To = to; }
        public Float3 From { get; }
        public Float3 To { get; }
    }

    public sealed class SiteRulesReport
    {
        public SiteRulesReport(IReadOnlyList<ArenaRuleResult> rules, IReadOnlyList<SiteExposure> exposures, float builtFraction,
            float largestVoid, float longestLocalSightline, float firstContactMeters)
        {
            Rules = rules; Exposures = exposures; BuiltFraction = builtFraction; LargestVoidRadius = largestVoid;
            LongestLocalSightline = longestLocalSightline; FirstContactMeters = firstContactMeters;
            bool all = true;
            var parts = new List<string>();
            for (int i = 0; i < rules.Count; i++)
                if (!rules[i].Passed) { all = false; parts.Add(rules[i].Id + " measured=" + rules[i].Measured + " required=" + rules[i].Threshold); }
            Passed = all;
            Diagnostic = all ? "ok" : string.Join("; ", parts);
        }

        public bool Passed { get; }
        public string Diagnostic { get; }
        public IReadOnlyList<ArenaRuleResult> Rules { get; }
        public IReadOnlyList<SiteExposure> Exposures { get; }
        public float BuiltFraction { get; }
        public float LargestVoidRadius { get; }
        public float LongestLocalSightline { get; }
        public float FirstContactMeters { get; }

        public ArenaRuleResult? Rule(string id)
        {
            for (int i = 0; i < Rules.Count; i++) if (Rules[i].Id == id) return Rules[i];
            return null;
        }

        public string ToText()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Rules.Count; i++) sb.AppendLine(Rules[i].ToString());
            return sb.ToString();
        }
    }

    /// <summary>
    /// ADR-109 place-like layout rules on top of <see cref="ArenaRules"/>: every box belongs to something, buildings
    /// shape the space (no big voids, no field), spawn exits do not look into the enemy half, first contact is not at
    /// the door, and upper floors are reachable with two ways down. Geometry checks only; not a fun guarantee.
    /// </summary>
    public static class SiteRules
    {
        public const string RuleRoles = "site-roles";
        public const string RuleLandmarks = "site-landmarks";
        public const string RuleBuilt = "built-coverage";
        public const string RuleVoids = "open-voids";
        public const string RuleExitExposure = "spawn-exit-exposure";
        public const string RuleLocalSightline = "local-sightline";
        public const string RuleFirstContact = "first-contact";
        public const string RuleUpperFloors = "upper-floors";

        public const float AttachToleranceMeters = 0.5f;
        public const int MaxLandmarksPerTeam = 5;
        public const float MinBuiltFraction = 0.25f, MaxBuiltFraction = 0.60f;
        public const float MaxVoidRadius = 9f, MaxDeclaredVoidRadius = 13f;
        public const float ApronRadiusMeters = 10f;
        /// <summary>Team-1 apron must not see team-2 ground beyond this z (and never the enemy staging).</summary>
        public const float EnemyHalfMarginMeters = 8f;
        public const float MaxLocalSightlineMeters = 40f;
        public const float MinFirstContactMeters = 18f;

        public static SiteRulesReport Validate(CompoundPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var rules = new List<ArenaRuleResult>();
            var exposures = new List<SiteExposure>();
            var site = plan.Site;
            var arena = plan.Arena;
            if (site == null || arena == null)
            {
                rules.Add(new ArenaRuleResult("site-present", false, "no SiteLayout", "plan.Site != null"));
                return new SiteRulesReport(rules, exposures, 0, 0, 0, 0);
            }
            var inv = CultureInfo.InvariantCulture;
            string F(float v) => v.ToString("0.##", inv);
            var boxes = plan.CollisionBoxes;

            // 1. Every gameplay box has a role; attached props touch their host.
            int untagged = 0, detached = 0;
            string firstBad = "";
            for (int i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                if (!box.Gameplay) continue;
                if (!site.TryGetBox(box.StableId, out var info)) { untagged++; if (firstBad.Length == 0) firstBad = "untagged #" + box.StableId; continue; }
                if (info.Role != SiteRole.Attached) continue;
                if (!AttachedOk(info, box, arena))
                {
                    detached++;
                    if (firstBad.Length == 0) firstBad = info.Host + " prop at (" + F(box.Center.X) + "," + F(box.Center.Z) + ")";
                }
            }
            rules.Add(new ArenaRuleResult(RuleRoles, untagged == 0 && detached == 0,
                "untagged=" + untagged + " detached=" + detached + (firstBad.Length > 0 ? " first=" + firstBad : ""), "0 and 0"));

            int lm1 = 0, lm0 = 0;
            for (int i = 0; i < site.Landmarks.Count; i++) { if (site.Landmarks[i].Team == 1) lm1++; else if (site.Landmarks[i].Team == 0) lm0++; }
            rules.Add(new ArenaRuleResult(RuleLandmarks, lm1 + lm0 <= MaxLandmarksPerTeam, "team1=" + lm1 + " shared=" + lm0, "<= " + MaxLandmarksPerTeam));

            // Grids.
            var grid = new SiteGrid(arena, boxes);

            // 2. Built coverage: building and landmark footprints plus tall props, over the playable area.
            float built = BuiltFraction(site, boxes, arena);
            rules.Add(new ArenaRuleResult(RuleBuilt, built >= MinBuiltFraction && built <= MaxBuiltFraction, F(built * 100f) + "%",
                F(MinBuiltFraction * 100f) + "-" + F(MaxBuiltFraction * 100f) + "%"));

            // 3. Voids: open ground far from any building, landmark, boundary or tall prop.
            float voidR = LargestVoid(site, boxes, arena, grid, out float vx, out float vz, out bool vDeclared);
            float declaredMax = LargestDeclaredVoid(site, boxes, arena, grid);
            rules.Add(new ArenaRuleResult(RuleVoids, voidR <= MaxVoidRadius && declaredMax <= MaxDeclaredVoidRadius,
                "street/yard=" + F(voidR) + " at (" + F(vx) + "," + F(vz) + ") plaza/quay=" + F(declaredMax),
                "<= " + F(MaxVoidRadius) + " (plaza/quay <= " + F(MaxDeclaredVoidRadius) + ")"));
            _ = vDeclared;

            // 4. Spawn-exit exposure (team 1; team 2 is its symmetry image).
            var apron1 = Apron(arena, grid, 1);
            var targets = EnemyTargets(arena, grid);
            int exposed = 0;
            for (int i = 0; i < apron1.Count; i++)
            {
                for (int j = 0; j < targets.Count; j++)
                {
                    if (!grid.EyeLos(apron1[i], targets[j])) continue;
                    exposed++;
                    if (exposures.Count < 64) exposures.Add(new SiteExposure(apron1[i], targets[j]));
                    break;
                }
            }
            rules.Add(new ArenaRuleResult(RuleExitExposure, exposed == 0,
                "exposedApronPoints=" + exposed + "/" + apron1.Count + (exposures.Count > 0 ? " first=(" + F(exposures[0].From.X) + "," + F(exposures[0].From.Z) + ")->(" + F(exposures[0].To.X) + "," + F(exposures[0].To.Z) + ")" : ""),
                "0 apron points within " + F(ApronRadiusMeters) + " m of an exit see z >= " + F(EnemyHalfMarginMeters) + " or enemy staging"));

            // 5. Local sightline (outside declared plaza/quay).
            float local = LongestLocalSightline(site, arena, grid, out float lx, out float lz, out float ldeg);
            rules.Add(new ArenaRuleResult(RuleLocalSightline, local <= MaxLocalSightlineMeters,
                F(local) + " from (" + F(lx) + "," + F(lz) + ") dir " + F(ldeg) + "deg", "<= " + F(MaxLocalSightlineMeters) + " outside plaza/quay"));

            // 6. First contact: walk distance from a team-1 exit to the nearest ground that sees a team-2 exit apron.
            float contact = FirstContact(arena, grid, out float cx, out float cz);
            rules.Add(new ArenaRuleResult(RuleFirstContact, contact >= MinFirstContactMeters,
                F(contact) + " at (" + F(cx) + "," + F(cz) + ")", ">= " + F(MinFirstContactMeters)));

            // 7. Upper floors: reachable from the ground and served by >= 2 stairs.
            string floorIssue = UpperFloors(site, boxes, arena);
            rules.Add(new ArenaRuleResult(RuleUpperFloors, floorIssue.Length == 0, floorIssue.Length == 0 ? "ok" : floorIssue,
                "every upper floor reachable, >= 2 stairs"));

            return new SiteRulesReport(rules, exposures, built, Math.Max(voidR, 0f), local, contact);
        }

        // ------------------------------------------------------------------ rule helpers

        private static bool AttachedOk(SiteBoxInfo info, CollisionBox box, ArenaLayout arena)
        {
            float cx = box.Center.X, cz = box.Center.Z, sx = box.Size.X, sz = box.Size.Z;
            switch (info.HostKind)
            {
                case SiteHostKind.Building:
                case SiteHostKind.Landmark:
                    return info.HostArea.GapTo(cx, cz, sx, sz) <= AttachToleranceMeters;
                case SiteHostKind.Zone:
                    return info.HostArea.Contains(cx - sx * 0.5f, cz - sz * 0.5f, 0.05f) && info.HostArea.Contains(cx + sx * 0.5f, cz + sz * 0.5f, 0.05f);
                case SiteHostKind.Boundary:
                    float gx = arena.HalfExtentX - (Math.Abs(cx) + sx * 0.5f);
                    float gz = arena.HalfExtentZ - (Math.Abs(cz) + sz * 0.5f);
                    return Math.Min(gx, gz) <= AttachToleranceMeters;
                default:
                    return false;
            }
        }

        private static bool IsTallSolid(CollisionBox b, ArenaLayout arena)
            => b.Gameplay && b.Center.Y - b.Size.Y * 0.5f < 0.1f && b.Center.Y + b.Size.Y * 0.5f >= 2f && !ArenaRules.IsPerimeterBox(b, arena);

        private static float BuiltFraction(SiteLayout site, IReadOnlyList<CollisionBox> boxes, ArenaLayout arena)
        {
            const float cell = 0.5f;
            float hx = arena.HalfExtentX, hz = arena.HalfExtentZ;
            int built = 0, total = 0;
            for (float z = -hz + cell * 0.5f; z < hz; z += cell)
            for (float x = -hx + cell * 0.5f; x < hx; x += cell)
            {
                total++;
                if (InStructure(site, boxes, arena, x, z)) built++;
            }
            return total == 0 ? 0f : built / (float)total;
        }

        private static bool InStructure(SiteLayout site, IReadOnlyList<CollisionBox> boxes, ArenaLayout arena, float x, float z)
        {
            for (int i = 0; i < site.Buildings.Count; i++) if (site.Buildings[i].Footprint.Contains(x, z)) return true;
            for (int i = 0; i < site.Landmarks.Count; i++) if (site.Landmarks[i].Footprint.Contains(x, z)) return true;
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (!IsTallSolid(b, arena)) continue;
                if (Math.Abs(x - b.Center.X) <= b.Size.X * 0.5f && Math.Abs(z - b.Center.Z) <= b.Size.Z * 0.5f) return true;
            }
            return false;
        }

        /// <summary>Rectangles that shape space: buildings, landmarks, tall props (boundary handled by the edge distance).</summary>
        private static List<SiteRect> Structures(SiteLayout site, IReadOnlyList<CollisionBox> boxes, ArenaLayout arena)
        {
            var list = new List<SiteRect>();
            for (int i = 0; i < site.Buildings.Count; i++) list.Add(site.Buildings[i].Footprint);
            for (int i = 0; i < site.Landmarks.Count; i++) list.Add(site.Landmarks[i].Footprint);
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (!IsTallSolid(b, arena)) continue;
                list.Add(new SiteRect(b.Center.X - b.Size.X * 0.5f, b.Center.Z - b.Size.Z * 0.5f, b.Center.X + b.Size.X * 0.5f, b.Center.Z + b.Size.Z * 0.5f));
            }
            return list;
        }

        private static float NearestStructure(List<SiteRect> rects, ArenaLayout arena, float x, float z)
        {
            float best = Math.Min(arena.HalfExtentX - Math.Abs(x), arena.HalfExtentZ - Math.Abs(z));
            for (int i = 0; i < rects.Count; i++)
            {
                var r = rects[i];
                float dx = Math.Max(0f, Math.Max(r.MinX - x, x - r.MaxX));
                float dz = Math.Max(0f, Math.Max(r.MinZ - z, z - r.MaxZ));
                float d = MathF.Sqrt(dx * dx + dz * dz);
                if (d < best) best = d;
            }
            return best;
        }

        private static bool InDeclaredOpen(SiteLayout site, float x, float z)
        {
            for (int i = 0; i < site.Zones.Count; i++)
                if (site.Zones[i].DeclaredOpen && site.Zones[i].Area.Contains(x, z)) return true;
            return false;
        }

        private static float LargestVoid(SiteLayout site, IReadOnlyList<CollisionBox> boxes, ArenaLayout arena, SiteGrid grid, out float vx, out float vz, out bool declared)
        {
            var rects = Structures(site, boxes, arena);
            float worst = 0f; vx = 0f; vz = 0f; declared = false;
            for (float z = -arena.HalfExtentZ + 0.5f; z < arena.HalfExtentZ; z += 1f)
            for (float x = -arena.HalfExtentX + 0.5f; x < arena.HalfExtentX; x += 1f)
            {
                if (InDeclaredOpen(site, x, z) || InStructure(site, boxes, arena, x, z)) continue;
                float d = NearestStructure(rects, arena, x, z);
                if (d > worst) { worst = d; vx = x; vz = z; }
            }
            _ = grid;
            return worst;
        }

        private static float LargestDeclaredVoid(SiteLayout site, IReadOnlyList<CollisionBox> boxes, ArenaLayout arena, SiteGrid grid)
        {
            var rects = Structures(site, boxes, arena);
            float worst = 0f;
            for (float z = -arena.HalfExtentZ + 0.5f; z < arena.HalfExtentZ; z += 1f)
            for (float x = -arena.HalfExtentX + 0.5f; x < arena.HalfExtentX; x += 1f)
            {
                if (!InDeclaredOpen(site, x, z) || InStructure(site, boxes, arena, x, z)) continue;
                worst = Math.Max(worst, NearestStructure(rects, arena, x, z));
            }
            _ = grid;
            return worst;
        }

        /// <summary>Open ground points (1 m grid) within the apron radius of a team's exits, outside its staging.</summary>
        public static List<Float3> Apron(ArenaLayout arena, SiteGrid grid, int team)
        {
            var list = new List<Float3>();
            var exits = arena.Exits(team);
            for (float z = -arena.HalfExtentZ + 0.5f; z < arena.HalfExtentZ; z += 1f)
            for (float x = -arena.HalfExtentX + 0.5f; x < arena.HalfExtentX; x += 1f)
            {
                if (arena.StagingContains(team, x, z) || grid.Blocked(x, z)) continue;
                bool near = false;
                for (int i = 0; i < exits.Count && !near; i++)
                {
                    float dx = x - exits[i].X, dz = z - exits[i].Z;
                    near = dx * dx + dz * dz <= ApronRadiusMeters * ApronRadiusMeters;
                }
                if (!near) continue;
                // Only apron ground reachable from the exits without walking far (not the far side of a wall).
                if (grid.WalkDistanceFromExits(team, x, z) > ApronRadiusMeters * 1.5f) continue;
                list.Add(new Float3(x, 0f, z));
            }
            return list;
        }

        /// <summary>Team-2 ground (2 m grid) beyond the margin, plus the team-2 staging rectangle.</summary>
        private static List<Float3> EnemyTargets(ArenaLayout arena, SiteGrid grid)
        {
            var list = new List<Float3>();
            for (float z = EnemyHalfMarginMeters + 1f; z < arena.HalfExtentZ; z += 2f)
            for (float x = -arena.HalfExtentX + 1f; x < arena.HalfExtentX; x += 2f)
                if (!grid.Blocked(x, z)) list.Add(new Float3(x, 0f, z));
            return list;
        }

        private static float LongestLocalSightline(SiteLayout site, ArenaLayout arena, SiteGrid grid, out float ox, out float oz, out float deg)
        {
            ox = 0f; oz = 0f; deg = 0f;
            const int dirs = 16;
            float longest = 0f;
            for (float sz = -arena.HalfExtentZ + 2f; sz < arena.HalfExtentZ; sz += 4f)
            for (float sx = -arena.HalfExtentX + 2f; sx < arena.HalfExtentX; sx += 4f)
            {
                if (grid.Blocked(sx, sz) || InDeclaredOpen(site, sx, sz)) continue;
                for (int d = 0; d < dirs; d++)
                {
                    double a = d * (Math.PI * 2.0 / dirs);
                    float t = grid.RayLength(sx, sz, (float)Math.Cos(a), (float)Math.Sin(a));
                    if (t > longest) { longest = t; ox = sx; oz = sz; deg = d * 360f / dirs; }
                }
            }
            return longest;
        }

        private static float FirstContact(ArenaLayout arena, SiteGrid grid, out float cx, out float cz)
        {
            cx = 0f; cz = 0f;
            var apron2 = Apron(arena, grid, 2);
            // Thin the enemy apron to a 2 m lattice to bound the ray count.
            var thin = new List<Float3>();
            for (int i = 0; i < apron2.Count; i++)
                if (((int)Math.Floor(apron2[i].X) & 1) == 0 && ((int)Math.Floor(apron2[i].Z) & 1) == 0) thin.Add(apron2[i]);
            float best = float.PositiveInfinity;
            for (float z = -arena.HalfExtentZ + 0.5f; z < arena.HalfExtentZ; z += 1f)
            for (float x = -arena.HalfExtentX + 0.5f; x < arena.HalfExtentX; x += 1f)
            {
                if (grid.Blocked(x, z) || arena.StagingContains(1, x, z)) continue;
                float walk = grid.WalkDistanceFromExits(1, x, z);
                if (walk >= best) continue;
                var p = new Float3(x, 0f, z);
                for (int i = 0; i < thin.Count; i++)
                {
                    if (!grid.EyeLos(p, thin[i])) continue;
                    best = walk; cx = x; cz = z;
                    break;
                }
            }
            return best;
        }

        private static string UpperFloors(SiteLayout site, IReadOnlyList<CollisionBox> boxes, ArenaLayout arena)
        {
            var scratch = new List<Float3>();
            for (int i = 0; i < site.Buildings.Count; i++)
            {
                var bld = site.Buildings[i];
                if (bld.Storeys < 2 || bld.Team == 2) continue;
                int stairTops = 0;
                CollisionBox? slab = null;
                for (int k = 0; k < boxes.Count; k++)
                {
                    if (!site.TryGetBox(boxes[k].StableId, out var info) || info.Host != bld.Name) continue;
                    float top = boxes[k].Center.Y + boxes[k].Size.Y * 0.5f;
                    if (info.Role == SiteRole.Stair && Math.Abs(top - bld.UpperFloorY) < 0.01f) stairTops++;
                    if (info.Role == SiteRole.Floor && slab == null) slab = boxes[k];
                }
                if (slab == null) return bld.Name + ": no floor slab";
                if (stairTops < 2) return bld.Name + ": stairs=" + stairTops;
                var s = slab.Value;
                var goal = new Float3(s.Center.X, bld.UpperFloorY, s.Center.Z);
                if (!CompoundConnectivity.TryFindPath(boxes, arena.Team1Anchor, goal, scratch, CompoundConnectivity.RequiredClearance))
                    return bld.Name + ": floor unreachable from team-1 anchor";
            }
            return string.Empty;
        }
    }

    /// <summary>Raster helpers shared by <see cref="SiteRules"/> and the review render: eye-plane occupancy and exit walk distance.</summary>
    public sealed class SiteGrid
    {
        private const float EyeCell = 0.25f;
        private readonly ArenaLayout arena;
        private readonly bool[] eye;
        private readonly int nx, nz;
        private readonly bool[] walk;
        private readonly int wx, wz;
        private readonly Dictionary<int, float[]> exitDistance = new Dictionary<int, float[]>();
        private readonly IReadOnlyList<CollisionBox> boxes;

        public SiteGrid(ArenaLayout arena, IReadOnlyList<CollisionBox> boxes)
        {
            this.arena = arena; this.boxes = boxes;
            float hx = arena.HalfExtentX, hz = arena.HalfExtentZ;
            nx = (int)Math.Ceiling(hx * 2f / EyeCell); nz = (int)Math.Ceiling(hz * 2f / EyeCell);
            eye = new bool[nx * nz];
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (!b.Gameplay) continue;
                float minY = b.Center.Y - b.Size.Y * 0.5f, maxY = b.Center.Y + b.Size.Y * 0.5f;
                if (ArenaRules.EyeHeight < minY || ArenaRules.EyeHeight > maxY) continue;
                int ix0 = Math.Max(0, (int)Math.Floor((b.Center.X - b.Size.X * 0.5f + hx) / EyeCell));
                int ix1 = Math.Min(nx - 1, (int)Math.Floor((b.Center.X + b.Size.X * 0.5f + hx) / EyeCell));
                int iz0 = Math.Max(0, (int)Math.Floor((b.Center.Z - b.Size.Z * 0.5f + hz) / EyeCell));
                int iz1 = Math.Min(nz - 1, (int)Math.Floor((b.Center.Z + b.Size.Z * 0.5f + hz) / EyeCell));
                for (int iz = iz0; iz <= iz1; iz++)
                for (int ix = ix0; ix <= ix1; ix++)
                    eye[iz * nx + ix] = true;
            }
            wx = (int)Math.Round(hx * 2f); wz = (int)Math.Round(hz * 2f);
            walk = new bool[wx * wz];
            float clearance = CompoundConnectivity.RequiredClearance;
            for (int iz = 0; iz < wz; iz++)
            for (int ix = 0; ix < wx; ix++)
                walk[iz * wx + ix] = CompoundConnectivity.IsBlocked(boxes, ix + 0.5f - hx, iz + 0.5f - hz, clearance);
        }

        public bool Blocked(float x, float z)
        {
            int ix = (int)Math.Floor(x + arena.HalfExtentX), iz = (int)Math.Floor(z + arena.HalfExtentZ);
            if (ix < 0 || iz < 0 || ix >= wx || iz >= wz) return true;
            return walk[iz * wx + ix];
        }

        public bool EyeBlocked(float x, float z)
        {
            int ix = (int)Math.Floor((x + arena.HalfExtentX) / EyeCell), iz = (int)Math.Floor((z + arena.HalfExtentZ) / EyeCell);
            if (ix < 0 || iz < 0 || ix >= nx || iz >= nz) return true;
            return eye[iz * nx + ix];
        }

        /// <summary>Eye-height (1.65 m) line of sight between two ground points through the occupancy raster.</summary>
        public bool EyeLos(Float3 a, Float3 b)
        {
            float dx = b.X - a.X, dz = b.Z - a.Z;
            float dist = MathF.Sqrt(dx * dx + dz * dz);
            int steps = Math.Max(1, (int)Math.Ceiling(dist / (EyeCell * 0.5f)));
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                if (EyeBlocked(a.X + dx * t, a.Z + dz * t)) return false;
            }
            return true;
        }

        public float RayLength(float x, float z, float dirX, float dirZ)
        {
            float t = 0f;
            while (true)
            {
                t += EyeCell;
                float px = x + dirX * t, pz = z + dirZ * t;
                if (Math.Abs(px) > arena.HalfExtentX || Math.Abs(pz) > arena.HalfExtentZ) return t;
                if (EyeBlocked(px, pz)) return t;
            }
        }

        /// <summary>Ground walk distance (1 m grid, 8-neighbour) from the nearest exit of <paramref name="team"/>.</summary>
        public float WalkDistanceFromExits(int team, float x, float z)
        {
            if (!exitDistance.TryGetValue(team, out var dist))
            {
                dist = Flood(arena.Exits(team));
                exitDistance[team] = dist;
            }
            int ix = (int)Math.Floor(x + arena.HalfExtentX), iz = (int)Math.Floor(z + arena.HalfExtentZ);
            if (ix < 0 || iz < 0 || ix >= wx || iz >= wz) return float.PositiveInfinity;
            return dist[iz * wx + ix];
        }

        private float[] Flood(IReadOnlyList<Float3> sources)
        {
            var dist = new float[wx * wz];
            for (int i = 0; i < dist.Length; i++) dist[i] = float.PositiveInfinity;
            var open = new SortedSet<(float d, int idx)>();
            for (int s = 0; s < sources.Count; s++)
            {
                int ix = (int)Math.Floor(sources[s].X + arena.HalfExtentX), iz = (int)Math.Floor(sources[s].Z + arena.HalfExtentZ);
                if (ix < 0 || iz < 0 || ix >= wx || iz >= wz) continue;
                int idx = iz * wx + ix;
                dist[idx] = 0f;
                open.Add((0f, idx));
            }
            const float diag = 1.41421356f;
            while (open.Count > 0)
            {
                var cur = open.Min;
                open.Remove(cur);
                int cx = cur.idx % wx, cz = cur.idx / wx;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || z < 0 || x >= wx || z >= wz) continue;
                    int n = z * wx + x;
                    if (walk[n]) continue;
                    if (dx != 0 && dz != 0 && (walk[cz * wx + x] || walk[z * wx + cx])) continue;
                    float nd = cur.d + (dx != 0 && dz != 0 ? diag : 1f);
                    if (nd >= dist[n]) continue;
                    if (!float.IsPositiveInfinity(dist[n])) open.Remove((dist[n], n));
                    dist[n] = nd;
                    open.Add((nd, n));
                }
            }
            return dist;
        }

        public IReadOnlyList<CollisionBox> Boxes => boxes;
    }
}
