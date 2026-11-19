#nullable enable

namespace Shooter.Game.Session
{
    /// <summary>
    /// ADR-108 local-human match counts kept beside the frozen results rows (which carry only kills, deaths and score):
    /// assists credited, enemy kills finished with a head hit and the best kill streak in one life.
    /// </summary>
    public readonly struct LocalMatchTally
    {
        public uint Assists { get; }
        public uint Headshots { get; }
        public uint BestStreak { get; }

        public LocalMatchTally(uint assists, uint headshots, uint bestStreak)
        {
            Assists = assists;
            Headshots = headshots;
            BestStreak = bestStreak;
        }
    }
}
