#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Motor;

namespace Shooter.Simulation.AI
{
    /// <summary>Bot navigation: destinations, path requests and corners, stuck handling, relocation and cover claims.</summary>
    public sealed partial class CombatAiDirector
    {
        private void MoveToCover(AiBlackboard board)
        {
            var point = board.CoverPoint!.Value;
            float dx = point.X - board.Feet.X, dz = point.Z - board.Feet.Z;
            if (dx * dx + dz * dz <= AiTuning.CoverArrivalMeters * AiTuning.CoverArrivalMeters)
            {
                board.InCover = true;
                board.InCoverSinceTick = tick;
                DropPath(board);
                return;
            }
            EnsurePath(board, point, retarget: true);
        }

        /// <summary>Last seen position pushed forward along the target's observed motion (≤1 s, ≤5 m).</summary>
        private static Float3 SearchPoint(AiBlackboard board)
        {
            var p = board.LastSeenEnemy!.Value;
            float vx = board.TargetVelocity.X, vz = board.TargetVelocity.Z;
            float len = MathF.Sqrt(vx * vx + vz * vz);
            if (len > 5f) { vx *= 5f / len; vz *= 5f / len; }
            return new Float3(p.X + vx, p.Y, p.Z + vz);
        }

        /// <summary>Combat destination that no ally is already heading for (or standing at).</summary>
        private Float3 ChooseCombatDestination(AiBlackboard board)
        {
            if (board.PathGoal.HasValue) return board.PathGoal.Value;
            return combatDestinations[ChooseSpreadDestination(board, combatDestinations, -1, -1)];
        }

        /// <summary>
        /// T085/T090 team spread: score every destination by how far it is from allies' goals, positions and
        /// active path corners (shared corridors), by a preferred leg length, and by a seeded per-bot preference;
        /// never repeat the last two picks. Deterministic; runs only when a bot needs a new destination.
        /// </summary>
        private int ChooseSpreadDestination(AiBlackboard board, List<Float3> points, int last, int previous)
        {
            int count = points.Count;
            if (count == 1) return 0;
            int best = Rotate(board, count);
            float bestScore = float.MinValue;
            bool hasEnemyHome = EnemyHome(board, out Float3 enemyHome);
            for (int k = 0; k < count; k++)
            {
                if ((k == last || k == previous) && count > 2) continue;
                var c = points[k];
                float dx = c.X - board.Feet.X, dz = c.Z - board.Feet.Z;
                float leg = MathF.Sqrt(dx * dx + dz * dz);
                if (leg < 3f) continue;
                float score = -Math.Abs(leg - AiTuning.PatrolPreferredMeters) / AiTuning.PatrolPreferredMeters;
                score += 0.6f * AiNoise.Unit(board.Self.Value, (ulong)(uint)board.DestinationCursor * 131UL + (ulong)k, AiNoise.SaltPatrol);
                score += TeamTerrainScore(board, c, hasEnemyHome, enemyHome);
                for (int i = 0; i < botCount; i++)
                {
                    var other = boards[i];
                    if (other == board || !other.Alive || other.State == AiState.Dead) continue;
                    if (board.Team != 0 && !PerceptionSystem.IsAlliedTeam(board.Team, other.Team)) continue;
                    var goal = other.State == AiState.Patrol && other.PatrolGoal.HasValue ? other.PatrolGoal : other.PathGoal;
                    if (goal.HasValue && Within(goal.Value, c, AiTuning.SpreadMeters)) score -= 2f;
                    if (Within(other.Feet, c, AiTuning.SpreadMeters * 0.5f)) score -= 0.5f;
                    // T090: penalize destinations that sit on an ally's current path corridor.
                    int corners = other.PathCornerCount;
                    for (int p = other.PathCornerIndex; p < corners; p++)
                    {
                        if (!Within(other.PathCorners[p], c, AiTuning.RouteCorridorMeters)) continue;
                        score -= AiTuning.RouteCorridorPenalty;
                        counters.RouteCorridorPenalties++;
                        break;
                    }
                }
                if (score > bestScore) { bestScore = score; best = k; }
            }
            return best;
        }

        // ---------------------------------------------------------------- navigation

        /// <param name="retarget">
        /// Urgent goals (stimuli, last contact, cover, advancing on a visible enemy) replace an active route
        /// whose goal is elsewhere, within the replan cooldown and the per-tick path budget. Routine
        /// patrol/combat destinations keep the current route until it finishes.
        /// </param>
        private void EnsurePath(AiBlackboard board, Float3 goal, bool retarget = false)
        {
            if (board.PathTicket.HasValue || board.PathCornerIndex < board.PathCornerCount)
            {
                if (!retarget || !board.PathGoal.HasValue || Within(board.PathGoal.Value, goal, RetargetDistance)) return;
                if (pathsThisTick >= MaxNewPathsPerTick || tick - board.LastReplanTick < ReplanCooldownTicks) return;
                DropPath(board);
            }
            if (pathsThisTick >= MaxNewPathsPerTick) return;
            if (tick < board.RetryAfterTick) return;
            // ADR-128: one new route per bot per ReplanCooldownTicks, also after arriving (arrival clears PathGoal).
            // A goal at the bot's feet otherwise gave a one-corner path, an immediate arrival and a new request on the
            // next tick, so bots in combat filled the per-tick budget with synchronous native path solves every tick.
            if (board.LastReplanTick != 0 && tick - board.LastReplanTick < ReplanCooldownTicks) return;
            var ticket = navigation.RequestPath(board.Self, goal, navRevision);
            board.PathTicket = ticket;
            board.PathGoal = goal;
            board.PathRequestedTick = tick;
            board.LastReplanTick = tick;
            pathsThisTick++;
            counters.PathsRequestedThisTick++;
        }

        private void DropPath(AiBlackboard board)
        {
            if (board.PathTicket.HasValue)
            {
                navigation.Cancel(board.PathTicket.Value);
                board.PathTicket = null;
            }
            board.PathCornerCount = 0;
            board.PathCornerIndex = 0;
            board.PathGoal = null;
        }

        private void PollPaths()
        {
            for (int i = 0; i < botCount; i++)
            {
                var board = boards[i];
                if (!board.PathTicket.HasValue) continue;
                if (unchecked(tick - board.PathRequestedTick) >= PathTimeoutTicks)
                {
                    navigation.Cancel(board.PathTicket.Value);
                    board.PathTicket = null;
                    board.PathGoal = null;
                    FailPath(board);
                    continue;
                }
                var result = navigation.Poll(board.PathTicket.Value, pathBuffer);
                if (result.Status == PathStatus.Pending) continue;
                if (result.Status == PathStatus.Stale || result.Status == PathStatus.Cancelled || result.Status == PathStatus.Failed)
                {
                    board.PathTicket = null;
                    board.PathGoal = null;
                    if (result.Status == PathStatus.Failed) FailPath(board);
                    continue;
                }
                if (result.Status == PathStatus.Partial) counters.PathsPartial++;
                else counters.PathsCompleted++;
                board.PathCornerCount = result.WrittenCount;
                board.PathCornerIndex = 0;
                for (int c = 0; c < result.WrittenCount; c++)
                    board.PathCorners[c] = pathBuffer[c];
                board.PathTicket = null;
                AdvancePathCorners(board);
            }
        }

        /// <summary>Back off and rotate to the next destination so an unreachable goal is not retried every tick.</summary>
        private void FailPath(AiBlackboard board)
        {
            counters.PathsFailed++;
            board.RetryAfterTick = tick + ReplanCooldownTicks;
            board.DestinationCursor++;
            if (board.CoverPoint.HasValue && !board.InCover && board.CoverReservation.HasValue &&
                (board.CoverReservation.Value & CoverFinder.DynamicCoverIdFlag) != 0)
            {
                // Unreachable probe point: give it up so the bot does not hold an orphan claim.
                ReleaseCover(board);
            }
        }

        private static void AdvancePathCorners(AiBlackboard board)
        {
            float arrivalSq = CornerArrivalDistance * CornerArrivalDistance;
            while (board.PathCornerIndex < board.PathCornerCount)
            {
                var corner = board.PathCorners[board.PathCornerIndex];
                float dx = corner.X - board.Feet.X;
                float dz = corner.Z - board.Feet.Z;
                if (dx * dx + dz * dz > arrivalSq) break;
                board.PathCornerIndex++;
            }
            if (board.PathCornerIndex >= board.PathCornerCount && board.PathCornerCount > 0)
            {
                // Arrived: rotate patrol/search destinations; an investigated last-seen or heard point is spent.
                board.DestinationCursor++;
                if (board.LastSeenEnemy.HasValue && !board.HasVisibleTarget)
                {
                    float sx = board.LastSeenEnemy.Value.X - board.Feet.X;
                    float sz = board.LastSeenEnemy.Value.Z - board.Feet.Z;
                    var predicted = SearchPoint(board);
                    float px = predicted.X - board.Feet.X, pz = predicted.Z - board.Feet.Z;
                    if (sx * sx + sz * sz <= 2.25f || px * px + pz * pz <= 4f) board.LastSeenEnemy = null;
                }
                if (board.LastHeard.HasValue && board.State == AiState.Investigate)
                {
                    float hx = board.LastHeard.Value.X - board.Feet.X;
                    float hz = board.LastHeard.Value.Z - board.Feet.Z;
                    if (hx * hx + hz * hz <= 4f)
                    {
                        board.LastHeard = null;
                        board.Heard = HeardClass.None;
                    }
                }
                board.PathCornerCount = 0;
                board.PathCornerIndex = 0;
                board.PathGoal = null;
                board.StuckTicks = 0;
                board.StuckFailures = 0;
            }
        }

        private static void UpdateSpeed(AiBlackboard board)
        {
            if (board.HasPrevFeet)
            {
                float dx = board.Feet.X - board.PrevFeet.X, dz = board.Feet.Z - board.PrevFeet.Z;
                board.SpeedMps = MathF.Sqrt(dx * dx + dz * dz) * 60f;
            }
            else board.SpeedMps = 0;
            board.PrevFeet = board.Feet;
            board.HasPrevFeet = true;
        }

        private void UpdateStuck(AiBlackboard board)
        {
            bool hasPath = board.PathTicket.HasValue || board.PathCornerIndex < board.PathCornerCount;
            bool moveIntent = WantsHorizontalProgress(board);
            // No path and no move intent: idle is fine. Move intent without progress still counts as stuck
            // (bots wedged in geometry often drop their path ticket).
            if (!hasPath && !moveIntent)
            {
                board.StuckAnchor = board.Feet;
                board.StuckTicks = 0;
                return;
            }
            float dx = board.Feet.X - board.StuckAnchor.X;
            float dz = board.Feet.Z - board.StuckAnchor.Z;
            if (dx * dx + dz * dz >= StuckDistance * StuckDistance)
            {
                board.StuckAnchor = board.Feet;
                board.StuckTicks = 0;
                return;
            }
            board.StuckTicks++;
            // Hop obstacles before (or together with) the replan; cooldown prevents jump spam.
            if (board.StuckTicks >= JumpStuckTicks && tick >= board.NextJumpTick)
            {
                board.JumpPending = true;
                board.NextJumpTick = tick + JumpCooldownTicks;
            }
            if (board.StuckTicks < StuckTicksThreshold) return;
            counters.StuckEvents++;
            board.StuckTicks = 0;
            board.StuckFailures++;
            if (board.PathTicket.HasValue)
                navigation.Cancel(board.PathTicket.Value);
            board.PathTicket = null;
            board.PathCornerCount = 0;
            board.PathCornerIndex = 0;
            board.PathGoal = null;
            if (board.StuckFailures >= 3)
            {
                board.NeedsRelocation = true;
                Enter(board, AiState.Search);
            }
            else if (board.CoverReservation.HasValue) Enter(board, AiState.Reposition);
            else Enter(board, AiState.SeekCover);
        }

        /// <summary>States that expect horizontal travel; used when path tickets are already dropped.</summary>
        private static bool WantsHorizontalProgress(AiBlackboard board) =>
            board.State == AiState.Patrol
            || board.State == AiState.Investigate
            || board.State == AiState.SeekCover
            || board.State == AiState.Reposition
            || board.State == AiState.Search
            // ADR-112: an engaged bot only expects progress when it actually asked to move last tick; a long-range or
            // crouch shooter holding still is not stuck (it used to fall into SeekCover and spawn relocation).
            || (board.State == AiState.Engage && board.HasVisibleTarget && board.CommandedMove);

        /// <summary>Copy living bots that need a spawn-slot rescue into <paramref name="destination"/>.</summary>
        public int CollectRelocationRequests(List<EntityId> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int n = 0;
            for (int i = 0; i < botCount; i++)
            {
                var board = boards[i];
                if (!board.Alive || board.State == AiState.Dead || !board.NeedsRelocation) continue;
                destination.Add(board.Self);
                n++;
            }
            return n;
        }

        public void ClearRelocationRequest(EntityId entity)
        {
            var board = FindBoard(entity);
            if (board == null) return;
            board.NeedsRelocation = false;
            board.StuckTicks = 0;
            board.StuckFailures = 0;
            board.StuckAnchor = board.Feet;
        }

        // ---------------------------------------------------------------- cover

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

        // ---------------------------------------------------------------- ADR-112 map tactics

        /// <summary>
        /// Claims the best authored cover against <paramref name="threat"/>: the hiding spot is on the box face away
        /// from the threat, nearest the bot's own lateral position. Scored by travel distance, hard cover and ally
        /// spacing; never a spot that is behind the threat or that the threat stands next to. No rays.
        /// </summary>
        private bool TryClaimMapCover(AiBlackboard board, Float3 threat)
        {
            if (mapCovers.Count == 0) return false;
            float botThreat = Distance2D(board.Feet, threat);
            int best = -1; float bestScore = float.MinValue; Float3 bestSpot = default;
            for (int k = 0; k < mapCovers.Count; k++)
            {
                var cover = mapCovers[k];
                if (covers.IsClaimed(cover.Id)) continue;
                var spot = HideSpot(cover, threat, board.Feet);
                float travel = Distance2D(board.Feet, spot);
                if (travel > AiTuning.MapCoverSearchMeters) continue;
                float spotThreat = Distance2D(spot, threat);
                if (spotThreat < AiTuning.MapCoverMinThreatMeters) continue;
                // The box must sit between the threat and the spot, and the spot must not be a charge at the enemy.
                if (Distance2D(cover.Center, threat) >= spotThreat) continue;
                if (spotThreat < botThreat - AiTuning.MapCoverMaxAdvanceMeters) continue;
                float score = -travel / AiTuning.MapCoverSearchMeters;
                if (cover.Hard) score += 0.25f;
                score += 0.1f * AiNoise.Unit(board.Self.Value, cover.Id, AiNoise.SaltCover);
                for (int i = 0; i < botCount; i++)
                {
                    var other = boards[i];
                    if (other == board || !other.Alive || other.State == AiState.Dead) continue;
                    if (board.Team != 0 && !PerceptionSystem.IsAlliedTeam(board.Team, other.Team)) continue;
                    if (Within(other.Feet, spot, AiTuning.MapCoverAllySpacing)) score -= 0.6f;
                    if (other.CoverPoint.HasValue && Within(other.CoverPoint.Value, spot, AiTuning.MapCoverAllySpacing)) score -= 0.6f;
                }
                if (score > bestScore) { bestScore = score; best = k; bestSpot = spot; }
            }
            if (best < 0) return false;
            var chosen = mapCovers[best];
            if (!covers.TryClaim(chosen.Id, board.Self)) return false;
            if (board.CoverReservation.HasValue && board.CoverReservation.Value != chosen.Id)
                covers.Release(board.CoverReservation.Value, board.Self);
            board.CoverReservation = chosen.Id;
            board.CoverPoint = bestSpot;
            board.CoverThreat = threat;
            board.CoverLow = chosen.Size.Y < AiTuning.StandingCoverHeight;
            board.InCover = false;
            board.CoverProbeActive = false;
            counters.CoverClaims++;
            counters.MapCoverClaims++;
            return true;
        }

        /// <summary>Spot 0.6 m off the box face that looks away from the threat, slid toward the bot along it.</summary>
        public static Float3 HideSpot(MapCover cover, Float3 threat, Float3 bot)
        {
            float hx = cover.Size.X * 0.5f, hz = cover.Size.Z * 0.5f;
            float dx = cover.Center.X - threat.X, dz = cover.Center.Z - threat.Z;
            float baseY = cover.Center.Y - cover.Size.Y * 0.5f;
            const float standOff = 0.6f, inset = 0.4f;
            // Which face the threat looks at, normalised by the box's extents (a long wall is hidden behind its length).
            if (Math.Abs(dx) * Math.Max(hz, 0.05f) >= Math.Abs(dz) * Math.Max(hx, 0.05f))
            {
                float x = cover.Center.X + (dx >= 0f ? 1f : -1f) * (hx + standOff);
                float z = Clamp(bot.Z, cover.Center.Z - Math.Max(0f, hz - inset), cover.Center.Z + Math.Max(0f, hz - inset));
                return new Float3(x, baseY, z);
            }
            else
            {
                float z = cover.Center.Z + (dz >= 0f ? 1f : -1f) * (hz + standOff);
                float x = Clamp(bot.X, cover.Center.X - Math.Max(0f, hx - inset), cover.Center.X + Math.Max(0f, hx - inset));
                return new Float3(x, baseY, z);
            }
        }

        private void ReleaseCover(AiBlackboard board)
        {
            if (board.CoverReservation.HasValue)
            {
                if (covers.Release(board.CoverReservation.Value, board.Self)) counters.CoverReleases++;
                else counters.OrphanReleases++;
                board.CoverReservation = null;
            }
            board.CoverPoint = null;
            board.InCover = false;
            board.InCoverSinceTick = 0;
            board.CoverProbeActive = false;
        }

        private static float StraightMetersToCorner(AiBlackboard board)
        {
            if (board.PathCornerIndex >= board.PathCornerCount) return 0f;
            var c = board.PathCorners[board.PathCornerIndex];
            float dx = c.X - board.Feet.X, dz = c.Z - board.Feet.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }
    }
}
