#nullable enable
using Shooter.Contracts;
using Shooter.Presentation.Fx;
using Shooter.Presentation.View;
using Shooter.Simulation.Combat;
using Xunit;

namespace Shooter.Simulation.Tests.Fx
{
    /// <summary>ADR-111: thrown-grenade presentation mirrors the authority throw, and the knife/grenade are one-handed.</summary>
    public sealed class GrenadeThrowPresentationTests
    {
        private static bool FlatGround(Float3 from, Float3 delta, out Float3 point, out Float3 normal)
        {
            point = default; normal = new Float3(0, 1, 0);
            float endY = from.Y + delta.Y;
            if (endY > 0f || delta.Y >= 0f) return false;
            float t = from.Y / -delta.Y;
            point = new Float3(from.X + delta.X * t, 0f, from.Z + delta.Z * t);
            return true;
        }

        [Fact]
        public void Presentation_constants_mirror_the_authority_throw()
        {
            var def = WeaponDefinition.FromId(new DefinitionId("weapons/grenade"));
            Assert.Equal(GrenadeArcPreview.ThrowSpeedMetersPerSecond, def.ThrowSpeedMetersPerSecond);
            Assert.Equal(GrenadeSimulator.LoftDegrees, GrenadeArcPreview.LoftDegrees);
            Assert.Equal(GrenadeSimulator.MaxLaunchPitchDegrees, GrenadeArcPreview.MaxLaunchPitchDegrees);
            Assert.Equal(GrenadeSimulator.GravityY, CosmeticGrenadeFlight.GravityY);
            Assert.Equal(GrenadeSimulator.BounceRetain, CosmeticGrenadeFlight.BounceRetain);
            Assert.Equal(GrenadeSimulator.GroundFriction, CosmeticGrenadeFlight.GroundFriction);
            Assert.Equal(GrenadeSimulator.Radius, CosmeticGrenadeFlight.Radius);
            var aim = new Float3(0.3f, 0.4f, 0.866f);
            Assert.Equal(GrenadeSimulator.Loft(aim), GrenadeArcPreview.Loft(aim));
        }

        [Fact]
        public void Preview_lands_about_half_a_map_away_for_a_high_throw()
        {
            double a = 37.0 * System.Math.PI / 180.0;
            var aim = new Float3(0, (float)System.Math.Sin(a), (float)System.Math.Cos(a));
            var points = new Float3[GrenadeArcPreview.MaxPoints];
            var result = GrenadeArcPreview.Compute(new Float3(0, 1.5f, 0), aim, 2f, FlatGround, points);
            Assert.True(result.Landed);
            Assert.InRange(result.Landing.Z, 31f, 38f);
            Assert.True(result.Count >= 2 && result.Count <= GrenadeArcPreview.MaxPoints);
            Assert.Equal(new Float3(0, 1.5f, 0), points[0]);
        }

        [Fact]
        public void Preview_without_a_world_never_lands()
        {
            var points = new Float3[GrenadeArcPreview.MaxPoints];
            var result = GrenadeArcPreview.Compute(new Float3(0, 1.5f, 0), new Float3(0, 0, 1), 1f, null, points);
            Assert.False(result.Landed);
            Assert.True(points[result.Count - 1].Y < 0f);
        }

        [Fact]
        public void Cosmetic_flight_bounces_off_the_ground_and_tumbles()
        {
            var flight = new CosmeticGrenadeFlight { Probe = FlatGround };
            Assert.True(flight.TrySpawn(new EntityId(9), new Float3(0, 1.5f, 0), new Float3(0, 2f, 8f)));
            for (int i = 0; i < 90; i++) flight.Tick(1f / 60f);
            var live = flight[0];
            Assert.True(live.Active);
            Assert.True(live.Bounces >= 1);
            Assert.True(live.Position.Y >= -0.01f, "stays above the ground");
            Assert.True(live.TumbleDegrees != 0f);
        }

        [Theory]
        [InlineData((byte)0, true)]
        [InlineData((byte)1, true)]
        [InlineData((byte)2, false)]
        [InlineData((byte)3, false)]
        public void Knife_and_grenade_are_held_one_handed(byte slot, bool supportVisible)
        {
            Assert.Equal(supportVisible, FirstPersonSpecialActions.SupportHandVisible(slot, cooking: true, cookSeconds: 0.1f));
        }

        [Fact]
        public void Knife_and_grenade_never_raise_to_the_sights()
        {
            Assert.True(FirstPersonHandling.ViewAimTarget(true, false, false, 0));
            Assert.False(FirstPersonHandling.ViewAimTarget(true, false, false, 2));
            Assert.False(FirstPersonHandling.ViewAimTarget(true, false, false, 3));
        }

        [Fact]
        public void Blade_trail_shows_only_through_the_cut()
        {
            Assert.Equal(0f, FirstPersonSpecialActions.BladeTrailStrength(0.05f, false));
            Assert.True(FirstPersonSpecialActions.BladeTrailStrength(0.25f, false) > 0.5f);
            Assert.Equal(0f, FirstPersonSpecialActions.BladeTrailStrength(0.6f, false));
            Assert.True(FirstPersonSpecialActions.BladeTrailStrength(0.25f, true) < FirstPersonSpecialActions.BladeTrailStrength(0.25f, false));
        }
    }
}
