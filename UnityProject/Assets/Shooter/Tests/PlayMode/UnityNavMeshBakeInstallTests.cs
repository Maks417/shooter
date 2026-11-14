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
            Assert.That(install.TryCalculatePath(new Float3(-4f, 0f, -4f), new Float3(4f, 0f, 4f), corners), Is.True);
            Assert.That(corners.Count, Is.GreaterThanOrEqualTo(1));
        }
    }
}
