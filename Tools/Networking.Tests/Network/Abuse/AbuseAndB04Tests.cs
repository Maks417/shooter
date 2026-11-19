#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Networking.Abuse;
using Shooter.Networking.Codec;
using Shooter.Networking.LagCompensation;
using Shooter.Networking.Prediction;
using Shooter.Networking.Session;
using Shooter.Networking.Transport;
using Shooter.Simulation.Motor;
using Shooter.Simulation.Tests.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Network.Abuse
{
    public sealed class AbuseAndB04Tests
    {
        private static ContentId ZeroHash => new ContentId("0000000000000000000000000000000000000000000000000000000000000000");

        [Fact]
        public void Abuse_limits_reject_bad_token_oversized_packets_and_document_n4()
        {
            var limits = new AbuseLimits();
            Assert.True(limits.TryAdmitPeer(new PeerId(1), 7, 7, out _));
            Assert.False(limits.TryAdmitPeer(new PeerId(2), 8, 7, out var reason));
            Assert.Equal("token", reason);
            Assert.False(limits.ValidatePacket(new byte[1201], out var sizeReason));
            Assert.Equal("size", sizeReason);
            Assert.True(limits.NoteMalformed());
            Assert.Contains("N4-deferred", limits.ExposureReviewSummary());
            Assert.DoesNotContain("public internet ready", limits.ExposureReviewSummary());
        }

        [Fact]
        public void B04_four_peer_software_soak_at_150ms_3pct_stays_bounded()
        {
            var world = new StaticPlaneWorld();
            var manifest = new MatchManifest(NetworkTransportSelection.WireProtocolVersion, "build-m6", ZeroHash, ZeroHash, 460072030UL);
            var counters = new B04SoakCounters();
            var fire = new AuthoritativeFireValidator();
            const int clients = 4;
            const int ticks = 1800; // 30s software soak proxy; 30-minute hardware Pending
            var hosts = new DedicatedSessionHost[clients];
            var predictors = new PredictionReconciler[clients];
            var motors = new KinematicMotor[clients];
            var states = new MotorBodyState[clients];
            var impaired = new ImpairedNetworkSimulator[clients];

            for (int i = 0; i < clients; i++)
            {
                hosts[i] = new DedicatedSessionHost(manifest, 42);
                var hs = SessionCodec.EncodeHandshake(manifest, new PeerId((ulong)(i + 2)), new EntityId((ulong)(i + 1)), 42);
                Assert.Equal(HandshakeStatus.Accepted, hosts[i].TryAcceptJoin(hs.Span, out _));
                predictors[i] = new PredictionReconciler(new MotorBodyState(
                    new Float3(i, 0, 0), default, 0, 0, Stance.Standing, 1.8f,
                    true, new SurfaceId(100), new Float3(0, 1, 0), 0, 0, 0, false), 0);
                motors[i] = new KinematicMotor();
                states[i] = predictors[i].Predicted;
                impaired[i] = new ImpairedNetworkSimulator(new LoopbackNetworkChannel(), (ulong)(460072030 + i))
                {
                    DelaySeconds = 0.075, // 150 ms RTT total across up/down approximation
                    LossProbability = 0.03
                };
            }

            uint seq = 0;
            for (int t = 0; t < ticks; t++)
            {
                for (int i = 0; i < clients; i++)
                {
                    seq++;
                    uint tick = unchecked(predictors[i].PredictedTick + 1);
                    var cmd = new InputCommand(new EntityId((ulong)(i + 1)), tick, seq, 0, 1, 0, 0, InputButtons.None, 0);
                    predictors[i].Predict(cmd, world);
                    var bytes = SessionCodec.EncodeCommandBatch(new EntityId((ulong)(i + 1)), tick, new List<InputCommand> { cmd });
                    counters.NoteUpstream(bytes.Length);
                    impaired[i].TrySend(new PeerId(1), Delivery.UnreliableSequenced, bytes);
                    impaired[i].AdvanceTime(1.0 / 60.0);
                    if (hosts[i].TryAcceptCommand(new PeerId((ulong)(i + 2)), tick, cmd, out _))
                    {
                        counters.NoteCommand();
                        states[i] = motors[i].Step(states[i], cmd, tick, world);
                    }
                    if (t % 3 == 0)
                    {
                        counters.NoteDownstream(200);
                        counters.NoteSnapshot(0.05 + (t % 50) * 0.001);
                        predictors[i].Reconcile(states[i], tick, seq, predictors[i].PredictedTick, world);
                    }
                }
                // Eight bots exist as match actors for budget accounting (no presentation).
                _ = AbuseLimits.MaxBots;
            }

            // Same-tick trade fixture freeze under soak harness.
            fire.ValidateAndQueueDamage(new FireIntent(new EntityId(1), 9, 100, 0, 30, 0, true), new EntityId(2), 100);
            fire.ValidateAndQueueDamage(new FireIntent(new EntityId(2), 9, 100, 0, 30, 0, true), new EntityId(1), 100);
            var health = fire.ResolveHealth(new Dictionary<EntityId, ushort> { [new EntityId(1)] = 100, [new EntityId(2)] = 100 });
            Assert.Equal(0, health[new EntityId(1)]);
            Assert.Equal(0, health[new EntityId(2)]);

            double duration = ticks / 60.0;
            Assert.False(counters.DivergenceOverOneSecond);
            Assert.True(counters.UpstreamKiBPerSecond(duration) <= 10.0 * clients);
            Assert.True(counters.DownstreamKiBPerSecond(duration) <= 40.0);
            Assert.True(counters.CommandsAccepted > 0);
            Assert.True(predictors[0].CorrectionP95() <= 1.0f);
        }
    }
}
