#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Networking.Codec;
using Shooter.Networking.Recovery;
using Shooter.Networking.Session;
using Shooter.Networking.Transport;
using Xunit;

namespace Shooter.Simulation.Tests.Network.Recovery
{
    public sealed class LateJoinTests
    {
        private static ContentId ZeroHash => new ContentId("0000000000000000000000000000000000000000000000000000000000000000");
        private static MatchManifest Manifest => new MatchManifest(NetworkTransportSelection.WireProtocolVersion, "build-m6", ZeroHash, ZeroHash, 460072030UL);

        private static SimulationSnapshot Snap(uint tick)
        {
            var weapon = new WeaponState(new DefinitionId("weapons/rifle"), WeaponAction.Ready, 30, 90, 0, 0, 0, 0, false);
            var entity = new EntitySnapshot(new EntityId(1), new Float3(0, 0, 0), 0, default, Stance.Standing, 100, 1, 0, weapon, 0, 0, 1);
            return new SimulationSnapshot(tick, 1, MatchState.Playing, default, new[] { entity }, Array.Empty<HitVolume>());
        }

        [Fact]
        public void Cannot_act_before_ready_and_reconnect_gets_fresh_session()
        {
            var session = new LateJoinSession(Manifest, 99);
            var hs = SessionCodec.EncodeHandshake(Manifest, new PeerId(2), new EntityId(1), 99);
            Assert.Equal(HandshakeStatus.Accepted, session.BeginJoin(hs.Span, MatchState.Ready, out var peer, out _));
            Assert.False(session.CanAcceptCommands(peer));
            Assert.True(session.TrySendFullSnapshot(peer, Snap(5), out _, out _));
            Assert.False(session.CanAcceptCommands(peer));
            Assert.True(session.TryMarkReady(peer, 5, out _));
            Assert.True(session.CanAcceptCommands(peer));

            session.Reserve(peer, "cover:1");
            ulong oldSession = session.AuthoritySessionId;
            Assert.True(session.Disconnect(peer, out var released));
            Assert.Contains("cover:1", released);
            Assert.True(session.IsEventSessionRetired(oldSession));

            Assert.True(session.ReconnectFresh(hs.Span, MatchState.Ready, out var peer2, out ulong newSession, out _));
            Assert.NotEqual(oldSession, newSession);
            Assert.False(session.CanAcceptCommands(peer2));
            Assert.True(session.IsEventSessionRetired(oldSession));
        }

        [Fact]
        public void Playing_late_join_spectates_until_restart()
        {
            var session = new LateJoinSession(Manifest, 1);
            var hs = SessionCodec.EncodeHandshake(Manifest, new PeerId(3), new EntityId(2), 1);
            Assert.Equal(HandshakeStatus.Accepted, session.BeginJoin(hs.Span, MatchState.Playing, out var peer, out _));
            Assert.Equal(JoinPhase.Spectating, session.GetPhase(peer));
            Assert.True(session.TrySendFullSnapshot(peer, Snap(9), out _, out _));
            Assert.True(session.TryMarkReady(peer, 9, out _));
            Assert.False(session.CanAcceptCommands(peer));
        }
    }
}
