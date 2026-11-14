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

        public void RegisterBot(EntityId id, Float3 feet, float yaw, byte team = 0)
        {
            director.RegisterBot(id, feet, yaw, team);
            sequences[id.Value] = 0;
        }

        public void SetCoverSites(IReadOnlyList<CoverSite> sites) => director.SetCoverSites(sites);
        public void SetNavigationDestinations(
            IReadOnlyList<Float3>? patrol,
            IReadOnlyList<Float3>? combat) =>
            director.SetNavigationDestinations(patrol, combat);

        public int Plan(uint nextTick, MatchViewSnapshot match, IActorLifecycle lifecycle, CommandBuffer destination)
        {
            if (match == null || lifecycle == null || destination == null) throw new ArgumentNullException(nameof(match));
            SyncFromLifecycle(lifecycle);

            // Living participants only; opposite-team filtering happens per bot before perception.
            int enemyCount = 0;
            for (int i = 0; i < match.Participants.Count; i++)
            {
                var p = match.Participants[i];
                if (!p.Alive) continue;
                if (!lifecycle.TryGetActorState(p.EntityId, out var state) || !state.Alive) continue;
                enemyScratch[enemyCount++] = new SenseTarget(
                    p.EntityId,
                    new Float3(state.Feet.X, state.Feet.Y + 1.65f, state.Feet.Z),
                    state.Team,
                    state.Alive);
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
            sequences.Remove(entity.Value);
        }

        /// <summary>
        /// T053 life rebinding: cancel old resources, clear Dead/reaction/target, and start a
        /// fresh command sequence for the replacement EntityId.
        /// </summary>
        public bool TryReplaceLife(EntityId previous, EntityId next, Float3 feet, float yaw, byte team)
        {
            if (!director.TryReplaceLife(previous, next, feet, yaw, team)) return false;
            sequences.Remove(previous.Value);
            sequences[next.Value] = 0;
            return true;
        }

        public uint GetSequence(EntityId entity) =>
            sequences.TryGetValue(entity.Value, out var seq) ? seq : 0u;

        public void CancelAll() => director.CancelAll();

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
                    // Authority owns ammo/health; never fabricate reload completion here.
                    board.Alive = true;
                    board.Team = state.Team;
                    board.Feet = state.Feet;
                    board.Yaw = state.AimYaw;
                    board.Health = state.Health;
                    board.MagAmmo = state.Magazine;
                    board.ReserveAmmo = state.Reserve;
                    if (board.State == AiState.Dead)
                    {
                        board.State = AiState.Patrol;
                        board.VisibleTargetId = null;
                        board.HasVisibleTarget = false;
                        board.VisibleTarget = null;
                        board.LastSeenEnemy = null;
                        board.Confidence = 0;
                        board.ReactionReadyTick = 0;
                    }
                }
            }
        }
    }
}
