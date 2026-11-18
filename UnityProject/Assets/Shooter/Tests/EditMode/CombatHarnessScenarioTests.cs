#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Presentation.View;
using Shooter.Simulation.Pose;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Tests
{
    /// <summary>
    /// M2 V05 / V09 EditMode scenarios: hit-volume overlays vs sample poses and grip tolerance,
    /// identical across quality tiers (render-FPS / LOD independent gameplay pose).
    /// </summary>
    public sealed class CombatHarnessScenarioTests
    {
        [Test]
        public void V05_HitVolumeCentersMatchSamplePoseTolerances()
        {
            var eval = new GameplayPoseEvaluator();
            var volumes = new List<HitVolume>();
            eval.Evaluate(new EntityId(1), default, 0, 0, Stance.Standing, 1.8f, 1, volumes);
            Assert.That(volumes.Count, Is.EqualTo(6));
            // ADR-112: markers of the drawn soldier standing still at level aim.
            AssertCenter(volumes, 1, new Float3(0.094f, 1.592f, -0.101f), 0.05f);
            AssertCenter(volumes, 2, new Float3(0, 1.11f, 0), 0.05f);
            AssertCenter(volumes, 3, new Float3(-0.22f, 1.26f, 0), 0.10f);
            AssertCenter(volumes, 4, new Float3(0.22f, 1.26f, 0), 0.10f);
            AssertCenter(volumes, 5, new Float3(-0.1f, 0.47f, 0), 0.10f);
        }

        [Test]
        public void V05_WeaponHandlingHandsStayWithinGripTolerance()
        {
            var view = new ProceduralViewModel();
            using var snap = new SnapshotLease(
                new SimulationSnapshot(0, 1, MatchState.Ready, default, Array.Empty<EntitySnapshot>(), Array.Empty<HitVolume>()),
                () => { });
            using var lease = new EventLease(new FrozenList<CombatEvent>(Array.Empty<CombatEvent>(), 4096), () => { });
            view.Consume(snap, lease, true, 0.15f);
            Assert.That(view.HandsWithinGripTolerance(view.Pose.LeftHand, view.Pose.RightHand), Is.True);
            Assert.That(view.Pose.AdsBlend, Is.InRange(0.99f, 1.01f));
        }

        [Test]
        public void V09_HitVolumeFingerprintIndependentOfQualityTier()
        {
            var eval = new GameplayPoseEvaluator();
            int previous = QualitySettings.GetQualityLevel();
            string? baseline = null;
            try
            {
                for (int tier = 0; tier < QualitySettings.names.Length; tier++)
                {
                    QualitySettings.SetQualityLevel(tier, false);
                    var volumes = new List<HitVolume>();
                    eval.Evaluate(new EntityId(7), new Float3(1, 0, 2), 0.25f, -0.1f, Stance.Standing, 1.8f, 3, volumes);
                    string fingerprint = Fingerprint(volumes);
                    if (baseline == null) baseline = fingerprint;
                    else Assert.That(fingerprint, Is.EqualTo(baseline), "Quality tier {0} changed hit volumes", QualitySettings.names[tier]);
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(previous, false);
            }
        }

        private static void AssertCenter(List<HitVolume> volumes, ushort region, Float3 expected, float tol)
        {
            var volume = volumes.Find(h => h.RegionId == region);
            Assert.That(volume.RegionId, Is.EqualTo(region));
            var center = new Float3(
                (volume.Capsule.BottomSphereCenter.X + volume.Capsule.TopSphereCenter.X) * 0.5f,
                (volume.Capsule.BottomSphereCenter.Y + volume.Capsule.TopSphereCenter.Y) * 0.5f,
                (volume.Capsule.BottomSphereCenter.Z + volume.Capsule.TopSphereCenter.Z) * 0.5f);
            float dx = center.X - expected.X, dy = center.Y - expected.Y, dz = center.Z - expected.Z;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            Assert.That(distance, Is.LessThanOrEqualTo(tol), "region {0} center distance {1}", region, distance);
        }

        private static string Fingerprint(List<HitVolume> volumes)
        {
            var parts = new List<string>(volumes.Count);
            for (int i = 0; i < volumes.Count; i++)
            {
                var v = volumes[i];
                parts.Add(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}:{1:F5},{2:F5},{3:F5}/{4:F5},{5:F5},{6:F5}/r{7:F5}",
                    v.RegionId,
                    v.Capsule.BottomSphereCenter.X, v.Capsule.BottomSphereCenter.Y, v.Capsule.BottomSphereCenter.Z,
                    v.Capsule.TopSphereCenter.X, v.Capsule.TopSphereCenter.Y, v.Capsule.TopSphereCenter.Z,
                    v.Capsule.Radius));
            }
            return string.Join("|", parts);
        }
    }
}
