#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Networking.Session
{
    /// <summary>Server-side ownership/tick/rate validation for 60 Hz commands batched at 20 Hz with redundancy.</summary>
    public sealed class CommandValidator
    {
        public const int MaxCommandsPerBatch = 8;
        public const int MaxFutureTicks = 3;
        public const int MaxPastTicks = 12;
        public const int MaxCommandsPerSecond = 90;

        private readonly Dictionary<EntityId, PeerOwnership> owners = new Dictionary<EntityId, PeerOwnership>();

        private sealed class PeerOwnership
        {
            public PeerId Peer;
            public uint LastSequence;
            public uint LastTick;
            public int AcceptedInWindow;
            public uint WindowStartTick;
        }

        public void Bind(EntityId actor, PeerId peer)
        {
            if (!actor.IsValid || !peer.IsValid) throw new ArgumentException("bind");
            owners[actor] = new PeerOwnership { Peer = peer, LastSequence = 0, LastTick = 0, AcceptedInWindow = 0, WindowStartTick = 0 };
        }

        public void Unbind(EntityId actor) => owners.Remove(actor);

        public bool TryAccept(PeerId peer, uint serverTick, InputCommand command, out string rejectReason)
        {
            rejectReason = "";
            if (!command.IsValid) { rejectReason = "invalid"; return false; }
            if (!owners.TryGetValue(command.EntityId, out var ownership) || ownership.Peer != peer)
            {
                rejectReason = "ownership";
                return false;
            }

            if (Serial32.IsNewer(command.ClientTick, unchecked(serverTick + MaxFutureTicks)))
            {
                rejectReason = "future";
                return false;
            }
            if (Serial32.IsNewer(unchecked(serverTick - MaxPastTicks), command.ClientTick) && serverTick >= MaxPastTicks)
            {
                rejectReason = "stale";
                return false;
            }

            if (ownership.LastSequence != 0 && !Serial32.IsNewer(command.Sequence, ownership.LastSequence))
            {
                rejectReason = "duplicate-or-stale-sequence";
                return false;
            }

            if (ownership.WindowStartTick == 0 || Serial32.ForwardDistance(serverTick, ownership.WindowStartTick) >= 60)
            {
                ownership.WindowStartTick = serverTick;
                ownership.AcceptedInWindow = 0;
            }
            if (ownership.AcceptedInWindow >= MaxCommandsPerSecond)
            {
                rejectReason = "rate";
                return false;
            }

            ownership.LastSequence = command.Sequence;
            ownership.LastTick = command.ClientTick;
            ownership.AcceptedInWindow++;
            return true;
        }
    }

    /// <summary>Client helper: packs three 60 Hz commands per 20 Hz send with recent redundant copies.</summary>
    public sealed class CommandBatcher
    {
        private readonly Queue<InputCommand> pending = new Queue<InputCommand>();
        private readonly InputCommand[] recent = new InputCommand[3];
        private int recentCount;

        public void Push(InputCommand command)
        {
            if (!command.IsValid) throw new ArgumentException(nameof(command));
            pending.Enqueue(command);
            if (recentCount < 3) recent[recentCount++] = command;
            else
            {
                recent[0] = recent[1];
                recent[1] = recent[2];
                recent[2] = command;
            }
        }

        public bool TryBuildBatch(out List<InputCommand> batch)
        {
            batch = new List<InputCommand>(8);
            if (pending.Count < 3) return false;
            // Include up to three new commands plus redundancy of recent window.
            for (int i = 0; i < 3 && pending.Count > 0; i++)
                batch.Add(pending.Dequeue());
            for (int i = 0; i < recentCount; i++)
            {
                bool already = false;
                for (int j = 0; j < batch.Count; j++)
                    if (batch[j].Sequence == recent[i].Sequence) { already = true; break; }
                if (!already && batch.Count < 8) batch.Add(recent[i]);
            }
            return batch.Count > 0;
        }
    }
}
