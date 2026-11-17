using System.Threading;
using System.Threading.Tasks;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.World.Runtime;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    /// <summary>
    /// A frame can run several simulation ticks; the presentation lease must carry every tick's combat events once
    /// (lost ReloadStarted edges left the reload animation unplayed).
    /// </summary>
    public sealed class PresentationEventLeaseTests
    {
        [Fact]
        public async Task Lease_carries_events_from_every_tick_since_previous_lease_once()
        {
            var session = new OfflineGameSession(StaticMapCatalog.CreateReleaseSeed(),
                (_, __) => new FakeMapRuntime(new PreMatchCountdownTests.FloorWorld(), new PreMatchCountdownTests.NoNav()), allowFakeMapRuntime: true);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            var human = session.HumanEntityId;

            for (uint i = 0; i < 90; i++)
            {
                var fire = new InputCommand(human, session.CompletedTick, i + 1, 0f, 0f, 0f, 0f,
                    InputButtons.FireHeld | InputButtons.FirePressed, 0);
                Assert.True(session.Step(fire).Succeeded);
            }
            Assert.True(session.Measurement.Shots > 1, "fixture must produce shots on several ticks");

            int shots = 0;
            using (var lease = session.TryLeaseCombatEvents())
            {
                Assert.NotNull(lease);
                var events = lease!.Events;
                for (int i = 0; i < events.Count; i++)
                    if (events[i].Kind == CombatEventKind.ShotAccepted) shots++;
            }
            Assert.Equal(session.Measurement.Shots, shots);

            using (var again = session.TryLeaseCombatEvents())
                Assert.Empty(again!.Events);
            session.Dispose();
        }
    }
}
