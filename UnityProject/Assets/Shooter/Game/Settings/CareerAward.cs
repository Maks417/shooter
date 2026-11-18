#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    /// <summary>Human-only match counts the frozen results rows do not carry (ADR-108).</summary>
    public readonly struct CareerMatchExtras
    {
        public uint Assists { get; }
        public uint Headshots { get; }
        public uint BestStreak { get; }

        public CareerMatchExtras(uint assists, uint headshots, uint bestStreak)
        {
            Assists = assists;
            Headshots = headshots;
            BestStreak = bestStreak;
        }
    }

    public enum CareerMatchOutcome : byte
    {
        Loss = 0,
        Draw = 1,
        Win = 2
    }

    /// <summary>One finished match's career award and the profile it produced.</summary>
    public sealed class CareerAwardResult
    {
        public CareerProfile Before { get; }
        public CareerProfile After { get; }
        public CareerMatchOutcome Outcome { get; }
        public long ScorePoints { get; }
        public long CompletionPoints { get; }
        public long OutcomePoints { get; }
        public long Total => ScorePoints + CompletionPoints + OutcomePoints;
        public int RankBefore => Before.Rank;
        public int RankAfter => After.Rank;
        public bool Promoted => RankAfter > RankBefore;

        /// <summary>Every rank reached by this match, lowest first (more than one when a match jumps several ranks).</summary>
        public IReadOnlyList<RankDefinition> NewRanks
        {
            get
            {
                var list = new List<RankDefinition>();
                for (int r = RankBefore + 1; r <= RankAfter; r++) list.Add(CareerLadder.Get(r));
                return list;
            }
        }

        internal CareerAwardResult(CareerProfile before, CareerProfile after, CareerMatchOutcome outcome, long score, long completion, long outcomePoints)
        {
            Before = before;
            After = after;
            Outcome = outcome;
            ScorePoints = score;
            CompletionPoints = completion;
            OutcomePoints = outcomePoints;
        }
    }

    /// <summary>
    /// ADR-108 career award: CP = the human's match score + 500 for finishing + 1000 for a win (500 for a draw), and the
    /// service record gains the match. Pure and deterministic; aborted matches and results without a human row earn nothing.
    /// </summary>
    public static class CareerAward
    {
        public const long CompletionPoints = 500;
        public const long WinPoints = 1000;
        public const long DrawPoints = 500;

        public static CareerAwardResult? Apply(CareerProfile profile, MatchResults results, CareerMatchExtras extras)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (results.Reason == FinishReason.Aborted || results.Outcome == MatchOutcome.None) return null;
            ParticipantState? human = null;
            foreach (var row in results.FinalRows)
                if (row.IsHuman) { human = row; break; }
            if (human == null) return null;

            CareerMatchOutcome outcome = results.Outcome == MatchOutcome.Draw ? CareerMatchOutcome.Draw
                : (results.Outcome == MatchOutcome.Team1 && human.Team == 1) || (results.Outcome == MatchOutcome.Team2 && human.Team == 2)
                    ? CareerMatchOutcome.Win : CareerMatchOutcome.Loss;
            long score = human.Score;
            long outcomePoints = outcome == CareerMatchOutcome.Win ? WinPoints : outcome == CareerMatchOutcome.Draw ? DrawPoints : 0;
            long total = score + CompletionPoints + outcomePoints;

            var after = new CareerProfile(
                Add(profile.CareerPoints, total),
                Add(profile.Matches, 1),
                Add(profile.Wins, outcome == CareerMatchOutcome.Win ? 1 : 0),
                Add(profile.Losses, outcome == CareerMatchOutcome.Loss ? 1 : 0),
                Add(profile.Draws, outcome == CareerMatchOutcome.Draw ? 1 : 0),
                Add(profile.Kills, human.Kills),
                Add(profile.Deaths, human.Deaths),
                Add(profile.Assists, extras.Assists),
                Add(profile.Headshots, extras.Headshots),
                Add(profile.ScoreTotal, score),
                Math.Max(profile.BestScore, score),
                Math.Max(profile.BestStreak, extras.BestStreak),
                Add(profile.ActiveTicks, results.ActiveDurationTicks));
            return new CareerAwardResult(profile, after, outcome, score, CompletionPoints, outcomePoints);
        }

        private static long Add(long a, long b) => CareerProfile.Clamp(a + b);
    }

    /// <summary>
    /// ADR-108 once-per-match guard for the shell: the host polls the finished session every frame, so the same results
    /// are offered repeatedly. A match is identified by the session load epoch plus its authority session id.
    /// </summary>
    public sealed class CareerRecorder
    {
        private ulong lastEpoch;
        private ulong lastSession;
        private bool any;

        public CareerProfile Profile { get; private set; }
        /// <summary>The most recent award this run (debrief), null before the first finished match.</summary>
        public CareerAwardResult? LastAward { get; private set; }
        /// <summary>Authority session id of <see cref="LastAward"/> (0 before the first award).</summary>
        public ulong LastAwardSessionId { get; private set; }

        public CareerRecorder(CareerProfile profile) => Profile = profile ?? throw new ArgumentNullException(nameof(profile));

        /// <summary>Applies <paramref name="results"/> once per (epoch, session); returns true when the profile changed.</summary>
        public bool TryRecord(ulong sessionEpoch, MatchResults results, CareerMatchExtras extras)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (any && lastEpoch == sessionEpoch && lastSession == results.AuthoritySessionId) return false;
            any = true;
            lastEpoch = sessionEpoch;
            lastSession = results.AuthoritySessionId;
            var award = CareerAward.Apply(Profile, results, extras);
            if (award == null) return false;
            LastAward = award;
            LastAwardSessionId = results.AuthoritySessionId;
            Profile = award.After;
            return true;
        }

        /// <summary>Clears the debrief award (a new match started).</summary>
        public void ClearLastAward()
        {
            LastAward = null;
            LastAwardSessionId = 0;
        }
    }
}
