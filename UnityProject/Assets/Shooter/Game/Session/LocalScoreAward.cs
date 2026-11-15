#nullable enable
using System;

namespace Shooter.Game.Session
{
    /// <summary>Presentation-facing personal score award for the local human (ADR-059). Not a Game-v1 DTO.</summary>
    public enum LocalScoreAwardKind : byte
    {
        Kill = 1,
        Assist = 2
    }

    public readonly struct LocalScoreAward
    {
        public LocalScoreAwardKind Kind { get; }
        public uint Points { get; }
        public string VictimName { get; }

        public LocalScoreAward(LocalScoreAwardKind kind, uint points, string victimName)
        {
            Kind = kind;
            Points = points;
            VictimName = victimName ?? "";
        }
    }
}
