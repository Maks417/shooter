#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Motor;

namespace Shooter.Networking.Prediction
{
    public readonly struct CorrectionSample
    {
        public uint Tick { get; }
        public float DistanceMeters { get; }
        public CorrectionSample(uint tick, float distanceMeters)
        {
            Tick = tick;
            DistanceMeters = distanceMeters;
        }
    }

    /// <summary>
    /// Restores authoritative motor-relevant state and replays unacked inputs with the shared kinematic motor.
    /// Missing inputs are clamped to last move with edge buttons cleared; provisional shot feedback is deduplicated by sequence.
    /// </summary>
    public sealed class PredictionReconciler
    {
        private readonly KinematicMotor motor;
        private readonly InputHistoryRing history = new InputHistoryRing();
        private readonly HashSet<uint> consumedShotSequences = new HashSet<uint>();
        private readonly List<CorrectionSample> corrections = new List<CorrectionSample>();
        private MotorBodyState predicted;
        private uint predictedTick;
        private InputCommand lastValid;
        private bool hasLastValid;
        private uint missingHoldTicks;

        public PredictionReconciler(MotorBodyState initial, uint initialTick, KinematicMotor? motor = null)
        {
            this.motor = motor ?? new KinematicMotor();
            predicted = initial;
            predictedTick = initialTick;
        }

        public MotorBodyState Predicted => predicted;
        public uint PredictedTick => predictedTick;
        public IReadOnlyList<CorrectionSample> Corrections => corrections;
        public InputHistoryRing History => history;

        public void Predict(InputCommand command, IWorldQueries world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            history.Store(command);
            lastValid = command;
            hasLastValid = true;
            missingHoldTicks = 0;
            predictedTick = unchecked(predictedTick + 1);
            predicted = motor.Step(predicted, command, predictedTick, world);
            if ((command.Buttons & InputButtons.FirePressed) != 0)
                consumedShotSequences.Add(command.Sequence);
        }

        public MotorBodyState HoldMissing(IWorldQueries world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!hasLastValid) return predicted;
            missingHoldTicks++;
            InputCommand held;
            if (missingHoldTicks <= 6)
            {
                // Hold last move/aim/held fire; clear one-shot edges.
                var buttons = lastValid.Buttons & ~(InputButtons.FirePressed | InputButtons.JumpPressed | InputButtons.ReloadPressed);
                held = new InputCommand(lastValid.EntityId, unchecked(predictedTick + 1), lastValid.Sequence,
                    lastValid.MoveX, lastValid.MoveY, lastValid.AimYaw, lastValid.AimPitch, buttons, lastValid.SelectedSlot);
            }
            else
            {
                held = new InputCommand(lastValid.EntityId, unchecked(predictedTick + 1), lastValid.Sequence,
                    0, 0, lastValid.AimYaw, lastValid.AimPitch, InputButtons.None, lastValid.SelectedSlot);
            }
            predictedTick = unchecked(predictedTick + 1);
            predicted = motor.Step(predicted, held, predictedTick, world);
            return predicted;
        }

        public bool TryRegisterProvisionalShot(uint sequence)
        {
            return consumedShotSequences.Add(sequence);
        }

        public CorrectionSample Reconcile(MotorBodyState authoritative, uint authoritativeTick, uint ackedSequence, uint replayThroughTick, IWorldQueries world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            // Correction = visual delta at the current predicted tick after restore+replay (not raw auth lag).
            MotorBodyState before = predicted;
            uint beforeTick = predictedTick;

            predicted = authoritative;
            predictedTick = authoritativeTick;
            history.DiscardThrough(ackedSequence);

            for (uint tick = unchecked(authoritativeTick + 1); !IsAfter(tick, replayThroughTick); tick = unchecked(tick + 1))
            {
                if (!history.TryGet(tick, out var command))
                {
                    // Clamp missing inputs without advancing beyond the requested replay end via HoldMissing side effects.
                    if (!hasLastValid) continue;
                    InputCommand held;
                    if (missingHoldTicks < 6)
                    {
                        missingHoldTicks++;
                        var buttons = lastValid.Buttons & ~(InputButtons.FirePressed | InputButtons.JumpPressed | InputButtons.ReloadPressed);
                        held = new InputCommand(lastValid.EntityId, tick, lastValid.Sequence,
                            lastValid.MoveX, lastValid.MoveY, lastValid.AimYaw, lastValid.AimPitch, buttons, lastValid.SelectedSlot);
                    }
                    else
                    {
                        held = new InputCommand(lastValid.EntityId, tick, lastValid.Sequence,
                            0, 0, lastValid.AimYaw, lastValid.AimPitch, InputButtons.None, lastValid.SelectedSlot);
                    }
                    predictedTick = tick;
                    predicted = motor.Step(predicted, held, tick, world);
                    continue;
                }
                if (command.Sequence <= ackedSequence) continue;
                missingHoldTicks = 0;
                lastValid = command;
                hasLastValid = true;
                predictedTick = tick;
                predicted = motor.Step(predicted, command, tick, world);
            }

            float distance = 0f;
            if (beforeTick == predictedTick)
            {
                float dx = before.Feet.X - predicted.Feet.X;
                float dy = before.Feet.Y - predicted.Feet.Y;
                float dz = before.Feet.Z - predicted.Feet.Z;
                distance = (float)Math.Sqrt((double)dx * dx + (double)dy * dy + (double)dz * dz);
            }
            var sample = new CorrectionSample(authoritativeTick, distance);
            corrections.Add(sample);
            return sample;
        }

        public float CorrectionP95()
        {
            if (corrections.Count == 0) return 0;
            var distances = new float[corrections.Count];
            for (int i = 0; i < corrections.Count; i++) distances[i] = corrections[i].DistanceMeters;
            Array.Sort(distances);
            int index = (int)Math.Ceiling(distances.Length * 0.95) - 1;
            if (index < 0) index = 0;
            if (index >= distances.Length) index = distances.Length - 1;
            return distances[index];
        }

        private static bool IsAfter(uint tick, uint end) => Serial32.IsNewer(tick, end);
    }
}
