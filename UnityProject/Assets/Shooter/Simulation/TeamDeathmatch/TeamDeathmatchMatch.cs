#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;

namespace Shooter.Simulation.TeamDeathmatch
{
    /// <summary>
    /// Pure M9 team-deathmatch score/deadline/respawn reducer. No Unity or file I/O.
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
            public HashSet<ulong> SeenDeathEvents = new HashSet<ulong>();
        }

        private SessionRequest? request;
        private MapGameplayDefinition? map;
        private string catalogIdentity = "unset";
        private string gameplayIdentity = "unset";
        private string buildIdentity = "build-local";
        private readonly List<Row> rows = new List<Row>(GameContractVersion.MaxParticipants);
        private readonly Dictionary<ulong, int> entityToRow = new Dictionary<ulong, int>();
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
                    RespawnDueActiveTick = p.RespawnDueActiveTick
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
            ulong eventKey = HashEvent(death.DeathEventId);
            if (!victim.SeenDeathEvents.Add(eventKey)) return;
            if (!victim.Alive) return;

            victim.Alive = false;
            victim.Deaths++;
            victim.RespawnDueActiveTick = unchecked(activeTicksElapsed + (uint)GameContractVersion.RespawnDelayActiveTicks);

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

            results = BuildResults();
        }

        public void ScheduleDueRespawns(uint activeTicks, IActorLifecycle lifecycle, IMapRuntime map)
        {
            if (phase != GameMatchPhase.Playing || map == null || request == null) return;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Alive || row.RespawnDueActiveTick == 0) continue;
                if (activeTicks < row.RespawnDueActiveTick) continue;
                if (activeTicks > unchecked(row.RespawnDueActiveTick + (uint)GameContractVersion.SpawnFailureTimeoutTicks))
                {
                    phase = GameMatchPhase.Stopped;
                    reason = FinishReason.Aborted;
                    outcome = MatchOutcome.None;
                    return;
                }

                if (!TryPickSpawn(row.Team, map, out var slot))
                {
                    row.RespawnDueActiveTick = unchecked(activeTicks + (uint)GameContractVersion.SpawnRetryDeferTicks);
                    continue;
                }

                entityToRow.Remove(row.EntityId.Value);
                var nextLife = new LifeId(unchecked(row.LifeId.Value + 1));
                var entity = lifecycle.SpawnLife(row.ParticipantId, nextLife, slot.Position, slot.Yaw, row.Team);
                row.EntityId = entity;
                row.LifeId = nextLife;
                row.Alive = true;
                row.RespawnDueActiveTick = 0;
                entityToRow[entity.Value] = i;
                lifecycle.ResetLife(entity, slot.Position, slot.Yaw);
            }
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

        private bool TryPickSpawn(byte team, IMapRuntime map, out SpawnSlot chosen)
        {
            chosen = default;
            if (this.map == null) return false;
            SpawnSlot? best = null;
            foreach (var slot in this.map.SpawnSlots)
            {
                if (slot.Team != team) continue;
                if (!map.TryValidateSpawn(slot, out _)) continue;
                if (best == null || slot.SlotId < best.Value.SlotId) best = slot;
            }
            if (best == null) return false;
            chosen = best.Value;
            return true;
        }

        private static ulong HashEvent(EventId id)
        {
            unchecked
            {
                ulong h = id.AuthoritySessionId;
                h = (h * 397) ^ id.Tick;
                h = (h * 397) ^ id.EntityId.Value;
                h = (h * 397) ^ id.Ordinal;
                return h;
            }
        }
    }
}
