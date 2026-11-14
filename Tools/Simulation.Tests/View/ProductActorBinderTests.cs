#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Presentation.View;
using Xunit;

namespace Shooter.Simulation.Tests.View
{
    public sealed class ProductActorBinderTests
    {
        private static ActorLifecycleState Live(ulong id, byte team = 1) =>
            new ActorLifecycleState(new EntityId(id), team, true, 100, new Float3(id, 0, 0), 0, 0, 0, 30, 90);

        private static ActorLifecycleState Dead(ulong id, byte team = 1) =>
            new ActorLifecycleState(new EntityId(id), team, false, 0, new Float3(id, 0, 0), 0, 0, 0, 0, 0);

        [Fact]
        public void SpawnsLivingActorsAndDespawnsMissingOnes()
        {
            var binder = new ProductActorBinder();
            var spawn = new List<EntityId>();
            var despawn = new List<EntityId>();
            var corpses = new List<EntityId>();

            binder.Reconcile(new[] { Live(1), Live(2) }, System.Array.Empty<ActorLifecycleState>(), spawn, despawn, corpses);
            Assert.Equal(2, spawn.Count);
            Assert.Empty(despawn);
            Assert.Equal(2, binder.LiveHandleCount);

            binder.Reconcile(new[] { Live(2) }, System.Array.Empty<ActorLifecycleState>(), spawn, despawn, corpses);
            Assert.Empty(spawn);
            Assert.Single(despawn);
            Assert.Equal(1UL, despawn[0].Value);
            Assert.Equal(1, binder.LiveHandleCount);
        }

        [Fact]
        public void LifeReplacementDespawnsOldAndSpawnsFreshEntityId()
        {
            var binder = new ProductActorBinder();
            var spawn = new List<EntityId>();
            var despawn = new List<EntityId>();
            var corpses = new List<EntityId>();

            binder.Reconcile(new[] { Live(10) }, System.Array.Empty<ActorLifecycleState>(), spawn, despawn, corpses);
            binder.Reconcile(new[] { Live(11) }, new[] { Dead(10) }, spawn, despawn, corpses);

            Assert.Contains(spawn, e => e.Value == 11UL);
            // Old EntityId converts to a corpse handle without a destructive despawn.
            Assert.DoesNotContain(despawn, e => e.Value == 10UL);
            Assert.Contains(corpses, e => e.Value == 10UL);
            Assert.Equal(1, binder.LiveHandleCount);
            Assert.Equal(1, binder.CorpseHandleCount);
        }

        [Fact]
        public void CapsCosmeticCorpsesAndClearsStaleHandles()
        {
            var binder = new ProductActorBinder();
            var spawn = new List<EntityId>();
            var despawn = new List<EntityId>();
            var ensure = new List<EntityId>();

            binder.Reconcile(System.Array.Empty<ActorLifecycleState>(), new[]
            {
                Dead(1), Dead(2), Dead(3), Dead(4), Dead(5)
            }, spawn, despawn, ensure);

            Assert.Equal(ProductActorBinder.MaxCosmeticCorpses, binder.CorpseHandleCount);
            Assert.True(ensure.Count <= ProductActorBinder.MaxCosmeticCorpses);

            binder.Reconcile(System.Array.Empty<ActorLifecycleState>(), System.Array.Empty<ActorLifecycleState>(), spawn, despawn, ensure);
            Assert.Equal(0, binder.CorpseHandleCount);
            Assert.NotEmpty(despawn);
        }

        [Fact]
        public void ClearRemovesAllHandles()
        {
            var binder = new ProductActorBinder();
            var spawn = new List<EntityId>();
            var despawn = new List<EntityId>();
            var ensure = new List<EntityId>();
            binder.Reconcile(new[] { Live(1) }, new[] { Dead(2) }, spawn, despawn, ensure);
            binder.Clear();
            Assert.Equal(0, binder.LiveHandleCount);
            Assert.Equal(0, binder.CorpseHandleCount);
            binder.Reconcile(new[] { Live(1) }, System.Array.Empty<ActorLifecycleState>(), spawn, despawn, ensure);
            Assert.Single(spawn);
        }

        [Fact]
        public void TickAimMovesTowardAdsWithoutAuthorityMutation()
        {
            var view = new ProceduralViewModel();
            view.TickAim(true, 0.15f);
            Assert.InRange(view.Pose.AdsBlend, 0.99f, 1.01f);
            view.TickAim(false, 0.15f);
            Assert.InRange(view.Pose.AdsBlend, 0f, 0.01f);
        }
    }
}
