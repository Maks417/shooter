#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Motor;

namespace Shooter.Simulation.AI
{
    /// <summary>Bot perception: gunfire, damage and footstep stimuli, vision, target acquisition and callouts.</summary>
    public sealed partial class CombatAiDirector
    {
        /// <summary>Legacy damage stimulus with a known source point (no attacker identity).</summary>
        public void NotifyDamage(EntityId victim, Float3 source) => ObserveDamage(victim, default, source);

        /// <summary>
        /// Authoritative damage on <paramref name="victim"/> (tick t), consumed by the next plan (t+1).
        /// The bot flinches; if it was not already fighting the attacker it turns toward a noisy source
        /// estimate and investigates.
        /// </summary>
        public void ObserveDamage(EntityId victim, EntityId attacker, Float3? attackerPoint)
        {
            for (int i = 0; i < botCount; i++)
            {
                var board = boards[i];
                if (board.Self != victim) continue;
                if (!board.Alive || board.State == AiState.Dead) return;
                board.WasDamaged = true;
                board.LastDamagedTick = tick;
                board.FlinchUntilTick = tick + AiTuning.FlinchTicks;
                if (attacker.IsValid) board.LastAttacker = attacker;
                counters.DamageReactions++;
                bool fightingAttacker = board.HasVisibleTarget && attacker.IsValid && board.TargetId == attacker;
                if (!attackerPoint.HasValue || fightingAttacker) return;
                if (board.HasVisibleTarget && attacker.IsValid)
                {
                    // Shot by someone else while fighting: a second hit (or getting low) makes the bot turn on them.
                    bool sameUnseen = board.UnseenAttacker.HasValue && board.UnseenAttacker.Value == attacker &&
                        unchecked(tick - board.UnseenHitTick) <= AiTuning.UnseenHitWindowTicks;
                    board.UnseenHits = sameUnseen ? board.UnseenHits + 1 : 1;
                    board.UnseenAttacker = attacker;
                    board.UnseenHitTick = tick;
                    if (board.UnseenHits >= AiTuning.UnseenHitsToTurn || board.Health < AiTuning.TurnImmediatelyBelowHealth)
                    {
                        board.TurnToThreatUntilTick = tick + AiTuning.ThreatTurnTicks;
                        counters.ThreatTurns++;
                    }
                }
                float n = AiTuning.DamageSourceNoiseMeters;
                var p = attackerPoint.Value;
                board.LastHeard = new Float3(
                    p.X + n * AiNoise.Signed(board.Self.Value, tick, AiNoise.SaltHearingX),
                    p.Y,
                    p.Z + n * AiNoise.Signed(board.Self.Value, tick, AiNoise.SaltHearingZ));
                board.LastHeardTick = tick;
                board.Heard = HeardClass.Damage;
                if (IsCalm(board.State)) Enter(board, AiState.Investigate);
                return;
            }
        }

        /// <summary>
        /// A shot fired at <paramref name="origin"/> (tick t). Calm bots within hearing radius investigate a
        /// distance-attenuated estimate of the position. T090: a direct occlusion sample (not the vision budget)
        /// drops gunfire that is fully blocked by geometry; presence-range always passes.
        /// </summary>
        public void ObserveGunfire(EntityId shooter, Float3 origin)
        {
            float radiusSq = AiTuning.GunfireHearingRadius * AiTuning.GunfireHearingRadius;
            for (int i = 0; i < botCount; i++)
            {
                var board = boards[i];
                if (!board.Alive || board.State == AiState.Dead || board.Self == shooter) continue;
                if (board.HasVisibleTarget || !IsCalm(board.State)) continue;
                var source = origin;
                var shooterBoard = FindBoard(shooter);
                if (shooterBoard != null && board.Team != 0 && PerceptionSystem.IsAlliedTeam(board.Team, shooterBoard.Team))
                {
                    // A teammate shooting is not a threat at the teammate: look where it is fighting, or ignore it.
                    if (!shooterBoard.LastSeenEnemy.HasValue ||
                        unchecked(tick - shooterBoard.LastSeenTick) > AiTuning.CombatMemoryTicks)
                    {
                        counters.AllyGunfireIgnored++;
                        continue;
                    }
                    source = shooterBoard.LastSeenEnemy.Value;
                }
                else if (shooterBoard == null && IsAlliedHuman(board, shooter)) { counters.AllyGunfireIgnored++; continue; }
                float dx = source.X - board.Feet.X, dz = source.Z - board.Feet.Z;
                float dSq = dx * dx + dz * dz;
                if (dSq > radiusSq) continue;
                if (board.LastHeard.HasValue && unchecked(tick - board.LastHeardTick) < AiTuning.StimulusRefreshTicks)
                {
                    // Recent stimulus: only a clearly closer source replaces it.
                    float hx = board.LastHeard.Value.X - board.Feet.X, hz = board.LastHeard.Value.Z - board.Feet.Z;
                    if (dSq >= (hx * hx + hz * hz) * 0.5f) continue;
                }
                if (!SoundReaches(board, source, useBudget: false))
                {
                    counters.HearingOccluded++;
                    continue;
                }
                float noise = MathF.Sqrt(dSq) * AiTuning.GunfireNoisePerMeter;
                board.LastHeard = new Float3(
                    source.X + noise * AiNoise.Signed(board.Self.Value, tick, AiNoise.SaltHearingX),
                    source.Y,
                    source.Z + noise * AiNoise.Signed(board.Self.Value, tick, AiNoise.SaltHearingZ));
                board.LastHeardTick = tick;
                board.Heard = HeardClass.Gunfire;
                counters.StimuliHeard++;
                EnterInvestigateUnlessCrowded(board);
            }
        }

        public void NotifyDeath(EntityId id)
        {
            for (int i = 0; i < botCount; i++)
            {
                if (boards[i].Self != id) continue;
                ReleaseBotResources(boards[i]);
                boards[i].Alive = false;
                boards[i].HasVisibleTarget = false;
                boards[i].VisibleTargetId = null;
                boards[i].VisibleTarget = null;
                boards[i].TargetId = null;
                boards[i].ReactionReadyTick = 0;
                Enter(boards[i], AiState.Dead);
                return;
            }
        }

        /// <summary>Non-bot shooter (the human) on the listener's team, as last seen in the planner's actor list.</summary>
        private bool IsAlliedHuman(AiBlackboard board, EntityId shooter)
        {
            if (board.Team == 0) return false;
            for (int i = 0; i < humanTeamCount; i++)
                if (humanTeamIds[i] == shooter.Value) return PerceptionSystem.IsAlliedTeam(board.Team, humanTeams[i]);
            return false;
        }

        /// <summary>
        /// Investigate a stimulus unless enough allies already are; the rest keep their route and watch the
        /// point (ThreatLook), so a team does not run to the same spot in a line.
        /// </summary>
        private void EnterInvestigateUnlessCrowded(AiBlackboard board)
        {
            if (board.State == AiState.Investigate || !board.LastHeard.HasValue || board.Team == 0)
            {
                Enter(board, AiState.Investigate);
                return;
            }
            int crowd = 0;
            var p = board.LastHeard.Value;
            for (int i = 0; i < botCount; i++)
            {
                var other = boards[i];
                if (other == board || !other.Alive || other.State != AiState.Investigate || !other.LastHeard.HasValue) continue;
                if (!PerceptionSystem.IsAlliedTeam(board.Team, other.Team)) continue;
                if (Within(other.LastHeard.Value, p, AiTuning.InvestigateCrowdMeters)) crowd++;
            }
            // ADR-112: allies already fighting at the spot count toward the crowd too.
            crowd += AlliesPressing(board, p);
            if (crowd >= AiTuning.InvestigateCrowdLimit)
            {
                counters.CrowdedStimuliHeld++;
                // Enough allies are on it: take a flanking point round the contact instead of joining the queue.
                var flank = ChooseFlankGoal(board, p);
                if (flank.HasValue && IsCalm(board.State) && board.State != AiState.Reposition)
                {
                    board.FlankGoal = flank;
                    counters.FlankMoves++;
                    Enter(board, AiState.Reposition);
                }
                return;
            }
            Enter(board, AiState.Investigate);
        }

        private static bool IsCalm(AiState state) =>
            state == AiState.Patrol || state == AiState.Idle || state == AiState.Search ||
            state == AiState.Investigate || state == AiState.Reposition;

        /// <summary>Per-enemy horizontal speed from consecutive planner ticks (feeds footstep hearing).</summary>
        private void TrackEnemies(SenseTarget[]? enemies, int enemyCount)
        {
            humanTeamCount = 0;
            for (int k = 0; k < enemyCount; k++)
            {
                var e = enemies![k];
                if (FindBoard(e.Id) == null && humanTeamCount < MaxTracked)
                {
                    humanTeamIds[humanTeamCount] = e.Id.Value;
                    humanTeams[humanTeamCount++] = e.Team;
                }
                int slot = -1, oldest = 0;
                for (int i = 0; i < MaxTracked; i++)
                {
                    if (trackTick[i] != 0 && trackId[i] == e.Id.Value) { slot = i; break; }
                    if (trackTick[i] < trackTick[oldest]) oldest = i;
                }
                float speed = 0;
                if (slot >= 0)
                {
                    uint dt = unchecked(tick - trackTick[slot]);
                    if (dt > 0 && dt <= 6)
                    {
                        float dx = e.Point.X - trackPoint[slot].X, dz = e.Point.Z - trackPoint[slot].Z;
                        speed = MathF.Sqrt(dx * dx + dz * dz) * 60f / dt;
                    }
                    else if (dt == 0) speed = trackSpeed[slot];
                }
                else slot = oldest;
                trackId[slot] = e.Id.Value;
                trackPoint[slot] = e.Point;
                trackTick[slot] = tick == 0 ? 1u : tick;
                trackSpeed[slot] = speed;
                enemySpeed[k] = speed;
            }
        }

        /// <summary>
        /// Footsteps (T085/T090): an enemy moving fast enough within its audible radius, or anyone within arm reach,
        /// is heard from any direction. T090 occludes beyond presence with leftover AI rays (fail-open when deferred).
        /// </summary>
        private void HearFootsteps(AiBlackboard board, SenseTarget[] enemies, int enemyCount)
        {
            // Rank audible candidates by distance; skip occluded ones beyond presence.
            int best = -1;
            float bestSq = float.MaxValue;
            for (int k = 0; k < enemyCount; k++)
            {
                var e = enemies[k];
                if (!PerceptionSystem.IsOpponent(board, e)) continue;
                float sp = enemySpeed[k];
                float radius = sp >= AiTuning.SprintStepSpeed ? AiTuning.SprintStepRadius
                    : sp >= AiTuning.RunStepSpeed ? AiTuning.RunStepRadius
                    : sp >= AiTuning.WalkStepSpeed ? AiTuning.WalkStepRadius : 0f;
                if (radius < AiTuning.PresenceRadius) radius = AiTuning.PresenceRadius;
                float dx = e.Point.X - board.Feet.X, dz = e.Point.Z - board.Feet.Z;
                float dSq = dx * dx + dz * dz;
                if (dSq > radius * radius || dSq >= bestSq) continue;
                if (dSq > AiTuning.PresenceRadius * AiTuning.PresenceRadius &&
                    !SoundReaches(board, e.Point, useBudget: true))
                {
                    counters.HearingOccluded++;
                    continue;
                }
                best = k;
                bestSq = dSq;
            }
            if (best < 0) return;
            // A fresh damage report is better information than footsteps; keep it.
            if (board.Heard == HeardClass.Damage && unchecked(tick - board.LastHeardTick) < AiTuning.StimulusRefreshTicks) return;
            bool fresh = board.Heard != HeardClass.Footsteps || unchecked(tick - board.LastHeardTick) > AiTuning.StimulusRefreshTicks;
            var p = enemies[best].Point;
            float noise = MathF.Sqrt(bestSq) * AiTuning.FootstepNoisePerMeter;
            board.LastHeard = new Float3(
                p.X + noise * AiNoise.Signed(board.Self.Value, tick / 15, AiNoise.SaltHearingX),
                board.Feet.Y,
                p.Z + noise * AiNoise.Signed(board.Self.Value, tick / 15, AiNoise.SaltHearingZ));
            board.LastHeardTick = tick;
            board.Heard = HeardClass.Footsteps;
            if (fresh) counters.FootstepsHeard++;
            if (board.State == AiState.Patrol || board.State == AiState.Idle || board.State == AiState.Search)
                EnterInvestigateUnlessCrowded(board);
        }

        /// <summary>
        /// T090: true when sound from <paramref name="source"/> reaches the bot. Presence always passes.
        /// Budgeted samples use leftover AI rays (fail-open when exhausted); gunfire uses a direct collision ray.
        /// </summary>
        private bool SoundReaches(AiBlackboard board, Float3 source, bool useBudget)
        {
            float dx = source.X - board.Feet.X, dy = source.Y - (board.Feet.Y + EyeHeight(board)), dz = source.Z - board.Feet.Z;
            float dSq = dx * dx + dz * dz;
            if (dSq <= AiTuning.PresenceRadius * AiTuning.PresenceRadius) return true;
            float len = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 0.5f) return true;
            var ear = new Float3(board.Feet.X, board.Feet.Y + EyeHeight(board), board.Feet.Z);
            var dir = new Float3(dx / len, dy / len, dz / len);
            RayHit hit;
            if (useBudget)
            {
                if (board.LastHearOcclusionTick != 0 &&
                    unchecked(tick - board.LastHearOcclusionTick) < AiTuning.HearOcclusionIntervalTicks)
                    return board.HearOcclusionClear;
                board.LastHearOcclusionTick = tick;
                if (!perception.TryBudgetedRay(ear, dir, len, board.Self, out hit))
                {
                    board.HearOcclusionClear = true; // fail-open when the vision/cover budget is exhausted
                    return true;
                }
            }
            else
            {
                hit = world.Raycast(ear, dir, len, new QueryFilter(visionMask, board.Self, false));
            }
            counters.HearingOcclusionRays++;
            bool blocked = hit.Status == QueryStatus.Hit && hit.Hit.DistanceMeters < len - 0.3f;
            if (useBudget) board.HearOcclusionClear = !blocked;
            return !blocked;
        }

        private void Sense(AiBlackboard board, SenseTarget[] enemies, int enemyCount)
        {
            var eye = new Float3(board.Feet.X, board.Feet.Y + EyeHeight(board), board.Feet.Z);
            int stickyIdx = -1, attackerIdx = -1;
            bool attackerRecent = board.WasDamaged && board.LastAttacker.HasValue &&
                unchecked(tick - board.LastDamagedTick) <= AiTuning.AttackerPriorityTicks;
            for (int k = 0; k < enemyCount; k++)
            {
                var id = enemies[k].Id;
                if (board.TargetId.HasValue && board.TargetId.Value == id) stickyIdx = k;
                if (attackerRecent && board.LastAttacker!.Value == id) attackerIdx = k;
            }
            if (attackerIdx == stickyIdx) attackerIdx = -1;
            int preferred = attackerIdx >= 0 ? attackerIdx : stickyIdx;
            // A sticky target is periodically re-scanned against others (staggered per bot) so a much
            // closer threat can take over; the attacker priority never yields.
            bool scan = attackerIdx < 0 && stickyIdx >= 0 &&
                (tick + board.Self.Value) % AiTuning.TargetScanIntervalTicks == 0;

            int n = 0;
            if (preferred >= 0 && !scan) visitOrder[n++] = preferred;
            int candidateStart = (int)((tick + board.Self.Value) % (uint)enemyCount);
            for (int offset = 0; offset < enemyCount; offset++)
            {
                int idx = (candidateStart + offset) % enemyCount;
                if (idx == preferred) continue;
                visitOrder[n++] = idx;
            }
            if (preferred >= 0 && scan) visitOrder[n++] = preferred;

            bool any = false;
            for (int v = 0; v < n; v++)
            {
                var candidate = enemies[visitOrder[v]];
                // Filter team/life before perception so allies never consume LOS budget.
                if (!PerceptionSystem.IsOpponent(board, candidate))
                {
                    if (candidate.Alive && candidate.Id != board.Self &&
                        PerceptionSystem.IsAlliedTeam(board.Team, candidate.Team))
                        counters.AlliedCandidatesSkipped++;
                    continue;
                }
                var savedLastSeen = board.LastSeenEnemy;
                float savedConfidence = board.Confidence;
                int raysBefore = perception.RaysUsedThisTick;
                bool sensed = perception.TrySense(board, eye, board.Yaw, candidate, counters, out bool acquired);
                // A real negative answer (a ray, range or field-of-view verdict) on the held target ends the hold.
                if (sensed && !acquired && senseHeldTarget.HasValue && candidate.Id == senseHeldTarget.Value) senseHeldLost = true;
                if (sensed && acquired)
                {
                    float d = Distance(eye, candidate.Point);
                    bool challenger = scan && stickyIdx >= 0 && candidate.Id != enemies[stickyIdx].Id &&
                        unchecked(tick - board.LastSeenTick) <= AiTuning.TargetMemoryTicks;
                    if (challenger && !(d < board.TargetDistance * AiTuning.TargetSwitchCloserFactor))
                    {
                        // Not worth switching; this tick's opportunity is spent and the sticky target is unconfirmed.
                        board.HasVisibleTarget = false;
                        board.VisibleTargetId = null;
                        board.VisibleTarget = null;
                        board.LastSeenEnemy = savedLastSeen;
                        board.Confidence = savedConfidence;
                        break;
                    }
                    Acquire(board, candidate, d, savedLastSeen);
                    any = true;
                    break;
                }
                // One actual LOS opportunity per bot per pass keeps the shared budget fair.
                if (perception.RaysUsedThisTick != raysBefore) break;
            }
            if (!any && board.Confidence > 0)
            {
                board.Confidence = Math.Max(0, board.Confidence - 0.02f);
                if (board.Confidence <= 0 && board.State == AiState.Engage) Enter(board, AiState.Search);
            }
        }

        /// <summary>
        /// ADR-112: a bot that saw its target within <see cref="AiTuning.VisibleHoldTicks"/> keeps tracking it on a tick
        /// where the shared vision budget gave it no ray (or a target re-scan left the sticky target unconfirmed).
        /// Before this, every budget-starved tick dropped the engagement: the aim jumped to a different point, the
        /// strafe stopped and the burst was cut, so engaged bots flickered. A ray that says "not visible" ends it.
        /// </summary>
        private void HoldVisibleTarget(AiBlackboard board, EntityId? held, SenseTarget[] enemies, int enemyCount)
        {
            if (!held.HasValue || senseHeldLost || board.LastSeenTick == 0) return;
            if (unchecked(tick - board.LastSeenTick) > AiTuning.VisibleHoldTicks) return;
            for (int k = 0; k < enemyCount; k++)
            {
                var candidate = enemies[k];
                if (candidate.Id != held.Value) continue;
                if (!PerceptionSystem.IsOpponent(board, candidate)) return;
                board.HasVisibleTarget = true;
                board.VisibleTargetId = candidate.Id;
                board.VisibleTarget = candidate.Point;
                board.Confidence = Math.Max(board.Confidence, 0.5f);
                counters.VisibleHolds++;
                return;
            }
        }

        private void Acquire(AiBlackboard board, SenseTarget candidate, float distance, Float3? previousPoint)
        {
            bool sameTarget = board.TargetId.HasValue && board.TargetId.Value == candidate.Id &&
                board.LastSeenTick != 0 && unchecked(tick - board.LastSeenTick) <= AiTuning.TargetMemoryTicks;
            bool inCombat = board.LastSeenTick != 0 && unchecked(tick - board.LastSeenTick) <= AiTuning.CombatMemoryTicks;
            if (sameTarget && previousPoint.HasValue)
            {
                uint dt = Math.Max(1u, unchecked(tick - board.LastSeenTick));
                float s = 60f / dt;
                var p = previousPoint.Value;
                float vx = (candidate.Point.X - p.X) * s, vz = (candidate.Point.Z - p.Z) * s;
                float sx = board.TargetVelocity.X * 0.7f + vx * 0.3f, sz = board.TargetVelocity.Z * 0.7f + vz * 0.3f;
                float speed = MathF.Sqrt(sx * sx + sz * sz);
                if (speed > 10f) { sx *= 10f / speed; sz *= 10f / speed; speed = 10f; }
                board.TargetVelocity = new Float3(sx, 0, sz);
                board.TargetSpeed = speed;
            }
            else if (!sameTarget)
            {
                if (board.TargetId.HasValue && board.TargetId.Value != candidate.Id) counters.TargetSwitches++;
                uint reaction = inCombat
                    ? AiTuning.SwitchReactionBaseTicks + AiNoise.Range(board.Self.Value, candidate.Id.Value, AiNoise.SaltReaction, AiTuning.SwitchReactionSpreadTicks)
                    : AiTuning.ReactionBaseTicks + AiNoise.Range(board.Self.Value, candidate.Id.Value, AiNoise.SaltReaction, AiTuning.ReactionSpreadTicks);
                float extra = Math.Max(0f, distance - 15f) * 0.1f * AiTuning.RangeReactionTicksPer10m;
                reaction += Math.Min(AiTuning.MaxRangeReactionTicks, (uint)extra);
                board.ReactionReadyTick = tick + reaction;
                board.AcquiredTick = tick;
                board.TargetVelocity = default;
                board.TargetSpeed = 0;
                board.HasStrafeCenter = false;
                if (!inCombat) Callout(board, candidate.Point);
            }
            board.TargetId = candidate.Id;
            board.LastSeenTick = tick;
            board.LastSeenEnemy = candidate.Point;
            board.HasVisibleTarget = true;
            board.VisibleTargetId = candidate.Id;
            board.VisibleTarget = candidate.Point;
            board.Confidence = 1f;
            board.TargetDistance = distance;
            board.TargetHealth = candidate.Health;
            if (tick >= board.ReactionReadyTick && board.State != AiState.Engage && board.State != AiState.SeekCover &&
                !(board.State == AiState.Reload && board.MagAmmo <= 0))
                Enter(board, AiState.Engage);
        }

        /// <summary>First sighting of an enemy: nearby calm allies investigate it (no rays).</summary>
        private void Callout(AiBlackboard caller, Float3 point)
        {
            if (caller.Team == 0) return;
            float radiusSq = AiTuning.CalloutRadius * AiTuning.CalloutRadius;
            for (int i = 0; i < botCount; i++)
            {
                var ally = boards[i];
                if (ally == caller || !ally.Alive || ally.State == AiState.Dead) continue;
                if (!PerceptionSystem.IsAlliedTeam(caller.Team, ally.Team)) continue;
                if (ally.HasVisibleTarget || !IsCalm(ally.State)) continue;
                float dx = ally.Feet.X - caller.Feet.X, dz = ally.Feet.Z - caller.Feet.Z;
                if (dx * dx + dz * dz > radiusSq) continue;
                ally.LastHeard = point;
                ally.LastHeardTick = tick;
                ally.Heard = HeardClass.Callout;
                counters.Callouts++;
                EnterInvestigateUnlessCrowded(ally);
            }
        }

        private void ExpireStimuli(AiBlackboard board)
        {
            if (board.LastHeard.HasValue && unchecked(tick - board.LastHeardTick) > StimulusExpiryTicks)
            {
                board.LastHeard = null;
                board.Heard = HeardClass.None;
            }
        }

        // ---------------------------------------------------------------- utility decisions
    }
}
