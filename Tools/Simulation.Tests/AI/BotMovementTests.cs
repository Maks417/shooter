#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation;
using Shooter.Simulation.AI;
using Shooter.Simulation.Motor;
using Shooter.Simulation.Pose;
using Shooter.Simulation.Tests.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.AI
{
    /// <summary>Bot sprint / crouch / jump / slide behaviour and stance-aware aim height. All runs are short.</summary>
    public sealed class BotMovementTests
    {
        private sealed class OpenWorld : IWorldQueries
        {
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter) => new RayHit(QueryStatus.NoHit);
        }

        private sealed class FixedNav : INavigation
        {
            private readonly Float3[]? corners;
            private ulong next;
            public FixedNav(Float3[]? corners) { this.corners = corners; }
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision) =>
                new PathTicket(new NavTicketId(++next), actor, navRevision);
            public void Cancel(PathTicket ticket) { }
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned)
            {
                if (corners == null) return new PathResult(PathStatus.Failed, 0, 0, ErrorCode.MissingContent);
                for (int i = 0; i < corners.Length; i++) callerOwned[i] = corners[i];
                return new PathResult(PathStatus.Complete, corners.Length, corners.Length, ErrorCode.None);
            }
        }

        private static CombatAiDirector Director(Float3[]? corners) =>
            new CombatAiDirector(new OpenWorld(), new FixedNav(corners), 1u << 8);

        private static readonly Float3[] LongLeg = { new Float3(0, 0, 0), new Float3(60, 0, 0) };

        [Fact]
        public void Patrol_on_a_path_sprints_without_fire()
        {
            var ai = Director(LongLeg);
            ai.SetNavigationDestinations(new[] { new Float3(60, 0, 0) }, null);
            ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), 0, team: 1);
            var cmds = new InputCommand[1];
            ai.Plan(1, null, cmds, 0);
            ai.Plan(2, null, cmds, 0);
            Assert.True(cmds[0].MoveY > 0.9f);
            Assert.NotEqual(InputButtons.None, cmds[0].Buttons & InputButtons.SprintHeld);
            Assert.Equal(InputButtons.None, cmds[0].Buttons & (InputButtons.FireHeld | InputButtons.CrouchHeld | InputButtons.AimHeld));
        }

        private static InputCommand EngageStationary(ulong id, float distance, out AiBlackboard board)
        {
            var ai = Director(null);
            board = ai.RegisterBot(new EntityId(id), new Float3(0, 0, 0), 0, team: 1);
            var enemy = new SenseTarget(new EntityId(99), new Float3(0, CombatAiDirector.StandingAimHeight, distance), 2);
            var enemies = new[] { enemy };
            var cmds = new InputCommand[1];
            for (uint t = 1; t <= 60; t++) ai.PlanEnemies(t, enemies, 1, cmds, 0);
            return cmds[0];
        }

        [Fact]
        public void Engaging_bot_never_sprints_and_fires()
        {
            var cmd = EngageStationary(3, 20f, out var board);
            Assert.Equal(AiState.Engage, board.State);
            Assert.NotEqual(InputButtons.None, cmd.Buttons & InputButtons.FireHeld);
            Assert.Equal(InputButtons.None, cmd.Buttons & InputButtons.SprintHeld);
            Assert.Equal(InputButtons.None, cmd.Buttons & InputButtons.JumpPressed);
        }

        [Fact]
        public void Stationary_engage_crouches_by_id_or_long_range()
        {
            Assert.NotEqual(InputButtons.None, EngageStationary(2, 20f, out _).Buttons & InputButtons.CrouchHeld);
            Assert.Equal(InputButtons.None, EngageStationary(3, 20f, out _).Buttons & InputButtons.CrouchHeld);
            Assert.NotEqual(InputButtons.None, EngageStationary(3, CombatAiDirector.LongRangeCrouchDistance + 5f, out _).Buttons & InputButtons.CrouchHeld);
        }

        [Fact]
        public void Stuck_bot_jumps_once_per_cooldown_as_a_single_tick_edge()
        {
            var ai = Director(LongLeg);
            ai.SetNavigationDestinations(new[] { new Float3(60, 0, 0) }, null);
            ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), 0, team: 1);
            var cmds = new InputCommand[1];
            var jumpTicks = new List<uint>();
            for (uint t = 1; t <= 130; t++)
            {
                ai.Plan(t, null, cmds, 0);
                if ((cmds[0].Buttons & InputButtons.JumpPressed) != 0) jumpTicks.Add(t);
            }
            Assert.NotEmpty(jumpTicks);
            for (int i = 1; i < jumpTicks.Count; i++)
                Assert.True(jumpTicks[i] - jumpTicks[i - 1] >= CombatAiDirector.JumpCooldownTicks);
            Assert.True(jumpTicks[0] <= CombatAiDirector.StuckTicksThreshold + 10);
        }

        private static bool RunSlideRequests(out List<uint> requests)
        {
            var ai = Director(new[] { new Float3(0, 0, 0), new Float3(500, 0, 0) });
            ai.SetCoverSites(new[] { new CoverSite(1, new Float3(500, 0, 0), new Float3(500, 0, 2)) });
            var bot = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), 0, team: 1);
            bot.State = AiState.SeekCover;
            var cmds = new InputCommand[1];
            requests = new List<uint>();
            for (uint t = 1; t <= 200; t++)
            {
                bot.Feet = new Float3(0.1f * t, 0, 0); // 6 m/s along the leg
                ai.Plan(t, null, cmds, 0);
                if (ai.WantsSlide(new EntityId(1))) requests.Add(t);
            }
            return requests.Count > 0;
        }

        [Fact]
        public void Sprinting_toward_cover_requests_a_slide_with_cooldown()
        {
            Assert.True(RunSlideRequests(out var requests));
            Assert.True(requests[0] >= CombatAiDirector.SlideMinSprintTicks);
            Assert.Single(requests); // 600+ tick bot cooldown inside a 200 tick run
        }

        [Fact]
        public void Slide_not_requested_when_slow_or_in_patrol()
        {
            var ai = Director(LongLeg);
            ai.SetNavigationDestinations(new[] { new Float3(60, 0, 0) }, null);
            var bot = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), 0, team: 1);
            var cmds = new InputCommand[1];
            for (uint t = 1; t <= 100; t++)
            {
                bot.Feet = new Float3(0.1f * t, 0, 0);
                ai.Plan(t, null, cmds, 0);
                Assert.False(ai.WantsSlide(new EntityId(1)));
            }
        }

        [Fact]
        public void Aim_point_is_inside_the_torso_of_the_current_stance_and_eye_follows_own_stance()
        {
            var pose = new GameplayPoseEvaluator();
            foreach (var (stance, height) in new[] { (Stance.Standing, 1.8f), (Stance.Crouching, 1.2f) })
            {
                var volumes = new List<HitVolume>();
                var feet = new Float3(0, 0, 0);
                pose.Evaluate(new EntityId(5), feet, 0, 0, stance, height, 1, volumes);
                float y = stance == Stance.Crouching ? CombatAiDirector.CrouchedAimHeight : CombatAiDirector.StandingAimHeight;
                HitVolume torso = default;
                foreach (var v in volumes) if (v.RegionId == 2) torso = v;
                var cap = torso.Capsule;
                Assert.True(y >= cap.BottomSphereCenter.Y - cap.Radius && y <= cap.TopSphereCenter.Y + cap.Radius, stance + " aim height outside torso");
                // The old standing-eye point (1.65 m) is above the crouched head (eye 1.05 + 0.12).
                if (stance == Stance.Crouching) Assert.True(1.65f > 1.05f + 0.12f);
            }

            // Own eye: a crouched bot aiming at a point at its own eye level looks level (pitch 0).
            var ai = Director(null);
            var bot = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), 0, team: 1);
            bot.Crouched = true;
            var cmds = new InputCommand[1];
            var enemy = new[] { new SenseTarget(new EntityId(99), new Float3(0, CombatAiDirector.CrouchedEyeHeight, 20), 2) };
            for (uint t = 1; t <= 60; t++) ai.PlanEnemies(t, enemy, 1, cmds, 0);
            Assert.Equal(AiState.Engage, bot.State);
            // Level target at own crouched eye height; the muzzle-offset compensation lifts the aim by 0.15 m.
            float expected = -(float)Math.Atan2(CombatAiDirector.MuzzleDownOffset, 20f);
            Assert.Equal(expected, cmds[0].AimPitch, 2);
        }

        private static float DamageDealtToTarget(bool targetCrouches)
        {
            var sim = new CombatHarnessSimulation(new StaticPlaneWorld());
            var shooter = sim.SpawnActor(new Float3(0, 0, 0), 0, 1);
            var target = sim.SpawnActor(new Float3(0, 0, 8), 0, 2);
            var ai = Director(null);
            var bot = ai.RegisterBot(shooter, new Float3(0, 0, 0), 0, team: 1);
            var cmds = new InputCommand[1];
            for (uint t = 0; t < 150; t++)
            {
                Assert.True(sim.TryGetMotorState(shooter, out var sm));
                Assert.True(sim.TryGetMotorState(target, out var tm));
                bot.Feet = sm.Feet;
                bot.Crouched = sm.Stance == Stance.Crouching;
                var point = new Float3(tm.Feet.X, tm.Feet.Y + (tm.Stance == Stance.Crouching ? CombatAiDirector.CrouchedAimHeight : CombatAiDirector.StandingAimHeight), tm.Feet.Z);
                ai.PlanEnemies(t, new[] { new SenseTarget(target, point, 2) }, 1, cmds, 0);
                var c = cmds[0];
                var botCmd = new InputCommand(shooter, t, t + 1, 0, 0, c.AimYaw, c.AimPitch, c.Buttons, 0);
                var tgtCmd = new InputCommand(target, t, t + 1, 0, 0, 3f, 0, targetCrouches ? InputButtons.CrouchHeld : InputButtons.None, 0);
                sim.Step(t, new[] { botCmd, tgtCmd });
            }
            using var snap = sim.AcquireSnapshot();
            foreach (var e in snap.Snapshot.Entities) if (e.Id.Equals(target)) return 100 - e.Health;
            return 0;
        }

        [Fact]
        public void Bot_damages_standing_and_crouched_targets()
        {
            Assert.True(DamageDealtToTarget(false) > 0);
            Assert.True(DamageDealtToTarget(true) > 0);
        }

        [Fact]
        public void Bot_reaches_sprint_speed_and_slides_in_the_motor()
        {
            var world = new StaticPlaneWorld();
            var sim = new CombatHarnessSimulation(world);
            var id = sim.SpawnActor(new Float3(0, 0, 0), 0, 1);
            var ai = Director(new[] { new Float3(0, 0, 0), new Float3(500, 0, 0) });
            ai.SetCoverSites(new[] { new CoverSite(1, new Float3(500, 0, 0), new Float3(500, 0, 2)) });
            var bot = ai.RegisterBot(id, new Float3(0, 0, 0), 0, team: 1);
            bot.State = AiState.SeekCover;
            var cmds = new InputCommand[1];
            var slideIds = new List<EntityId>();
            float maxSpeed = 0;
            bool slid = false;
            for (uint t = 0; t < 240; t++)
            {
                Assert.True(sim.TryGetMotorState(id, out var m));
                bot.Feet = m.Feet;
                bot.Crouched = m.Stance == Stance.Crouching;
                ai.Plan(t, null, cmds, 0);
                var c = cmds[0];
                var cmd = new InputCommand(id, t, t + 1, c.MoveX, c.MoveY, c.AimYaw, c.AimPitch, c.Buttons, c.SelectedSlot);
                slideIds.Clear();
                if (ai.WantsSlide(id)) slideIds.Add(id);
                sim.StepWithSlideActors(t, new[] { cmd }, slideIds);
                Assert.True(sim.TryGetMotorState(id, out var after));
                maxSpeed = Math.Max(maxSpeed, MotorMath.Length(MotorMath.Horizontal(after.Velocity)));
                slid |= after.SlideActive;
            }
            Assert.True(maxSpeed >= MotorConfig.BaselineV1.SprintSpeed - 0.5f, "max speed " + maxSpeed);
            Assert.True(slid, "bot never slid");
        }
    }
}
