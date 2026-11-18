using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Simulation.Pose;
using Xunit;

/// <summary>
/// ADR-112: the authoritative head and torso hit volumes must sit where the drawn soldier (SoldierMotion with the
/// ADR-095 shooting posture, as ProductActorPresentation draws it) has its head and chest, for every stance and aim
/// pitch the presentation can show. Before ADR-112 a crouched head was drawn 0.22 m above its hit sphere.
/// </summary>
public sealed class HitVolumeRigAlignmentTests
{
    private sealed class Flat : ISoldierGround { public bool TrySample(float x, float z, out float height) { height = 0; return true; } }

    private static WeaponRecipe Rifle() => WeaponRecipe.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "FeatureRecipes", "rifle.json")));

    [Theory]
    [InlineData(0f, false, -0.4f)]
    [InlineData(0f, false, 0f)]
    [InlineData(0f, false, 0.4f)]
    [InlineData(3.5f, false, 0f)]
    [InlineData(5.5f, false, 0.3f)]
    [InlineData(0f, true, 0f)]
    [InlineData(2f, true, -0.3f)]
    public void HeadSphereContainsTheDrawnHeadAndTheTorsoTheDrawnChest(float speed, bool crouch, float pitch)
    {
        const float yaw = 0.7f;
        var motion = new SoldierMotion(Rifle(), new ShootingPosture());
        var ground = new Flat();
        double distance = 0;
        float s = (float)Math.Sin(yaw), c = (float)Math.Cos(yaw);
        Float3 root = default;
        SoldierPose? pose = null;
        for (int i = 0; i < 180; i++)
        {
            distance += speed / 60.0;
            root = new Float3(s * (float)distance, 0, c * (float)distance);
            pose = motion.Evaluate(new SoldierMotionInput(root, yaw, distance, speed, crouch ? 1f : 0f, 0f, pitch, -1f, i == 0), ground);
        }

        var volumes = new List<HitVolume>();
        var stance = crouch ? Stance.Crouching : Stance.Standing;
        new GameplayPoseEvaluator().Evaluate(new EntityId(1), root, yaw, pitch, stance, crouch ? 1.2f : 1.8f, speed, 1, volumes);

        var drawnHead = pose!.WorldPositions[6];
        var head = volumes[0].Capsule;
        float headGap = Distance(drawnHead, head.BottomSphereCenter);
        Assert.True(headGap <= 0.05f, $"drawn head {drawnHead.X:F3},{drawnHead.Y:F3},{drawnHead.Z:F3} is {headGap:F3} m from the head sphere centre");

        var chest = pose.WorldPositions[4];
        var torso = volumes[1].Capsule;
        Assert.InRange(chest.Y, torso.BottomSphereCenter.Y, torso.TopSphereCenter.Y + torso.Radius);
        float planar = (float)Math.Sqrt((chest.X - root.X) * (chest.X - root.X) + (chest.Z - root.Z) * (chest.Z - root.Z));
        Assert.True(planar <= torso.Radius, $"drawn chest {planar:F3} m off the torso axis");
    }

    private static float Distance(Float3 a, Float3 b) =>
        (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
}
