#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Shooter.Bootstrap;
using Shooter.Contracts;
using Shooter.Simulation.Combat;
using Shooter.Simulation.Motor;
using Shooter.Simulation.Pose;
using Shooter.Simulation.Tests.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    /// <summary>ADR-118: multi-pellet hitscan and the new optional definition fields.</summary>
    public sealed class PelletTests
    {
        private const ulong Seed = 460072030UL;

        private static WeaponDefinition Shotgun(byte pellets = 8, float spreadDegrees = 2.6f) => new WeaponDefinition(
            new DefinitionId("weapons/test-shotgun"), FireMode.SemiAutomatic, 36, 28, 26, 7, 28, 25,
            1.2f * (float)Math.PI / 180f, 0.5f * (float)Math.PI / 180f, 0.04f, 0.008f,
            maxRangeMeters: 60f, falloffStartMeters: 8f, falloffEndMeters: 30f, falloffMinScale: 0.3f, moveSpeedScale: 0.95f,
            pelletCount: pellets, pelletSpreadRadians: spreadDegrees * (float)Math.PI / 180f, reloadPerRound: true);

        private static (HitscanResolver hitscan, StaticPlaneWorld world, Float3 eye, Float3 muzzle, Float3 aim) Range()
        {
            var world = new StaticPlaneWorld();
            world.AddBox(new Float3(-6, 0, 20f), new Float3(6, 6f, 20.5f), 50);
            var pose = new GameplayPoseEvaluator();
            var eye = pose.EyePosition(default, Stance.Standing, 1.8f);
            return (new HitscanResolver(CollisionLayers.MotorMask, CollisionLayers.BulletObstructionMask), world, eye,
                pose.CanonicalMuzzle(eye, 0, 0), pose.AimDirection(0, 0));
        }

        [Fact]
        public void Default_definition_has_one_pellet_and_full_magazine_reload()
        {
            var def = WeaponDefinition.Rifle;
            Assert.Equal(1, def.PelletCount);
            Assert.Equal(0f, def.PelletSpreadRadians);
            Assert.False(def.ReloadPerRound);
            foreach (var entry in WeaponCatalog.All)
                Assert.True(entry.Definition.PelletCount >= 1);
        }

        [Fact]
        public void Single_pellet_guns_resolve_exactly_as_before()
        {
            var (hitscan, world, eye, muzzle, aim) = Range();
            var rifle = WeaponDefinition.Rifle;
            var a = hitscan.Resolve(new HitscanRequest(new EntityId(1), eye, muzzle, aim, rifle, 3, false, Seed, 1), world);
            var b = hitscan.Resolve(new HitscanRequest(new EntityId(1), eye, muzzle, aim, rifle, 3, false, Seed, 1, 5), world);
            // The pellet index is ignored for single-projectile guns.
            Assert.Equal(a.Impact.Point, b.Impact.Point);
            // And the direction is the ADR-112 sample from channels 1 and 2.
            var expected = SpreadSampling.SampleDirection(aim, rifle.HipSpreadRadians,
                SpreadSampling.MixShot(Seed, 3, 1), SpreadSampling.MixShot(Seed, 3, 2));
            float t = (a.Impact.Point.Z - eye.Z) / expected.Z;
            Assert.Equal(eye.X + expected.X * t, a.Impact.Point.X, 4);
            Assert.Equal(eye.Y + expected.Y * t, a.Impact.Point.Y, 4);
        }

        [Fact]
        public void Pellets_scatter_inside_their_cone_and_are_deterministic()
        {
            var (hitscan, world, eye, muzzle, aim) = Range();
            var gun = Shotgun();
            var centre = SpreadSampling.SampleDirection(aim, gun.HipSpreadRadians,
                SpreadSampling.MixShot(Seed, 4, 1), SpreadSampling.MixShot(Seed, 4, 2));
            var points = new HashSet<(float, float)>();
            for (byte p = 0; p < gun.PelletCount; p++)
            {
                var r1 = hitscan.Resolve(new HitscanRequest(new EntityId(1), eye, muzzle, aim, gun, 4, false, Seed, 1, p), world);
                var r2 = hitscan.Resolve(new HitscanRequest(new EntityId(1), eye, muzzle, aim, gun, 4, false, Seed, 1, p), world);
                Assert.True(r1.Impacted);
                Assert.Equal(r1.Impact.Point, r2.Impact.Point);
                points.Add((r1.Impact.Point.X, r1.Impact.Point.Y));
                var dir = HitscanResolver.PelletDirection(centre, gun.PelletSpreadRadians, Seed, 4, p);
                double cos = dir.X * centre.X + dir.Y * centre.Y + dir.Z * centre.Z;
                Assert.True(Math.Acos(Math.Min(1.0, cos)) <= gun.PelletSpreadRadians + 1e-4);
            }
            Assert.Equal(gun.PelletCount, points.Count);
        }

        [Fact]
        public void Modifiers_copy_every_unscaled_field()
        {
            var gun = Shotgun();
            var buffed = new WeaponModifiers(900, 900, 900, 900, 1).Apply(gun);
            Assert.Equal(gun.PelletCount, buffed.PelletCount);
            Assert.Equal(gun.PelletSpreadRadians, buffed.PelletSpreadRadians);
            Assert.Equal(gun.ReloadPerRound, buffed.ReloadPerRound);
            // Every property not named here must survive Apply unchanged, so a new field cannot be dropped silently.
            var scaled = new HashSet<string> { "ReloadTicks", "EquipTicks", "ReserveCapacity", "AdsSpreadRadians",
                "RecoilPitchRadians", "RecoilYawHalfRadians" };
            foreach (var property in typeof(WeaponDefinition).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (scaled.Contains(property.Name)) continue;
                Assert.Equal(property.GetValue(gun), property.GetValue(buffed));
            }
        }
    }
}
