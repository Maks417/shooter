#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>Outcome of one numeric rule from docs/features/tdm-map-layouts.md.</summary>
    public sealed class ArenaRuleResult
    {
        public string Id { get; }
        public bool Passed { get; }
        public string Measured { get; }
        public string Threshold { get; }

        public ArenaRuleResult(string id, bool passed, string measured, string threshold)
        {
            Id = id; Passed = passed; Measured = measured; Threshold = threshold;
        }

        public override string ToString() => (Passed ? "PASS " : "FAIL ") + Id + " measured=" + Measured + " required=" + Threshold;
    }

    public sealed class ArenaRulesReport
    {
        public bool Passed { get; }
        public IReadOnlyList<ArenaRuleResult> Rules { get; }
        public string Diagnostic { get; }

        public float PlayableSizeX { get; }
        public float PlayableSizeZ { get; }
        public float MinPerimeterHeight { get; }
        public bool PerimeterClosed { get; }
        public float AnchorSeparationZ { get; }
        public float PathAnchorToCenterTeam1 { get; }
        public float PathAnchorToCenterTeam2 { get; }
        public float PathAnchorToEnemy { get; }
        public int ExitCountTeam1 { get; }
        public int ExitCountTeam2 { get; }
        public float MinExitSpacing { get; }
        public int DistinctRoutes { get; }
        public bool HasCoveredRoute { get; }
        public int SpawnLosPairs { get; }
        public int CenterLosSlots { get; }
        public float LongestSightline { get; }
        public float WorstCoverDistance { get; }
        public int UncoveredSamples { get; }
        public int CoverSamples { get; }
        public int MirrorMismatches { get; }
        public int GameplayBoxCount { get; }
        public int CoverProxyCount { get; }
        public int SlotsTeam1 { get; }
        public int SlotsTeam2 { get; }

        public ArenaRulesReport(
            IReadOnlyList<ArenaRuleResult> rules,
            float playableSizeX, float playableSizeZ, float minPerimeterHeight, bool perimeterClosed,
            float anchorSeparationZ, float pathCenter1, float pathCenter2, float pathEnemy,
            int exits1, int exits2, float minExitSpacing, int distinctRoutes, bool coveredRoute,
            int spawnLosPairs, int centerLosSlots, float longestSightline, float worstCoverDistance,
            int uncoveredSamples, int coverSamples, int mirrorMismatches, int gameplayBoxes, int coverProxies,
            int slots1, int slots2)
        {
            Rules = rules;
            bool all = true;
            var sb = new StringBuilder();
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i].Passed) continue;
                all = false;
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(rules[i].Id).Append(" measured=").Append(rules[i].Measured).Append(" required=").Append(rules[i].Threshold);
            }
            Passed = all;
            Diagnostic = all ? "ok" : sb.ToString();
            PlayableSizeX = playableSizeX; PlayableSizeZ = playableSizeZ;
            MinPerimeterHeight = minPerimeterHeight; PerimeterClosed = perimeterClosed;
            AnchorSeparationZ = anchorSeparationZ;
            PathAnchorToCenterTeam1 = pathCenter1; PathAnchorToCenterTeam2 = pathCenter2; PathAnchorToEnemy = pathEnemy;
            ExitCountTeam1 = exits1; ExitCountTeam2 = exits2; MinExitSpacing = minExitSpacing;
            DistinctRoutes = distinctRoutes; HasCoveredRoute = coveredRoute;
            SpawnLosPairs = spawnLosPairs; CenterLosSlots = centerLosSlots;
            LongestSightline = longestSightline; WorstCoverDistance = worstCoverDistance;
            UncoveredSamples = uncoveredSamples; CoverSamples = coverSamples;
            MirrorMismatches = mirrorMismatches; GameplayBoxCount = gameplayBoxes; CoverProxyCount = coverProxies;
            SlotsTeam1 = slots1; SlotsTeam2 = slots2;
        }

        public ArenaRuleResult? Rule(string id)
        {
            for (int i = 0; i < Rules.Count; i++) if (Rules[i].Id == id) return Rules[i];
            return null;
        }

        /// <summary>Readable multi-line report with every rule and its measured value.</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Rules.Count; i++) sb.AppendLine(Rules[i].ToString());
            return sb.ToString();
        }
    }

    /// <summary>
    /// Validator for the numeric rules table of docs/features/tdm-map-layouts.md. Geometry constraints only;
    /// it does not prove the map is fun to play.
    /// </summary>
    public static class ArenaRules
    {
        public const string RulePlayable = "playable-rectangle";
        public const string RulePerimeter = "perimeter";
        public const string RuleAnchorSeparation = "anchor-separation";
        public const string RulePathCenter = "path-anchor-center";
        public const string RulePathEnemy = "path-anchor-enemy";
        public const string RuleExits = "staging-exits";
        public const string RuleRoutes = "distinct-routes";
        public const string RuleSpawnLos = "spawn-los";
        public const string RuleSightline = "longest-sightline";
        public const string RuleCover = "cover-coverage";
        public const string RuleMirror = "mirror-symmetry";
        public const string RuleCeilings = "ceilings";
        public const string RuleSpawnSlots = "spawn-slots";

        public const float EyeHeight = 1.65f;
        public const float SightlineSampleMeters = 4f;
        public const int SightlineDirections = 16;
        public const float SightlineRayStepMeters = 0.25f;
        public const float MaxSightlineMeters = 48f;
        public const float MaxCoverDistanceMeters = 8f;
        public const float CoverMinTopMeters = 1.0f;
        public const float MirrorToleranceMeters = 0.001f;
        public const int MaxGameplayBoxes = 400;
        public const int MaxCoverProxies = 64;
        public const float MinPerimeterHeightMeters = 4f;
        // ADR-074 compact sizing (supersedes the ADR-045/071 70-100 x 90-120 m table).
        public const float MinPlayableX = 56f, MaxPlayableX = 72f;
        public const float MinPlayableZ = 72f, MaxPlayableZ = 88f;
        public const float MinAnchorSeparation = 60f, MaxAnchorSeparation = 76f;
        public const float MinPathToCenter = 26f, MaxPathToCenter = 44f;
        public const float MinPathToEnemy = 60f, MaxPathToEnemy = 100f;

        public static ArenaRulesReport Validate(CompoundPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var arena = plan.Arena;
            if (arena == null)
            {
                var none = new List<ArenaRuleResult> { new ArenaRuleResult("arena-present", false, "no ArenaLayout", "plan.Arena != null") };
                return new ArenaRulesReport(none, 0, 0, 0, false, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            }

            var boxes = plan.CollisionBoxes;
            float clearance = CompoundConnectivity.RequiredClearance;
            var rules = new List<ArenaRuleResult>();
            var inv = CultureInfo.InvariantCulture;

            // 1. Playable rectangle.
            float sizeX = arena.HalfExtentX * 2f, sizeZ = arena.HalfExtentZ * 2f;
            rules.Add(new ArenaRuleResult(RulePlayable, sizeX >= MinPlayableX && sizeX <= MaxPlayableX && sizeZ >= MinPlayableZ && sizeZ <= MaxPlayableZ,
                F(sizeX) + "x" + F(sizeZ), "X 56-72, Z 72-88"));

            // 2. Perimeter walls: >= 4 m tall, closed ring just outside the playable rectangle.
            MeasurePerimeter(boxes, arena, out float minHeight, out bool closed);
            rules.Add(new ArenaRuleResult(RulePerimeter, closed && minHeight >= MinPerimeterHeightMeters,
                "closed=" + closed + " minHeight=" + F(minHeight), "closed, height >= 4.0"));

            // 3. Anchor separation along Z.
            float separation = Math.Abs(arena.Team2Anchor.Z - arena.Team1Anchor.Z);
            rules.Add(new ArenaRuleResult(RuleAnchorSeparation, separation >= MinAnchorSeparation && separation <= MaxAnchorSeparation, F(separation), "60-76"));

            // 4/5. Nav path lengths (1 m grid, capsule clearance, sum of corner distances).
            var centre = ResolveOpenCenter(boxes, arena.Center, clearance, arena.Team1Anchor);
            var centre2 = ResolveOpenCenter(boxes, arena.Center, clearance, arena.Team2Anchor);
            var path = new List<Float3>();
            float c1 = PathLength(boxes, arena.Team1Anchor, centre, path, clearance);
            float c2 = PathLength(boxes, arena.Team2Anchor, centre2, path, clearance);
            float enemy = PathLength(boxes, arena.Team1Anchor, arena.Team2Anchor, path, clearance);
            rules.Add(new ArenaRuleResult(RulePathCenter, InRange(c1, MinPathToCenter, MaxPathToCenter) && InRange(c2, MinPathToCenter, MaxPathToCenter),
                "t1=" + F(c1) + " t2=" + F(c2), "26-44 each"));
            rules.Add(new ArenaRuleResult(RulePathEnemy, InRange(enemy, MinPathToEnemy, MaxPathToEnemy), F(enemy), "60-100"));

            // 6. Staging exits.
            int exits1 = CountReachingExits(boxes, arena, 1, centre, clearance);
            int exits2 = CountReachingExits(boxes, arena, 2, centre, clearance);
            float minSpacing = MinPairwiseDistance(arena.Team1Exits);
            minSpacing = Math.Min(minSpacing, MinPairwiseDistance(arena.Team2Exits));
            rules.Add(new ArenaRuleResult(RuleExits,
                arena.Team1Exits.Count >= 3 && exits1 >= 3 && exits2 >= 3 && minSpacing >= 8f,
                "declared=" + arena.Team1Exits.Count + " reach t1=" + exits1 + " t2=" + exits2 + " minSpacing=" + F(minSpacing),
                ">= 3 reaching centre, spacing >= 8"));

            // 7. Distinct routes (existing similarity rule, via points = lane points) and a covered one.
            var topology = MapTopologyValidation.Validate("arena", plan);
            rules.Add(new ArenaRuleResult(RuleRoutes, topology.DistinctRoutes >= 3 && topology.HasCoveredRoute,
                "routes=" + topology.DistinctRoutes + " covered=" + topology.HasCoveredRoute, ">= 3 routes, >= 1 covered"));

            // 8. Spawn LOS.
            List<SpawnCandidate>? slots = null;
            string slotError = string.Empty;
            try { slots = MapSpawnLayout.BuildBothTeams(plan); }
            catch (InvalidOperationException e) { slotError = e.Message; }
            int losPairs = 0, centreLos = 0, s1 = 0, s2 = 0;
            bool slotsOk = slots != null;
            if (slots != null)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].Team == 1) s1++; else s2++;
                    if (MapSpawnLayout.HasSpawnLos(boxes, slots[i].Position, new Float3(0f, 0f, 0f))) centreLos++;
                    if (slots[i].Team != 1) continue;
                    for (int j = 0; j < slots.Count; j++)
                    {
                        if (slots[j].Team != 2) continue;
                        if (MapSpawnLayout.HasSpawnLos(boxes, slots[i].Position, slots[j].Position)) losPairs++;
                    }
                }
            }
            rules.Add(new ArenaRuleResult(RuleSpawnLos, slotsOk && losPairs == 0 && centreLos == 0,
                "t1-t2 pairs=" + losPairs + " centre-to-slot=" + centreLos, "0 and 0"));

            // 9. Longest eye-height sightline.
            float longest = LongestSightline(boxes, arena, clearance, out float lox, out float loz, out int ldir);
            rules.Add(new ArenaRuleResult(RuleSightline, longest <= MaxSightlineMeters,
                F(longest) + " from (" + F(lox) + "," + F(loz) + ") dir " + F(ldir * 360f / SightlineDirections) + "deg", "<= 48"));

            // 10. Cover coverage.
            MeasureCover(boxes, arena, clearance, out float worst, out int uncovered, out int samples, out float wx, out float wz);
            rules.Add(new ArenaRuleResult(RuleCover, uncovered == 0 && samples > 0,
                "worst=" + F(worst) + " at (" + F(wx) + "," + F(wz) + ") uncovered=" + uncovered + "/" + samples, "every open sample within 8 m of cover (height >= 1.0)"));

            // 11. Mirror symmetry.
            int mismatches = CountMirrorMismatches(boxes, arena.Symmetry);
            rules.Add(new ArenaRuleResult(RuleMirror, mismatches == 0, arena.Symmetry + " unmatched=" + mismatches, "0 (1 mm)"));

            // 12. Ceilings.
            int gameplay = 0;
            for (int i = 0; i < boxes.Count; i++) if (boxes[i].Gameplay) gameplay++;
            rules.Add(new ArenaRuleResult(RuleCeilings, gameplay <= MaxGameplayBoxes && plan.Covers.Count <= MaxCoverProxies,
                "boxes=" + gameplay + " covers=" + plan.Covers.Count, "<= 400 boxes, <= 64 covers"));

            // 13. Spawn slots.
            bool inside = slotsOk;
            bool spacing = slotsOk;
            if (slots != null)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    if (!arena.StagingContains(slots[i].Team, slots[i].Position.X, slots[i].Position.Z)) inside = false;
                    for (int j = i + 1; j < slots.Count; j++)
                    {
                        if (slots[i].Team != slots[j].Team) continue;
                        float dx = slots[i].Position.X - slots[j].Position.X, dz = slots[i].Position.Z - slots[j].Position.Z;
                        if (dx * dx + dz * dz < MapSpawnLayout.MinSlotSpacingMeters * MapSpawnLayout.MinSlotSpacingMeters) spacing = false;
                    }
                }
            }
            rules.Add(new ArenaRuleResult(RuleSpawnSlots, slotsOk && s1 == 12 && s2 == 12 && inside && spacing,
                slotsOk ? "t1=" + s1 + " t2=" + s2 + " insideStaging=" + inside + " spacingOk=" + spacing : slotError,
                "12/team inside own staging, spacing >= 2"));

            return new ArenaRulesReport(rules, sizeX, sizeZ, minHeight, closed, separation, c1, c2, enemy,
                exits1, exits2, minSpacing, topology.DistinctRoutes, topology.HasCoveredRoute,
                losPairs, centreLos, longest, worst, uncovered, samples, mismatches, gameplay, plan.Covers.Count, s1, s2);

            string F(float v) => v.ToString("0.##", inv);
        }

        /// <summary>True when the box is a perimeter wall (centre outside the playable rectangle).</summary>
        public static bool IsPerimeterBox(CollisionBox box, ArenaLayout arena)
            => Math.Abs(box.Center.X) > arena.HalfExtentX || Math.Abs(box.Center.Z) > arena.HalfExtentZ;

        /// <summary>Cover box: gameplay, top height >= 1.0 m, not a perimeter wall.</summary>
        public static bool IsCoverBox(CollisionBox box, ArenaLayout arena)
            => box.Gameplay && box.Center.Y + box.Size.Y * 0.5f >= CoverMinTopMeters && !IsPerimeterBox(box, arena);

        private static bool InRange(float v, float lo, float hi) => v >= lo && v <= hi;

        private static float PathLength(IReadOnlyList<CollisionBox> boxes, Float3 from, Float3 to, List<Float3> scratch, float clearance)
        {
            if (!CompoundConnectivity.TryFindPath(boxes, from, to, scratch, clearance)) return float.PositiveInfinity;
            float sum = 0f;
            for (int i = 1; i < scratch.Count; i++)
            {
                float dx = scratch[i].X - scratch[i - 1].X;
                float dy = scratch[i].Y - scratch[i - 1].Y;
                float dz = scratch[i].Z - scratch[i - 1].Z;
                sum += MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            }
            return sum;
        }

        /// <summary>Nearest open cell to the centre (ties broken toward <paramref name="prefer"/>, so both teams measure symmetrically when the centre is solid).</summary>
        private static Float3 ResolveOpenCenter(IReadOnlyList<CollisionBox> boxes, Float3 start, float clearance, Float3 prefer)
        {
            if (!CompoundConnectivity.IsBlocked(boxes, start.X, start.Z, clearance)) return start;
            for (int r = 1; r <= 24; r++)
            {
                Float3? best = null;
                float bestD = float.MaxValue;
                for (int a = 0; a < 16; a++)
                {
                    double ang = a * Math.PI / 8;
                    var p = new Float3(start.X + (float)Math.Cos(ang) * r, 0f, start.Z + (float)Math.Sin(ang) * r);
                    if (CompoundConnectivity.IsBlocked(boxes, p.X, p.Z, clearance)) continue;
                    // Nearest cell; the tiny anchor-distance term is a deterministic tie-break.
                    float d = (p.X - start.X) * (p.X - start.X) + (p.Z - start.Z) * (p.Z - start.Z)
                        + 1e-4f * ((p.X - prefer.X) * (p.X - prefer.X) + (p.Z - prefer.Z) * (p.Z - prefer.Z));
                    if (d < bestD) { bestD = d; best = p; }
                }
                if (best.HasValue) return best.Value;
            }
            return start;
        }

        private static int CountReachingExits(IReadOnlyList<CollisionBox> boxes, ArenaLayout arena, int team, Float3 centre, float clearance)
        {
            var exits = arena.Exits(team);
            var scratch = new List<Float3>();
            int n = 0;
            for (int i = 0; i < exits.Count; i++)
            {
                if (CompoundConnectivity.IsBlocked(boxes, exits[i].X, exits[i].Z, clearance)) continue;
                if (!CompoundConnectivity.TryFindPath(boxes, arena.Anchor(team), exits[i], scratch, clearance)) continue;
                if (!CompoundConnectivity.TryFindPath(boxes, exits[i], centre, scratch, clearance)) continue;
                n++;
            }
            return n;
        }

        private static float MinPairwiseDistance(IReadOnlyList<Float3> pts)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < pts.Count; i++)
            for (int j = i + 1; j < pts.Count; j++)
            {
                float dx = pts[i].X - pts[j].X, dz = pts[i].Z - pts[j].Z;
                best = Math.Min(best, MathF.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }

        private static void MeasurePerimeter(IReadOnlyList<CollisionBox> boxes, ArenaLayout arena, out float minHeight, out bool closed)
        {
            minHeight = float.PositiveInfinity;
            closed = true;
            float hx = arena.HalfExtentX, hz = arena.HalfExtentZ;
            const float outside = 0.2f, step = 0.5f;
            for (float t = -hx; t <= hx + 1e-3f; t += step)
            {
                Probe(boxes, t, hz + outside, ref minHeight, ref closed);
                Probe(boxes, t, -hz - outside, ref minHeight, ref closed);
            }
            for (float t = -hz; t <= hz + 1e-3f; t += step)
            {
                Probe(boxes, hx + outside, t, ref minHeight, ref closed);
                Probe(boxes, -hx - outside, t, ref minHeight, ref closed);
            }
            if (float.IsPositiveInfinity(minHeight)) minHeight = 0f;
        }

        private static void Probe(IReadOnlyList<CollisionBox> boxes, float x, float z, ref float minHeight, ref bool closed)
        {
            float tallest = 0f;
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (!b.Gameplay) continue;
                if (b.Center.Y - b.Size.Y * 0.5f > 0.1f) continue;
                if (Math.Abs(x - b.Center.X) > b.Size.X * 0.5f || Math.Abs(z - b.Center.Z) > b.Size.Z * 0.5f) continue;
                tallest = Math.Max(tallest, b.Center.Y + b.Size.Y * 0.5f);
            }
            if (tallest <= 0f) { closed = false; minHeight = 0f; return; }
            minHeight = Math.Min(minHeight, tallest);
        }

        private static float LongestSightline(IReadOnlyList<CollisionBox> boxes, ArenaLayout arena, float clearance, out float originX, out float originZ, out int direction)
        {
            originX = 0f; originZ = 0f; direction = 0;
            const float cell = 0.25f;
            float hx = arena.HalfExtentX, hz = arena.HalfExtentZ;
            int nx = (int)Math.Ceiling(hx * 2f / cell), nz = (int)Math.Ceiling(hz * 2f / cell);
            var blocked = new bool[nx * nz];
            // Rasterise boxes that intersect the eye plane using the same vertical test as HasLineOfSight.
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (!b.Gameplay) continue;
                float minY = b.Center.Y - b.Size.Y * 0.5f, maxY = b.Center.Y + b.Size.Y * 0.5f;
                if (EyeHeight < minY || EyeHeight > maxY) continue;
                int ix0 = Math.Max(0, (int)Math.Floor((b.Center.X - b.Size.X * 0.5f + hx) / cell - 0.5f));
                int ix1 = Math.Min(nx - 1, (int)Math.Ceiling((b.Center.X + b.Size.X * 0.5f + hx) / cell - 0.5f));
                int iz0 = Math.Max(0, (int)Math.Floor((b.Center.Z - b.Size.Z * 0.5f + hz) / cell - 0.5f));
                int iz1 = Math.Min(nz - 1, (int)Math.Ceiling((b.Center.Z + b.Size.Z * 0.5f + hz) / cell - 0.5f));
                for (int iz = iz0; iz <= iz1; iz++)
                for (int ix = ix0; ix <= ix1; ix++)
                {
                    float wx = (ix + 0.5f) * cell - hx, wz = (iz + 0.5f) * cell - hz;
                    if (Math.Abs(wx - b.Center.X) <= b.Size.X * 0.5f + cell * 0.5f && Math.Abs(wz - b.Center.Z) <= b.Size.Z * 0.5f + cell * 0.5f)
                        blocked[iz * nx + ix] = true;
                }
            }

            var dirX = new float[SightlineDirections];
            var dirZ = new float[SightlineDirections];
            for (int d = 0; d < SightlineDirections; d++)
            {
                double a = d * (Math.PI * 2.0 / SightlineDirections);
                dirX[d] = (float)Math.Cos(a);
                dirZ[d] = (float)Math.Sin(a);
            }

            float longest = 0f;
            for (float sz = -hz + SightlineSampleMeters * 0.5f; sz < hz; sz += SightlineSampleMeters)
            for (float sx = -hx + SightlineSampleMeters * 0.5f; sx < hx; sx += SightlineSampleMeters)
            {
                if (CompoundConnectivity.IsBlocked(boxes, sx, sz, clearance)) continue;
                for (int d = 0; d < SightlineDirections; d++)
                {
                    float t = 0f;
                    while (true)
                    {
                        t += SightlineRayStepMeters;
                        float x = sx + dirX[d] * t, z = sz + dirZ[d] * t;
                        if (Math.Abs(x) > hx || Math.Abs(z) > hz) break;
                        int ix = Math.Min(nx - 1, (int)((x + hx) / cell)), iz = Math.Min(nz - 1, (int)((z + hz) / cell));
                        if (blocked[iz * nx + ix]) break;
                    }
                    if (t > longest) { longest = t; originX = sx; originZ = sz; direction = d; }
                }
            }
            return longest;
        }

        private static void MeasureCover(IReadOnlyList<CollisionBox> boxes, ArenaLayout arena, float clearance, out float worst, out int uncovered, out int samples, out float worstX, out float worstZ)
        {
            worstX = 0f; worstZ = 0f;
            var cover = new List<CollisionBox>();
            for (int i = 0; i < boxes.Count; i++) if (IsCoverBox(boxes[i], arena)) cover.Add(boxes[i]);
            worst = 0f; uncovered = 0; samples = 0;
            float hx = arena.HalfExtentX, hz = arena.HalfExtentZ;
            for (float sz = -hz + 2f; sz < hz; sz += 4f)
            for (float sx = -hx + 2f; sx < hx; sx += 4f)
            {
                if (arena.StagingContains(1, sx, sz) || arena.StagingContains(2, sx, sz)) continue;
                if (CompoundConnectivity.IsBlocked(boxes, sx, sz, clearance)) continue;
                samples++;
                float best = float.PositiveInfinity;
                for (int i = 0; i < cover.Count; i++)
                {
                    var b = cover[i];
                    float dx = Math.Max(0f, Math.Abs(sx - b.Center.X) - b.Size.X * 0.5f);
                    float dz = Math.Max(0f, Math.Abs(sz - b.Center.Z) - b.Size.Z * 0.5f);
                    float d = MathF.Sqrt(dx * dx + dz * dz);
                    if (d < best) best = d;
                }
                if (best > worst) { worst = best; worstX = sx; worstZ = sz; }
                if (best > MaxCoverDistanceMeters) uncovered++;
            }
        }

        private static int CountMirrorMismatches(IReadOnlyList<CollisionBox> boxes, ArenaSymmetry symmetry)
        {
            int bad = 0;
            for (int i = 0; i < boxes.Count; i++)
            {
                var a = boxes[i];
                if (!a.Gameplay) continue;
                var t = ArenaLayout.Twin(symmetry, a.Center);
                bool found = false;
                for (int j = 0; j < boxes.Count && !found; j++)
                {
                    var b = boxes[j];
                    if (!b.Gameplay) continue;
                    found = Math.Abs(t.X - b.Center.X) <= MirrorToleranceMeters &&
                            Math.Abs(t.Y - b.Center.Y) <= MirrorToleranceMeters &&
                            Math.Abs(t.Z - b.Center.Z) <= MirrorToleranceMeters &&
                            Math.Abs(a.Size.X - b.Size.X) <= MirrorToleranceMeters &&
                            Math.Abs(a.Size.Y - b.Size.Y) <= MirrorToleranceMeters &&
                            Math.Abs(a.Size.Z - b.Size.Z) <= MirrorToleranceMeters;
                }
                if (!found) bad++;
            }
            return bad;
        }
    }
}
