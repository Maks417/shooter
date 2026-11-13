#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Simulation.AI
{
    public readonly struct CoverSite
    {
        public ulong Id { get; }
        public Float3 Anchor { get; }
        public Float3 Peek { get; }
        public CoverSite(ulong id, Float3 anchor, Float3 peek)
        {
            if (id == 0) throw new ArgumentOutOfRangeException(nameof(id));
            Id = id; Anchor = anchor; Peek = peek;
        }
    }

    /// <summary>Team AI: ≤16 vision rays and ≤2 new paths per tick; cover/reload/stuck through normal commands. Capacity 11 for 6v6.</summary>
    public sealed class CombatAiDirector
    {
        public const int MaxBots = 11;
        public const int MaxNewPathsPerTick = 2;
        public const float StuckDistance = 0.2f;
        public const uint StuckTicksThreshold = 120; // 2 s at 60 Hz
        public const uint ReplanCooldownTicks = 30;

        private readonly AiBlackboard[] boards = new AiBlackboard[MaxBots];
        private readonly PerceptionSystem perception;
        private readonly CoverReservationBoard covers = new CoverReservationBoard();
        private readonly INavigation navigation;
        private readonly List<CoverSite> coverSites = new List<CoverSite>();
        private readonly AiCounters counters = new AiCounters();
        private int botCount;
        private uint tick;
        private int pathsThisTick;
        private uint navRevision = 1;

        public AiCounters Counters => counters;
        public int BotCount => botCount;
        public CoverReservationBoard CoverBoard => covers;
        public uint NavRevision { get => navRevision; set => navRevision = value == 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value; }

        public CombatAiDirector(IWorldQueries world, INavigation navigation, uint visionMask, uint navRevision = 1)
        {
            perception = new PerceptionSystem(world, visionMask);
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            this.navRevision = navRevision == 0 ? throw new ArgumentOutOfRangeException(nameof(navRevision)) : navRevision;
            for (int i = 0; i < MaxBots; i++) boards[i] = new AiBlackboard();
        }

        public void SetCoverSites(IReadOnlyList<CoverSite> sites)
        {
            coverSites.Clear();
            if (sites == null) return;
            for (int i = 0; i < sites.Count; i++) coverSites.Add(sites[i]);
        }

        public AiBlackboard RegisterBot(EntityId id, Float3 feet, float yaw)
        {
            if (botCount >= MaxBots) throw new InvalidOperationException("Team AI capacity exhausted");
            var board = boards[botCount++];
            board.Self = id;
            board.Feet = feet;
            board.Yaw = yaw;
            board.Alive = true;
            board.State = AiState.Patrol;
            board.Health = 100;
            board.MagAmmo = 30;
            board.StateEnteredTick = tick;
            return board;
        }

        public AiBlackboard GetBoard(int index)
        {
            if (index < 0 || index >= botCount) throw new ArgumentOutOfRangeException(nameof(index));
            return boards[index];
        }

        public void NotifyDamage(EntityId victim, Float3 source)
        {
            for (int i = 0; i < botCount; i++)
            {
                if (boards[i].Self != victim) continue;
                boards[i].LastHeard = source;
                boards[i].LastHeardTick = tick;
                if (boards[i].State != AiState.Dead) Enter(boards[i], AiState.Investigate);
                return;
            }
        }

        public void NotifyDeath(EntityId id)
        {
            for (int i = 0; i < botCount; i++)
            {
                if (boards[i].Self != id) continue;
                ReleaseBotResources(boards[i]);
                boards[i].Alive = false;
                Enter(boards[i], AiState.Dead);
                return;
            }
        }

        /// <summary>Plan next-tick commands for all living bots. Caller feeds them through normal simulation command path.</summary>
        public int Plan(uint simulationTick, SenseTarget? playerTarget, InputCommand[] destination, int destinationOffset)
        {
            SenseTarget[]? single = null;
            int count = 0;
            if (playerTarget.HasValue)
            {
                single = new[] { playerTarget.Value };
                count = 1;
            }
            return PlanEnemies(simulationTick, single, count, destination, destinationOffset);
        }

        /// <summary>Multi-enemy sensing with global ray budget and round-robin candidate order.</summary>
        public int PlanEnemies(uint simulationTick, SenseTarget[]? enemies, int enemyCount, InputCommand[] destination, int destinationOffset)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (enemyCount < 0 || (enemies == null && enemyCount != 0) || (enemies != null && enemyCount > enemies.Length))
                throw new ArgumentOutOfRangeException(nameof(enemyCount));
            tick = simulationTick;
            pathsThisTick = 0;
            counters.VisionRaysThisTick = 0;
            counters.PathsRequestedThisTick = 0;
            perception.BeginTick();
            PollPaths();

            int written = 0;
            for (int i = 0; i < botCount; i++)
            {
                var board = boards[i];
                if (!board.Alive || board.State == AiState.Dead) continue;
                UpdateStuck(board);
                if (enemies != null && enemyCount > 0)
                {
                    var eye = new Float3(board.Feet.X, board.Feet.Y + 1.65f, board.Feet.Z);
                    bool any = false;
                    for (int e = 0; e < enemyCount; e++)
                    {
                        int idx = (i + e) % enemyCount;
                        var candidate = enemies[idx];
                        if (candidate.Id.Value == board.Self.Value) continue;
                        if (perception.TrySense(board, eye, board.Yaw, candidate, counters, out bool acquired) && acquired)
                        {
                            board.LastSeenTick = tick;
                            board.LastSeenEnemy = candidate.Point;
                            board.HasVisibleTarget = true;
                            board.VisibleTarget = candidate.Point;
                            board.Confidence = 1f;
                            if (board.ReactionReadyTick == 0)
                                board.ReactionReadyTick = tick + 15 + (uint)(board.Self.Value % 16);
                            if (tick >= board.ReactionReadyTick && board.State != AiState.Engage && board.State != AiState.Reload)
                                Enter(board, AiState.Engage);
                            any = true;
                            break;
                        }
                    }
                    if (!any && board.HasVisibleTarget == false && board.Confidence > 0)
                    {
                        board.Confidence = Math.Max(0, board.Confidence - 0.02f);
                        if (board.Confidence <= 0 && board.State == AiState.Engage) Enter(board, AiState.Search);
                    }
                }

                if (board.MagAmmo <= 0 && board.State != AiState.Reload) Enter(board, AiState.Reload);
                Decide(board);
                if (destinationOffset + written >= destination.Length) break;
                destination[destinationOffset + written] = BuildCommand(board, simulationTick, (uint)(written + 1));
                written++;
            }
            counters.MaxPathQueue = Math.Max(counters.MaxPathQueue, pathsThisTick);
            counters.VisionRaysThisTick = perception.RaysUsedThisTick;
            return written;
        }

        private void Decide(AiBlackboard board)
        {
            switch (board.State)
            {
                case AiState.Patrol:
                    EnsurePath(board, PatrolPoint(board));
                    break;
                case AiState.Investigate:
                    if (board.LastHeard.HasValue) EnsurePath(board, board.LastHeard.Value);
                    else Enter(board, AiState.Patrol);
                    break;
                case AiState.Engage:
                    if (board.HasVisibleTarget && board.VisibleTarget.HasValue)
                        AimAt(board, board.VisibleTarget.Value);
                    else if (board.LastSeenEnemy.HasValue)
                        EnsurePath(board, board.LastSeenEnemy.Value);
                    break;
                case AiState.SeekCover:
                    if (!board.CoverReservation.HasValue) ClaimBestCover(board);
                    if (board.CoverReservation.HasValue)
                    {
                        var site = FindCover(board.CoverReservation.Value);
                        if (site.HasValue) EnsurePath(board, site.Value.Anchor);
                    }
                    break;
                case AiState.Reload:
                    board.MagAmmo = 30;
                    Enter(board, board.HasVisibleTarget ? AiState.Engage : AiState.SeekCover);
                    break;
                case AiState.Reposition:
                case AiState.Search:
                    if (board.LastSeenEnemy.HasValue) EnsurePath(board, board.LastSeenEnemy.Value);
                    else Enter(board, AiState.Patrol);
                    break;
            }
        }

        private void EnsurePath(AiBlackboard board, Float3 goal)
        {
            if (board.PathTicket.HasValue) return;
            if (pathsThisTick >= MaxNewPathsPerTick) return;
            if (tick - board.LastReplanTick < ReplanCooldownTicks && board.PathGoal.HasValue) return;
            var ticket = navigation.RequestPath(board.Self, goal, navRevision);
            // Use navigation revision from ticket
            board.PathTicket = ticket;
            board.PathGoal = goal;
            board.PathRequestedTick = tick;
            board.LastReplanTick = tick;
            pathsThisTick++;
            counters.PathsRequestedThisTick++;
        }

        private void PollPaths()
        {
            var buffer = new PathBuffer(32);
            for (int i = 0; i < botCount; i++)
            {
                var board = boards[i];
                if (!board.PathTicket.HasValue) continue;
                var result = navigation.Poll(board.PathTicket.Value, buffer);
                if (result.Status == PathStatus.Pending) continue;
                if (result.Status == PathStatus.Stale || result.Status == PathStatus.Cancelled || result.Status == PathStatus.Failed)
                {
                    board.PathTicket = null;
                    continue;
                }
                // Path complete — consume ticket; movement uses goal for command generation.
                board.PathTicket = null;
            }
        }

        private void UpdateStuck(AiBlackboard board)
        {
            float dx = board.Feet.X - board.StuckAnchor.X;
            float dz = board.Feet.Z - board.StuckAnchor.Z;
            if (dx * dx + dz * dz >= StuckDistance * StuckDistance)
            {
                board.StuckAnchor = board.Feet;
                board.StuckTicks = 0;
                return;
            }
            board.StuckTicks++;
            if (board.StuckTicks < StuckTicksThreshold) return;
            counters.StuckEvents++;
            board.StuckTicks = 0;
            board.StuckFailures++;
            board.PathTicket = null;
            if (board.StuckFailures >= 3) Enter(board, AiState.Search);
            else if (board.CoverReservation.HasValue) Enter(board, AiState.Reposition);
            else Enter(board, AiState.SeekCover);
        }

        private void ClaimBestCover(AiBlackboard board)
        {
            for (int i = 0; i < coverSites.Count; i++)
            {
                var site = coverSites[i];
                if (covers.IsClaimed(site.Id)) continue;
                if (!covers.TryClaim(site.Id, board.Self)) continue;
                board.CoverReservation = site.Id;
                counters.CoverClaims++;
                return;
            }
        }

        private CoverSite? FindCover(ulong id)
        {
            for (int i = 0; i < coverSites.Count; i++)
                if (coverSites[i].Id == id) return coverSites[i];
            return null;
        }

        private void ReleaseBotResources(AiBlackboard board)
        {
            if (board.PathTicket.HasValue)
            {
                navigation.Cancel(board.PathTicket.Value);
                board.PathTicket = null;
            }
            if (board.CoverReservation.HasValue)
            {
                if (covers.Release(board.CoverReservation.Value, board.Self)) counters.CoverReleases++;
                else counters.OrphanReleases++;
                board.CoverReservation = null;
            }
            else
            {
                int n = covers.ReleaseAll(board.Self);
                counters.CoverReleases += n;
                if (n > 0) counters.OrphanReleases += n;
            }
        }

        private void Enter(AiBlackboard board, AiState state)
        {
            if (board.State == state) return;
            board.State = state;
            board.StateEnteredTick = tick;
            counters.StateChanges++;
            if (state == AiState.SeekCover) ClaimBestCover(board);
        }

        private static Float3 PatrolPoint(AiBlackboard board)
        {
            // Deterministic local orbit based on entity id.
            double a = (board.Self.Value % 8) * (Math.PI / 4);
            return new Float3(board.Feet.X + (float)Math.Cos(a) * 4f, board.Feet.Y, board.Feet.Z + (float)Math.Sin(a) * 4f);
        }

        private static void AimAt(AiBlackboard board, Float3 target)
        {
            float dx = target.X - board.Feet.X, dy = target.Y - (board.Feet.Y + 1.65f), dz = target.Z - board.Feet.Z;
            board.Yaw = MathF.Atan2(dx, dz);
            float flat = MathF.Sqrt(dx * dx + dz * dz);
            board.Pitch = flat < 1e-5f ? 0 : -MathF.Atan2(dy, flat);
        }

        private InputCommand BuildCommand(AiBlackboard board, uint simulationTick, uint sequence)
        {
            float mx = 0, my = 0;
            var buttons = InputButtons.None;
            if (board.PathGoal.HasValue && board.State != AiState.Reload)
            {
                float dx = board.PathGoal.Value.X - board.Feet.X;
                float dz = board.PathGoal.Value.Z - board.Feet.Z;
                float len = MathF.Sqrt(dx * dx + dz * dz);
                if (len > 0.2f)
                {
                    // Move relative to aim yaw: +Y forward, +X right in command space.
                    float fx = MathF.Sin(board.Yaw), fz = MathF.Cos(board.Yaw);
                    float rx = fz, rz = -fx;
                    float worldX = dx / len, worldZ = dz / len;
                    mx = worldX * rx + worldZ * rz;
                    my = worldX * fx + worldZ * fz;
                    float mlen = MathF.Sqrt(mx * mx + my * my);
                    if (mlen > 1e-5f) { mx /= mlen; my /= mlen; }
                    else { mx = 0; my = 0; }
                    // Keep strictly inside the contract unit-disk tolerance (≤1.0000001).
                    double disk = (double)mx * mx + (double)my * my;
                    if (disk > 0.999999) { double s = Math.Sqrt(0.999999 / disk); mx = (float)(mx * s); my = (float)(my * s); }
                }
            }
            if (board.State == AiState.Engage && board.HasVisibleTarget && board.MagAmmo > 0 && tick >= board.ReactionReadyTick)
            {
                buttons |= InputButtons.FireHeld | InputButtons.FirePressed;
                counters.ShotsRequested++;
            }
            if (board.State == AiState.Reload) buttons |= InputButtons.ReloadPressed;
            return new InputCommand(board.Self, simulationTick, sequence, mx, my, board.Yaw, board.Pitch, buttons, 0);
        }
    }
}
