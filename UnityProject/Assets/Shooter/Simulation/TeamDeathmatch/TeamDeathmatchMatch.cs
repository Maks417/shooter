#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;

namespace Shooter.Simulation.TeamDeathmatch
{
    /// <summary>
    /// Pure M9 team-deathmatch score/deadline/respawn reducer. No Unity or file I/O.
    /// T058: occupancy/LOS/pressure spawn ranking with independent first-failure deadline.
    /// </summary>
    public sealed class TeamDeathmatchMatch : ITeamMatch
    {
        private sealed class Row
        {
            public ParticipantId ParticipantId;
            public string DisplayName = string.Empty;
            public byte Team;
            public bool IsHuman;
            public EntityId EntityId;
            public LifeId LifeId;
            public bool Alive;
            public uint Kills;
            public uint Deaths;
            public uint RespawnDueActiveTick;
            /// <summary>Fixed deadline from first failed spawn attempt; never moved by 30-tick retries.</summary>
            public uint SpawnFailureDeadlineActiveTick;
            public bool HasLastDeathEvent;
            public EventId LastDeathEvent;
        }

        private SessionRequest? request;
        private MapGameplayDefinition? map;
        private string catalogIdentity = "unset";
        private string gameplayIdentity = "unset";
        private string buildIdentity = "build-local";
        private readonly List<Row> rows = new List<Row>(GameContractVersion.MaxParticipants);
        private readonly Dictionary<ulong, int> entityToRow = new Dictionary<ulong, int>();
        private readonly Dictionary<uint, uint> slotLastUsedActiveTick = new Dictionary<uint, uint>();
        private uint team1Kills;
        private uint team2Kills;
        private uint activeTicksElapsed;
        private GameMatchPhase phase = GameMatchPhase.Loading;
        private MatchOutcome outcome = MatchOutcome.None;
        private FinishReason reason = FinishReason.None;
        private MatchResults? results;
        private uint startTick;
        private uint endTick;
        private ulong authoritySessionId;

        public void ConfigureIdentities(string catalog, string gameplay, string build)
        {
            catalogIdentity = GameRequireDisplay(catalog, 128);
            gameplayIdentity = GameRequireDisplay(gameplay, 128);
            buildIdentity = GameRequireDisplay(build, 128);
        }

        private static string GameRequireDisplay(string value, int max)
        {
            if (value == null || value.Length < 1 || value.Length > max) throw new ArgumentException(nameof(value));
            foreach (char c in value) if (c < 32 || c > 126) throw new ArgumentException(nameof(value));
            return value;
        }

        public void Begin(SessionRequest request, IReadOnlyList<ParticipantState> roster, MapGameplayDefinition map)
        {
            this.request = request ?? throw new ArgumentNullException(nameof(request));
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            if (roster.Count != request.ParticipantCount) throw new ArgumentException("roster count", nameof(roster));
            rows.Clear();
            entityToRow.Clear();
            slotLastUsedActiveTick.Clear();
            team1Kills = team2Kills = 0;
            activeTicksElapsed = 0;
            outcome = MatchOutcome.None;
            reason = FinishReason.None;
            results = null;
            phase = GameMatchPhase.Ready;
            for (int i = 0; i < roster.Count; i++)
            {
                var p = roster[i] ?? throw new ArgumentException("null participant", nameof(roster));
                var row = new Row
                {
                    ParticipantId = p.ParticipantId,
                    DisplayName = p.DisplayName,
                    Team = p.Team,
                    IsHuman = p.IsHuman,
                    EntityId = p.EntityId,
                    LifeId = p.LifeId,
                    Alive = p.Alive,
                    Kills = p.Kills,
                    Deaths = p.Deaths,
                    RespawnDueActiveTick = p.RespawnDueActiveTick,
                    SpawnFailureDeadlineActiveTick = 0
                };
                rows.Add(row);
                entityToRow[p.EntityId.Value] = i;
            }
        }

        public void MarkPlaying(ulong sessionId, uint simulationTick)
        {
            if (phase != GameMatchPhase.Ready && phase != GameMatchPhase.Playing) throw new InvalidOperationException("phase");
            authoritySessionId = sessionId;
            startTick = simulationTick;
            phase = GameMatchPhase.Playing;
        }

        public void ProcessDeath(DeathResolution death)
        {
            if (phase != GameMatchPhase.Playing || request == null) return;
            if (death.AuthoritySessionId != authoritySessionId) return;
            if (!entityToRow.TryGetValue(death.VictimEntityId.Value, out int victimIndex)) return;
            var victim = rows[victimIndex];
            if (victim.LifeId.Value != death.VictimLifeId.Value) return;
            if (victim.HasLastDeathEvent && SameEvent(victim.LastDeathEvent, death.DeathEventId)) return;
            if (!victim.Alive) return;

            victim.HasLastDeathEvent = true;
            victim.LastDeathEvent = death.DeathEventId;
            victim.Alive = false;
            victim.Deaths++;
            victim.RespawnDueActiveTick = unchecked(activeTicksElapsed + (uint)GameContractVersion.RespawnDelayActiveTicks);
            victim.SpawnFailureDeadlineActiveTick = 0;

            if (death.KillerEntityId is EntityId killerId &&
                entityToRow.TryGetValue(killerId.Value, out int killerIndex))
            {
                var killer = rows[killerIndex];
                if (killer.Team != victim.Team)
                {
                    killer.Kills++;
                    if (killer.Team == 1) team1Kills++;
                    else team2Kills++;
                }
            }
        }

        public void EvaluateEndOfTick(uint completedSimulationTick, bool advanceActiveClock)
        {
            if (phase != GameMatchPhase.Playing || request == null) return;
            if (advanceActiveClock) activeTicksElapsed++;

            bool scoreHit = team1Kills >= (uint)request.ScoreLimit || team2Kills >= (uint)request.ScoreLimit;
            bool timeHit = activeTicksElapsed >= (uint)request.TimeLimitActiveTicks;
            if (!scoreHit && !timeHit) return;

            endTick = completedSimulationTick;
            phase = GameMatchPhase.Finished;
            if (scoreHit)
            {
                reason = FinishReason.ScoreLimit;
            }
            else
            {
                reason = FinishReason.TimeLimit;
            }

            if (team1Kills > team2Kills) outcome = MatchOutcome.Team1;
            else if (team2Kills > team1Kills) outcome = MatchOutcome.Team2;
            else outcome = MatchOutcome.Draw;

            CancelPendingRespawns();
            results = BuildResults();
        }

        public void ScheduleDueRespawns(uint activeTicks, IActorLifecycle lifecycle, IMapRuntime map)
        {
            if (phase != GameMatchPhase.Playing || map == null || request == null || this.map == null) return;

            var living = new List<EntityId>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Alive) living.Add(rows[i].EntityId);

            var provisional = new List<Float3>(4);
            // Stable ParticipantId order for simultaneous due respawns.
            var dueOrder = new List<int>(rows.Count);
            for (int i = 0; i < rows.Count; i++) dueOrder.Add(i);
            dueOrder.Sort((a, b) => rows[a].ParticipantId.CompareTo(rows[b].ParticipantId));

            for (int oi = 0; oi < dueOrder.Count; oi++)
            {
                var row = rows[dueOrder[oi]];
                if (row.Alive || row.RespawnDueActiveTick == 0) continue;
                if (activeTicks < row.RespawnDueActiveTick) continue;

                // Abort at first-failure + 600 inclusive; retries must not move this deadline.
                if (row.SpawnFailureDeadlineActiveTick != 0 &&
                    activeTicks >= row.SpawnFailureDeadlineActiveTick)
                {
                    phase = GameMatchPhase.Stopped;
                    reason = FinishReason.Aborted;
                    outcome = MatchOutcome.None;
                    return;
                }

                if (!SpawnSafetyPolicy.TryPick(
                        row.Team,
                        this.map.SpawnSlots,
                        map,
                        lifecycle,
                        living,
                        slotLastUsedActiveTick,
                        provisional,
                        out var slot,
                        relaxLineOfFire: RelaxDue(activeTicks, row.SpawnFailureDeadlineActiveTick)))
                {
                    if (row.SpawnFailureDeadlineActiveTick == 0)
                        row.SpawnFailureDeadlineActiveTick = unchecked(activeTicks + (uint)GameContractVersion.SpawnFailureTimeoutTicks);
                    // Retry cadence only; never move the first-failure deadline.
                    row.RespawnDueActiveTick = unchecked(activeTicks + (uint)GameContractVersion.SpawnRetryDeferTicks);
                    continue;
                }

                entityToRow.Remove(row.EntityId.Value);
                lifecycle.DespawnLife(row.EntityId);
                var nextLife = new LifeId(unchecked(row.LifeId.Value + 1));
                var entity = lifecycle.SpawnLife(row.ParticipantId, nextLife, slot.Position, slot.Yaw, row.Team);
                row.EntityId = entity;
                row.LifeId = nextLife;
                row.Alive = true;
                row.RespawnDueActiveTick = 0;
                row.SpawnFailureDeadlineActiveTick = 0;
                row.HasLastDeathEvent = false;
                row.LastDeathEvent = default;
                entityToRow[entity.Value] = dueOrder[oi];
                lifecycle.ResetLife(entity, slot.Position, slot.Yaw);
                slotLastUsedActiveTick[slot.SlotId] = activeTicks;
                provisional.Add(slot.Position);
                living.Add(entity);
            }
        }

        private static bool RelaxDue(uint activeTicks, uint failureDeadline)
        {
            if (failureDeadline == 0) return false;
            uint firstFailure = unchecked(failureDeadline - (uint)GameContractVersion.SpawnFailureTimeoutTicks);
            return unchecked(activeTicks - firstFailure) >= (uint)SpawnSafetyPolicy.SpawnRelaxAfterTicks;
        }

        public MatchViewSnapshot AcquireSnapshot(ulong sessionId, uint completedSimulationTick)
        {
            uint remaining = 0;
            if (request != null)
            {
                uint limit = (uint)request.TimeLimitActiveTicks;
                remaining = activeTicksElapsed >= limit ? 0u : limit - activeTicksElapsed;
            }
            var list = new ParticipantState[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                list[i] = new ParticipantState(r.ParticipantId, r.DisplayName, r.Team, r.IsHuman, r.EntityId, r.LifeId, r.Alive, r.Kills, r.Deaths, r.RespawnDueActiveTick);
            }
            uint pending = 0;
            for (int i = 0; i < rows.Count; i++) if (!rows[i].Alive && rows[i].RespawnDueActiveTick != 0) pending++;
            return new MatchViewSnapshot(sessionId, completedSimulationTick, activeTicksElapsed, remaining, phase, outcome, reason, team1Kills, team2Kills, list, pending);
        }

        public MatchResults? TryAcquireResults() => results;

        public void Abort(ulong sessionId)
        {
            authoritySessionId = sessionId;
            phase = GameMatchPhase.Stopped;
            reason = FinishReason.Aborted;
            outcome = MatchOutcome.None;
            results = null;
            CancelPendingRespawns();
        }

        private MatchResults BuildResults()
        {
            if (request == null || map == null) throw new InvalidOperationException("not started");
            var sorted = new List<ParticipantState>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                sorted.Add(new ParticipantState(r.ParticipantId, r.DisplayName, r.Team, r.IsHuman, r.EntityId, r.LifeId, r.Alive, r.Kills, r.Deaths, r.RespawnDueActiveTick));
            }
            sorted.Sort(CompareRows);
            return new MatchResults(
                request,
                map.MapId,
                catalogIdentity,
                gameplayIdentity,
                buildIdentity,
                authoritySessionId,
                startTick,
                endTick,
                activeTicksElapsed,
                team1Kills,
                team2Kills,
                outcome,
                reason,
                sorted);
        }

        private static int CompareRows(ParticipantState a, ParticipantState b)
        {
            int cmp = b.Kills.CompareTo(a.Kills);
            if (cmp != 0) return cmp;
            cmp = a.Deaths.CompareTo(b.Deaths);
            if (cmp != 0) return cmp;
            return a.ParticipantId.CompareTo(b.ParticipantId);
        }

        private void CancelPendingRespawns()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (!rows[i].Alive)
                {
                    rows[i].RespawnDueActiveTick = 0;
                    rows[i].SpawnFailureDeadlineActiveTick = 0;
                }
            }
        }

        private static bool SameEvent(EventId a, EventId b) =>
            a.AuthoritySessionId == b.AuthoritySessionId &&
            a.Tick == b.Tick &&
            a.EntityId.Equals(b.EntityId) &&
            a.Ordinal == b.Ordinal;
    }
}
