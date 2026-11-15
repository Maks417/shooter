#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Simulation.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Motor
{
    /// <summary>
    /// Emulates the PhysX CapsuleCast behaviour found in the 0.3.5 release: a capsule that already touches a collider (gap below
    /// the contact offset) reports Hit with distance 0, point zero and normal = -sweep direction, for every direction.
    /// </summary>
    internal sealed class InitialContactWorld : IWorldQueries
    {
        public const float ContactOffset = 0.005f;
        private readonly StaticPlaneWorld inner = new StaticPlaneWorld();

        public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter) =>
            inner.Raycast(origin, unitDirection, maxMeters, filter);

        public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) =>
            inner.Overlap(capsule, filter, callerOwned);

        public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter)
        {
            float gap = Math.Min(capsule.BottomSphereCenter.Y, capsule.TopSphereCenter.Y) - capsule.Radius;
            float length = (float)Math.Sqrt(displacement.LengthSquared);
            if (length > 0f && gap < ContactOffset)
            {
                var normal = new Float3(-displacement.X / length, -displacement.Y / length, -displacement.Z / length);
                return new SweepHit(QueryStatus.Hit, new QueryHit(0f, default, normal, default, new SurfaceId(100), 1, 1, 1));
            }
            return inner.SweepCapsule(capsule, displacement, filter);
        }
    }

    public sealed class InitialContactWorldTests
    {
        private static MotorBodyState Spawn(float y) => new MotorBodyState(
            new Float3(0, y, 0), default, 0, 0, Stance.Standing, 1.8f,
            false, default, new Float3(0, 1, 0), 0, 0, 0, false);

        private static InputCommand Cmd(uint seq, float moveY, InputButtons buttons) =>
            new InputCommand(new EntityId(1), seq, seq, 0, moveY, 0, 0, buttons, 0);

        [Theory]
        [InlineData(0f)]
        [InlineData(0.02f)]
        public void SprintJumpAndSlideWorkWhenSpawnedTouchingGround(float spawnY)
        {
            var world = new InitialContactWorld();
            var motor = new KinematicMotor();
            var s = Spawn(spawnY);
            uint tick = 0;
            for (int i = 0; i < 20; i++) { tick++; s = motor.Step(s, Cmd(tick, 0, InputButtons.None), tick, world); }
            Assert.True(s.Grounded);

            float maxSpeed = 0;
            for (int i = 0; i < 60; i++)
            {
                var before = s.Feet; tick++;
                s = motor.Step(s, Cmd(tick, 1, InputButtons.SprintHeld), tick, world);
                float dz = s.Feet.Z - before.Z;
                maxSpeed = Math.Max(maxSpeed, Math.Abs(dz) * 60f);
            }
            Assert.True(maxSpeed > 5.2f, $"sprint speed {maxSpeed}");
            Assert.True(s.SprintActive);

            tick++;
            s = motor.Step(s, Cmd(tick, 1, InputButtons.SprintHeld), tick, world, slidePressed: true);
            Assert.True(s.SlideActive, "slide must start from a sprint");
            for (int i = 0; i < 90; i++) { tick++; s = motor.Step(s, Cmd(tick, 0, InputButtons.None), tick, world); }

            float y0 = s.Feet.Y, maxY = y0;
            tick++;
            s = motor.Step(s, Cmd(tick, 0, InputButtons.JumpPressed), tick, world);
            for (int i = 0; i < 40; i++) { tick++; s = motor.Step(s, Cmd(tick, 0, InputButtons.None), tick, world); maxY = Math.Max(maxY, s.Feet.Y); }
            Assert.True(maxY - y0 > 0.5f, $"jump rise {maxY - y0}");
        }
    }
}
