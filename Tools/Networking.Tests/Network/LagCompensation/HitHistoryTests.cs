#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Networking.LagCompensation;
using Xunit;

namespace Shooter.Simulation.Tests.Network.LagCompensation
{
    public sealed class HitHistoryTests
    {
        private static Capsule Cap(float z) => new Capsule(new Float3(0, 0.32f, z), new Float3(0, 1.48f, z), 0.32f);

        [Fact]
        public void History_boundaries_dead_actors_and_cover_eligibility()
        {
            var buffer = new HitHistoryBuffer();
            for (uint t = 1; t <= 20; t++)
            {
                bool alive = t < 15;
                buffer.Push(new HistoryFrame(t, new List<HistoricalActorSample>
                {
                    new HistoricalActorSample(new EntityId(2), 1, Cap(t), alive, alive)
                }));
            }
            Assert.Equal(HitHistoryBuffer.Capacity, buffer.Count);
            Assert.False(buffer.TryGetExact(1, out _)); // evicted
            Assert.True(buffer.TryGetExact(20, out _));

            Assert.True(buffer.TryApproveShotTick(20, 20, out var now, out _));
            Assert.Equal(20u, now);
            Assert.False(buffer.TryApproveShotTick(20, 21, out _, out var future));
            Assert.Equal("future", future);
            Assert.False(buffer.TryApproveShotTick(20, 1, out _, out var stale));
            Assert.Equal("stale", stale);

            Assert.True(buffer.TryApproveShotTick(20, 10, out var approved, out _));
            Assert.True(approved >= 20u - HitHistoryBuffer.MaxRewindTicks);
            Assert.True(buffer.TryQueryInterpolated(14, new EntityId(2), 1, out var livingCapsule, out bool living));
            Assert.True(livingCapsule.IsValid);
            Assert.True(living);
            Assert.True(buffer.TryQueryInterpolated(16, new EntityId(2), 1, out _, out bool dead));
            Assert.False(dead);
        }

        [Fact]
        public void Fire_validator_rejects_duplicates_dead_and_allows_same_tick_trade()
        {
            var fire = new AuthoritativeFireValidator();
            var a = new FireIntent(new EntityId(1), 1, 10, 0, 30, 0, true);
            var b = new FireIntent(new EntityId(2), 1, 10, 0, 30, 0, true);
            Assert.Equal(FireValidationResult.Accepted, fire.ValidateAndQueueDamage(a, new EntityId(2), 100));
            Assert.Equal(FireValidationResult.Accepted, fire.ValidateAndQueueDamage(b, new EntityId(1), 100));
            Assert.Equal(FireValidationResult.RejectDuplicate, fire.Validate(a));
            Assert.Equal(FireValidationResult.RejectDead, fire.Validate(new FireIntent(new EntityId(3), 1, 10, 0, 30, 0, false)));
            Assert.Equal(FireValidationResult.RejectAmmo, fire.Validate(new FireIntent(new EntityId(3), 2, 10, 0, 0, 0, true)));
            Assert.Equal(FireValidationResult.RejectCadence, fire.Validate(new FireIntent(new EntityId(3), 3, 5, 0, 10, 10, true)));

            var health = fire.ResolveHealth(new Dictionary<EntityId, ushort>
            {
                [new EntityId(1)] = 100,
                [new EntityId(2)] = 100
            });
            Assert.Equal(0, health[new EntityId(1)]);
            Assert.Equal(0, health[new EntityId(2)]);
        }
    }
}
