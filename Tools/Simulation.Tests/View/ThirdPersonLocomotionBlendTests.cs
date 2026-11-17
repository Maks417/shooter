#nullable enable
using System;
using Xunit;
using Shooter.Presentation.View;

namespace Shooter.Simulation.Tests.View
{
    /// <summary>ADR-100 third-person locomotion filter: heading, airborne, landing and slide blends.</summary>
    public sealed class ThirdPersonLocomotionBlendTests
    {
        private const float Dt = 1f / 60f;

        [Fact]
        public void HeadingFollowsTravelHoldsThroughSlowFramesAndReleasesWhenStill()
        {
            var blend = new ThirdPersonLocomotionBlend();
            blend.Step(Dt, true, true, 0f, 0f, 0f, false);
            Assert.True(float.IsNaN(blend.HeadingRadians));
            blend.Step(Dt, true, true, 0f, 3f, 0f, false); // strafing toward +X
            Assert.Equal(MathF.PI / 2, blend.HeadingRadians, 4);
            blend.Step(Dt, true, true, 0f, 0.1f, 0f, false); // decelerating through a strafe flip: hold
            Assert.Equal(MathF.PI / 2, blend.HeadingRadians, 4);
            blend.Step(Dt, true, true, 0f, -3f, 0f, false); // reversed
            Assert.Equal(-MathF.PI / 2, blend.HeadingRadians, 4);
            blend.Step(Dt, true, true, 0f, 0f, -2f, false); // backpedal
            Assert.Equal(MathF.PI, MathF.Abs(blend.HeadingRadians), 4);
            blend.Step(Dt, true, true, 0f, 0f, 0f, false);
            Assert.True(float.IsNaN(blend.HeadingRadians));
        }

        [Fact]
        public void JumpBlendsAirborneInAndPlaysTheLandingDip()
        {
            var blend = new ThirdPersonLocomotionBlend();
            blend.Step(Dt, true, true, 0f, 0f, 3f, false);
            blend.Step(Dt, true, false, 5f, 0f, 3f, false);
            Assert.InRange(blend.Airborne, 0.1f, 0.5f);
            for (int i = 0; i < 10; i++) blend.Step(Dt, true, false, 5f - i * 0.33f, 0f, 3f, false);
            Assert.Equal(1f, blend.Airborne);
            for (int i = 0; i < 20; i++) blend.Step(Dt, true, false, -2f - i * 0.33f, 0f, 3f, false);
            Assert.Equal(-1f, blend.Land);
            blend.Step(Dt, true, true, 0f, 0f, 3f, false);
            Assert.Equal(0f, blend.Land);
            float previous = 0f;
            for (int i = 0; i < 11; i++)
            {
                blend.Step(Dt, true, true, 0f, 0f, 3f, false);
                Assert.True(blend.Land > previous);
                previous = blend.Land;
            }
            Assert.Equal(0f, blend.Airborne);
            for (int i = 0; i < 2; i++) blend.Step(Dt, true, true, 0f, 0f, 3f, false);
            Assert.Equal(-1f, blend.Land);
        }

        [Fact]
        public void BriefStepOffEdgesDoNotReadAsAirborneOrLanding()
        {
            var blend = new ThirdPersonLocomotionBlend();
            for (int frame = 0; frame < 120; frame++)
            {
                // Stairs: four ungrounded frames (about 67 ms) out of every twelve, slight fall.
                bool grounded = frame % 12 >= 4;
                blend.Step(Dt, true, grounded, grounded ? 0f : -1f, 0f, 2f, false);
                Assert.Equal(0f, blend.Airborne);
                Assert.Equal(-1f, blend.Land);
            }
        }

        [Fact]
        public void SlideEasesInAndOutAndNeverReadsAirborne()
        {
            var blend = new ThirdPersonLocomotionBlend();
            blend.Step(Dt, true, true, 0f, 0f, 6f, true);
            Assert.InRange(blend.Slide, 0.1f, 0.3f);
            for (int i = 0; i < 6; i++) blend.Step(Dt, true, false, -1f, 0f, 6f, true);
            Assert.Equal(1f, blend.Slide);
            Assert.Equal(0f, blend.Airborne);
            blend.Step(Dt, true, true, 0f, 0f, 3f, false);
            Assert.InRange(blend.Slide, 0.8f, 0.95f);
            for (int i = 0; i < 9; i++) blend.Step(Dt, true, true, 0f, 0f, 3f, false);
            Assert.Equal(0f, blend.Slide);
        }

        [Fact]
        public void DeathOrInvalidInputsResetToNeutral()
        {
            var blend = new ThirdPersonLocomotionBlend();
            for (int i = 0; i < 10; i++) blend.Step(Dt, true, false, 4f, 1f, 1f, false);
            blend.Step(Dt, false, false, 4f, 1f, 1f, false);
            Assert.Equal(0f, blend.Airborne); Assert.Equal(-1f, blend.Land); Assert.Equal(0f, blend.Slide);
            Assert.True(float.IsNaN(blend.HeadingRadians));
            blend.Step(float.NaN, true, true, float.NaN, float.PositiveInfinity, float.NaN, false);
            Assert.True(float.IsNaN(blend.HeadingRadians));
            Assert.Equal(0f, blend.Airborne);
        }
    }
}
