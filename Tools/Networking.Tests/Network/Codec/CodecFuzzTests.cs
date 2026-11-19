#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Networking.Codec;
using Shooter.Networking.Transport;
using Xunit;

namespace Shooter.Simulation.Tests.Network.Codec
{
    public sealed class CodecFuzzTests
    {
        private static ContentId ZeroHash => new ContentId("0000000000000000000000000000000000000000000000000000000000000000");

        [Fact]
        public void Handshake_roundtrip_and_reject_mismatch()
        {
            var manifest = new MatchManifest(NetworkTransportSelection.WireProtocolVersion, "build-m5", ZeroHash, ZeroHash, 460072030UL);
            var bytes = SessionCodec.EncodeHandshake(manifest, new PeerId(7), new EntityId(1), 99UL);
            Assert.True(SessionCodec.TryDecodeHandshake(bytes.Span, out var decoded, out var peer, out var actor, out var token, out _));
            Assert.Equal(manifest.MapSeed, decoded.MapSeed);
            Assert.Equal(7UL, peer.Value);
            Assert.Equal(1UL, actor.Value);
            Assert.Equal(99UL, token);

            var bad = bytes.ToArray();
            // Corrupt protocol version length prefix so decode fails closed.
            bad[0] = 0xFF;
            Assert.False(SessionCodec.TryDecodeHandshake(bad, out _, out _, out _, out _, out _));
            Assert.False(SessionCodec.TryDecodeHandshake(bytes.Span.Slice(0, 4), out _, out _, out _, out _, out _));
        }

        [Fact]
        public void Truncation_and_oversized_payloads_do_not_throw_out_of_decode()
        {
            var manifest = new MatchManifest(NetworkTransportSelection.WireProtocolVersion, "build-m5", ZeroHash, ZeroHash, 1UL);
            var full = SessionCodec.EncodeHandshake(manifest, new PeerId(1), new EntityId(2), 3UL).ToArray();
            for (int len = 0; len < full.Length; len++)
            {
                Assert.False(SessionCodec.TryDecodeHandshake(full.AsSpan(0, len), out _, out _, out _, out _, out _));
            }

            var channel = new LoopbackNetworkChannel();
            Assert.False(channel.TrySend(new PeerId(1), Delivery.ReliableOrdered, new byte[1201]));
            Assert.True(channel.TrySend(new PeerId(1), Delivery.ReliableOrdered, new byte[1200]));
        }

        [Fact]
        public void Command_batch_rejects_invalid_owner_on_encode()
        {
            var cmds = new List<InputCommand>
            {
                new InputCommand(new EntityId(1), 1, 1, 0, 1, 0, 0, InputButtons.None, 0),
                new InputCommand(new EntityId(2), 2, 2, 0, 1, 0, 0, InputButtons.None, 0)
            };
            Assert.Throws<ArgumentException>(() => SessionCodec.EncodeCommandBatch(new EntityId(1), 1, cmds));
        }

        [Fact]
        public void Snapshot_roundtrip_preserves_entity_ammo()
        {
            var weapon = new WeaponState(new DefinitionId("weapons/rifle"), WeaponAction.Ready, 30, 90, 0, 0, 0, 0, false);
            var entity = new EntitySnapshot(new EntityId(1), new Float3(1, 0, 2), 0.1f, new Float3(0, 0, 1), Stance.Standing, 100, 1, 0, weapon, 3f, 0.2f, 1);
            var snapshot = new SimulationSnapshot(10, 1, MatchState.Playing, default, new[] { entity }, Array.Empty<HitVolume>());
            var bytes = SnapshotCodec.EncodeSnapshot(snapshot, 0, 7);
            Assert.True(SnapshotCodec.TryDecodeSnapshot(bytes.Span, out var decoded, out uint baseline, out uint seq, out _));
            Assert.NotNull(decoded);
            Assert.Equal(0u, baseline);
            Assert.Equal(7u, seq);
            Assert.Equal(30, decoded!.Entities[0].Weapon.AmmoInMagazine);
            Assert.Equal(entity.Position.X, decoded.Entities[0].Position.X, 3);
        }
    }
}
