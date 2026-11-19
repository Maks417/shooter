#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Networking.Prediction;
using Shooter.Simulation.Motor;
using Shooter.Simulation.Tests.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Network.Prediction
{
    public sealed class PredictionReconcileTests
    {
        private static MotorBodyState Spawn() => new MotorBodyState(
            new Float3(0, 0, 0), default, 0, 0, Stance.Standing, 1.8f,
            true, new SurfaceId(100), new Float3(0, 1, 0), 0, 0, 0, false);

        private static InputCommand Cmd(EntityId id, uint tick, uint seq, float my = 1f, InputButtons buttons = InputButtons.None) =>
            new InputCommand(id, tick, seq, 0, my, 0, 0, buttons, 0);

        [Fact]
        public void Static_map_correction_p95_within_10cm_under_impaired_delay()
        {
            var world = new StaticPlaneWorld();
            var actor = new EntityId(1);
            var client = new PredictionReconciler(Spawn(), 0);
            var serverMotor = new KinematicMotor();
            var serverState = Spawn();
            uint serverTick = 0;
            uint seq = 0;
            const int steps = 600;
            const int delayTicks = 6; // ~100 ms one-way
            var inFlight = new Queue<(uint tick, uint seq, InputCommand cmd)>();
            var serverByTick = new Dictionary<uint, MotorBodyState>();
            InputCommand lastDelivered = default;
            bool hasLast = false;

            for (int i = 0; i < steps; i++)
            {
                seq++;
                uint clientTick = unchecked(client.PredictedTick + 1);
                var cmd = Cmd(actor, clientTick, seq);
                client.Predict(cmd, world);

                // 1% loss: skip enqueue; server will hold missing input for that tick when gap detected.
                if (seq % 100 != 0)
                    inFlight.Enqueue((clientTick, seq, cmd));

                while (inFlight.Count > 0 && inFlight.Peek().tick + delayTicks <= client.PredictedTick)
                {
                    var delivered = inFlight.Dequeue();
                    // Fill server ticks up to delivered.tick with holds when packets were lost.
                    while (serverTick + 1 < delivered.tick)
                    {
                        serverTick++;
                        InputCommand hold = hasLast
                            ? new InputCommand(actor, serverTick, lastDelivered.Sequence, lastDelivered.MoveX, lastDelivered.MoveY,
                                lastDelivered.AimYaw, lastDelivered.AimPitch,
                                lastDelivered.Buttons & ~(InputButtons.FirePressed | InputButtons.JumpPressed | InputButtons.ReloadPressed),
                                lastDelivered.SelectedSlot)
                            : Cmd(actor, serverTick, serverTick, 0);
                        serverState = serverMotor.Step(serverState, hold, serverTick, world);
                        serverByTick[serverTick] = serverState;
                    }
                    serverTick = delivered.tick;
                    serverState = serverMotor.Step(serverState, delivered.cmd, serverTick, world);
                    serverByTick[serverTick] = serverState;
                    lastDelivered = delivered.cmd;
                    hasLast = true;
                }

                uint authTick = unchecked(client.PredictedTick - (uint)(2 * delayTicks));
                if (client.PredictedTick > 2 * delayTicks && authTick % 3 == 0 && serverByTick.TryGetValue(authTick, out var auth))
                    client.Reconcile(auth, authTick, authTick, client.PredictedTick, world);
            }

            float p95 = client.CorrectionP95();
            Assert.True(p95 <= 0.1f, $"correction p95 {p95} exceeds 0.1 m");
        }

        [Fact]
        public void Provisional_shots_dedupe_and_missing_input_hold_clears_edges()
        {
            var world = new StaticPlaneWorld();
            var reconciler = new PredictionReconciler(Spawn(), 0);
            Assert.True(reconciler.TryRegisterProvisionalShot(5));
            Assert.False(reconciler.TryRegisterProvisionalShot(5));

            var fire = new InputCommand(new EntityId(1), 1, 1, 0, 0, 0, 0, InputButtons.FirePressed | InputButtons.FireHeld, 0);
            reconciler.Predict(fire, world);
            var held = reconciler.HoldMissing(world);
            Assert.Equal(Stance.Standing, held.Stance);
        }
    }
}
