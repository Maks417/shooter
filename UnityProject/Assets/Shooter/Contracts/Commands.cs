#nullable enable
using System;

namespace Shooter.Contracts
{
    public readonly struct InputCommand
    {
        public EntityId EntityId { get; }
        public uint ClientTick { get; }
        public uint Sequence { get; }
        public float MoveX { get; }
        public float MoveY { get; }
        public float AimYaw { get; }
        public float AimPitch { get; }
        public InputButtons Buttons { get; }
        public byte SelectedSlot { get; }
        public bool IsValid => EntityId.IsValid;
        public InputCommand(EntityId entityId, uint clientTick, uint sequence, float moveX, float moveY, float aimYaw, float aimPitch, InputButtons buttons, byte selectedSlot)
        {
            Require.That(entityId.IsValid, nameof(entityId));
            Require.Finite(moveX, nameof(moveX)); Require.Finite(moveY, nameof(moveY));
            // A small tolerance accounts for float32 normalized diagonal input.
            Require.That(Math.Abs(moveX) <= 1 && Math.Abs(moveY) <= 1 && (double)moveX * moveX + (double)moveY * moveY <= 1.0000001, "movement");
            Require.Aim(aimYaw, aimPitch); Require.Buttons(buttons); Require.Slot(selectedSlot);
            EntityId = entityId; ClientTick = clientTick; Sequence = sequence; MoveX = moveX; MoveY = moveY;
            AimYaw = aimYaw; AimPitch = aimPitch; Buttons = buttons; SelectedSlot = selectedSlot;
        }
    }
    public sealed class CommandBuffer
    {
        private readonly InputCommand[] values;
        public int Capacity => values.Length;
        public CommandBuffer(int capacity = ContractVersion.MaxCommands)
        { Require.That(capacity > 0 && capacity <= ContractVersion.MaxCommands, nameof(capacity)); values = new InputCommand[capacity]; }
        public InputCommand this[int index] { get => values[index]; set { Require.That(value.IsValid, nameof(value)); values[index] = value; } }
    }
    public readonly struct DrainResult
    {
        public ErrorCode Error { get; }
        public int WrittenCount { get; }
        public int RequiredCount { get; }
        public DrainResult(ErrorCode error, int writtenCount, int requiredCount)
        {
            Require.Enum(error, nameof(error));
            Require.That(writtenCount >= 0 && writtenCount <= 256 && requiredCount >= writtenCount, "drain counts");
            Require.That(error == ErrorCode.None ? writtenCount == requiredCount : writtenCount == 0, "drain status/counts");
            Error = error; WrittenCount = writtenCount; RequiredCount = requiredCount;
        }
    }
    public sealed class NetworkPacket
    {
        public PeerId Peer { get; }
        public Delivery Delivery { get; }
        private readonly byte[] payload;
        public ReadOnlySpan<byte> Payload => payload;
        public NetworkPacket(PeerId peer, Delivery delivery, ReadOnlySpan<byte> payload)
        {
            Require.That(peer.IsValid && payload.Length <= 1200, "packet"); Require.Enum(delivery, nameof(delivery));
            Peer = peer; Delivery = delivery; this.payload = payload.ToArray();
        }
    }
    public sealed class PacketBuffer
    {
        private readonly NetworkPacket?[] packets;
        public int Capacity => packets.Length;
        public PacketBuffer(int capacity = 256)
        { Require.That(capacity > 0 && capacity <= 256, nameof(capacity)); packets = new NetworkPacket[capacity]; }
        public NetworkPacket this[int index] { get => packets[index] ?? throw new InvalidOperationException("Unwritten packet"); set => packets[index] = value ?? throw new ArgumentNullException(nameof(value)); }
    }
}
