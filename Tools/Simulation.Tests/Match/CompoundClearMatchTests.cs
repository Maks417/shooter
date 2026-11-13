#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Match;
using Xunit;

namespace Shooter.Simulation.Tests.Match
{
    public sealed class CompoundClearMatchTests
    {
        private static CompoundClearMatch Create()
        {
            var bots = new List<Float3>();
            for (int i = 0; i < 8; i++) bots.Add(new Float3(i, 0, i + 10));
            return new CompoundClearMatch(460072030UL, new Float3(-104, 0, -104), new Float3(96, 0, 96), bots);
        }

        [Fact]
        public void Restart_returns_identical_spawn_state_and_new_session()
        {
            var match = Create();
            ulong session = match.AuthoritySessionId;
            int leases = 0;
            match.RegisterLease(() => leases++);
            match.NotifyDeath(new EntityId(1));
            Assert.Equal(MatchState.Lost, match.State);
            Assert.Equal(1, match.LossEvents);
            match.Restart();
            Assert.Equal(1, leases);
            Assert.Equal(session + 1, match.AuthoritySessionId);
            Assert.Equal(MatchState.Playing, match.State);
            Assert.Equal(0, match.CaptureProgress);
            Assert.Equal(9, match.Actors.Count);
            Assert.Equal(new Float3(-104, 0, -104), match.Actors[0].Feet);
            var fresh = match.CloneExpectedFreshState();
            Assert.Equal(fresh.Actors[0].Feet, match.Actors[0].Feet);
            for (int i = 0; i < 8; i++) Assert.Equal(fresh.Actors[i + 1].Feet, match.Actors[i + 1].Feet);
        }

        [Fact]
        public void Death_and_win_emit_exactly_once()
        {
            var match = Create();
            match.NotifyDeath(new EntityId(1));
            match.NotifyDeath(new EntityId(1));
            Assert.Equal(1, match.LossEvents);
            Assert.Equal(MatchState.Lost, match.State);

            match.Restart();
            var living = new List<CompoundClearMatch.ActorState>
            {
                new CompoundClearMatch.ActorState(new EntityId(1), 1, new Float3(96, 0, 96), true, 100)
            };
            // No living bots → capture
            for (ushort t = 1; t <= CompoundClearMatch.CaptureTicksRequired; t++)
                match.Step(t, living);
            Assert.Equal(MatchState.Won, match.State);
            Assert.Equal(1, match.WinEvents);
            match.Step(500, living);
            Assert.Equal(1, match.WinEvents);
        }

        [Fact]
        public void Bot_in_zone_or_player_leaving_resets_capture()
        {
            var match = Create();
            var progress = new List<CompoundClearMatch.ActorState>
            {
                new CompoundClearMatch.ActorState(new EntityId(1), 1, new Float3(96, 0, 96), true, 100)
            };
            for (uint t = 1; t <= 50; t++) match.Step(t, progress);
            Assert.Equal(50, match.CaptureProgress);
            var withBot = new List<CompoundClearMatch.ActorState>
            {
                progress[0],
                new CompoundClearMatch.ActorState(new EntityId(2), 2, new Float3(96, 0, 96), true, 100)
            };
            match.Step(51, withBot);
            Assert.Equal(0, match.CaptureProgress);
        }
    }
}
