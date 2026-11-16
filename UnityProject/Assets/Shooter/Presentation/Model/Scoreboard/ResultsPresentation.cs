#nullable enable
using System;
using System.Collections.Generic;

namespace Shooter.Presentation.Scoreboard
{
    public enum ResultsAwardKind : byte
    {
        /// <summary>Highest score in the match.</summary>
        Mvp = 1,
        /// <summary>Most eliminations.</summary>
        TopFragger = 2,
        /// <summary>Fewest deaths among operators with at least one elimination.</summary>
        Survivor = 3,
        /// <summary>Best eliminations-per-death ratio (minimum two eliminations).</summary>
        Efficiency = 4,
    }

    /// <summary>One debrief award card (presentation only; never feeds score or stats).</summary>
    public readonly struct ResultsAward
    {
        public ResultsAward(ResultsAwardKind kind, string title, string displayName, byte team, bool isLocal, string stat, int tier)
        {
            Kind = kind; Title = title; DisplayName = displayName; Team = team; IsLocal = isLocal; Stat = stat; Tier = tier;
        }

        public ResultsAwardKind Kind { get; }
        public string Title { get; }
        public string DisplayName { get; }
        public byte Team { get; }
        public bool IsLocal { get; }
        public string Stat { get; }
        /// <summary>Medal tier 1..4 used for the procedural medal art.</summary>
        public int Tier { get; }
    }

    /// <summary>
    /// ADR-086 debrief presentation: deterministic awards from the immutable final rows, and the reveal timeline.
    /// Ties resolve to the earlier row in authority order (score, then kills).
    /// </summary>
    public static class ResultsPresentation
    {
        public const float BannerSweepSeconds = 0.55f;
        public const float ScoreCountStart = 0.25f;
        public const float ScoreCountSeconds = 1.1f;
        public const float AwardsStart = 0.65f;
        public const float AwardStagger = 0.16f;
        public const float TablesStart = 1.0f;
        public const int MaxAwards = 4;

        public static IReadOnlyList<ResultsAward> Awards(IReadOnlyList<ScoreboardRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var awards = new List<ResultsAward>(MaxAwards);
            int mvp = -1, frag = -1, survivor = -1, efficiency = -1;
            double bestRatio = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.Score > 0 && (mvp < 0 || r.Score > rows[mvp].Score)) mvp = i;
                if (r.Kills > 0 && (frag < 0 || r.Kills > rows[frag].Kills)) frag = i;
                if (r.Kills > 0 && (survivor < 0 || r.Deaths < rows[survivor].Deaths)) survivor = i;
                if (r.Kills >= 2)
                {
                    double ratio = r.Kills / (double)Math.Max(1u, r.Deaths);
                    if (ratio > bestRatio) { bestRatio = ratio; efficiency = i; }
                }
            }
            if (mvp >= 0) awards.Add(Make(ResultsAwardKind.Mvp, "MVP", rows[mvp], rows[mvp].Score + " PTS", 4));
            if (frag >= 0) awards.Add(Make(ResultsAwardKind.TopFragger, "TOP FRAGGER", rows[frag], rows[frag].Kills + " KILLS", 3));
            if (survivor >= 0) awards.Add(Make(ResultsAwardKind.Survivor, "SURVIVOR", rows[survivor], rows[survivor].Deaths + " DEATHS", 2));
            if (efficiency >= 0)
                awards.Add(Make(ResultsAwardKind.Efficiency, "SHARPEST", rows[efficiency], FormatRatio(bestRatio) + " K/D", 2));
            return awards;
        }

        /// <summary>K/D with two decimals, invariant culture, no rounding surprises for integers.</summary>
        public static string FormatRatio(double ratio) =>
            (Math.Round(ratio * 100.0, MidpointRounding.AwayFromZero) / 100.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        private static ResultsAward Make(ResultsAwardKind kind, string title, ScoreboardRow row, string stat, int tier) =>
            new ResultsAward(kind, title, row.DisplayName, row.Team, row.IsLocal, stat, tier);

        /// <summary>Elapsed time seen by award card <paramref name="index"/>.</summary>
        public static float AwardElapsed(float sinceShown, int index) => sinceShown - AwardsStart - index * AwardStagger;

        /// <summary>Banner reveal fraction 0..1 (1 immediately under reduced motion).</summary>
        public static float BannerReveal(float sinceShown, bool reducedMotion) =>
            reducedMotion ? 1f : Hud.UiMotion.OutCubic(Hud.UiMotion.Progress(sinceShown, BannerSweepSeconds));

        /// <summary>Animated team score (final value under reduced motion).</summary>
        public static int TeamScore(uint finalKills, float sinceShown, bool reducedMotion) =>
            Hud.UiMotion.CountUp(0, (int)Math.Min(finalKills, int.MaxValue), sinceShown - ScoreCountStart, ScoreCountSeconds, reducedMotion);

        /// <summary>Team table alpha (fade only; tables never slide).</summary>
        public static float TablesAlpha(float sinceShown, bool reducedMotion) =>
            reducedMotion ? 1f : Hud.UiMotion.EnterAlpha(sinceShown - TablesStart, 0.35f);
    }
}
