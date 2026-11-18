#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Simulation.Combat;
using Shooter.Simulation.Session;
using Shooter.World.Runtime;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    /// <summary>ADR-105: a dying soldier's primary drops for 15 s; the human takes it with the pickup key.</summary>
    public sealed class DroppedWeaponSessionTests
    {
        private static readonly DefinitionId Rifle = WeaponLoadoutPolicy.DefaultPrimaryId;
        private static readonly DefinitionId Carbine = WeaponLoadoutPolicy.CarbinePrimaryId;

        private sealed class Harness : IDisposable
        {
            public OfflineGameSession Session = null!;
            public HarnessActorLifecycle Lifecycle = null!;
            public uint Sequence;
            public void Dispose() => Session.Dispose();
        }

        private static async Task<Harness> StartAsync()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            var session = new OfflineGameSession(catalog,
                (_, __) => new FakeMapRuntime(new PreMatchCountdownTests.FloorWorld(), new PreMatchCountdownTests.NoNav()),
                allowFakeMapRuntime: true) { RandomLoadouts = false };
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            return new Harness { Session = session, Lifecycle = Assert.IsType<HarnessActorLifecycle>(session.Lifecycle) };
        }

        private static InputCommand Idle(Harness h, InputButtons buttons = InputButtons.None) =>
            new InputCommand(h.Session.HumanEntityId, h.Session.CompletedTick, ++h.Sequence, 0f, 0f, 0f, 0f, buttons, 0);

        private static ActorLifecycleState Human(Harness h)
        {
            Assert.True(h.Lifecycle.TryGetActorState(h.Session.HumanEntityId, out var state));
            return state;
        }

        private static WeaponState HumanPrimary(Harness h)
        {
            Assert.True(h.Lifecycle.Simulation.TryGetWeapon(h.Session.HumanEntityId, 0, out var weapon));
            return weapon;
        }

        /// <summary>Pins an enemy bot 4 m in front of the human and fires (ADS) until it dies. Returns its drop.</summary>
        private static DroppedWeaponView KillEnemyBot(Harness h)
        {
            var snap = h.Session.TryAcquireSnapshot()!;
            EntityId victim = default;
            foreach (var p in snap.Participants)
                if (!p.IsHuman && p.Team == 2 && p.Alive) { victim = p.EntityId; break; }
            Assert.True(victim.IsValid);
            var origin = Human(h).Feet;
            var target = new Float3(origin.X, origin.Y, origin.Z + 4f);
            for (int t = 0; t < 600; t++)
            {
                Assert.True(Human(h).Alive, "human died before the kill");
                Assert.True(h.Lifecycle.Simulation.RelocateActor(h.Session.HumanEntityId, origin, 0f));
                if (!h.Lifecycle.Simulation.RelocateActor(victim, target, (float)Math.PI)) break;
                var fire = InputButtons.AimHeld | InputButtons.FireHeld | (t % 2 == 0 ? InputButtons.FirePressed : InputButtons.None);
                Assert.True(h.Session.Step(Idle(h, fire)).Succeeded);
                if (!h.Lifecycle.TryGetActorState(victim, out var state) || !state.Alive) break;
            }
            Assert.True(TryFindNear(h, target, out var drop), "no weapon dropped at the body");
            return drop;
        }

        private static bool TryFindNear(Harness h, Float3 position, out DroppedWeaponView found)
        {
            for (int i = 0; i < h.Session.DroppedWeaponCount; i++)
            {
                Assert.True(h.Session.TryGetDroppedWeapon(i, out var view));
                double dx = view.Position.X - position.X, dz = view.Position.Z - position.Z;
                if (dx * dx + dz * dz < 0.25) { found = view; return true; }
            }
            found = default;
            return false;
        }

        private static bool Exists(Harness h, int id)
        {
            for (int i = 0; i < h.Session.DroppedWeaponCount; i++)
                if (h.Session.TryGetDroppedWeapon(i, out var view) && view.Id == id) return true;
            return false;
        }

        [Fact]
        public async Task Killed_bot_drops_its_primary_which_expires_after_fifteen_seconds()
        {
            using var h = await StartAsync();
            var drop = KillEnemyBot(h);
            Assert.Equal(Rifle, drop.WeaponId);
            Assert.True(drop.Magazine + drop.Reserve > 0);
            Assert.InRange(drop.SecondsRemaining, 14.9f, 15f);

            uint droppedAt = h.Session.TryAcquireSnapshot()!.ActiveTicksElapsed;
            while (h.Session.TryAcquireSnapshot()!.ActiveTicksElapsed < droppedAt + DroppedWeaponField.LifetimeActiveTicks - 2)
            {
                Assert.True(h.Session.Step(null).Succeeded);
                if (h.Session.Phase != GameMatchPhase.Playing) return;
            }
            Assert.True(Exists(h, drop.Id));
            for (int i = 0; i < 4; i++) Assert.True(h.Session.Step(null).Succeeded);
            Assert.False(Exists(h, drop.Id));
        }

        [Fact]
        public async Task Same_gun_pickup_tops_up_reserve_capped_and_removes_the_drop()
        {
            using var h = await StartAsync();
            var drop = KillEnemyBot(h);
            var human = h.Session.HumanEntityId;
            Assert.True(h.Lifecycle.Simulation.SetPrimaryReserve(human, 0));
            Assert.True(h.Lifecycle.Simulation.RelocateActor(human, drop.Position, 0f));
            Assert.True(h.Session.Step(Idle(h)).Succeeded);

            Assert.True(h.Session.TryGetHumanPickupPrompt(out var prompt, out bool ammo));
            Assert.Equal(drop.Id, prompt.Id);
            Assert.True(ammo);

            int pickups = h.Session.HumanPickupCount;
            Assert.True(h.Session.Step(Idle(h), false, pickupRequested: true).Succeeded);
            var cap = WeaponDefinition.FromId(Rifle).ReserveCapacity;
            Assert.Equal((ushort)Math.Min((int)cap, drop.Magazine + drop.Reserve), HumanPrimary(h).Reserve);
            Assert.Equal(Rifle, HumanPrimary(h).DefinitionId);
            Assert.False(Exists(h, drop.Id));
            Assert.Equal(pickups + 1, h.Session.HumanPickupCount);
        }

        [Fact]
        public async Task Different_gun_pickup_swaps_primary_and_drops_the_old_one()
        {
            using var h = await StartAsync();
            var drop = KillEnemyBot(h);
            var human = h.Session.HumanEntityId;
            Assert.True(h.Lifecycle.Simulation.ReplacePrimary(human, Carbine, 10, 20));
            Assert.True(h.Lifecycle.Simulation.RelocateActor(human, drop.Position, 0f));
            Assert.True(h.Session.Step(Idle(h)).Succeeded);
            Assert.True(h.Session.TryGetHumanPickupPrompt(out _, out bool ammo));
            Assert.False(ammo);

            var carbineBefore = HumanPrimary(h);
            Assert.True(h.Session.Step(Idle(h), false, pickupRequested: true).Succeeded);

            var primary = HumanPrimary(h);
            Assert.Equal(Rifle, primary.DefinitionId);
            Assert.Equal(drop.Magazine, primary.AmmoInMagazine);
            Assert.Equal(drop.Reserve, primary.Reserve);
            Assert.False(Exists(h, drop.Id));
            Assert.True(TryFindNear(h, drop.Position, out var old));
            Assert.Equal(Carbine, old.WeaponId);
            Assert.Equal(carbineBefore.AmmoInMagazine, old.Magazine);
            Assert.Equal(carbineBefore.Reserve, old.Reserve);
            Assert.InRange(old.SecondsRemaining, 14.9f, 15f);
        }

        [Fact]
        public async Task Pickup_out_of_reach_or_without_the_key_does_nothing()
        {
            using var h = await StartAsync();
            var drop = KillEnemyBot(h);
            var human = h.Session.HumanEntityId;
            Assert.True(h.Lifecycle.Simulation.ReplacePrimary(human, Carbine, 10, 20));

            Assert.True(h.Lifecycle.Simulation.RelocateActor(human, drop.Position, 0f));
            Assert.True(h.Session.Step(Idle(h)).Succeeded); // in reach, no key
            Assert.Equal(Carbine, HumanPrimary(h).DefinitionId);
            Assert.True(Exists(h, drop.Id));

            var far = new Float3(drop.Position.X + 5f, drop.Position.Y, drop.Position.Z);
            Assert.True(h.Lifecycle.Simulation.RelocateActor(human, far, 0f));
            Assert.True(h.Session.Step(Idle(h), false, pickupRequested: true).Succeeded);
            Assert.False(h.Session.TryGetHumanPickupPrompt(out _, out _));
            Assert.Equal(Carbine, HumanPrimary(h).DefinitionId);
            Assert.True(Exists(h, drop.Id));
        }

        [Fact]
        public async Task Out_of_world_death_drops_nothing()
        {
            using var h = await StartAsync();
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.True(catalog.TryGetGameplay(WellKnownMaps.TrainingCompound, out var gameplay));
            var human = h.Session.HumanEntityId;
            Assert.True(h.Lifecycle.Simulation.RelocateActor(human,
                new Float3(gameplay!.Bounds.Min.X, gameplay.Bounds.Min.Y - 10.5f, gameplay.Bounds.Min.Z), 0));
            int before = h.Session.DroppedWeaponCount;
            Assert.True(h.Session.Step(null).Succeeded);
            foreach (var p in h.Session.TryAcquireSnapshot()!.Participants)
                if (p.IsHuman) Assert.False(p.Alive);
            Assert.Equal(before, h.Session.DroppedWeaponCount);
        }
    }
}
