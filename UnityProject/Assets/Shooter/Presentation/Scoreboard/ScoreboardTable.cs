#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;

namespace Shooter.Presentation.Scoreboard
{
    public readonly struct ScoreboardRow
    {
        public ParticipantId ParticipantId { get; }
        public string DisplayName { get; }
        public byte Team { get; }
        public bool IsHuman { get; }
        public uint Kills { get; }
        public uint Deaths { get; }
        public bool IsLocal { get; }
        public bool Alive { get; }

        public ScoreboardRow(
            ParticipantId participantId,
            string displayName,
            byte team,
            bool isHuman,
            uint kills,
            uint deaths,
            bool isLocal,
            bool alive)
        {
            ParticipantId = participantId;
            DisplayName = displayName ?? "";
            Team = team;
            IsHuman = isHuman;
            Kills = kills;
            Deaths = deaths;
            IsLocal = isLocal;
            Alive = alive;
        }

        public string RoleLabel => IsHuman ? "Human" : "Bot";
    }

    public sealed class ScoreboardTable
    {
        public const int MaxRows = GameContractVersion.MaxParticipants;

        private readonly List<ScoreboardRow> rows = new List<ScoreboardRow>(MaxRows);

        public uint Team1Kills { get; private set; }
        public uint Team2Kills { get; private set; }
        public MatchOutcome Outcome { get; private set; }
        public FinishReason Reason { get; private set; }
        public IReadOnlyList<ScoreboardRow> Rows => rows;
        public int RowCount => rows.Count;

        public void Clear()
        {
            rows.Clear();
            Team1Kills = 0;
            Team2Kills = 0;
            Outcome = MatchOutcome.None;
            Reason = FinishReason.None;
        }

        public void BindLive(MatchViewSnapshot snapshot, EntityId localHuman)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            rows.Clear();
            Team1Kills = snapshot.Team1Kills;
            Team2Kills = snapshot.Team2Kills;
            Outcome = snapshot.Outcome;
            Reason = snapshot.Reason;
            for (int i = 0; i < snapshot.Participants.Count && rows.Count < MaxRows; i++)
            {
                var p = snapshot.Participants[i];
                rows.Add(new ScoreboardRow(
                    p.ParticipantId,
                    p.DisplayName,
                    p.Team,
                    p.IsHuman,
                    p.Kills,
                    p.Deaths,
                    p.EntityId.Equals(localHuman) || p.IsHuman,
                    p.Alive));
            }
            rows.Sort(CompareRows);
        }

        public void BindResults(MatchResults results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            rows.Clear();
            Team1Kills = results.Team1Kills;
            Team2Kills = results.Team2Kills;
            Outcome = results.Outcome;
            Reason = results.Reason;
            for (int i = 0; i < results.FinalRows.Count && rows.Count < MaxRows; i++)
            {
                var p = results.FinalRows[i];
                rows.Add(new ScoreboardRow(
                    p.ParticipantId,
                    p.DisplayName,
                    p.Team,
                    p.IsHuman,
                    p.Kills,
                    p.Deaths,
                    p.IsHuman,
                    p.Alive));
            }
            // FinalRows are already sorted by authority; re-sort defensively for UI immutability.
            rows.Sort(CompareRows);
        }

        public string LocalOutcomeLabel(byte localTeam)
        {
            if (Outcome == MatchOutcome.Draw) return "Draw";
            if (Outcome == MatchOutcome.Team1) return localTeam == 1 ? "Victory" : "Defeat";
            if (Outcome == MatchOutcome.Team2) return localTeam == 2 ? "Victory" : "Defeat";
            return "Results";
        }

        private static int CompareRows(ScoreboardRow a, ScoreboardRow b)
        {
            int cmp = b.Kills.CompareTo(a.Kills);
            if (cmp != 0) return cmp;
            cmp = a.Deaths.CompareTo(b.Deaths);
            if (cmp != 0) return cmp;
            return a.ParticipantId.CompareTo(b.ParticipantId);
        }
    }
}
