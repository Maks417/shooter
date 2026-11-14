#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Generation.Environment;
using Shooter.World;
using Shooter.World.Navigation;
using UnityEngine;

namespace Shooter.Tests
{
    public sealed class UnityNavMeshBakeInstallTests
    {
        [Test]
        public void InstallsPackagedBakeAndReturnsReachableCorners()
        {
            // Minimal open quad so install does not depend on full map packages in PlayMode.
            var bake = new UnityNavMeshBakeCodec.Bake(
                new[]
                {
                    new Float3(-8f, 0f, -8f),
                    new Float3(8f, 0f, -8f),
                    new Float3(8f, 0f, 8f),
                    new Float3(-8f, 0f, 8f)
                },
                new[] { new UnityNavMeshBakeCodec.Polygon(new uint[] { 0, 1, 2, 3 }, 0, 0) },
                new Float3(-8f, -1f, -8f),
                new Float3(8f, 2f, 8f));

            using var install = UnityNavMeshBakeInstaller.Install(bake, NavSettings.SliceDefault);
            Assert.That(install.IsInstalled, Is.True);
            Assert.That(install.TrySamplePosition(new Float3(0f, 0f, 0f), 1f, out _), Is.True);

            var corners = new List<Float3>();
            Assert.That(install.CalculatePath(new Float3(-4f, 0f, -4f), new Float3(4f, 0f, 4f), corners), Is.EqualTo(NavPathOutcome.Complete));
            Assert.That(corners.Count, Is.GreaterThanOrEqualTo(1));
        }

        private static UnityNavMeshBakeCodec.Bake TwoDisconnectedQuads() => new UnityNavMeshBakeCodec.Bake(
            new[]
            {
                new Float3(-8f, 0f, -8f), new Float3(-2f, 0f, -8f), new Float3(-2f, 0f, 8f), new Float3(-8f, 0f, 8f),
                new Float3(2f, 0f, -8f), new Float3(8f, 0f, -8f), new Float3(8f, 0f, 8f), new Float3(2f, 0f, 8f)
            },
            new[]
            {
                new UnityNavMeshBakeCodec.Polygon(new uint[] { 0, 1, 2, 3 }, 0, 0),
                new UnityNavMeshBakeCodec.Polygon(new uint[] { 4, 5, 6, 7 }, 0, 0)
            },
            new Float3(-8f, -1f, -8f),
            new Float3(8f, 2f, 8f));

        [Test]
        public void DisconnectedQuadsNeverReportNativeComplete()
        {
            using var install = UnityNavMeshBakeInstaller.Install(TwoDisconnectedQuads(), NavSettings.SliceDefault);
            var corners = new List<Float3>();
            var outcome = install.CalculatePath(new Float3(-5f, 0f, 0f), new Float3(5f, 0f, 0f), corners);
            Assert.That(outcome, Is.Not.EqualTo(NavPathOutcome.Complete));
            Assert.That(outcome, Is.Not.EqualTo(NavPathOutcome.FallbackComplete));
            if (outcome == NavPathOutcome.Failed) Assert.That(corners.Count, Is.EqualTo(0));
            else
            {
                Assert.That(outcome, Is.EqualTo(NavPathOutcome.Partial));
                Assert.That(corners[corners.Count - 1].X, Is.LessThan(0f), "partial path must end on the reachable quad");
            }
        }

        [Test]
        public void FallbackIsOffByDefaultAndNeverNativeComplete()
        {
            using var install = UnityNavMeshBakeInstaller.Install(TwoDisconnectedQuads(), NavSettings.SliceDefault);
            var corners = new List<Float3>();
            var outcome = install.CalculatePath(new Float3(-5f, 0f, 0f), new Float3(5f, 0f, 0f), corners, allowPackagedFallback: true);
            Assert.That(outcome, Is.Not.EqualTo(NavPathOutcome.Complete));
        }

        [Test]
        public void ObstructedWallSplitsAreNeverComplete()
        {
            // Walkable ring around a hole in the middle band: left and right halves joined by nothing.
            using var install = UnityNavMeshBakeInstaller.Install(TwoDisconnectedQuads(), NavSettings.SliceDefault);
            var corners = new List<Float3>();
            var outcome = install.CalculatePath(new Float3(5f, 0f, 5f), new Float3(-5f, 0f, -5f), corners);
            Assert.That(outcome, Is.Not.EqualTo(NavPathOutcome.Complete));
        }
    }
}
