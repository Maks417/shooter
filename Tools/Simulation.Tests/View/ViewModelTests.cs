#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Presentation.View;
using Xunit;

namespace Shooter.Simulation.Tests.View
{
    public sealed class ViewModelTests
    {
        [Fact]
        public void DuplicateEventsDoNotDoubleRecoil()
        {
            var view = new ProceduralViewModel();
            var ev = new CombatEvent(new EventId(9, 1, new EntityId(1), 1), CombatEventKind.ShotAccepted, new EntityId(1), default,
                new ShotPayload(new DefinitionId("weapons/rifle"), 1, default, new Float3(0, 0, 1), 1, 1));
            var list = new FrozenList<CombatEvent>(new[] { ev, ev }, 4096);
            using var snap = new SnapshotLease(new SimulationSnapshot(1, 1, MatchState.Playing, default, Array.Empty<EntitySnapshot>(), Array.Empty<HitVolume>()), () => { });
            using var lease = new EventLease(list, () => { });
            view.Consume(snap, lease, false, 1f / 60f);
            Assert.Equal(1, view.ConsumedEventCount);
            float pitchAfterFirstImpulseWindow = view.Pose.RecoilPitch;
            // Second consume of same EventIds must not register another impulse (dedupe).
            view.Consume(snap, lease, false, 0f);
            Assert.Equal(1, view.ConsumedEventCount);
            Assert.Equal(pitchAfterFirstImpulseWindow, view.Pose.RecoilPitch);
        }

        [Fact]
        public void GripTolerancePassesAtDefaultSockets()
        {
            var view = new ProceduralViewModel();
            using var snap = new SnapshotLease(new SimulationSnapshot(0, 1, MatchState.Ready, default, Array.Empty<EntitySnapshot>(), Array.Empty<HitVolume>()), () => { });
            using var lease = new EventLease(new FrozenList<CombatEvent>(Array.Empty<CombatEvent>(), 4096), () => { });
            view.Consume(snap, lease, true, 0.15f);
            Assert.True(view.HandsWithinGripTolerance(view.Pose.LeftHand, view.Pose.RightHand));
            Assert.InRange(view.Pose.AdsBlend, 0.99f, 1.01f);
        }
    }
}
