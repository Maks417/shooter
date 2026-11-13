#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Networking.Prediction
{
    /// <summary>256-tick input history for own-player reconciliation.</summary>
    public sealed class InputHistoryRing
    {
        public const int Capacity = 256;
        private readonly InputCommand[] commands = new InputCommand[Capacity];
        private readonly bool[] present = new bool[Capacity];

        public void Store(InputCommand command)
        {
            if (!command.IsValid) throw new ArgumentException(nameof(command));
            int index = (int)(command.ClientTick % Capacity);
            commands[index] = command;
            present[index] = true;
        }

        public bool TryGet(uint tick, out InputCommand command)
        {
            int index = (int)(tick % Capacity);
            if (!present[index] || commands[index].ClientTick != tick)
            {
                command = default;
                return false;
            }
            command = commands[index];
            return true;
        }

        public void DiscardThrough(uint ackedSequence)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (!present[i]) continue;
                if (commands[i].Sequence <= ackedSequence)
                    present[i] = false;
            }
        }
    }
}
