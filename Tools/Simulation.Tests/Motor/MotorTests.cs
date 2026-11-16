#nullable enable
using System;
using Shooter.Bootstrap;
using Shooter.Contracts;
using Shooter.VectorMath;
using Shooter.Simulation.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Motor
{
    public sealed class MotorTests
    {
        private static MotorBodyState Spawn() => new MotorBodyState(
            new Float3(0, 0, 0), default, 0, 0, Stance.Standing, 1.8f,
            true, new SurfaceId(100), new Float3(0, 1, 0), 0, 0, 0, false);

        private static MotorBodyState CrouchedSpawn(float x = 0f) => new MotorBodyState(
            new Float3(x, 0, 0), default, 0, 0, Stance.Crouching, 1.2f,
            true, new SurfaceId(100), new Float3(0, 1, 0), 0, 0, 0, false);

        [Fact]
        public void OverlappingSpawnIsPushedOutWithinRecoveryLimit()
        {
            var world = new StaticPlaneWorld();
            world.AddBox(new Float3(0.2f, 0, -1), new Float3(2, 3, 1), 60);
            var motor = new KinematicMotor();
            var state = motor.Step(Spawn(), Cmd(1), 1, world);
            Assert.False(state.OverlapUnresolved);
            Assert.True(state.Feet.X <= 0.2f - 0.32f + 1e-3f, $"x={state.Feet.X}");
            var check = world.Overlap(MotorMath.BodyCapsule(state.Feet, state.CapsuleHeight, 0.32f), new QueryFilter(CollisionLayers.MotorMask, default, false), new HitBuffer(8));
            Assert.Equal(QueryStatus.NoHit, check.Status);
        }

        [Fact]
        public void UnresolvableOverlapStopsAndReports()
        {
            // Wedged between opposing geometry: every push-out is answered by a new penetration, so recovery cannot converge.
            var world = new WedgedWorld();
            var motor = new KinematicMotor();
            var start = Spawn();
            var state = motor.Step(start, Cmd(1, my: 1), 1, world);
            Assert.True(state.OverlapUnresolved);
            Assert.Equal(start.Feet.X, state.Feet.X); Assert.Equal(start.Feet.Y, state.Feet.Y); Assert.Equal(start.Feet.Z, state.Feet.Z);
            Assert.Equal(0f, state.Velocity.LengthSquared);
        }

        [Fact]
        public void RestingOnGroundIsNotPerturbedByRecovery()
        {
            var world = new StaticPlaneWorld();
            var motor = new KinematicMotor();
            var state = Spawn();
            for (uint i = 1; i <= 30; i++) state = motor.Step(state, Cmd(i), i, world);
            Assert.False(state.OverlapUnresolved);
            Assert.True(state.Grounded);
            Assert.InRange(state.Feet.Y, -1e-3f, 0.03f);
            Assert.Equal(0f, state.Feet.X, 3);
        }

        [Fact]
        public void StandUpUnderOffCentreLedgeIsRefusedButAllowedInOpen()
        {
            var world = new StaticPlaneWorld();
            world.AddBox(new Float3(0.2f, 1.35f, -2), new Float3(3, 1.55f, 2), 62);
            var motor = new KinematicMotor();
            var blocked = motor.Step(CrouchedSpawn(), Cmd(1), 1, world);
            Assert.Equal(Stance.Crouching, blocked.Stance);
            Assert.Equal(1.2f, blocked.CapsuleHeight, 3);
            var open = motor.Step(CrouchedSpawn(-2f), Cmd(1), 1, world);
            Assert.Equal(Stance.Standing, open.Stance);
            Assert.Equal(1.8f, open.CapsuleHeight, 3);
        }

        private sealed class WedgedWorld : IWorldQueries
        {
            public int OverlapCalls;
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter) => new RayHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned)
            {
                OverlapCalls++;
                callerOwned[0] = new QueryHit(0.5f, capsule.BottomSphereCenter, new Float3(0, 1, 0), default, new SurfaceId(100), 7, 1, 1);
                return new OverlapResult(QueryStatus.Hit, 1, 1);
            }
        }

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
            // Start already crouched under the slab: a standing capsule would overlap it (invalid spawn).
            var state = CrouchedSpawn();
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

        private static MotorBodyState Sprint(KinematicMotor motor, StaticPlaneWorld world)
        {
            var state = Spawn();
            for (uint t = 1; t <= 20; t++) state = motor.Step(state, Cmd(t, my: 1, buttons: InputButtons.SprintHeld), t, world);
            return state;
        }

        private static MotorBodyState WithTightness(MotorBodyState state, float tightness) => new MotorBodyState(
            state.Feet, state.Velocity, state.AimYaw, state.AimPitch, state.Stance, state.CapsuleHeight,
            state.Grounded, state.GroundSurfaceId, state.GroundNormal, state.LastGroundedTick,
            state.JumpConsumedSequence, state.LocomotionDistanceMeters, state.SprintActive,
            state.SlideTicksRemaining, state.SlideCooldownTicksRemaining, state.OverlapUnresolved, tightness);

        [Fact]
        public void SpawnStartsAtFullTightness()
        {
            Assert.Equal(MotorConfig.TightnessMax, Spawn().Tightness, 5);
        }

        [Fact]
        public void ContinuousSprintEmptiesTightnessInEightSeconds()
        {
            var motor = new KinematicMotor();
            var world = new StaticPlaneWorld();
            var state = Spawn();
            // 480 drains of TickSeconds/8 empty the bar; the 480th tick still sprints then clamps to 0.
            for (uint t = 1; t <= 480; t++)
            {
                state = motor.Step(state, Cmd(t, my: 1, buttons: InputButtons.SprintHeld), t, world);
                Assert.True(state.SprintActive, $"expected sprint at tick {t}, tightness={state.Tightness}");
            }
            Assert.Equal(0f, state.Tightness, 3);
            state = motor.Step(state, Cmd(481, my: 1, buttons: InputButtons.SprintHeld), 481, world);
            Assert.False(state.SprintActive);
            Assert.Equal(0f, state.Tightness, 3); // holding Sprint while empty does not regen
            // Release Sprint and walk: regen + decel to walk speed.
            for (uint t = 482; t <= 520; t++)
                state = motor.Step(state, Cmd(t, my: 1), t, world);
            Assert.False(state.SprintActive);
            Assert.True(state.Tightness > 0f);
            Assert.InRange(VecMath.Length(MotorMath.Horizontal(state.Velocity)), motor.Config.WalkSpeed - 0.05f, motor.Config.WalkSpeed + 0.05f);
        }

        [Fact]
        public void WalkRegenFillsTightnessInFiveSecondsFromEmpty()
        {
            var motor = new KinematicMotor();
            var world = new StaticPlaneWorld();
            var state = WithTightness(Spawn(), 0f);
            for (uint t = 1; t <= 300; t++)
                state = motor.Step(state, Cmd(t, my: 1), t, world);
            Assert.Equal(MotorConfig.TightnessMax, state.Tightness, 3);
        }

        [Fact]
        public void JumpCostsTightnessAndRefusesWhenInsufficient()
        {
            var motor = new KinematicMotor();
            var world = new StaticPlaneWorld();
            var state = motor.Step(Spawn(), Cmd(1, buttons: InputButtons.JumpPressed), 1, world);
            Assert.True(state.Velocity.Y > 0f);
            Assert.Equal(MotorConfig.TightnessMax - MotorConfig.TightnessJumpCost, state.Tightness, 4);

            // Land, then try jump with too little tightness (walk regen still applies on the refused tick).
            state = WithTightness(Spawn(), MotorConfig.TightnessJumpCost - 0.01f);
            var refused = motor.Step(state, Cmd(2, buttons: InputButtons.JumpPressed), 2, world);
            Assert.True(refused.Velocity.Y <= 0f);
            Assert.InRange(refused.Tightness, state.Tightness, state.Tightness + MotorConfig.TightnessRegenPerTick + 1e-4f);
        }

        [Fact]
        public void SlideCostsTightnessAndRefusesWhenInsufficient()
        {
            var motor = new KinematicMotor();
            var world = new StaticPlaneWorld();
            var sprint = Sprint(motor, world);
            float before = sprint.Tightness;
            var slid = motor.Step(sprint, Cmd(21, my: 1, buttons: InputButtons.SprintHeld), 21, world, slidePressed: true);
            Assert.True(slid.SlideActive);
            Assert.Equal(before - MotorConfig.TightnessSlideCost, slid.Tightness, 4);

            sprint = WithTightness(Sprint(motor, world), MotorConfig.TightnessSlideCost - 0.01f);
            var refused = motor.Step(sprint, Cmd(21, my: 1, buttons: InputButtons.SprintHeld), 21, world, slidePressed: true);
            Assert.False(refused.SlideActive);
            // Refused slide still sprints, so tightness drains by the sprint tick cost only.
            Assert.Equal(sprint.Tightness - MotorConfig.TightnessSprintDrainPerTick, refused.Tightness, 4);
        }

        [Fact]
        public void SlideRequiresSprintMomentumAndDecaysWithoutSteeringOrRepeatBoost()
        {
            var motor = new KinematicMotor();
            var world = new StaticPlaneWorld();
            var state = motor.Step(Spawn(), Cmd(1, my: 1, buttons: InputButtons.SprintHeld), 1, world, slidePressed: true);
            Assert.False(state.SlideActive); // Cannot boost from rest.
            state = Sprint(motor, world);
            state = motor.Step(state, Cmd(21, my: 1, buttons: InputButtons.SprintHeld), 21, world, slidePressed: true);
            Assert.True(state.SlideActive);
            Assert.Equal(Stance.Crouching, state.Stance);
            Assert.Equal(1.2f, state.CapsuleHeight, 3);
            Assert.False(state.SprintActive);
            Assert.Equal(6.5f, state.Velocity.Z, 3);
            Assert.Equal(60u, state.SlideCooldownTicksRemaining);
            float priorSpeed = state.Velocity.Z;
            for (uint t = 22; t < 66; t++)
            {
                // Looking/pressing sideways and repeated intents must not steer or boost.
                state = motor.Step(state, Cmd(t, mx: 1, buttons: InputButtons.SprintHeld, yaw: 1.5f), t, world, slidePressed: true);
                Assert.True(state.Velocity.Z < priorSpeed);
                Assert.Equal(0f, state.Velocity.X, 4);
                Assert.Equal(Stance.Crouching, state.Stance);
                priorSpeed = state.Velocity.Z;
            }
            Assert.False(state.SlideActive);
            Assert.True(state.SlideCooldownTicksRemaining > 0);
            state = motor.Step(state, Cmd(66, my: 1, buttons: InputButtons.SprintHeld), 66, world, slidePressed: true);
            Assert.False(state.SlideActive);
            Assert.Equal(Stance.Standing, state.Stance);
            for (uint t = 67; t <= 81; t++) state = motor.Step(state, Cmd(t, my: 1, buttons: InputButtons.SprintHeld), t, world);
            state = motor.Step(state, Cmd(82, my: 1, buttons: InputButtons.SprintHeld), 82, world, slidePressed: true);
            Assert.True(state.SlideActive);
        }

        [Theory]
        [InlineData(InputButtons.None)]
        [InlineData(InputButtons.SprintHeld | InputButtons.AimHeld)]
        [InlineData(InputButtons.SprintHeld | InputButtons.CrouchHeld)]
        [InlineData(InputButtons.SprintHeld | InputButtons.FirePressed)]
        [InlineData(InputButtons.SprintHeld | InputButtons.ReloadPressed)]
        public void SlideRejectsMissingSprintAndConflictingActions(InputButtons buttons)
        {
            var motor = new KinematicMotor();
            var world = new StaticPlaneWorld();
            var state = motor.Step(Sprint(motor, world), Cmd(21, my: 1, buttons: buttons), 21, world, slidePressed: true);
            Assert.False(state.SlideActive);
        }

        [Fact]
        public void SlideJumpCancelsAndExpirationKeepsCrouchUnderCeiling()
        {
            var motor = new KinematicMotor();
            var world = new StaticPlaneWorld();
            var state = motor.Step(Sprint(motor, world), Cmd(21, my: 1, buttons: InputButtons.SprintHeld), 21, world, true);
            var jumped = motor.Step(state, Cmd(22, buttons: InputButtons.JumpPressed), 22, world);
            Assert.False(jumped.SlideActive);
            Assert.True(jumped.Velocity.Y > 0f);
            // A slide may end under cover; authority must not force the capsule through it.
            world.AddBox(new Float3(-10, 1.35f, -10), new Float3(10, 1.55f, 20), 99);
            for (uint t = 22; t < 68; t++) state = motor.Step(state, Cmd(t), t, world);
            Assert.False(state.SlideActive);
            Assert.Equal(Stance.Crouching, state.Stance);
            Assert.Equal(1.2f, state.CapsuleHeight, 3);
        }

        [Fact]
        public void SlideCannotBeginAirborneOrBackwardsAndStopsAtWall()
        {
            var motor = new KinematicMotor();
            var world = new StaticPlaneWorld();
            var sprint = Sprint(motor, world);
            var backward = motor.Step(sprint, Cmd(21, my: -1, buttons: InputButtons.SprintHeld), 21, world, true);
            Assert.False(backward.SlideActive);
            var airborne = motor.Step(sprint, Cmd(21, buttons: InputButtons.JumpPressed), 21, world);
            airborne = motor.Step(airborne, Cmd(22, my: 1, buttons: InputButtons.SprintHeld), 22, world, true);
            Assert.False(airborne.SlideActive);
            world.AddBox(new Float3(-3, 0, sprint.Feet.Z + 1f), new Float3(3, 4, sprint.Feet.Z + 2f), 98);
            var state = motor.Step(sprint, Cmd(21, my: 1, buttons: InputButtons.SprintHeld), 21, world, true);
            for (uint t = 22; t < 36; t++) state = motor.Step(state, Cmd(t), t, world);
            Assert.True(state.Feet.Z < sprint.Feet.Z + 1f);
            Assert.False(state.SlideActive);
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
