#nullable enable
using Shooter.Contracts;

namespace Shooter.Simulation.Motor
{
    /// <summary>
    /// Accumulates one-shot edges across multiple display frames, then drains once per simulation tick.
    /// Held buttons are sampled at drain time from the latest frame sample.
    /// </summary>
    public sealed class InputEdgeBuffer
    {
        private InputButtons pendingEdges;
        private InputButtons latestHeld;
        private float latestMoveX;
        private float latestMoveY;
        private float latestAimYaw;
        private float latestAimPitch;
        private byte latestSlot;
        private bool hasSample;

        public void AccumulateFrame(float moveX, float moveY, float aimYaw, float aimPitch, InputButtons buttons, byte selectedSlot)
        {
            // Validation occurs when draining into InputCommand.
            pendingEdges |= buttons & (InputButtons.FirePressed | InputButtons.ReloadPressed | InputButtons.JumpPressed);
            latestHeld = buttons & (InputButtons.FireHeld | InputButtons.SprintHeld | InputButtons.CrouchHeld | InputButtons.AimHeld);
            latestMoveX = moveX; latestMoveY = moveY; latestAimYaw = aimYaw; latestAimPitch = aimPitch; latestSlot = selectedSlot;
            hasSample = true;
        }

        public bool TryDrain(EntityId entityId, uint clientTick, uint sequence, out InputCommand command)
        {
            if (!hasSample)
            {
                command = default;
                return false;
            }
            var buttons = latestHeld | pendingEdges;
            pendingEdges = InputButtons.None;
            command = new InputCommand(entityId, clientTick, sequence, latestMoveX, latestMoveY, latestAimYaw, latestAimPitch, buttons, latestSlot);
            return true;
        }

        public void ClearOneShots() => pendingEdges = InputButtons.None;
    }
}
