#nullable enable
using Shooter.Game.Contracts;
using Shooter.Presentation.Session;
using Xunit;

namespace Shooter.Simulation.Tests.Shell
{
    public sealed class ShellSessionCoordinatorTests
    {
        [Fact]
        public void Start_is_accepted_once_and_rejects_double_submit()
        {
            var coord = new ShellSessionCoordinator();
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True(coord.TryBeginStart(request, out _));
            Assert.True(coord.StartInFlight);
            Assert.False(coord.TryBeginStart(request, out var error));
            Assert.Contains("already", error);
            coord.CompleteStartSuccess();
            Assert.True(coord.MatchActive);
            Assert.False(coord.StartInFlight);
            Assert.False(coord.TryBeginStart(request, out _));
        }

        [Fact]
        public void Cancel_and_failure_clear_inflight_without_activating_match()
        {
            var coord = new ShellSessionCoordinator();
            var request = new SessionRequest(WellKnownMaps.DepotYard, WellKnownModes.TeamDeathmatch, 8, 460072031UL);
            Assert.True(coord.TryBeginStart(request, out _));
            ulong epoch = coord.LoadEpoch;
            coord.CancelStart();
            Assert.False(coord.StartInFlight);
            Assert.False(coord.MatchActive);
            Assert.True(coord.SameLoadEpoch(epoch));

            Assert.True(coord.TryBeginStart(request, out _));
            coord.CompleteStartFailure();
            Assert.False(coord.MatchActive);
        }

        [Fact]
        public void Replay_reuses_last_request_after_teardown()
        {
            var coord = new ShellSessionCoordinator();
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True(coord.TryBeginStart(request, out _));
            coord.CompleteStartSuccess();
            coord.PrepareReplay(out var replay, out var error);
            Assert.Equal("", error);
            Assert.NotNull(replay);
            Assert.Equal(request.MapId, replay!.MapId);
            Assert.Equal(request.TeamSize, replay.TeamSize);
            Assert.True(coord.TryBeginStart(replay, out _));
        }
    }
}
