#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Motor;

namespace Shooter.Simulation.AI
{
    /// <summary>Bot decisions: utility scoring, state choice, cover decision, patrol and flank goals.</summary>
    public sealed partial class CombatAiDirector
    {
        private void ApplyUtility(AiBlackboard board)
        {
            bool recentlyFighting = board.LastSeenTick != 0 && unchecked(tick - board.LastSeenTick) <= 60;
            if (board.State == AiState.Reload)
            {
                // Authority owns reload timing and ammunition; leave only once the magazine actually grew.
                int magSize = ActiveMagazineSize(board);
                if (board.MagAmmo > board.ReloadStartMag || board.MagAmmo >= magSize)
                    Enter(board, board.HasVisibleTarget ? AiState.Engage
                        : board.CoverPoint.HasValue || board.CoverReservation.HasValue ? AiState.SeekCover : AiState.Search);
            }
            else if (board.MagAmmo <= 0 && board.ReserveAmmo > 0)
            {
                board.ReloadStartMag = 0;
                Enter(board, AiState.Reload);
                if (recentlyFighting && board.LastSeenEnemy.HasValue && coverSites.Count == 0 &&
                    !board.CoverPoint.HasValue && !board.CoverProbeActive &&
                    !TryClaimMapCover(board, board.LastSeenEnemy.Value))
                    CoverFinder.Start(board, board.LastSeenEnemy.Value);
            }
            else if (board.MagAmmo < TacticalReloadThreshold(board) && board.ReserveAmmo > 0 && !board.HasVisibleTarget &&
                     (board.LastSeenTick == 0 || unchecked(tick - board.LastSeenTick) > AiTuning.CombatMemoryTicks) &&
                     (IsCalm(board.State) || board.InCover))
            {
                board.ReloadStartMag = board.MagAmmo;
                counters.TacticalReloads++;
                Enter(board, AiState.Reload);
            }
            else if (board.InCover && board.State == AiState.SeekCover && !board.HasVisibleTarget &&
                     board.MagAmmo < ActiveMagazineSize(board) && board.ReserveAmmo > 0)
            {
                board.ReloadStartMag = board.MagAmmo;
                Enter(board, AiState.Reload);
            }

            // Low health and losing the trade: break off toward cover (cooldown keeps it from flapping).
            if (board.State == AiState.Engage && board.HasVisibleTarget && board.Health < AiTuning.LowHealth &&
                tick >= board.RetreatAllowedTick &&
                (board.TargetHealth > board.Health + AiTuning.LosingTradeMargin ||
                 (board.WasDamaged && unchecked(tick - board.LastDamagedTick) < 30)))
            {
                board.RetreatAllowedTick = tick + AiTuning.RetreatCooldownTicks;
                counters.Retreats++;
                Enter(board, AiState.SeekCover);
            }
            // ADR-112: fights move. After a seeded 2-4 s exchange in the open, a bot with authored cover in reach
            // moves to it (the hiding face away from its target) and fights from there by peeking.
            else if (board.State == AiState.Engage && board.HasVisibleTarget && board.VisibleTarget.HasValue && mapCovers.Count > 0 &&
                     tick >= board.NextRepositionTick && board.TargetDistance > AiTuning.CloseRange &&
                     unchecked(tick - board.StateEnteredTick) >= AiTuning.EngageInOpenTicks +
                        AiNoise.Range(board.Self.Value, board.StateEnteredTick, AiNoise.SaltCover, AiTuning.EngageInOpenSpreadTicks))
            {
                board.NextRepositionTick = tick + AiTuning.CoverRepositionCooldownTicks;
                if (!board.InCover && TryClaimMapCover(board, board.VisibleTarget.Value))
                {
                    counters.CoverRepositions++;
                    Enter(board, AiState.SeekCover);
                }
            }
        }

        private void Decide(AiBlackboard board)
        {
            switch (board.State)
            {
                case AiState.Patrol:
                    EnsurePath(board, PatrolPoint(board));
                    break;
                case AiState.Investigate:
                    if (board.LastHeard.HasValue) EnsurePath(board, board.LastHeard.Value, retarget: true);
                    else Enter(board, AiState.Search);
                    break;
                case AiState.Engage:
                    if (board.HasVisibleTarget && board.VisibleTarget.HasValue)
                    {
                        if (board.TargetDistance >= AiTuning.AdvanceRange) EnsurePath(board, board.VisibleTarget.Value, retarget: true);
                        else DropPath(board); // tactical movement replaces route following while the enemy is in view
                    }
                    else if (board.LastSeenEnemy.HasValue && unchecked(tick - board.LastSeenTick) > AiTuning.TargetMemoryTicks)
                        EnsurePath(board, SearchPoint(board), retarget: true);
                    break;
                case AiState.SeekCover:
                    DecideCover(board);
                    break;
                case AiState.Reload:
                    if (board.ReserveAmmo <= 0) Enter(board, AiState.SeekCover);
                    else if (board.CoverPoint.HasValue && !board.InCover) MoveToCover(board);
                    break;
                case AiState.Reposition:
                case AiState.Search:
                    if (board.FlankGoal.HasValue)
                    {
                        if (Within(board.Feet, board.FlankGoal.Value, AiTuning.FlankArrivalMeters)) board.FlankGoal = null;
                        else { EnsurePath(board, board.FlankGoal.Value, retarget: true); break; }
                    }
                    if (board.LastSeenEnemy.HasValue) EnsurePath(board, SearchPoint(board), retarget: true);
                    else if (combatDestinations.Count > 0) EnsurePath(board, ChooseCombatDestination(board));
                    else Enter(board, AiState.Patrol);
                    break;
            }
        }

        private void DecideCover(AiBlackboard board)
        {
            if (coverSites.Count > 0)
            {
                if (!board.CoverReservation.HasValue) ClaimBestCover(board);
                if (board.CoverReservation.HasValue)
                {
                    var site = FindCover(board.CoverReservation.Value);
                    if (site.HasValue) EnsurePath(board, site.Value.Anchor);
                }
                else if (combatDestinations.Count > 0 || board.LastSeenEnemy.HasValue)
                    Enter(board, AiState.Search);
                return;
            }
            if (board.CoverPoint.HasValue)
            {
                if (!board.InCover) { MoveToCover(board); return; }
                if (board.HasVisibleTarget && board.TargetDistance < AiTuning.CloseRange) { Enter(board, AiState.Engage); return; }
                // ADR-112: a bot trading shots from cover keeps the spot; it moves on once the fight goes quiet.
                uint quietSince = board.LastSeenTick > board.InCoverSinceTick ? board.LastSeenTick : board.InCoverSinceTick;
                if (unchecked(tick - quietSince) >= AiTuning.CoverHoldTicks) Enter(board, AiState.Reposition);
                return;
            }
            if (board.CoverProbeActive) return; // probes run with leftover rays at the end of this tick
            // No cover found (or no threat to hide from): fall back to the old behaviour.
            if (board.MagAmmo <= 0 && board.ReserveAmmo > 0) Enter(board, AiState.Reload);
            else if (board.HasVisibleTarget) Enter(board, AiState.Engage);
            else if (combatDestinations.Count > 0 || board.LastSeenEnemy.HasValue)
            {
                // No free cover site: keep fighting through the map instead of idling in place.
                Enter(board, AiState.Search);
            }
        }

        /// <summary>Allies (bots) engaging or searching within the press radius of <paramref name="point"/> (investigators are
        /// counted separately by the stimulus crowd check).</summary>
        private int AlliesPressing(AiBlackboard board, Float3 point)
        {
            if (board.Team == 0) return 0;
            int n = 0;
            for (int i = 0; i < botCount; i++)
            {
                var other = boards[i];
                if (other == board || !other.Alive || other.State == AiState.Dead) continue;
                if (!PerceptionSystem.IsAlliedTeam(board.Team, other.Team)) continue;
                bool pressing = other.State == AiState.Engage || other.State == AiState.Search;
                if (!pressing) continue;
                var at = other.LastSeenEnemy ?? other.LastHeard;
                if (at.HasValue && Within(at.Value, point, AiTuning.PressRadiusMeters)) n++;
            }
            return n;
        }

        /// <summary>
        /// A route/lane point beside the contact rather than on the straight line to it: 8-30 m from the contact,
        /// at a 45-135 degree angle off the bot's own approach, away from allies' goals. Null when none fits.
        /// </summary>
        private Float3? ChooseFlankGoal(AiBlackboard board, Float3 contact)
        {
            float ax = board.Feet.X - contact.X, az = board.Feet.Z - contact.Z;
            float alen = MathF.Sqrt(ax * ax + az * az);
            if (alen < 1e-3f) return null;
            ax /= alen; az /= alen;
            Float3? best = null; float bestScore = float.MinValue;
            for (int list = 0; list < 2; list++)
            {
                var points = list == 0 ? combatDestinations : patrolDestinations;
                for (int k = 0; k < points.Count; k++)
                {
                    var c = points[k];
                    float cx = c.X - contact.X, cz = c.Z - contact.Z;
                    float clen = MathF.Sqrt(cx * cx + cz * cz);
                    if (clen < AiTuning.FlankMinMeters || clen > AiTuning.FlankMaxMeters) continue;
                    float cos = (cx * ax + cz * az) / clen;
                    if (cos > 0.7071f || cos < -0.7071f) continue; // not ahead or behind the contact: beside it
                    if (Distance2D(board.Feet, c) > alen + AiTuning.FlankMaxMeters) continue;
                    float score = -Math.Abs(clen - AiTuning.FlankPreferredMeters) / AiTuning.FlankPreferredMeters;
                    score += 0.4f * AiNoise.Unit(board.Self.Value, (ulong)k * 7919UL + (ulong)list, AiNoise.SaltCover);
                    for (int i = 0; i < botCount; i++)
                    {
                        var other = boards[i];
                        if (other == board || !other.Alive || other.State == AiState.Dead) continue;
                        if (board.Team != 0 && !PerceptionSystem.IsAlliedTeam(board.Team, other.Team)) continue;
                        if (other.FlankGoal.HasValue && Within(other.FlankGoal.Value, c, AiTuning.SpreadMeters)) score -= 1.5f;
                        if (Within(other.Feet, c, AiTuning.SpreadMeters * 0.5f)) score -= 0.5f;
                    }
                    if (score > bestScore) { bestScore = score; best = c; }
                }
            }
            return best;
        }

        /// <summary>Centroid of the opposing bots' spawn points (their home side); false when none are known.</summary>
        private bool EnemyHome(AiBlackboard board, out Float3 home)
        {
            home = default;
            if (board.Team == 0) return false;
            float x = 0, y = 0, z = 0; int n = 0;
            for (int i = 0; i < botCount; i++)
            {
                var other = boards[i];
                if (other.Team == 0 || PerceptionSystem.IsAlliedTeam(board.Team, other.Team)) continue;
                x += other.SpawnPoint.X; y += other.SpawnPoint.Y; z += other.SpawnPoint.Z; n++;
            }
            if (n == 0) return false;
            home = new Float3(x / n, y / n, z / n);
            return true;
        }

        /// <summary>
        /// ADR-112 team-aware destination term: avoid loitering in the own spawn, lean toward the enemy's side, and
        /// avoid spots where two or more allies are already fighting (they do not need a third body there).
        /// </summary>
        private float TeamTerrainScore(AiBlackboard board, Float3 c, bool hasEnemyHome, Float3 enemyHome)
        {
            if (board.Team == 0) return 0f;
            float score = 0f;
            float own = Distance2D(c, board.SpawnPoint);
            if (own < AiTuning.OwnSpawnAvoidMeters) score -= 1f;
            if (hasEnemyHome)
            {
                // -1 at the own spawn, +1 at the enemy's, whatever the map size.
                float span = Math.Max(20f, Distance2D(board.SpawnPoint, enemyHome));
                score += 0.5f * Clamp((own - Distance2D(c, enemyHome)) / span, -1f, 1f);
            }
            int engaged = 0;
            for (int i = 0; i < botCount; i++)
            {
                var other = boards[i];
                if (other == board || !other.Alive || other.State != AiState.Engage) continue;
                if (!PerceptionSystem.IsAlliedTeam(board.Team, other.Team)) continue;
                if (Within(other.Feet, c, AiTuning.HotSpotMeters)) engaged++;
            }
            if (engaged >= 2) score -= 1.5f;
            return score;
        }

        private void Enter(AiBlackboard board, AiState state)
        {
            if (board.State == state) return;
            var previous = board.State;
            bool keepsCover = state == AiState.SeekCover || state == AiState.Reload;
            if ((previous == AiState.SeekCover || previous == AiState.Reload) && !keepsCover &&
                (board.CoverPoint.HasValue || board.CoverProbeActive ||
                 (board.CoverReservation.HasValue && (board.CoverReservation.Value & CoverFinder.DynamicCoverIdFlag) != 0)))
                ReleaseCover(board);
            board.State = state;
            board.StateEnteredTick = tick;
            counters.StateChanges++;
            if (state == AiState.Engage) { board.HasStrafeCenter = false; board.FlankGoal = null; }
            if (state == AiState.SeekCover)
            {
                if (coverSites.Count > 0) ClaimBestCover(board);
                else if (!board.CoverPoint.HasValue && !board.CoverProbeActive)
                {
                    Float3? threat = board.VisibleTarget ?? board.LastSeenEnemy ??
                        (board.Heard == HeardClass.Damage ? board.LastHeard : null);
                    if (threat.HasValue && !TryClaimMapCover(board, threat.Value)) CoverFinder.Start(board, threat.Value);
                }
            }
            // ADR-112: losing sight of an enemy that allies are already pressing sends this bot round a flank
            // instead of down the same line to the same last-seen point.
            if ((state == AiState.Search || state == AiState.Reposition) && !board.FlankGoal.HasValue && board.LastSeenEnemy.HasValue &&
                AlliesPressing(board, board.LastSeenEnemy.Value) >= 1)
                board.FlankGoal = ChooseFlankGoal(board, board.LastSeenEnemy.Value);
        }

        private Float3 PatrolPoint(AiBlackboard board)
        {
            if (patrolDestinations.Count > 0)
            {
                if (board.PatrolGoal.HasValue && board.PatrolGoalCursor == board.DestinationCursor) return board.PatrolGoal.Value;
                int index = ChooseSpreadDestination(board, patrolDestinations, board.PatrolIndex, board.PreviousPatrolIndex);
                board.PreviousPatrolIndex = board.PatrolIndex;
                board.PatrolIndex = index;
                board.PatrolGoal = patrolDestinations[index];
                board.PatrolGoalCursor = board.DestinationCursor;
                return board.PatrolGoal.Value;
            }
            // Deterministic local orbit based on entity id.
            double a = (board.Self.Value % 8) * (Math.PI / 4);
            return new Float3(board.Feet.X + (float)Math.Cos(a) * 4f, board.Feet.Y, board.Feet.Z + (float)Math.Sin(a) * 4f);
        }
    }
}
