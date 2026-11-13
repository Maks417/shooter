#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Simulation.Motor;

namespace Shooter.Simulation.AI
{
    /// <summary>
    /// Game-v1 ITeamBotPlanner adapter. M8 registers up to eight bots; M9 uses nine/eleven.
    /// </summary>
    public sealed class TeamBotPlanner : ITeamBotPlanner
    {
        private readonly CombatAiDirector director;
        private readonly Dictionary<ulong, uint> sequences = new Dictionary<ulong, uint>();
        private readonly SenseTarget[] enemyScratch = new SenseTarget[GameContractVersion.MaxParticipants];
        private readonly InputCommand[] planScratch;

        public TeamBotPlanner(IWorldQueries world, INavigation navigation, uint visionMask = 0, uint navRevision = 1)
        {
            if (visionMask == 0) visionMask = CollisionLayers.BulletObstructionMask;
            director = new CombatAiDirector(world, navigation, visionMask, navRevision);
            planScratch = new InputCommand[GameContractVersion.MaxActiveActors];
        }

        public CombatAiDirector Director => director;
        public AiCounters Counters => director.Counters;

        public void RegisterBot(EntityId id, Float3 feet, float yaw)
        {
            director.RegisterBot(id, feet, yaw);
            sequences[id.Value] = 0;
        }

        public void SetCoverSites(IReadOnlyList<CoverSite> sites) => director.SetCoverSites(sites);

        public int Plan(uint nextTick, MatchViewSnapshot match, IActorLifecycle lifecycle, CommandBuffer destination)
        {
            if (match == null || lifecycle == null || destination == null) throw new ArgumentNullException(nameof(match));
            SyncFromLifecycle(lifecycle);

            int enemyCount = 0;
            for (int i = 0; i < match.Participants.Count; i++)
            {
                var p = match.Participants[i];
                if (!p.Alive) continue;
                if (!lifecycle.TryGetActorState(p.EntityId, out var state) || !state.Alive) continue;
                enemyScratch[enemyCount++] = new SenseTarget(
                    p.EntityId,
                    new Float3(state.Feet.X, state.Feet.Y + 1.65f, state.Feet.Z));
            }

            int raw = director.PlanEnemies(nextTick, enemyScratch, enemyCount, planScratch, 0);
            int written = 0;
            for (int i = 0; i < raw; i++)
            {
                var cmd = planScratch[i];
                uint seq = sequences.TryGetValue(cmd.EntityId.Value, out var prev) ? prev + 1 : 1;
                sequences[cmd.EntityId.Value] = seq;
                if (written >= destination.Capacity) break;
                destination[written] = new InputCommand(
                    cmd.EntityId, nextTick, seq, cmd.MoveX, cmd.MoveY, cmd.AimYaw, cmd.AimPitch, cmd.Buttons, cmd.SelectedSlot);
                written++;
            }
            return written;
        }

        public void ResetLife(EntityId entity)
        {
            director.NotifyDeath(entity);
            sequences[entity.Value] = 0;
        }

        public void CancelAll()
        {
            for (int i = 0; i < director.BotCount; i++)
            {
                var board = director.GetBoard(i);
                if (board.PathTicket.HasValue)
                {
                    board.PathTicket = null;
                }
                board.CoverReservation = null;
            }
        }

        private void SyncFromLifecycle(IActorLifecycle lifecycle)
        {
            for (int i = 0; i < director.BotCount; i++)
            {
                var board = director.GetBoard(i);
                if (!lifecycle.TryGetActorState(board.Self, out var state))
                {
                    if (board.Alive) director.NotifyDeath(board.Self);
                    continue;
                }
                if (!state.Alive && board.Alive) director.NotifyDeath(board.Self);
                else if (state.Alive)
                {
                    board.Alive = true;
                    board.Feet = state.Feet;
                    board.Yaw = state.AimYaw;
                    board.Health = state.Health;
                    board.MagAmmo = state.Magazine;
                }
            }
        }
    }
}
