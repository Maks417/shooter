#nullable enable
using System;
using System.Linq;
using Xunit;
using Shooter.Presentation.View;

namespace Shooter.Simulation.Tests.View
{
    /// <summary>ADR-124 authored soldier locomotion weights, shared gait phase and deterministic variant pick.</summary>
    public sealed class AuthoredSoldierBlendTests
    {
        private static float All(SoldierClipRole role) => 1f;
        private static float W(AuthoredSoldierBlend b, SoldierClipRole r) => b.Weights[(int)r];
        private static float Sum(AuthoredSoldierBlend b) { float s = 0f; foreach (var w in b.Weights) s += w; return s; }

        [Fact]
        public void StandingStillIsPureIdle()
        {
            var b = new AuthoredSoldierBlend();
            b.Step(1f / 60, 0f, 0f, 0f, 0f, All);
            Assert.Equal(1f, W(b, SoldierClipRole.Idle), 4);
            Assert.Equal(1f, Sum(b), 4);
        }

        [Theory]
        [InlineData(0f, SoldierClipRole.RunForward)]
        [InlineData(3.14159265f, SoldierClipRole.RunBackward)]
        [InlineData(1.5707963f, SoldierClipRole.RunRight)]
        [InlineData(-1.5707963f, SoldierClipRole.RunLeft)]
        public void SprintPicksTheTravelDirection(float heading, SoldierClipRole expected)
        {
            var b = new AuthoredSoldierBlend();
            b.Step(1f / 60, 5.5f, heading, 0f, 0f, All);
            Assert.True(W(b, expected) > .99f, expected + " weight " + W(b, expected));
            Assert.Equal(1f, Sum(b), 4);
        }

        [Fact]
        public void DiagonalSplitsBetweenNeighbours()
        {
            var b = new AuthoredSoldierBlend();
            b.Step(1f / 60, AuthoredSoldierBlend.WalkSpeed, (float)Math.PI / 4, 0f, 0f, All);
            Assert.Equal(W(b, SoldierClipRole.WalkForward), W(b, SoldierClipRole.WalkRight), 3);
            Assert.Equal(1f, Sum(b), 4);
        }

        [Fact]
        public void CrouchAndAirborneTakeOver()
        {
            var b = new AuthoredSoldierBlend();
            b.Step(1f / 60, 0f, 0f, 1f, 0f, All);
            Assert.Equal(1f, W(b, SoldierClipRole.CrouchIdle), 4);
            b.Step(1f / 60, 3f, 0f, 0f, 1f, All);
            Assert.Equal(1f, W(b, SoldierClipRole.Airborne), 4);
        }

        [Fact]
        public void MissingRolesFallBackWithoutLosingWeight()
        {
            // Only idle and the four walks exist: running and crouching must land on them.
            Func<SoldierClipRole, float> walkOnly = r => r == SoldierClipRole.Idle || (r >= SoldierClipRole.WalkForward && r <= SoldierClipRole.WalkRight) ? 1f : 0f;
            var b = new AuthoredSoldierBlend();
            b.Step(1f / 60, 5f, 0f, .5f, 0f, walkOnly);
            Assert.Equal(1f, Sum(b), 4);
            Assert.True(W(b, SoldierClipRole.WalkForward) > .9f);
            Assert.Equal(0f, W(b, SoldierClipRole.RunForward));
            Assert.Equal(0f, W(b, SoldierClipRole.CrouchForward));
        }

        [Fact]
        public void PhaseAdvancesByTheBlendedCycle()
        {
            var b = new AuthoredSoldierBlend();
            for (int i = 0; i < 30; i++) b.Step(1f / 60, AuthoredSoldierBlend.RunSpeed, 0f, 0f, 0f, r => 2f);
            Assert.Equal(.25f, b.Phase, 3);
            b.Step(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, All);
            Assert.True(float.IsFinite(b.Phase));
        }

        [Fact]
        public void SlowStrafeOnARunClipSlowsItsCadence()
        {
            // No walk-sideways clips: a 3 m/s strafe plays Run_Right, whose cadence must slow to the real speed.
            Func<SoldierClipRole, float> noSideWalk = r => r == SoldierClipRole.WalkLeft || r == SoldierClipRole.WalkRight ? 0f : 1f;
            var b = new AuthoredSoldierBlend();
            b.Step(1f / 60, 3f, (float)Math.PI / 2, 0f, 0f, noSideWalk);
            Assert.True(W(b, SoldierClipRole.RunRight) > .99f);
            Assert.Equal(3f / AuthoredSoldierBlend.RunSpeed, b.Rate, 3);
            b.Step(1f / 60, 9f, 0f, 0f, 0f, All);
            Assert.Equal(AuthoredSoldierBlend.MaxRate, b.Rate, 3);
            b.Step(1f / 60, .3f, 0f, 0f, 0f, All);
            Assert.Equal(AuthoredSoldierBlend.MinRate, b.Rate, 3);
        }

        [Fact]
        public void StrafeReversalCrossfadesThenSettles()
        {
            var b = new AuthoredSoldierBlend();
            b.Step(1f / 60, 3.5f, (float)Math.PI / 2, 0f, 0f, All);
            Assert.True(W(b, SoldierClipRole.RunRight) > .7f);
            b.Step(1f / 60, 3.5f, -(float)Math.PI / 2, 0f, 0f, All);
            Assert.True(W(b, SoldierClipRole.RunRight) > W(b, SoldierClipRole.RunLeft), "one frame after a reversal the old side still leads");
            for (int i = 0; i < 60; i++) b.Step(1f / 60, 3.5f, -(float)Math.PI / 2, 0f, 0f, All);
            Assert.True(W(b, SoldierClipRole.RunLeft) > .7f);
            Assert.True(W(b, SoldierClipRole.RunRight) < .01f);
            Assert.Equal(1f, Sum(b), 4);
        }

        [Fact]
        public void VariantPickIsDeterministicAndSpreads()
        {
            Assert.Equal(SoldierVariantPicker.Pick(7, 1, 4), SoldierVariantPicker.Pick(7, 1, 4));
            var picks = Enumerable.Range(1, 40).Select(i => SoldierVariantPicker.Pick((ulong)i, 1, 4)).ToArray();
            Assert.All(picks, p => Assert.InRange(p, 0, 3));
            Assert.Equal(4, picks.Distinct().Count());
            Assert.Throws<ArgumentOutOfRangeException>(() => SoldierVariantPicker.Pick(1, 1, 0));
        }
    }
}
