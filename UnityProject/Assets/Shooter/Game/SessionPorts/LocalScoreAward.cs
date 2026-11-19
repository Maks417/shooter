#nullable enable
using System;

namespace Shooter.Game.Session
{
    /// <summary>Presentation-facing personal score award for the local human (ADR-059 / ADR-073). Not a Game-v1 DTO.</summary>
    public enum LocalScoreAwardKind : byte
    {
        Kill = 1,
        Assist = 2,
        /// <summary>Extra HUD badge only; Points stay 0 (ADR-073).</summary>
        Headshot = 3
    }

    public readonly struct LocalScoreAward
    {
        public LocalScoreAwardKind Kind { get; }
        public uint Points { get; }
        public string VictimName { get; }
        /// <summary>Life-scoped local kill count for Kill awards (1+); 0 for Assist/Headshot.</summary>
        public byte KillStreak { get; }
        /// <summary>True when the fatal hit region was head (Kill may also set this; Headshot kind always true).</summary>
        public bool Headshot { get; }

        public LocalScoreAward(
            LocalScoreAwardKind kind,
            uint points,
            string victimName,
            byte killStreak = 0,
            bool headshot = false)
        {
            Kind = kind;
            Points = points;
            VictimName = victimName ?? "";
            KillStreak = killStreak;
            Headshot = headshot;
        }
    }
}
