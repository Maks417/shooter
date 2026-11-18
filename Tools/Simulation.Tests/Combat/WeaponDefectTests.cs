#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Combat;
using Shooter.Simulation.Pose;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    public sealed class WeaponDefectTests
    {
        private static ushort? Probe(List<HitVolume> volumes, float x, float y, float z0 = -3f)
        {
            float best = float.MaxValue; ushort? region = null;
            foreach (var v in volumes)
            {
                if (HitscanResolver.RayCapsule(new Float3(x, y, z0), new Float3(0, 0, 1), 50f, v.Capsule, out float t, out _, out _) && t < best)
                { best = t; region = v.RegionId; }
            }
            return region;
        }

        private static List<HitVolume> Pose(Stance stance)
        {
            var list = new List<HitVolume>();
            new GameplayPoseEvaluator().Evaluate(new EntityId(2), default, 0, 0, stance, stance == Stance.Crouching ? 1.2f : 1.8f, 1, list);
            return list;
        }

        [Fact]
        public void StandingPoseCylinderBodiesAreHit()
        {
            var v = Pose(Stance.Standing);
            Assert.Equal((ushort)2, Probe(v, 0, 1.15f)); // torso gap between cap spheres
            Assert.Equal((ushort)5, Probe(v, -0.1f, 0.45f)); // mid-thigh/leg (ADR-112 symmetric legs at ±0.1 m)
            Assert.Equal((ushort)3, Probe(v, -0.245f, 1.225f)); // mid-arm
            Assert.Null(Probe(v, -0.33f, 1.225f)); // just outside arm radius
            Assert.Null(Probe(v, 0.21f, 0.45f)); // just outside leg radius
            Assert.Null(Probe(v, 0f, 0.45f)); // between the legs
        }

        [Fact]
        public void CrouchedPoseCylinderBodiesAreHit()
        {
            var v = Pose(Stance.Crouching);
            Assert.Equal((ushort)2, Probe(v, 0, 0.675f));
            Assert.Equal((ushort)5, Probe(v, -0.1f, 0.28f));
            Assert.Equal((ushort)3, Probe(v, -0.245f, 0.975f));
            Assert.Null(Probe(v, -0.33f, 0.975f));
        }

        [Fact]
        public void CylinderHitReportsOutwardNormalAndEntryPoint()
        {
            var cap = new Capsule(new Float3(0, 0, 5), new Float3(0, 2, 5), 0.5f);
            Assert.True(HitscanResolver.RayCapsule(new Float3(0, 1, 0), new Float3(0, 0, 1), 20f, cap, out float t, out var p, out var n));
            Assert.Equal(4.5f, t, 3);
            Assert.Equal(4.5f, p.Z, 3);
            Assert.Equal(-1f, n.Z, 3);
            Assert.False(HitscanResolver.RayCapsule(new Float3(0.51f, 1, 0), new Float3(0, 0, 1), 20f, cap, out _, out _, out _));
            Assert.False(HitscanResolver.RayCapsule(new Float3(0, 1, 0), new Float3(0, 0, 1), 4f, cap, out _, out _, out _));
        }

        [Fact]
        public void RecoilKicksUpNotDown()
        {
            float yaw = 0, pitch = 0;
            var pose = new GameplayPoseEvaluator();
            float baseY = pose.AimDirection(0, 0).Y;
            for (uint i = 1; i <= 30; i++)
            {
                AuthoritativeRecoil.ApplyImpulse(WeaponDefinition.Rifle, i, 7UL, ref yaw, ref pitch);
                Assert.True(pitch <= 0f);
                Assert.True(pitch >= -AuthoritativeRecoil.MaxPitch - 1e-6f);
                for (int d = 0; d < 5; d++) AuthoritativeRecoil.Decay(ref yaw, ref pitch);
            }
            Assert.True(pitch < 0f);
            Assert.True(pose.AimDirection(0, pitch).Y > baseY);
            for (int d = 0; d < 600; d++) AuthoritativeRecoil.Decay(ref yaw, ref pitch);
            Assert.True(Math.Abs(pitch) < 1e-4f);
        }

        private static (WeaponSimulator sim, WeaponState[] slots) Setup(WeaponState? rifle = null)
        {
            var slots = WeaponSimulator.CreateDefaultLoadout();
            if (rifle.HasValue) slots[0] = rifle.Value;
            return (new WeaponSimulator(), slots);
        }

        [Fact]
        public void ReloadAcceptedDuringFiringRecovery()
        {
            var (sim, slots) = Setup();
            byte active = 0, desired = 0;
            var shots = new List<AcceptedShot>();
            var grenades = new List<AcceptedGrenade>();
            var events = new List<CombatEvent>();
            var next = new EventId(1, 0, new EntityId(1), 0);
            sim.Step(0, InputButtons.FireHeld | InputButtons.FirePressed, 0, true, false, slots, ref active, ref desired, shots, grenades, events, ref next, new EntityId(1));
            Assert.Single(shots);
            Assert.Equal(WeaponAction.FiringRecovery, slots[0].Action);
            sim.Step(1, InputButtons.FireHeld | InputButtons.ReloadPressed, 0, true, false, slots, ref active, ref desired, shots, grenades, events, ref next, new EntityId(1));
            Assert.Equal(WeaponAction.Reloading, slots[0].Action);
            Assert.Empty(shots);
        }

        [Fact]
        public void ReloadStillRejectedWithFullMagazineOrNoReserve()
        {
            var (sim, slots) = Setup();
            byte active = 0, desired = 0;
            var shots = new List<AcceptedShot>();
            var grenades = new List<AcceptedGrenade>();
            var events = new List<CombatEvent>();
            var next = new EventId(1, 0, new EntityId(1), 0);
            sim.Step(0, InputButtons.ReloadPressed, 0, true, false, slots, ref active, ref desired, shots, grenades, events, ref next, new EntityId(1));
            Assert.Equal(WeaponAction.Ready, slots[0].Action);
            var def = WeaponDefinition.Rifle;
            var (sim2, slots2) = Setup(new WeaponState(def.Id, WeaponAction.Ready, 5, 0, 0, 0, 0, 0, false));
            sim2.Step(0, InputButtons.ReloadPressed, 0, true, false, slots2, ref active, ref desired, shots, grenades, events, ref next, new EntityId(1));
            Assert.Equal(WeaponAction.Ready, slots2[0].Action);
        }

        [Fact]
        public void SemiAutoFiresOnPressEdgeEvenIfPreviousTickHeldFire()
        {
            var (sim, slots) = Setup();
            byte active = 1, desired = 1;
            var shots = new List<AcceptedShot>();
            var grenades = new List<AcceptedGrenade>();
            var events = new List<CombatEvent>();
            var next = new EventId(1, 0, new EntityId(1), 0);
            int accepted = 0;
            for (uint tick = 0; tick < 12; tick++)
            {
                var b = InputButtons.FireHeld;
                if (tick == 0 || tick == 10) b |= InputButtons.FirePressed;
                sim.Step(tick, b, 1, true, false, slots, ref active, ref desired, shots, grenades, events, ref next, new EntityId(1));
                accepted += shots.Count;
            }
            Assert.Equal(2, accepted);
        }

        [Fact]
        public void AutomaticBotStyleInputStillFiresEveryInterval()
        {
            var (sim, slots) = Setup();
            byte active = 0, desired = 0;
            var shots = new List<AcceptedShot>();
            var grenades = new List<AcceptedGrenade>();
            var events = new List<CombatEvent>();
            var next = new EventId(1, 0, new EntityId(1), 0);
            int accepted = 0;
            for (uint tick = 0; tick < 60; tick++)
            {
                sim.Step(tick, InputButtons.FireHeld | InputButtons.FirePressed, 0, true, false, slots, ref active, ref desired, shots, grenades, events, ref next, new EntityId(1));
                accepted += shots.Count;
            }
            Assert.Equal(10, accepted);
        }
    }
}

