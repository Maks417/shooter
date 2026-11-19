#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Contracts;
using Shooter.Game.Contracts;

namespace Shooter.Game.Session
{
    /// <summary>Read-only actor state query: the only lifecycle capability a presentation consumer gets.</summary>
    public interface IActorStateReader
    {
        bool TryGetActorState(EntityId entity, out ActorLifecycleState state);
    }

    /// <summary>
    /// T195: everything Presentation may read from the offline session. Presentation references only this assembly, so the
    /// compiler (not convention) keeps it a read-only consumer: no lifecycle mutators, no simulation types.
    /// </summary>
    public interface IGameSessionView : IActorStateReader
    {
        ulong AuthoritySessionId { get; }
        ulong SessionEpoch { get; }
        GameMatchPhase Phase { get; }
        SessionPauseState PauseState { get; }
        GameOperationResult? TerminalFault { get; }
        uint CompletedTick { get; }
        EntityId HumanEntityId { get; }
        int PreMatchCountdownTicks { get; }
        int PreMatchTicksRemaining { get; }
        int CareerRank { get; }
        LocalMatchTally LocalTally { get; }

        MatchViewSnapshot? TryAcquireSnapshot();
        MatchResults? TryAcquireResults();
        EventLease? TryLeaseCombatEvents();
        int CopyHitVolumes(List<HitVolume> destination);
        void DrainLocalScoreAwards(List<LocalScoreAward> destination);
        int DisplayRankOf(ParticipantId participant, bool isHuman);

        bool TryGetMovementView(EntityId entity, out LocalMovementView view);
        bool TryGetPrimarySight(EntityId entity, out DefinitionId sight);
        bool TryGetArmor(EntityId entity, out ushort armor);
        float HumanHipSpreadRadians(DefinitionId weapon);
        bool TryGetHumanPickupPrompt(out DroppedWeaponView view, out bool ammoTopUp);
        int DroppedWeaponCount { get; }
        bool TryGetDroppedWeapon(int index, out DroppedWeaponView view);
        int CosmeticCorpseCount { get; }
        bool TryGetCosmeticCorpse(int index, out ActorLifecycleState state);
    }

    /// <summary>T195: the shell's match lifecycle and pre-match setup calls (the product shell owns load/start/stop).</summary>
    public interface IGameSessionControl : IGameSessionView
    {
        new int PreMatchCountdownTicks { get; set; }
        new int CareerRank { get; set; }
        Task<GameOperationResult> BeginLoadAsync(SessionRequest request, CancellationToken cancel);
        GameOperationResult SignalReady();
        GameOperationResult Start();
        GameOperationResult Pause();
        GameOperationResult Resume();
        GameOperationResult RequestAbort();
        GameOperationResult Stop();

        /// <summary>ADR-107/108/119 Armory loadout with the career scales (per mille; spare magazines 0..2).</summary>
        void UseArmoryLoadout(DefinitionId primary, DefinitionId primarySight, DefinitionId sidearm,
            ushort recoilPerMille, ushort reloadPerMille, ushort adsSpreadPerMille, ushort equipPerMille, byte extraMagazines,
            ushort hipSpreadPerMille, ushort pelletSpreadPerMille, ushort manualCyclePerMille, ushort moveSpeedPerMille);
    }

    /// <summary>T195: what the Armory offers, in contract ids (implemented over the authority weapon catalog).</summary>
    public interface IArmoryCatalogView
    {
        IReadOnlyList<DefinitionId> Primaries { get; }
        IReadOnlyList<DefinitionId> Sidearms { get; }
        bool TryGetStats(DefinitionId weapon, out int damage, out int pellets, out int magazine, out int roundsPerMinute, out string fireMode);
        /// <summary>Unbuffed hip spread for a weapon when no session is bound.</summary>
        float BaseHipSpreadRadians(DefinitionId weapon);
    }
}
