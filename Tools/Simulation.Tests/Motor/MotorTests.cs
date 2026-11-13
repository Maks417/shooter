#nullable enable
using System;
using Shooter.Bootstrap;
using Shooter.Contracts;
using Shooter.Simulation.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Motor
{
    public sealed class MotorTests
    {
        private static MotorBodyState Spawn() => new MotorBodyState(
            new Float3(0, 0, 0), default, 0, 0, Stance.Standing, 1.8f,
            true, new SurfaceId(100), new Float3(0, 1, 0), 0, 0, 0, false);

        private static InputCommand Cmd(uint seq, float mx = 0, float my = 0, InputButtons buttons = InputButtons.None, float yaw = 0) =>
            new InputCommand(new EntityId(1), seq, seq, mx, my, yaw, 0, buttons, 0);

        [Fact]
        public void DisplayFpsScriptsAgreeWithinOneCentimeter()
        {
            var motor = new KinematicMotor();
            Float3 a = RunScript(motor, displayHz: 30);
            Float3 b = RunScript(motor, displayHz: 60);
            Float3 c = RunScript(motor, displayHz: 144);
            Assert.True(Distance(a, b) <= 0.01f, $"30 vs 60: {Distance(a, b)}");
            Assert.True(Distance(a, c) <= 0.01f, $"30 vs 144: {Distance(a, c)}");
            Assert.True(Distance(b, c) <= 0.01f, $"60 vs 144: {Distance(b, c)}");
        }

        [Fact]
        public void JumpAndFireEdgesDoNotDoubleAcrossFrames()
        {
            var buffer = new InputEdgeBuffer();
            // Two display frames inside one tick both see JumpPressed raw; buffer keeps one edge.
            buffer.AccumulateFrame(0, 0, 0, 0, InputButtons.JumpPressed | InputButtons.FirePressed, 0);
            buffer.AccumulateFrame(0, 0, 0, 0, InputButtons.JumpPressed | InputButtons.FirePressed | InputButtons.FireHeld, 0);
            Assert.True(buffer.TryDrain(new EntityId(1), 1, 1, out var command));
            Assert.Equal(InputButtons.JumpPressed | InputButtons.FirePressed | InputButtons.FireHeld, command.Buttons);
            Assert.True(buffer.TryDrain(new EntityId(1), 2, 2, out var second));
            Assert.Equal(InputButtons.FireHeld, second.Buttons & (InputButtons.JumpPressed | InputButtons.FirePressed | InputButtons.FireHeld));
            Assert.Equal(InputButtons.None, second.Buttons & (InputButtons.JumpPressed | InputButtons.FirePressed));
        }

        [Fact]
        public void CrouchCeilingFailureKeepsStandingHeight()
        {
            var world = new StaticPlaneWorld();
            // Low ceiling: crouch first under the slab, then standing up must fail.
            world.AddBox(new Float3(-2, 1.35f, -2), new Float3(2, 1.55f, 2), 50);
            var motor = new KinematicMotor();
            var state = Spawn();
            state = motor.Step(state, Cmd(1, buttons: InputButtons.CrouchHeld), 1, world);
            Assert.Equal(Stance.Crouching, state.Stance);
            Assert.Equal(1.2f, state.CapsuleHeight, 3);
            state = motor.Step(state, Cmd(2), 2, world);
            Assert.Equal(Stance.Crouching, state.Stance);
            Assert.Equal(1.2f, state.CapsuleHeight, 3);
        }

        [Fact]
        public void ShippingPolicyRejectsMotorWorldFake()
        {
            IWorldQueries fake = new StaticPlaneWorld();
            Assert.Throws<InvalidOperationException>(() => CompositionPolicy.RequireReal(fake));
        }

        private static Float3 RunScript(KinematicMotor motor, int displayHz)
        {
            var world = new StaticPlaneWorld();
            var state = Spawn();
            var buffer = new InputEdgeBuffer();
            uint sequence = 0;
            // 2 seconds forward walk at walk speed along +Z.
            int displayFrames = displayHz * 2;
            double frameDt = 1.0 / displayHz;
            double simDebt = 0;
            uint tick = 0;
            for (int f = 0; f < displayFrames; f++)
            {
                buffer.AccumulateFrame(0, 1, 0, 0, InputButtons.None, 0);
                simDebt += frameDt;
                while (simDebt + 1e-12 >= MotorMath.TickSeconds)
                {
                    sequence++;
                    Assert.True(buffer.TryDrain(new EntityId(1), tick, sequence, out var command));
                    state = motor.Step(state, command, tick, world);
                    tick++;
                    simDebt -= MotorMath.TickSeconds;
                }
            }
            return state.Feet;
        }

        private static float Distance(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
