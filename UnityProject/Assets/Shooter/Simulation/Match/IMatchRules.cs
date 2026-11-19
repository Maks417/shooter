#nullable enable
using Shooter.Game.Contracts;

namespace Shooter.Simulation.Match
{
    /// <summary>
    /// ADR-122: everything the offline session needs from a mode's match rules: the frozen <see cref="ITeamMatch"/> port
    /// plus identity, start and abort. Team Deathmatch is one implementation; a new mode adds another.
    /// </summary>
    public interface IMatchRules : ITeamMatch
    {
        void ConfigureIdentities(string catalog, string gameplay, string build);
        void MarkPlaying(ulong sessionId, uint simulationTick);
        void Abort(ulong sessionId);
    }
}
