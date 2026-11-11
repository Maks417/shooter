using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Shooter.Contracts;
using Xunit;

namespace Shooter.Contracts.Tests
{
    public sealed class ValueAndFixtureTests
    {
        [Fact]
        public void ContractsAssemblyHasOnlyBclDependenciesAndNoMutableDtoProperties()
        {
            var assembly = typeof(InputCommand).Assembly;
            Assert.All(assembly.GetReferencedAssemblies(), a => Assert.Equal("netstandard", a.Name));
            foreach (var type in assembly.GetExportedTypes().Where(t => t != typeof(HitBuffer) && t != typeof(PathBuffer) && t != typeof(CommandBuffer) && t != typeof(PacketBuffer)))
                Assert.All(type.GetProperties(), p => Assert.Null(p.SetMethod));
            Assert.Equal(60, ContractVersion.SimulationHz);
        }

        [Fact]
        public void EnumRegistryMatchesSpecification()
        {
            var expected = new Dictionary<Type, int[]>
            {
                [typeof(QueryStatus)] = new[] { 0, 1, 2, 3 }, [typeof(Stance)] = new[] { 0, 1, 2 },
                [typeof(WeaponAction)] = new[] { 0, 1, 2, 3, 4 }, [typeof(ContentKind)] = new[] { 1, 2, 3, 4, 5 },
                [typeof(PathStatus)] = new[] { 0, 1, 2, 3, 4, 5 }, [typeof(MatchState)] = new[] { 0, 1, 2, 3, 4, 5 },
                [typeof(CombatEventKind)] = new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, [typeof(ErrorCode)] = Enumerable.Range(0, 10).ToArray(),
                [typeof(InputButtons)] = new[] { 0, 1, 2, 4, 8, 16, 32, 64 }, [typeof(Delivery)] = new[] { 0, 1 }
            };
            foreach (var e in expected) Assert.Equal(e.Value, Enum.GetValues(e.Key).Cast<object>().Select(Convert.ToInt32));
            Assert.Equal(8, (int)CombatEventKind.MatchChanged); Assert.Equal(4, (int)ContentKind.Collision);
        }

        [Theory]
        [InlineData(1UL)] [InlineData(ulong.MaxValue)]
        public void IdsRetainFull64Bits(ulong value)
        {
            Assert.Equal(value, new EntityId(value).Value); Assert.Equal(value, new SurfaceId(value).Value);
            Assert.Equal(value, new PeerId(value).Value); Assert.Equal(value, new NavTicketId(value).Value);
            Assert.Equal(value.ToString(System.Globalization.CultureInfo.InvariantCulture), new EntityId(value).ToString());
        }
        [Fact]
        public void ZeroIdsAndInvalidDefinitionsReject()
        {
            Assert.False(default(EntityId).IsValid);
            Assert.Throws<ArgumentException>(() => new EntityId(0)); Assert.Throws<ArgumentException>(() => new SurfaceId(0));
            Assert.Throws<ArgumentException>(() => new PeerId(0)); Assert.Throws<ArgumentException>(() => new NavTicketId(0));
            foreach (string bad in new[] { "", "/rifle", "Weapons/rifle", "a//b", "../a", "a/..", "a b", "a/", new string('a', 129) })
                Assert.Throws<ArgumentException>(() => new DefinitionId(bad));
            Assert.Equal("weapons/rifle", new DefinitionId("weapons/rifle").ToString());
        }
        [Fact]
        public void HashValueOwnsItsBytesAndPreservesLeadingZeros()
        {
            byte[] bytes = new byte[32]; bytes[31] = 255; var id = new ContentId(bytes); bytes[0] = 255;
            Assert.Equal(new string('0', 62) + "ff", id.ToString());
            var output = id.ToBytes(); output[0] = 1; Assert.Equal(0, id.ToBytes()[0]);
            Assert.Equal(id, new ContentId(id.ToString()));
            Assert.Throws<ArgumentException>(() => new ContentId(new string('A', 64)));
            Assert.Throws<ArgumentException>(() => new ContentId(new byte[31]));
        }
        [Theory]
        [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)] [InlineData(float.NegativeInfinity)]
        public void NonfiniteNumbersReject(float value)
        {
            Assert.Throws<ArgumentException>(() => new Float2(value, 0)); Assert.Throws<ArgumentException>(() => new Float3(0, value, 0));
            Assert.Throws<ArgumentException>(() => new Float4(0, 0, 0, value)); Assert.Throws<ArgumentException>(() => new Quat(0, 0, value, 1));
            Assert.Throws<ArgumentException>(() => new Capsule(default, default, value));
            Assert.Throws<ArgumentException>(() => Command(moveX: value)); Assert.Throws<ArgumentException>(() => Command(yaw: value));
        }
        [Fact]
        public void QuaternionAndBoundsValidate()
        {
            Assert.True(Quat.Identity.IsValid); Assert.False(default(Quat).IsValid);
            Assert.Throws<ArgumentException>(() => new Quat(0, 0, 0, 0));
            Assert.Throws<ArgumentException>(() => new Aabb(new Float3(1, 0, 0), default));
            Assert.Throws<ArgumentException>(() => new Capsule(default, default, 0));
        }
        [Fact]
        public void WrapComparisonAndOrdinalExhaustionAreExplicit()
        {
            Assert.True(Serial32.IsNewer(0, uint.MaxValue)); Assert.False(Serial32.IsNewer(uint.MaxValue, 0));
            Assert.False(Serial32.IsNewer(7, 7)); Assert.Equal(2u, Serial32.ForwardDistance(1, uint.MaxValue));
            Assert.Throws<ArgumentException>(() => Serial32.IsNewer(0x80000000, 0));
            Assert.Throws<ArgumentException>(() => Serial32.ForwardDistance(3, 4));
            Assert.True(default(EventId).IsEmpty);
            Assert.Throws<ArgumentException>(() => new EventId(0, 1, new EntityId(1), 0));
            Assert.Throws<OverflowException>(() => new EventId(1, 0, new EntityId(1), ushort.MaxValue).NextOrdinal());
        }

        internal static InputCommand Command(float moveX = 0, float moveY = 1, float yaw = 0, float pitch = 0, InputButtons buttons = InputButtons.None, byte slot = 0)
            => new InputCommand(new EntityId(1), 0, 1, moveX, moveY, yaw, pitch, buttons, slot);
        [Fact]
        public void InvalidMovementAnglesButtonsAndSlotsReject()
        {
            Assert.Throws<ArgumentException>(() => Command(moveX: 1, moveY: 1));
            Assert.Throws<ArgumentException>(() => Command(moveX: 1.01f, moveY: 0));
            Assert.Throws<ArgumentException>(() => Command(yaw: (float)Math.PI));
            Assert.Throws<ArgumentException>(() => Command(yaw: -(float)Math.PI));
            Assert.Throws<ArgumentException>(() => Command(pitch: (float)(89 * Math.PI / 180)));
            Assert.Throws<ArgumentException>(() => Command(pitch: (float)Math.PI / 2));
            Assert.Throws<ArgumentException>(() => Command(buttons: (InputButtons)128));
            Assert.Throws<ArgumentException>(() => Command(slot: 2));
            Assert.True(Command(moveX: 0.70710677f, moveY: 0.70710677f).IsValid);
        }
        [Fact]
        public void IndependentCommandFixtureRoundTripsAndInvalidFixtureRejects()
        {
            using var good = FixtureJson.Load("command.json"); var command = FixtureJson.Command(good.RootElement);
            using var roundTrip = JsonDocument.Parse(FixtureJson.Write(command));
            Assert.True(JsonElement.DeepEquals(good.RootElement, roundTrip.RootElement));
            using var bad = FixtureJson.Load("invalid-command.json"); Assert.Throws<ArgumentException>(() => FixtureJson.Command(bad.RootElement));
            var maximum = new InputCommand(new EntityId(ulong.MaxValue), uint.MaxValue, uint.MaxValue, -1, 0, -3.1415925f, 0, (InputButtons)127, 1);
            using var maxJson = JsonDocument.Parse(FixtureJson.Write(maximum)); var restored = FixtureJson.Command(maxJson.RootElement);
            Assert.Equal(ulong.MaxValue, restored.EntityId.Value); Assert.Equal(uint.MaxValue, restored.Sequence); Assert.Equal(maximum.AimYaw, restored.AimYaw);
            string text = Encoding.UTF8.GetString(FixtureJson.Write(command));
            using var duplicate = JsonDocument.Parse(text.Replace("\"entityId\":\"1\"", "\"entityId\":\"1\",\"entityId\":\"2\""));
            Assert.Throws<ArgumentException>(() => FixtureJson.Command(duplicate.RootElement));
        }
        [Fact]
        public void IndependentSnapshotAndFullPredictionStateRoundTrip()
        {
            using var snapshot = FixtureJson.Load("snapshot.json"); var state = FixtureJson.Snapshot(snapshot.RootElement);
            Assert.True(JsonElement.DeepEquals(snapshot.RootElement, FixtureJson.WriteSnapshot(state)));
            using var prediction = FixtureJson.Load("prediction-state.json"); var p = FixtureJson.Prediction(prediction.RootElement);
            Assert.True(JsonElement.DeepEquals(prediction.RootElement, FixtureJson.WritePrediction(p)));
            Assert.Equal(2, p.WeaponSlots.Count); Assert.Equal(100, p.Health); Assert.Equal(ContractVersion.PredictionSchema, p.SchemaVersion);
            Assert.Throws<ArgumentException>(() => FixtureJson.Prediction(JsonSerializer.SerializeToElement(new { schemaVersion = 2 })));
        }
        [Fact]
        public void EventsRejectWrongPayloadAndUnknownKinds()
        {
            var id = new EventId(1, 0, new EntityId(1), 0); var payload = new DeathPayload(new EntityId(2));
            Assert.Throws<ArgumentException>(() => new CombatEvent(id, CombatEventKind.ShotAccepted, new EntityId(1), default, payload));
            Assert.Throws<ArgumentException>(() => new CombatEvent(id, (CombatEventKind)99, new EntityId(1), default, payload));
            Assert.Throws<ArgumentException>(() => new CombatEvent(default, CombatEventKind.Death, new EntityId(1), default, payload));
            Assert.Equal(payload, new CombatEvent(id, CombatEventKind.Death, default, new EntityId(1), payload).Payload);
        }
    }
}
