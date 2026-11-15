#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Simulation;
using Shooter.Simulation.AI;
using Shooter.Simulation.Motor;
using Shooter.Simulation.Tests.Motor;
using Xunit;
using Xunit.Abstractions;

namespace Shooter.Simulation.Tests.AI
{
    /// <summary>
    /// T084 time-to-kill envelope: a bot duelling a stationary, standing human-sized target through the real
    /// combat harness (rifle 30 dmg, hit volumes, spread, recoil). Players must get a readable window — misses
    /// first, then hits — instead of dying inside the first half-second.
    /// </summary>
    public sealed class BotLethalityTests
    {
        private readonly ITestOutputHelper output;
        public BotLethalityTests(ITestOutputHelper output) { this.output = output; }

        private sealed class OpenWorld : IWorldQueries
        {
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter) => new RayHit(QueryStatus.NoHit);
        }

        private sealed class NoNav : INavigation
        {
            private ulong next;
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision) =>
                new PathTicket(new NavTicketId(++next), actor, navRevision);
            public void Cancel(PathTicket ticket) { }
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned) =>
                new PathResult(PathStatus.Failed, 0, 0, ErrorCode.MissingContent);
        }

        /// <summary>Ticks from the bot first seeing the target to the kill (uint.MaxValue when it survives 10 s).</summary>
        internal static uint TicksToKill(float distance, int padding, out uint firstShotTick, out int shots)
        {
            var sim = new CombatHarnessSimulation(new StaticPlaneWorld());
            // Burn entity ids so different bots (personality/noise seeds) are sampled.
            for (int i = 0; i < padding; i++) sim.RetireActor(sim.SpawnActor(new Float3(50 + i * 3, 0, -50), 0, 0));
            var shooter = sim.SpawnActor(new Float3(0, 0, 0), 0, 1);
            var target = sim.SpawnActor(new Float3(0, 0, distance), 3.1f, 2);
            var ai = new CombatAiDirector(new OpenWorld(), new NoNav(), 1u << 8);
            var bot = ai.RegisterBot(shooter, new Float3(0, 0, 0), 0, team: 1);
            var cmds = new InputCommand[1];
            firstShotTick = uint.MaxValue;
            shots = 0;
            for (uint t = 0; t < 600; t++)
            {
                Assert.True(sim.TryGetMotorState(shooter, out var sm));
                Assert.True(sim.TryGetMotorState(target, out var tm));
                bot.Feet = sm.Feet;
                bot.Crouched = sm.Stance == Stance.Crouching;
                var point = new Float3(tm.Feet.X, tm.Feet.Y + (tm.Stance == Stance.Crouching
                    ? CombatAiDirector.CrouchedAimHeight : CombatAiDirector.StandingAimHeight), tm.Feet.Z);
                ai.PlanEnemies(t, new[] { new SenseTarget(target, point, 2) }, 1, cmds, 0);
                var c = cmds[0];
                if ((c.Buttons & InputButtons.FireHeld) != 0)
                {
                    shots++;
                    if (firstShotTick == uint.MaxValue) firstShotTick = t;
                }
                var botCmd = new InputCommand(shooter, t, t + 1, c.MoveX, c.MoveY, c.AimYaw, c.AimPitch, c.Buttons, 0);
                var tgtCmd = new InputCommand(target, t, t + 1, 0, 0, 3.1f, 0, InputButtons.None, 0);
                sim.Step(t, new[] { botCmd, tgtCmd });
                using var snap = sim.AcquireSnapshot();
                foreach (var e in snap.Snapshot.Entities)
                {
                    if (e.Id.Equals(target) && e.Health == 0) return t;
                    if (e.Id.Equals(shooter)) bot.MagAmmo = e.Weapon.AmmoInMagazine;
                }
            }
            return uint.MaxValue;
        }

        [Theory]
        // Measured on the T084 tuning (median of 9 bots, ticks from sighting): 8 m ≈ 98, 20 m ≈ 151, 40 m ≈ 233.
        // T082 tuning was 61 / 90 / 143 with kills 0.4 s after the first shot.
        [InlineData(8f, 75u, 140u)]
        [InlineData(20f, 110u, 240u)]
        [InlineData(40f, 160u, 420u)]
        public void Bot_time_to_kill_a_still_target_stays_in_a_readable_window(float distance, uint minMedian, uint maxMedian)
        {
            const int samples = 9;
            var ttk = new uint[samples];
            var sinceFirstShot = new uint[samples];
            for (int s = 0; s < samples; s++)
            {
                ttk[s] = TicksToKill(distance, s, out uint first, out int shots);
                sinceFirstShot[s] = ttk[s] == uint.MaxValue || first == uint.MaxValue ? uint.MaxValue : ttk[s] - first;
                output.WriteLine($"d={distance} sample={s} ttk={ttk[s]} firstShot={first} sinceFirstShot={sinceFirstShot[s]} shots={shots}");
            }
            Array.Sort(ttk);
            Array.Sort(sinceFirstShot);
            uint median = ttk[samples / 2];
            output.WriteLine($"median ttk={median} median since first shot={sinceFirstShot[samples / 2]}");
            Assert.InRange(median, minMedian, maxMedian);
            // Nobody dies to the first trigger pull: the opening shots are visibly off target.
            Assert.True(sinceFirstShot[0] >= 40, "fastest kill after the first shot " + sinceFirstShot[0] + " ticks");
        }
    }
}
