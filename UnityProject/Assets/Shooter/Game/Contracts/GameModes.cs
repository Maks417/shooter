#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Game.Contracts
{
    /// <summary>
    /// ADR-122: the rules a mode plays by, as data. A session request reads its limits and team count from its mode
    /// instead of contract constants; adding a mode is one registered <see cref="ModeRules"/> plus its match rules.
    /// </summary>
    public sealed class ModeRules
    {
        public ModeId Id { get; }
        public string DisplayName { get; }
        public byte TeamCount { get; }
        public int ScoreLimit { get; }
        public int TimeLimitActiveTicks { get; }

        public ModeRules(ModeId id, string displayName, byte teamCount, int scoreLimit, int timeLimitActiveTicks)
        {
            GameRequire.That(teamCount >= 2 && teamCount <= GameContractVersion.MaxTeams, nameof(teamCount));
            GameRequire.That(scoreLimit > 0, nameof(scoreLimit));
            GameRequire.That(timeLimitActiveTicks > 0, nameof(timeLimitActiveTicks));
            Id = id;
            DisplayName = GameRequire.Display(displayName, nameof(displayName), GameContractVersion.MaxDisplayName);
            TeamCount = teamCount;
            ScoreLimit = scoreLimit;
            TimeLimitActiveTicks = timeLimitActiveTicks;
        }

        /// <summary>Game-v1 Team Deathmatch: two teams, first to 150 kills or 15 active minutes.</summary>
        public static ModeRules TeamDeathmatch { get; } = new ModeRules(
            WellKnownModes.TeamDeathmatch, "Team Deathmatch", 2, GameContractVersion.ScoreLimit, GameContractVersion.TimeLimitActiveTicks);

        /// <summary>Every registered mode, in menu order.</summary>
        public static IReadOnlyList<ModeRules> All { get; } =
            new FrozenList<ModeRules>(new[] { TeamDeathmatch }, GameContractVersion.MaxModesPerMap);

        public static bool TryGet(ModeId id, out ModeRules rules)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id) { rules = All[i]; return true; }
            rules = TeamDeathmatch;
            return false;
        }

        /// <summary>Registered rules for <paramref name="id"/>, or Team Deathmatch for an unregistered id (catalog validation rejects those).</summary>
        public static ModeRules ForOrDefault(ModeId id)
        {
            TryGet(id, out var rules);
            return rules;
        }
    }
}
