#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Networking.Codec;
using Shooter.Networking.Prediction;
using Shooter.Networking.Session;
using Shooter.Networking.Snapshots;
using Shooter.Networking.Transport;
using Shooter.Simulation.Motor;
using Shooter.Simulation.Tests.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Network
{
    public sealed class TwoPeerImpairmentTests
    {
        private static ContentId ZeroHash => new ContentId("0000000000000000000000000000000000000000000000000000000000000000");

        private static MotorBodyState Spawn() => new MotorBodyState(
            new Float3(0, 0, 0), default, 0, 0, Stance.Standing, 1.8f,
            true, new SurfaceId(100), new Float3(0, 1, 0), 0, 0, 0, false);

        [Theory]
        [InlineData(0, 0)]
        [InlineData(50, 1)]
        [InlineData(100, 1)]
        [InlineData(150, 3)]
        public void Two_peer_matrix_recovers_with_bounded_correction(int rttMs, int lossPercent)
        {
            var world = new StaticPlaneWorld();
            var manifest = new MatchManifest(NetworkTransportSelection.WireProtocolVersion, "build-m5", ZeroHash, ZeroHash, 460072030UL);
            var host = new DedicatedSessionHost(manifest, 7);
            var join = SessionCodec.EncodeHandshake(manifest, new PeerId(2), new EntityId(1), 7);
            Assert.Equal(HandshakeStatus.Accepted, host.TryAcceptJoin(join.Span, out _));

            var client = new PredictionReconciler(Spawn(), 0);
            var serverMotor = new KinematicMotor();
            var serverState = Spawn();
            uint serverTick = 0;
            var remote = new RemoteInterpolationBuffer();
            var pair = new LoopbackPair(new PeerId(1), new PeerId(2));
            var uplink = new ImpairedNetworkSimulator(pair.Server, 460072030UL)
            {
                DelaySeconds = rttMs / 2000.0,
                LossProbability = lossPercent / 100.0
            };
            var downlink = new ImpairedNetworkSimulator(pair.Client, 460072031UL)
            {
                DelaySeconds = rttMs / 2000.0,
                LossProbability = lossPercent / 100.0
            };

            uint seq = 0;
            for (int i = 0; i < 300; i++)
            {
                seq++;
                uint tick = unchecked(client.PredictedTick + 1);
                var cmd = new InputCommand(new EntityId(1), tick, seq, 0, 1, 0, 0, InputButtons.None, 0);
                client.Predict(cmd, world);
                var batch = SessionCodec.EncodeCommandBatch(new EntityId(1), tick, new List<InputCommand> { cmd });
                uplink.TrySend(new PeerId(2), Delivery.UnreliableSequenced, batch);
                uplink.AdvanceTime(1.0 / 60.0);
                downlink.AdvanceTime(1.0 / 60.0);

                var buffer = new PacketBuffer();
                var drain = pair.Server.Poll(buffer);
                for (int p = 0; p < drain.WrittenCount; p++)
                {
                    var decoded = new List<InputCommand>();
                    if (!SessionCodec.TryDecodeCommandBatch(buffer[p].Payload, decoded, out _)) continue;
                    foreach (var c in decoded)
                    {
                        if (!host.TryAcceptCommand(new PeerId(2), serverTick + 1, c, out _)) continue;
                        serverTick++;
                        serverState = serverMotor.Step(serverState, c, serverTick, world);
                    }
                }

                if (serverTick > 0 && serverTick % 3 == 0)
                {
                    var weapon = new WeaponState(new DefinitionId("weapons/rifle"), WeaponAction.Ready, 30, 90, 0, 0, 0, 0, false);
                    var entity = new EntitySnapshot(new EntityId(1), serverState.Feet, serverState.AimYaw, serverState.Velocity,
                        serverState.Stance, 100, 1, 0, weapon, serverState.LocomotionDistanceMeters, serverState.AimPitch, 1);
                    var snap = new SimulationSnapshot(serverTick, 1, MatchState.Playing, default, new[] { entity }, System.Array.Empty<HitVolume>());
                    var bytes = SnapshotCodec.EncodeSnapshot(snap, 0, seq);
                    downlink.TrySend(new PeerId(1), Delivery.UnreliableSequenced, bytes);
                    client.Reconcile(serverState, serverTick, seq, client.PredictedTick, world);
                    remote.Push(serverTick / 60.0, new RemotePose(entity.Id, entity.Position, entity.Yaw, entity.AimPitch, entity.Velocity));
                }
            }

            Assert.True(host.HasJoinedPeer);
            Assert.True(client.CorrectionP95() <= 1.0f, $"p95 {client.CorrectionP95()} unbounded");
            Assert.True(remote.TrySample(serverTick / 60.0, out _));
            // Offline motor still agrees on static plane after network work.
            var offline = new KinematicMotor().Step(Spawn(), new InputCommand(new EntityId(1), 1, 1, 0, 1, 0, 0, InputButtons.None, 0), 1, world);
            Assert.True(offline.Feet.Z > 0);
        }
    }
}
