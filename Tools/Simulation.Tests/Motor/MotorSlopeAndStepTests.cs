#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Simulation.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Motor
{
    public sealed class MotorSlopeAndStepTests
    {
        private static readonly MotorConfig Cfg = MotorConfig.BaselineV1;
        private const float Dt = 1f / 60f;

        private static MotorBodyState SpawnAt(float y, float z = 0f) => new MotorBodyState(
            new Float3(0, y, z), default, 0, 0, Stance.Standing, 1.8f,
            true, new SurfaceId(100), new Float3(0, 1, 0), 0, 0, 0, false);

        private static InputCommand Cmd(uint seq, float my = 0, InputButtons buttons = InputButtons.None, float mx = 0) =>
            new InputCommand(new EntityId(1), seq, seq, mx, my, 0, 0, buttons, 0);

        private static MotorBodyState Run(KinematicMotor m, SlopeWorld w, MotorBodyState s, ref uint tick, int ticks, float my, InputButtons b, bool assertGrounded = false)
        {
            for (int i = 0; i < ticks; i++)
            {
                tick++;
                s = m.Step(s, Cmd(tick, my, b), tick, w);
                if (assertGrounded) Assert.True(s.Grounded, $"airborne at tick {tick}: feet={s.Feet.X},{s.Feet.Y},{s.Feet.Z} vel.Y={s.Velocity.Y}");
            }
            return s;
        }

        // ---------------- A. ramp launch ----------------
        [Theory]
        [InlineData(15f, false)]
        [InlineData(30f, false)]
        [InlineData(30f, true)]
        [InlineData(44f, true)]
        public void UphillRampKeepsActorGrounded(float angle, bool sprint)
        {
            var world = new SlopeWorld(angle);
            var motor = new KinematicMotor();
            uint tick = 0;
            var s = SpawnAt(world.GroundHeightAt(0));
            var b = sprint ? InputButtons.SprintHeld : InputButtons.None;
            s = Run(motor, world, s, ref tick, 90, 1f, b, assertGrounded: true);
            Assert.True(s.Feet.Z > 1f);
            Assert.Equal(world.GroundHeightAt(s.Feet.Z), s.Feet.Y, 2);
        }

        [Fact]
        public void CanJumpFromRampAndLandsGrounded()
        {
            var world = new SlopeWorld(30f);
            var motor = new KinematicMotor();
            uint tick = 0;
            var s = Run(motor, world, SpawnAt(world.GroundHeightAt(0)), ref tick, 60, 1f, InputButtons.None, assertGrounded: true);
            tick++;
            s = motor.Step(s, Cmd(tick, 1f, InputButtons.JumpPressed), tick, world);
            Assert.True(s.Velocity.Y > 0f);
            Assert.False(s.Grounded);
            bool landed = false;
            for (int i = 0; i < 120 && !landed; i++) { tick++; s = motor.Step(s, Cmd(tick, 0f), tick, world); landed = s.Grounded; }
            Assert.True(landed);
        }

        [Fact]
        public void SlideOnUphillRampIsNotCancelledByGroundLoss()
        {
            var world = new SlopeWorld(30f);
            var motor = new KinematicMotor();
            uint tick = 0;
            var s = Run(motor, world, SpawnAt(world.GroundHeightAt(0)), ref tick, 30, 1f, InputButtons.SprintHeld, assertGrounded: true);
            tick++;
            s = motor.Step(s, Cmd(tick, 1f, InputButtons.SprintHeld), tick, world, slidePressed: true);
            Assert.True(s.SlideActive);
            for (int i = 0; i < 10; i++) { tick++; s = motor.Step(s, Cmd(tick, 0f), tick, world); Assert.True(s.Grounded); Assert.True(s.SlideActive); }
        }

        // ---------------- B. downhill ground loss ----------------
        [Fact]
        public void SprintingDownA44DegreeRampStaysGrounded()
        {
            var world = new SlopeWorld(-44f);
            var motor = new KinematicMotor();
            uint tick = 0;
            Run(motor, world, SpawnAt(world.GroundHeightAt(0)), ref tick, 90, 1f, InputButtons.SprintHeld, assertGrounded: true);
        }

        [Theory]
        [InlineData(30f)]
        [InlineData(44f)]
        public void SlideDownARampLastsFullDuration(float angle)
        {
            var world = new SlopeWorld(-angle);
            var motor = new KinematicMotor();
            uint tick = 0;
            var s = Run(motor, world, SpawnAt(world.GroundHeightAt(0)), ref tick, 30, 1f, InputButtons.SprintHeld, assertGrounded: true);
            tick++;
            s = motor.Step(s, Cmd(tick, 1f, InputButtons.SprintHeld), tick, world, slidePressed: true);
            Assert.True(s.SlideActive);
            for (uint i = 1; i < MotorConfig.SlideDurationTicks - 1; i++)
            {
                tick++;
                s = motor.Step(s, Cmd(tick, 1f, InputButtons.SprintHeld), tick, world);
                Assert.True(s.Grounded, $"airborne during slide tick {i}");
                Assert.True(s.SlideActive, $"slide ended early at {i}");
            }
            tick++;
            s = motor.Step(s, Cmd(tick, 1f, InputButtons.SprintHeld), tick, world);
            Assert.False(s.SlideActive);
        }

        [Fact]
        public void WalkingOffARealLedgeBecomesAirborne()
        {
            var world = new SlopeWorld(plane: false);
            world.AddBox(new Float3(-5, -3, -10), new Float3(5, 0, 2), 7);
            var motor = new KinematicMotor();
            uint tick = 0;
            var s = SpawnAt(0, -1f);
            bool airborne = false;
            for (int i = 0; i < 120 && !airborne; i++) { tick++; s = motor.Step(s, Cmd(tick, 1f), tick, world); airborne = !s.Grounded; }
            Assert.True(airborne);
            Assert.True(s.Feet.Z > 1.5f);
        }

        // ---------------- C. steps ----------------
        [Theory]
        [InlineData(0.1f)]
        [InlineData(0.2f)]
        [InlineData(0.25f)]
        public void StepsUpToStepHeightAreClimbedSmoothly(float height)
        {
            var world = new SlopeWorld();
            world.AddBox(new Float3(-3, -1, 1f), new Float3(3, height, 30f), 5);
            var motor = new KinematicMotor();
            uint tick = 0;
            var s = SpawnAt(world.GroundHeightAt(0));
            for (int i = 0; i < 100; i++)
            {
                var prev = s;
                tick++;
                s = motor.Step(s, Cmd(tick, 1f), tick, world);
                float horiz = (float)Math.Sqrt((s.Feet.X - prev.Feet.X) * (s.Feet.X - prev.Feet.X) + (s.Feet.Z - prev.Feet.Z) * (s.Feet.Z - prev.Feet.Z));
                Assert.True(horiz <= Cfg.WalkSpeed * Dt + 0.005f, $"overshoot {horiz} at tick {tick}");
                if (prev.Feet.Z > 1.2f || s.Feet.Z > 1.2f) Assert.True(s.Grounded, $"airborne on step tick {tick} z={s.Feet.Z} y={s.Feet.Y}");
            }
            Assert.True(s.Feet.Z > 1.8f, $"did not climb, z={s.Feet.Z}");
            Assert.InRange(s.Feet.Y, height - 0.01f, height + 0.03f);
            Assert.True(s.Grounded);
        }

        [Fact]
        public void StepTallerThanStepHeightBlocks()
        {
            var world = new SlopeWorld();
            world.AddBox(new Float3(-3, -1, 1f), new Float3(3, 0.4f, 30f), 5);
            var motor = new KinematicMotor();
            uint tick = 0;
            var s = Run(motor, world, SpawnAt(world.GroundHeightAt(0)), ref tick, 100, 1f, InputButtons.None);
            Assert.True(s.Feet.Z < 1f - Cfg.Radius + 0.05f, $"z={s.Feet.Z}");
            Assert.Equal(0f, s.Feet.Y, 2);
        }

        // ---------------- basics ----------------
        [Fact]
        public void WalkAndSprintReachTheirSpeedsOnFlatGround()
        {
            var world = new SlopeWorld();
            var motor = new KinematicMotor();
            uint tick = 0;
            var s = Run(motor, world, SpawnAt(world.GroundHeightAt(0)), ref tick, 30, 1f, InputButtons.None);
            Assert.Equal(Cfg.WalkSpeed, s.Velocity.Z, 3);
            s = Run(motor, world, s, ref tick, 30, 1f, InputButtons.SprintHeld);
            Assert.Equal(Cfg.SprintSpeed, s.Velocity.Z, 3);
            Assert.True(s.SprintActive);
        }

        [Fact]
        public void CrouchLowersHeightAndSpeed()
        {
            var world = new SlopeWorld();
            var motor = new KinematicMotor();
            uint tick = 0;
            var s = Run(motor, world, SpawnAt(world.GroundHeightAt(0)), ref tick, 40, 1f, InputButtons.CrouchHeld);
            Assert.Equal(Stance.Crouching, s.Stance);
            Assert.Equal(Cfg.CrouchHeight, s.CapsuleHeight, 3);
            Assert.Equal(Cfg.CrouchSpeed, s.Velocity.Z, 3);
        }

        [Fact]
        public void StandingUpIsBlockedUnderLowCeiling()
        {
            var world = new SlopeWorld();
            world.AddBox(new Float3(-5, 1.35f, -5), new Float3(5, 1.55f, 5), 9);
            var motor = new KinematicMotor();
            uint tick = 0;
            var s = Run(motor, world, SpawnAt(world.GroundHeightAt(0)), ref tick, 5, 0f, InputButtons.CrouchHeld);
            Assert.Equal(Cfg.CrouchHeight, s.CapsuleHeight, 3);
            s = Run(motor, world, s, ref tick, 20, 0f, InputButtons.None);
            Assert.Equal(Stance.Crouching, s.Stance);
            Assert.Equal(Cfg.CrouchHeight, s.CapsuleHeight, 3);
        }

        [Fact]
        public void JumpFromFlatGroundLandsGrounded()
        {
            var world = new SlopeWorld();
            var motor = new KinematicMotor();
            uint tick = 1;
            var s = motor.Step(SpawnAt(world.GroundHeightAt(0)), Cmd(tick, 0f, InputButtons.JumpPressed), tick, world);
            Assert.True(s.Velocity.Y > 0f);
            Assert.False(s.Grounded);
            bool landed = false;
            for (int i = 0; i < 90 && !landed; i++) { tick++; s = motor.Step(s, Cmd(tick), tick, world); landed = s.Grounded; }
            Assert.True(landed);
            Assert.InRange(s.Feet.Y, -0.01f, 0.03f); // rests one skin above the surface
        }

        [Fact]
        public void SlideStartsFromSprintAndSlideJumpCancelsIt()
        {
            var world = new SlopeWorld();
            var motor = new KinematicMotor();
            uint tick = 0;
            var s = Run(motor, world, SpawnAt(world.GroundHeightAt(0)), ref tick, 30, 1f, InputButtons.SprintHeld);
            tick++;
            s = motor.Step(s, Cmd(tick, 1f, InputButtons.SprintHeld), tick, world, slidePressed: true);
            Assert.True(s.SlideActive);
            tick++;
            s = motor.Step(s, Cmd(tick, 0f, InputButtons.JumpPressed), tick, world);
            Assert.False(s.SlideActive);
        }
    }
}
