#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Simulation.Match
{
    /// <summary>Compound Clear v1 objective loop: spawn→combat→capture/loss→restart with deterministic reset.</summary>
    public sealed class CompoundClearMatch
    {
        public const ushort CaptureTicksRequired = 120;
        public const float ObjectiveRadius = 3f;
        public const float ObjectiveVerticalMeters = 2f;

        public readonly struct ActorState
        {
            public EntityId Id { get; }
            public byte Team { get; }
            public Float3 Feet { get; }
            public bool Alive { get; }
            public ushort Health { get; }
            public ActorState(EntityId id, byte team, Float3 feet, bool alive, ushort health)
            { Id = id; Team = team; Feet = feet; Alive = alive; Health = health; }
        }

        private readonly ulong matchSeed;
        private readonly Float3 playerSpawn;
        private readonly Float3 objectiveCenter;
        private readonly Float3[] botSpawns;
        private readonly List<ActorState> actors = new List<ActorState>();
        private readonly List<Action> leaseReleases = new List<Action>();
        private MatchState state = MatchState.Ready;
        private ushort captureProgress;
        private uint tick;
        private ulong authoritySessionId = 1;
        private int deathEvents;
        private int winEvents;
        private int lossEvents;
        private int restartCount;

        public MatchState State => state;
        public ushort CaptureProgress => captureProgress;
        public ulong AuthoritySessionId => authoritySessionId;
        public uint Tick => tick;
        public int DeathEvents => deathEvents;
        public int WinEvents => winEvents;
        public int LossEvents => lossEvents;
        public int RestartCount => restartCount;
        public IReadOnlyList<ActorState> Actors => actors;

        public CompoundClearMatch(ulong matchSeed, Float3 playerSpawn, Float3 objectiveCenter, IReadOnlyList<Float3> botSpawns)
        {
            if (botSpawns == null || botSpawns.Count != 8) throw new ArgumentException("Eight bot spawns required.", nameof(botSpawns));
            this.matchSeed = matchSeed;
            this.playerSpawn = playerSpawn;
            this.objectiveCenter = objectiveCenter;
            this.botSpawns = new Float3[8];
            for (int i = 0; i < 8; i++) this.botSpawns[i] = botSpawns[i];
            ResetActorsFromSeed();
            state = MatchState.Playing;
        }

        public void RegisterLease(Action release) => leaseReleases.Add(release ?? throw new ArgumentNullException(nameof(release)));

        public void Step(uint simulationTick, IReadOnlyList<ActorState> liveActors)
        {
            if (liveActors == null) throw new ArgumentNullException(nameof(liveActors));
            if (state != MatchState.Playing) return;
            tick = simulationTick;
            actors.Clear();
            for (int i = 0; i < liveActors.Count; i++) actors.Add(liveActors[i]);

            ActorState? player = null;
            int livingBots = 0;
            bool botInZone = false;
            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                if (a.Team == 1) player = a;
                else if (a.Alive) { livingBots++; if (InObjective(a.Feet)) botInZone = true; }
            }
            if (!player.HasValue) throw new InvalidOperationException("Player actor missing.");
            if (!player.Value.Alive)
            {
                EmitLossOnce();
                return;
            }
            if (livingBots == 0 && InObjective(player.Value.Feet) && !botInZone)
            {
                captureProgress++;
                if (captureProgress >= CaptureTicksRequired) EmitWinOnce();
            }
            else captureProgress = 0;
        }

        public void NotifyDeath(EntityId id)
        {
            if (state != MatchState.Playing) return;
            deathEvents++;
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i].Id != id) continue;
                var a = actors[i];
                actors[i] = new ActorState(a.Id, a.Team, a.Feet, false, 0);
                if (a.Team == 1) EmitLossOnce();
                return;
            }
        }

        public void Restart()
        {
            restartCount++;
            for (int i = 0; i < leaseReleases.Count; i++) leaseReleases[i]();
            leaseReleases.Clear();
            authoritySessionId++;
            captureProgress = 0;
            tick = 0;
            // Death/win/loss counters are cumulative diagnostics; terminal state resets for play.
            ResetActorsFromSeed();
            state = MatchState.Playing;
        }

        public CompoundClearMatch CloneExpectedFreshState()
            => new CompoundClearMatch(matchSeed, playerSpawn, objectiveCenter, botSpawns);

        private void EmitWinOnce()
        {
            if (state == MatchState.Won) return;
            state = MatchState.Won;
            winEvents++;
            captureProgress = CaptureTicksRequired;
        }

        private void EmitLossOnce()
        {
            if (state == MatchState.Lost) return;
            state = MatchState.Lost;
            lossEvents++;
            captureProgress = 0;
        }

        private bool InObjective(Float3 feet)
        {
            float dx = feet.X - objectiveCenter.X, dz = feet.Z - objectiveCenter.Z;
            if (dx * dx + dz * dz > ObjectiveRadius * ObjectiveRadius) return false;
            return Math.Abs(feet.Y - objectiveCenter.Y) <= ObjectiveVerticalMeters;
        }

        private void ResetActorsFromSeed()
        {
            actors.Clear();
            actors.Add(new ActorState(new EntityId(1), 1, playerSpawn, true, 100));
            for (int i = 0; i < 8; i++)
                actors.Add(new ActorState(new EntityId((ulong)(i + 2)), 2, botSpawns[i], true, 100));
        }
    }
}
