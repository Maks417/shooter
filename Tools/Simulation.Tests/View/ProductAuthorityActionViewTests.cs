#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Presentation.View;
using Xunit;

namespace Shooter.Simulation.Tests.View
{
    public sealed class ProductAuthorityActionViewTests
    {
        private static CombatEvent ReloadStarted(ulong entity, byte slot, uint start, uint end, ushort ordinal = 1) =>
            new CombatEvent(
                new EventId(1, start, new EntityId(entity), ordinal),
                CombatEventKind.ReloadStarted,
                new EntityId(entity),
                default,
                new ActionPayload(slot, start, end));

        private static CombatEvent ReloadCompleted(ulong entity, byte slot, uint start, uint end, ushort ordinal = 2) =>
            new CombatEvent(
                new EventId(1, end, new EntityId(entity), ordinal),
                CombatEventKind.ReloadCompleted,
                new EntityId(entity),
                default,
                new ActionPayload(slot, start, end));

        private static CombatEvent Shot(ulong entity, ushort ordinal = 1) =>
            new CombatEvent(
                new EventId(1, 10, new EntityId(entity), ordinal),
                CombatEventKind.ShotAccepted,
                new EntityId(entity),
                default,
                new ShotPayload(new DefinitionId("weapons/rifle"), 1, default, new Float3(0, 0, 1), 1, 1));

        [Fact]
        public void ActiveWeaponSlotDrivesRifleAndPistolSelection()
        {
            var view = new ProductAuthorityActionView();
            var rifle = new EntityId(1);
            var pistol = new EntityId(2);
            view.SyncLifecycle(rifle, ProductAuthorityActionView.RifleSlot, true);
            view.SyncLifecycle(pistol, ProductAuthorityActionView.PistolSlot, true);
            Assert.True(view.IsRifle(rifle));
            Assert.True(view.IsPistol(pistol));
            Assert.Equal(ProductAuthorityActionView.RifleSlot, view.ActiveWeaponSlot(rifle));
            Assert.Equal(ProductAuthorityActionView.PistolSlot, view.ActiveWeaponSlot(pistol));
        }

        [Fact]
        public void WeaponSwitchCancelsReloadWithoutCompletedEvent()
        {
            var view = new ProductAuthorityActionView();
            var id = new EntityId(3);
            view.SyncLifecycle(id, ProductAuthorityActionView.RifleSlot, true);
            view.ApplyEvents(new[] { ReloadStarted(3, ProductAuthorityActionView.RifleSlot, 100, 250) });
            view.SyncLifecycle(id, ProductAuthorityActionView.RifleSlot, true);
            Assert.True(view.IsReloading(id));
            view.SyncLifecycle(id, ProductAuthorityActionView.PistolSlot, true);
            Assert.False(view.IsReloading(id));
            Assert.Equal(-1f, view.ReloadPhaseAtTick(id, 120));
        }

        [Fact]
        public void ReconcileClearsStaleReloadOnlyAfterWindowAndWhenAuthorityIsNotReloading()
        {
            var view = new ProductAuthorityActionView();
            var id = new EntityId(4);
            view.SyncLifecycle(id, ProductAuthorityActionView.RifleSlot, true);
            view.ApplyEvents(new[] { ReloadStarted(4, ProductAuthorityActionView.RifleSlot, 100, 250) });
            view.ReconcileReload(id, WeaponAction.Ready, 150);
            Assert.True(view.IsReloading(id));
            view.ReconcileReload(id, WeaponAction.Reloading, 260);
            Assert.True(view.IsReloading(id));
            view.ReconcileReload(id, WeaponAction.Ready, 260);
            Assert.False(view.IsReloading(id));
        }

        [Fact]
        public void ReloadStartedFromEarlierTickOfMultiTickFrameStillStartsPose()
        {
            // The session now leases every tick's events since the last frame; an earlier tick's ReloadStarted
            // followed by later-tick shots must still start the reload.
            var view = new ProductAuthorityActionView();
            var id = new EntityId(5);
            view.SyncLifecycle(id, ProductAuthorityActionView.RifleSlot, true);
            view.ApplyEvents(new[] { ReloadStarted(5, ProductAuthorityActionView.RifleSlot, 100, 250), Shot(9, 3) });
            Assert.True(view.IsReloading(id));
            Assert.InRange(view.ReloadPhaseAtTick(id, 103), 0.01f, 0.05f);
        }

        [Fact]
        public void ShotAcceptedDoesNotStartReloadPhase()
        {
            var view = new ProductAuthorityActionView();
            var id = new EntityId(7);
            view.SyncLifecycle(id, ProductAuthorityActionView.RifleSlot, true);
            view.ApplyEvents(new[] { Shot(7) });
            Assert.False(view.IsReloading(id));
            Assert.Equal(-1f, view.CurrentReloadPhase(id));
            Assert.True(view.ConsumeFireImpulse(id));
            Assert.False(view.ConsumeFireImpulse(id));
        }

        [Fact]
        public void ReloadUsesActionPayloadDurationNotAmmoDrop()
        {
            var view = new ProductAuthorityActionView();
            var id = new EntityId(3);
            view.SyncLifecycle(id, ProductAuthorityActionView.RifleSlot, true);
            view.ApplyEvents(new[] { ReloadStarted(3, ProductAuthorityActionView.RifleSlot, 100, 244) });
            Assert.True(view.IsReloading(id));
            Assert.Equal(2.4f, ProductAuthorityActionView.DurationSeconds(100, 244), 3);
            float mid = view.ReloadPhaseAtTick(id, 172);
            Assert.InRange(mid, 0.49f, 0.51f);
            view.ApplyEvents(new[] { ReloadCompleted(3, ProductAuthorityActionView.RifleSlot, 100, 244) });
            Assert.False(view.IsReloading(id));
            Assert.Equal(-1f, view.CurrentReloadPhase(id));
        }

        [Fact]
        public void PistolReloadDurationIsShorterThanRifle()
        {
            var view = new ProductAuthorityActionView();
            var rifle = new EntityId(10);
            var pistol = new EntityId(11);
            view.SyncLifecycle(rifle, ProductAuthorityActionView.RifleSlot, true);
            view.SyncLifecycle(pistol, ProductAuthorityActionView.PistolSlot, true);
            view.ApplyEvents(new[]
            {
                ReloadStarted(10, ProductAuthorityActionView.RifleSlot, 0, 144, 1),
                ReloadStarted(11, ProductAuthorityActionView.PistolSlot, 0, 108, 1)
            });
            Assert.Equal(2.4f, ProductAuthorityActionView.DurationSeconds(0, 144), 3);
            Assert.Equal(1.8f, ProductAuthorityActionView.DurationSeconds(0, 108), 3);
            float riflePhase = view.ResolveReloadPhase(rifle, 0, 0.9f);
            float pistolPhase = view.ResolveReloadPhase(pistol, 0, 0.9f);
            Assert.True(pistolPhase > riflePhase);
        }

        [Fact]
        public void HumanBotReplacementForgetsPriorActionState()
        {
            var view = new ProductAuthorityActionView();
            var oldLife = new EntityId(20);
            var newLife = new EntityId(21);
            view.SyncLifecycle(oldLife, ProductAuthorityActionView.PistolSlot, true);
            view.ApplyEvents(new[] { ReloadStarted(20, ProductAuthorityActionView.PistolSlot, 5, 113) });
            Assert.True(view.IsReloading(oldLife));
            view.Forget(oldLife);
            view.SyncLifecycle(newLife, ProductAuthorityActionView.RifleSlot, true);
            Assert.False(view.IsReloading(oldLife));
            Assert.False(view.IsReloading(newLife));
            Assert.True(view.IsRifle(newLife));
        }

        [Fact]
        public void DuplicateEventsAreIgnored()
        {
            var view = new ProductAuthorityActionView();
            var id = new EntityId(8);
            view.SyncLifecycle(id, 0, true);
            var started = ReloadStarted(8, 0, 1, 145, 3);
            view.ApplyEvents(new[] { started, started });
            Assert.Equal(1, view.ConsumedEventCount);
            Assert.True(view.IsReloading(id));
        }

        [Fact]
        public void ProceduralViewModelReloadUsesPayloadDuration()
        {
            var local = new ProceduralViewModel();
            local.ResetForLife(ProductAuthorityActionView.PistolSlot);
            var ev = ReloadStarted(1, ProductAuthorityActionView.PistolSlot, 0, 108, 1);
            local.ConsumeEvents(new[] { ev }, new EntityId(1), false, 0f);
            Assert.InRange(local.ReloadPhase, 0.009f, 0.02f);
            local.ConsumeEvents(Array.Empty<CombatEvent>(), new EntityId(1), false, 0.9f);
            Assert.InRange(local.ReloadPhase, 0.49f, 0.52f);
            Assert.Equal(ProductAuthorityActionView.PistolSlot, local.ActiveWeaponSlot);
        }

        [Fact]
        public void ProceduralViewModelIgnoresRemoteReloadForLocalFilter()
        {
            var local = new ProceduralViewModel();
            local.ResetForLife();
            var remote = ReloadStarted(99, 0, 0, 144, 1);
            local.ConsumeEvents(new[] { remote }, new EntityId(1), false, 0.5f);
            Assert.Equal(0f, local.ReloadPhase);
        }
    }
}
